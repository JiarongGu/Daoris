using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a start or a resume makes of the choice a session was asked to run with (<see cref="ActorAsked"/>, whose shape is the service's):
/// the opening's default, the composer's change laid over it, the spawn's command line and the native arguments. The roles' own members of
/// it, so every caller keeps `ActorAsked.Before`, `asked.Overlaid`, `ActorAsked.ForSpawn` and `asked.NativeArguments` as spelled.
/// </summary>
public static class ActorAskedChoices
{
    extension(ActorAsked)
    {
        public static ActorAsked Before(string agent) => new(agent, new("account-default", null, false), new("account-default", null, false));

        public static ActorAsked ForSpawn(string agent, System.Diagnostics.ProcessStartInfo info, ActorAsked? requested = null)
        {
            var asked = requested ?? Before(agent);
            ActorValue Command(string flag, ActorValue value)
            {
                var words = info.ArgumentList.ToArray();
                for (var i = 0; i < words.Length; i++)
                {
                    var named = words[i] == flag && i + 1 < words.Length ? words[i + 1]
                        : words[i].StartsWith(flag + "=", StringComparison.Ordinal) ? words[i][(flag.Length + 1)..] : null;
                    if (named is not null) return value is { Source: "entry" or "override", Carried: true } && value.Value == named ? value : new("command", named, false);
                }
                return value;
            }
            var model = Command("--model", asked.Model);
            var effort = Command("--effort", asked.Effort);
            if (model.Source == "account-default" && info.Environment.TryGetValue("ANTHROPIC_MODEL", out var ownModel) && !string.IsNullOrWhiteSpace(ownModel))
                model = new("environment", ownModel, false);
            if (info.Environment.TryGetValue("CLAUDE_CODE_EFFORT_LEVEL", out var ownEffort) && !string.IsNullOrWhiteSpace(ownEffort))
                effort = new("environment", ownEffort, false);
            return asked with { Model = model, Effort = effort };
        }
    }

    extension(ActorAsked asked)
    {
        /// <summary>
        /// This asked choice with the composer's <paramref name="change"/> laid over it (D205 §4.2, MODELROLE1d): what a resume and a briefed
        /// continuation set, the person's last change after the opening's. An override's model drops the asked effort unless the override
        /// names one, since a model change resets the effort the agent gives. Each value it names is carried, its source <c>override</c>.
        /// </summary>
        public ActorAsked Overlaid(ActorOverride? change) => change is null ? asked : asked with
        {
            Model = change.Model is { } model ? new("override", model, true) : asked.Model,
            Effort = change.Effort is { } effort ? new("override", effort, true)
                : change.Model is not null ? new("account-default", null, false) : asked.Effort,
        };

        /// <summary>The native carrier, shared by first starts, resumes and chats. Existing command flags retain precedence.</summary>
        public IEnumerable<string> NativeArguments(IReadOnlyList<string> command)
        {
            if (asked.Model is { Source: "entry" or "override", Value: { } model, Carried: true } && !Has(command, "--model"))
            { yield return "--model"; yield return model; }
            if (asked.Effort is { Source: "entry" or "override", Value: { } effort, Carried: true } && !Has(command, "--effort"))
            { yield return "--effort"; yield return effort; }
        }
    }

    private static bool Has(IEnumerable<string> command, string flag) => command.Any(word => word == flag || word.StartsWith(flag + "=", StringComparison.Ordinal));
}

/// <summary>
/// The composer's last change on a conversation, kept as values (D205 §4.2, §9.3's <c>override</c>, MODELROLE1d): the model's and the
/// effort's value ids as the agent returned them, never their display names, which need not be unique or reversible.
/// </summary>
public sealed record ActorOverride(string? Model, string? Effort)
{
    /// <summary>The record's <c>override</c> as the service keeps it; null where it holds none or none that reads.</summary>
    public static ActorOverride? Read(JsonElement? kept)
    {
        if (kept is not { ValueKind: JsonValueKind.Object } value) return null;
        string? Text(string name) => value.TryGetProperty(name, out var each) && each.ValueKind == JsonValueKind.String ? each.GetString() : null;
        var model = Text("model");
        var effort = Text("effort");
        return model is null && effort is null ? null : new(model, effort);
    }

    /// <summary>
    /// <paramref name="before"/> after one change of <paramref name="category"/>, reconciled from the configuration the agent
    /// <paramref name="returned"/> (round 3, Codex 5), never merged blindly: an effort-only change keeps the model; a model change sets
    /// the model and clears the effort, leaving it to what the new model gives. With no returned list nothing was acknowledged, and
    /// nothing changes.
    /// </summary>
    public static ActorOverride? Reconciled(ActorOverride? before, string category, IReadOnlyList<AcpConfigOption>? returned)
    {
        if (returned is null) return before;
        var now = returned.FirstOrDefault(option => option.Category == category)?.Current;
        return (category, now) switch
        {
            (_, null) => before,
            ("model", { } model) => new(model, null),
            ("thought_level", { } effort) => new(before?.Model, effort),
            _ => before,
        };
    }

    /// <summary>The record's <c>override</c> as it is written: only what it names.</summary>
    public IReadOnlyDictionary<string, string> Written
    {
        get
        {
            var written = new Dictionary<string, string>(StringComparer.Ordinal);
            if (Model is { } model) written["model"] = model;
            if (Effort is { } effort) written["effort"] = effort;
            return written;
        }
    }
}

/// <summary>
/// What a protocol open keeps and asks about its agent's offers (D205 §3.3, §9.2, MODELROLE1c2): the version and account its reading is
/// keyed by, and the start's re-ask where a choice is refused at the open, answering a later choice and its <c>choice</c> to set on the
/// same session, or null where the record ends <c>choice-not-offered</c>. A null re-ask keeps MODELROLE1c's not-carried reading.
/// </summary>
public sealed record OpenOffers(string? Version, string? Account, Func<ModelRefusal, Task<(ActorAsked Asked, string Choice)?>>? Refused);

/// <summary>Bounded provenance, independent of token attribution. No configured or opening model is a report of a turn.</summary>
public sealed class ActorReports
{
    private readonly HashSet<string> _seen;
    private readonly List<string> _models;
    private readonly List<ActorEntry> _entries;
    private int _more;
    private int _entriesMore;

    public ActorReports(ActorRan? prior = null)
    {
        _models = [.. prior?.Models ?? []];
        _seen = new(_models, StringComparer.Ordinal);
        _entries = [.. prior?.Entries ?? []];
        _more = prior?.More ?? 0;
        _entriesMore = prior?.EntriesMore ?? 0;
    }

    public ActorRan? Snapshot => _models.Count == 0 && _entries.Count == 0 ? null : new(_models.ToArray(), _more, _entries.ToArray(), _entriesMore);

    public bool Report(IEnumerable<string> models, ActorEntry? entry = null)
    {
        var changed = false;
        foreach (var model in models)
        {
            if (TurnModels.Name(model) is null || !_seen.Add(model)) continue;
            if (_models.Count < 8) _models.Add(model); else _more++;
            changed = true;
        }
        if (entry is not null && (_entries.LastOrDefault() is not { } last || last.Acknowledged != entry.Acknowledged || last.Effort != entry.Effort))
        {
            _entries.Add(entry);
            if (_entries.Count > 8) { _entries.RemoveAt(0); _entriesMore++; }
            changed = true;
        }
        return changed;
    }
}

/// <summary>One run's event watch. Writes are ordered and drained when the run ends; a failed report never changes its behaviour.</summary>
public sealed class ActorRun : IAsyncDisposable
{
    public static readonly JsonSerializerOptions Json = ActorWire.Json;
    private readonly SessionEvents _events;
    private readonly string _session;
    private readonly string _actor;
    private readonly Func<ActorRan, Task> _keep;
    private readonly MachineLog _log;
    private readonly ActorReports _reports;
    private readonly object _gate = new();
    private Task _tail = Task.CompletedTask;

    public ActorRun(SessionEvents events, string session, string actor, ActorRan? prior, Func<ActorRan, Task> keep, MachineLog? log = null)
    {
        if (actor is not ("build" or "intake" or "opinion" or "helper" or "partner" or "brainstorm" or "ask-daoris" or "chat"))
            throw new ArgumentException("unknown actor", nameof(actor));
        _events = events; _session = session; _actor = actor; _keep = keep; _log = log ?? MachineLog.None;
        _reports = new(prior);
        events.Evented += Heard;
    }

    public ActorRan? Ran { get { lock (_gate) return _reports.Snapshot; } }

    private void Heard(string session, SessionEvent e)
    {
        if (session != _session || e.Kind != SessionEventKind.Turn) return;
        Report(e.Models?.Reported ?? []);
    }

    public void Report(IEnumerable<string> models, ActorEntry? entry = null)
    {
        lock (_gate)
        {
            if (!_reports.Report(models, entry) || _reports.Snapshot is not { } ran) return;
            _tail = KeepAfterAsync(_tail, ran);
        }
    }

    private async Task KeepAfterAsync(Task before, ActorRan ran)
    {
        await before.ConfigureAwait(false);
        try { await _keep(ran).ConfigureAwait(false); }
        catch (Exception error) when (error is not OperationCanceledException)
        { _log.Write("warn", "actor.keep-failed", [("session", _session), ("actor", _actor)]); }
        _log.Write("info", "actor.ran", [("session", _session), ("actor", _actor), ("models", ran.Models), ("more", ran.More)]);
    }

    public async ValueTask DisposeAsync()
    {
        _events.Evented -= Heard;
        Task pending;
        lock (_gate) pending = _tail;
        await pending.ConfigureAwait(false);
    }
}
