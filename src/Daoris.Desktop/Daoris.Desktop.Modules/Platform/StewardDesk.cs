using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>Why the desk refuses a request or a decline, each said as data with nothing kept.</summary>
public static class DeskWhy
{
    /// <summary>The caller's ten open requests, or the desk's hundred, are filed: the per-caller bound is judged first.</summary>
    public const string Full = "desk-full";

    /// <summary>Not exactly <c>{ act, target, why }</c>, each a trimmed text within its length, every name once.</summary>
    public const string Form = "form";

    /// <summary>The act named is not one the steward's table makes: the owner's to do, so nobody asks the steward for it.</summary>
    public const string NotRole = "not-role";

    /// <summary>No holder holds the steward: the desk keeps nothing.</summary>
    public const string NotStaffed = "not-staffed";

    /// <summary>The decline names no request open on the desk: answered, declined, or never filed.</summary>
    public const string NotOpen = "not-open";
}

/// <summary>Where a desk request stands.</summary>
public static class DeskStates
{
    public const string Open = "open";

    /// <summary>Closed by the steward's act that made what it asked for (its act and its target).</summary>
    public const string Answered = "answered";

    /// <summary>Closed by the holder's decline, with its reason.</summary>
    public const string Declined = "declined";

    /// <summary>Open when Daoris stopped: its caller's run and the steward's appointment ended with it, so nothing answers it.</summary>
    public const string Interrupted = "interrupted";
}

/// <summary>
/// One request on the desk as the holder's verb and the owner's line read it: what the caller asked the steward
/// to do, to what and why, all three the caller's claims; when; where it stands; the act that answered it or the holder's reason for its
/// decline. Never the caller's key.
/// </summary>
public sealed record DeskRequestNow(
    string Id, string Act, string Target, string Why, DateTimeOffset Filed, string State, DateTimeOffset? Closed, string? AnsweredBy, string? Reason);

/// <summary>A filing's answer: filed with its id, or refused with why (<see cref="DeskWhy"/>), keeping nothing.</summary>
public sealed record DeskFiled(bool Filed, string? Id, string? Why);

/// <summary>Who a desk request is counted against: a key the per-caller bound is kept by, never shown.</summary>
public interface IDeskCaller
{
    /// <summary>The caller's key: the agent job the pipe's client is in, else its nearest long-lived ancestor; <see cref="DeskCaller.Untold"/> where neither is known.</summary>
    string KeyOf(PipeClientProcess? client);
}

/// <summary>
/// The desk's caller look. The pipe's client is the verb's own process, new at every
/// request, so the bound is never keyed by it: it is keyed by <b>the agent job the caller look places the verb in</b> (every session Daoris
/// runs), where it places it in one, else by <b>the nearest long-lived ancestor</b> the walk up the verb's parents finds,
/// the working session's harness or a terminal's shell. One agent running the verb a hundred times so fills its own ten alone.
/// </summary>
/// <remarks>
/// <para><b>Long-lived</b> is an ancestor that had lived <see cref="LongLived"/> as the request came: a shell a harness's tool starts for one
/// command, and the verb, are younger, so a loop of verbs is counted against the harness or terminal above it. The walk never passes this
/// Daoris's own process, which every terminal tab descends from. Where no ancestor is long-lived, the oldest it read is the key; where
/// it reads none, every such caller shares <see cref="Untold"/>'s ten.</para>
/// <para><b>One agents' job per Daoris process</b> (<c>AgentJobs</c>): every session one Daoris runs shares that job's ten. The working
/// session the owner runs outside it keeps its own.</para>
/// </remarks>
public sealed class DeskCaller(Func<PipeClientProcess?, string?> jobOf, IProcessTable processes, TimeProvider clock, int own) : IDeskCaller
{
    /// <summary>The key every caller whose job and ancestors cannot be read shares.</summary>
    public const string Untold = "untold";

    /// <summary>How long an ancestor has lived before it is the caller a request is counted against.</summary>
    public static readonly TimeSpan LongLived = TimeSpan.FromSeconds(60);

