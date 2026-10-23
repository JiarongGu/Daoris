using System.Text.Json;

namespace Daoris.Driver;

/// <summary>D205's tagged request: an explicit tool alias `default` differs from an account default.</summary>
public sealed record ActorValue(string Source, string? Value, bool Carried);
public sealed record ActorAsked(string Agent, ActorValue Model, ActorValue Effort)
{
    public static ActorAsked Before(string agent) => new(agent, new("account-default", null, false), new("account-default", null, false));

    /// <summary>
    /// The model its choice named, which each account's offer reading is judged for (D205 §4.1, the walk's model parameter); null for
    /// account default, the person's own command or environment, which no reading passes.
    /// </summary>
    public string? NamedModel => Model is { Source: "entry", Value: { } model } ? model : null;

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
                if (named is not null) return value is { Source: "entry", Carried: true } && value.Value == named ? value : new("command", named, false);
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

    /// <summary>The native carrier, shared by first starts, resumes and chats. Existing command flags retain precedence.</summary>
    public IEnumerable<string> NativeArguments(IReadOnlyList<string> command)
    {
        if (Model is { Source: "entry", Value: { } model, Carried: true } && !Has(command, "--model"))
        { yield return "--model"; yield return model; }
        if (Effort is { Source: "entry", Value: { } effort, Carried: true } && !Has(command, "--effort"))
        { yield return "--effort"; yield return effort; }
    }

    private static bool Has(IEnumerable<string> command, string flag) => command.Any(word => word == flag || word.StartsWith(flag + "=", StringComparison.Ordinal));
}

/// <summary>
/// What a protocol open keeps and asks about its agent's offers (D205 §3.3, §9.2, MODELROLE1c2): the version and account its reading is
/// keyed by, and the start's re-ask where a choice is refused at the open, answering a later choice and its <c>choice</c> to set on the
/// same session, or null where the record ends <c>choice-not-offered</c>. A null re-ask keeps MODELROLE1c's not-carried reading.
/// </summary>
public sealed record OpenOffers(string? Version, string? Account, Func<ModelRefusal, Task<(ActorAsked Asked, string Choice)?>>? Refused);

public sealed record ActorEntry(DateTimeOffset At, string? Acknowledged = null, string? Effort = null);
public sealed record ActorRan(IReadOnlyList<string> Models, int More, IReadOnlyList<ActorEntry> Entries, int EntriesMore);

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
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
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