    /// <summary>The system's look: this process's job listing and its processes.</summary>
    public static DeskCaller System(StandingCaller jobs) =>
        new(jobs.JobOf, new SystemProcesses(), TimeProvider.System, Environment.ProcessId);

    public string KeyOf(PipeClientProcess? client)
    {
        if (jobOf(client) is { Length: > 0 } job) return "job:" + job;
        if (processes.IdOf(client) is not { } id || processes.Read(id) is not { } verb) return Untold;
        var now = clock.GetUtcNow().UtcDateTime;
        ProcessFacts? oldest = null;
        var child = verb;
        for (var level = 0; level < HolderWalk.Depth && child.Parent > 0 && child.Parent != own; level++)
        {
            var parent = processes.Read(child.Parent);
            // A parent created after its child is another process given the id since: the walk ends there.
            if (parent is null || parent.Created > child.Created) break;
            if (now - parent.Created >= LongLived) return Key(parent);
            oldest = parent;
            child = parent;
        }

        return oldest is null ? Untold : Key(oldest);
    }

    private static string Key(ProcessFacts process) =>
        $"process:{process.Id.ToString(CultureInfo.InvariantCulture)}:{process.Created.Ticks.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>
/// The steward's desk: what any caller asks the steward to do, read by the holder as claims.
/// <c>STEWARD_REQUEST { act, target, why }</c> files one, an <c>Open</c> route any caller may use; at most
/// <see cref="PerCaller"/> open per caller (<see cref="IDeskCaller"/>) and <see cref="InAll"/> open in all, the per-caller bound judged
/// first, another refused <c>desk-full</c>. <b>It grants nothing and never admits an act</b>: an act it asks for is judged by
/// <c>ROLE_ACT</c> like any other, from the holder alone; the act that makes what a request asked (its act, on its target) closes it as
/// answered, and the holder's <c>desk-decline</c> closes it with a reason.
/// </summary>
/// <remarks>
/// <para>Its own lock, never taken around the judge's: the judge calls in under its own, and nothing here calls out.</para>
/// <para><b>Its history</b>: every change is written to <paramref name="path"/>, and a desk read back
/// at a restart reads each request still open <see cref="DeskStates.Interrupted"/>. The callers and the appointment were the run's, so
/// none is revived: the file keeps no caller's key and no grant, and an interrupted request counts toward no caller's bound.</para>
/// </remarks>
/// <param name="path">Where its history is kept; none keeps it in the run's memory alone.</param>
public sealed class StewardDesk(TimeProvider clock, string? path = null)
{
    /// <summary>Open requests one caller may have on the desk.</summary>
    public const int PerCaller = 10;

    /// <summary>Open requests the desk holds in all.</summary>
    public const int InAll = 100;

    /// <summary>Closed requests kept for the line and the verb, the oldest dropped past it: the desk's memory is bounded too.</summary>
    public const int ClosedKept = 100;

    /// <summary>The longest act name, target and reason a request carries.</summary>
    public const int ActLength = 40, TargetLength = 200, WhyLength = 2000;

    /// <summary>What a desk act's target names, so no owner's mark or gate is a request's.</summary>
    public const string Kind = "desk";

    private sealed class Held(string id, string act, string target, string why, string caller, DateTimeOffset filed)
    {
        public string Id { get; } = id;
        public string Act { get; } = act;
        public string Target { get; } = target;
        public string Why { get; } = why;
        public string Caller { get; } = caller;
        public DateTimeOffset Filed { get; } = filed;
        public string State { get; set; } = DeskStates.Open;
        public DateTimeOffset? Closed { get; set; }
        public string? AnsweredBy { get; set; }
        public string? Reason { get; set; }

        public DeskRequestNow Now() => new(Id, Act, Target, Why, Filed, State, Closed, AnsweredBy, Reason);
    }

    private readonly Lock _gate = new();
    private readonly List<Held> _requests = Restarted(path, clock);

    /// <summary>Where the desk's history is kept; null where the run's memory alone holds it.</summary>
    public string? Path => path;

    /// <summary>
    /// A request filed by <paramref name="caller"/>: exactly <c>{ act, target, why }</c>, the act one <paramref name="acts"/> names (the
    /// steward's table); kept open, or refused with nothing kept.
    /// </summary>
    public DeskFiled File(JsonElement? payload, string caller, IEnumerable<string> acts)
    {
        if (Read(payload) is not { } asked) return new DeskFiled(false, null, DeskWhy.Form);
        if (!acts.Contains(asked.Act, StringComparer.Ordinal)) return new DeskFiled(false, null, DeskWhy.NotRole);
        lock (_gate)
        {
            var open = _requests.Where(each => each.State == DeskStates.Open).ToList();
            if (open.Count(each => each.Caller == caller) >= PerCaller || open.Count >= InAll) return new DeskFiled(false, null, DeskWhy.Full);
            var id = "r" + Guid.NewGuid().ToString("N")[..10];
            _requests.Add(new Held(id, asked.Act, asked.Target, asked.Why, caller, clock.GetUtcNow()));
            Keep();
            return new DeskFiled(true, id, null);
        }
    }

    /// <summary>
    /// The steward's act that ran: every open request for that act on what it acted on (its repository, quest or session, as
    /// the act names them, without case or a quest's <c>#</c>) closed as answered by it.
    /// </summary>
    public void Answered(string actId, string act, params string?[] targets)
    {
        var named = targets.OfType<string>().Select(Plain).ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            foreach (var each in _requests.Where(each => each.State == DeskStates.Open && each.Act == act && named.Contains(Plain(each.Target))))
            {
                Close(each, DeskStates.Answered);
                each.AnsweredBy = actId;
            }

            Trim();
            Keep();
        }
    }

    /// <summary>Whether a request by this id is open on the desk.</summary>
    public bool IsOpen(string id)
    {
        lock (_gate) return _requests.Any(each => each.Id == id && each.State == DeskStates.Open);
    }

    /// <summary>The holder's decline of an open request with its reason; false where none by the id is open.</summary>
    public bool Decline(string id, string reason)
    {
        lock (_gate)
        {
            if (_requests.FirstOrDefault(each => each.Id == id && each.State == DeskStates.Open) is not { } held) return false;
            Close(held, DeskStates.Declined);
            held.Reason = reason;
            Trim();
            Keep();
            return true;
        }
    }

    /// <summary>The desk now, newest first: the open requests and the closed the desk still keeps.</summary>
    public IReadOnlyList<DeskRequestNow> Now()
    {
        lock (_gate) return [.. Enumerable.Reverse(_requests).Select(each => each.Now())];
    }

    // Under the lock.
    private void Close(Held held, string state)
    {
        held.State = state;
        held.Closed = clock.GetUtcNow();
    }

    // Under the lock: the oldest closed dropped past what the desk keeps.
    private void Trim() => Trim(_requests);

    private static void Trim(List<Held> requests)
    {
        var closed = requests.Where(each => each.State != DeskStates.Open).ToList();
        foreach (var dropped in closed.Take(Math.Max(0, closed.Count - ClosedKept))) requests.Remove(dropped);
    }

    // Under the lock: the history written whole, oldest first. A write that fails leaves the run's memory as it is: the desk still
    // answers, and the next change writes it again.
    private void Keep() => Write(path, _requests);

    private static void Write(string? path, IReadOnlyList<Held> requests)
    {
        if (path is null) return;
        try
        {
            JsonFile.Write(path, writer =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("requests");
                foreach (var each in requests)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", each.Id);
                    writer.WriteString("act", each.Act);
                    writer.WriteString("target", each.Target);
                    writer.WriteString("why", each.Why);
                    writer.WriteString("filed", Moments.Write(each.Filed));
                    writer.WriteString("state", each.State);
                    if (each.Closed is { } closed) writer.WriteString("closed", Moments.Write(closed));
                    if (each.AnsweredBy is { } by) writer.WriteString("answeredBy", by);
                    if (each.Reason is { } reason) writer.WriteString("reason", reason);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// The history a restart reads: each request as it is kept, one still open read <see cref="DeskStates.Interrupted"/> at
    /// this moment and written so. A file that does not read is an empty desk; a request in another shape is passed over.
    /// </summary>
    private static List<Held> Restarted(string? path, TimeProvider clock)
    {
        var read = new List<Held>();
        if (path is null || !System.IO.File.Exists(path)) return read;
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            if (!document.RootElement.TryGetProperty("requests", out var requests) || requests.ValueKind != JsonValueKind.Array) return read;
            foreach (var each in requests.EnumerateArray())
            {
                if (Json.Text(each, "id") is not { } id || Json.Text(each, "act") is not { } act || Json.Text(each, "target") is not { } target
                    || Json.Text(each, "why") is not { } why || Moments.Read(Json.Text(each, "filed")) is not { } filed
                    || Json.Text(each, "state") is not { } state)
                {
                    continue;
                }

                var held = new Held(id, act, target, why, "", filed)
                {
                    State = state, Closed = Moments.Read(Json.Text(each, "closed")), AnsweredBy = Json.Text(each, "answeredBy"), Reason = Json.Text(each, "reason"),
                };
                if (held.State == DeskStates.Open)
                {
                    held.State = DeskStates.Interrupted;
                    held.Closed = clock.GetUtcNow();
                }

                read.Add(held);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }

        Trim(read);
        Write(path, read);
        return read;
    }

    private static string Plain(string target) => target.TrimStart('#');

    /// <summary>The request's exact form: <c>{ act, target, why }</c>, every name once ignoring case, each a trimmed text within its length.</summary>
    public static (string Act, string Target, string Why)? Read(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body) return null;
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in body.EnumerateObject())
        {
            if (!fields.TryAdd(property.Name, property.Value)) return null;
        }

        string[] named = ["act", "target", "why"];
        if (fields.Count != named.Length || !named.All(fields.ContainsKey)) return null;
        return Text(fields["act"], ActLength) is { } act && Text(fields["target"], TargetLength) is { } target && Text(fields["why"], WhyLength) is { } why
            ? (act, target, why)
            : null;
    }

    /// <summary>A decline's exact form: <c>{ request, reason }</c>, as <see cref="Read"/> reads a request's.</summary>
    public static (string Request, string Reason)? ReadDecline(JsonElement form)
    {
        if (form.ValueKind != JsonValueKind.Object) return null;
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in form.EnumerateObject())
        {
            if (!fields.TryAdd(property.Name, property.Value)) return null;
        }

        string[] named = ["request", "reason"];
        if (fields.Count != named.Length || !named.All(fields.ContainsKey)) return null;
        return Text(fields["request"], 40) is { } request && Text(fields["reason"], WhyLength) is { } reason ? (request, reason) : null;
    }

    private static string? Text(JsonElement value, int max) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text && text.Length <= max
        && text.Trim().Length == text.Length ? text : null;
}

/// <summary>
/// The holder's decline of a desk request: <c>ROLE_ACT</c>'s <c>desk-decline</c>, <c>{ request, reason }</c>,
/// judged as every act is, from the holder alone. Not one of the steward's table acts: it changes nothing of Daoris's controls, so it is in no
/// appointment's <c>acts</c> and every steward's appointment may make it; its target is the request, which no owner's mark or gate names.
/// </summary>
public sealed class DeskDecline : IRoleAct
{
    public const string Named = "desk-decline";

    public string Name => Named;

    public string TargetKind => StewardDesk.Kind;

    public bool IsForm(JsonElement form) => StewardDesk.ReadDecline(form) is not null;

    public string Target(JsonElement form) => StewardDesk.ReadDecline(form)!.Value.Request;

    public Task<string?> UnderGatesAsync(RoleActRun run, CancellationToken ct) =>
        Task.FromResult(run.Judge.Desk.IsOpen(Target(run.Form)) ? null : DeskWhy.NotOpen);

    public Task<object?> RunAsync(RoleActRun run, CancellationToken ct)
    {
        var (request, reason) = StewardDesk.ReadDecline(run.Form)!.Value;
        return Task.FromResult<object?>(new { Declined = run.Judge.Desk.Decline(request, reason), Request = request });
    }
}
