using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What a plugin reports of a step (the fabric events design §C4, §D4): reached, failed, or not known.</summary>
public static class WorkStates
{
    public const string Reached = "reached";

    public const string Failed = "failed";

    public const string Unknown = "unknown";

    public static readonly IReadOnlyList<string> All = [Reached, Failed, Unknown];
}

/// <summary>What <c>work/reconcile</c> answers of an attempt (§D6), each with what Daoris then does (PERFORM1b).</summary>
public static class ReconcileStates
{
    public const string Done = "done";

    public const string NotStarted = "not-started";

    public const string Running = "running";

    public const string Failed = "failed";

    public const string Unknown = "unknown";

    public static readonly IReadOnlyList<string> All = [Done, NotStarted, Running, Failed, Unknown];
}

/// <summary>One unit a plugin is told about at <see cref="HookPoints.Watch"/> (§C4): where it is, its tip, and which of the plugin's steps it awaits.</summary>
public sealed record WatchedUnit(
    string Repository, string Workspace, string Root, string Branch, string Tip, string? Line, string? PullRequest, IReadOnlyList<string> Awaiting)
{
    /// <summary>
    /// The canonical unit it is told for (FABRIC-PERFORM1i): the host's half of the frame, never written on the wire, so an event bound to
    /// the frame actually sent is bound to the unit that frame named, never looked up by its repository's name alone (§C4's third rule).
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>The repository's identity (§B5): its common git folder's file identity, else its spelling. The host's alone, as <see cref="Unit"/> is.</summary>
    public string? Identity { get; init; }
}

/// <summary>
/// A <see cref="HookPoints.Watch"/> frame (§C4): the whole awaited set, the generation of the process it is sent to, and whether the plugin
/// sends again what it knows of each awaited step (after a start, a restart or any drop).
/// </summary>
public sealed record WorkWatchFrame(int Generation, bool Resync, IReadOnlyList<WatchedUnit> Units);

/// <summary>An event's evidence, the plugin's word (§C4, D143): each part optional, the message 300 characters at most.</summary>
public sealed record WorkEvidence(string? Ref, string? Url, string? Environment, string? Message);

/// <summary>An output a reached step names (§D6): a reference <c>&lt;schema&gt;:&lt;id&gt;</c>, never its payload, and its revision.</summary>
public sealed record WorkOutput(string Ref, string? Revision);

/// <summary>An outcome as a plugin reports it (§D4, §D6): its state, a failure's code, its evidence, and a reached step's outputs.</summary>
public sealed record WorkOutcome(
    string State, string? Code = null, WorkEvidence? Evidence = null, IReadOnlyDictionary<string, WorkOutput>? Outputs = null);

/// <summary>
/// A <see cref="HookPoints.Step"/> event (§C4, §D6): an observed step bound to a repository, a branch and a full commit, or a performed
/// step's completion bound to its invocation. The step is the plugin's own id, or <c>&lt;plugin&gt;/&lt;id&gt;</c>.
/// </summary>
public sealed record WorkStepFrame(
    string Step, WorkOutcome Outcome, DateTimeOffset At, string Id,
    string? Repository = null, string? Branch = null, string? Commit = null, string? Invocation = null);

/// <summary>A <see cref="HookPoints.Perform"/> request's params (§D6): one attempt, with the key the plugin deduplicates it by.</summary>
public sealed record WorkPerformCall(
    string Unit, string Step, int StepVersion, string Invocation, string IdempotencyKey, string SubjectRevision,
    IReadOnlyList<string> Inputs, DateTimeOffset DeadlineAt);

/// <summary>A <c>work/reconcile</c> request's params (§D6): what became of one attempt.</summary>
public sealed record WorkReconcileCall(string Unit, string Step, string Invocation, string IdempotencyKey, string SubjectRevision);

/// <summary><c>work/reconcile</c>'s answer (§D6): its state, and the outcome where it is done or failed.</summary>
public sealed record WorkReconcileAnswer(string State, WorkOutcome? Outcome = null);

/// <summary>
/// A step event as the hook set heard it (FABRIC-PERFORM1a): the plugin and the generation of the process it was read from, never what the
/// frame says of its sender (§C4's first belief rule), and the frame read by shape. What is believed of it is <see cref="WorkBelief"/>'s.
/// </summary>
public sealed record WorkStepHeard(string Plugin, int Generation, WorkStepFrame Step);

/// <summary>
/// The work wire's contract, <c>work.perform@1</c> (FABRIC-PERFORM1a; the fabric events design §C4, §D6): every message Daoris sends at the
/// work points and every message a plugin sends back, written and read here once, so the hook set, a stand-in plugin and the kit's
/// scaffold (PERFORM1e) say the same thing. Every name is spelled here rather than left to a serializer's policy, as
/// <see cref="HookFrames"/>'s are.
/// </summary>
public static class WorkWire
{
    /// <summary>The capability a performer's handshake names (§D6). <c>protocolVersion</c> stays 1 (D148 point 8's rule).</summary>
    public const string Capability = "work.perform@1";

    public const string WatchMethod = "hook/" + HookPoints.Watch;

    public const string PerformMethod = "hook/" + HookPoints.Perform;

    public const string ReconcileMethod = "hook/" + PluginSteps.ReconcilePoint;

    /// <summary>The plugin's notification: the one method a plugin may send unasked, and only to the lease's loop.</summary>
    public const string StepMethod = HookPoints.Step;

    /// <summary>An evidence message's length at most (§C4).</summary>
    public const int MessageChars = 300;

    /// <summary>How many outputs a reached step names at most, and a reference's length at most (§D6).</summary>
    public const int OutputsAtMost = 8;

    public const int RefChars = 200;

    /// <summary>A name's length at most: a repository, an id, an invocation, a key, a revision, an environment (FABRIC-PERFORM1a's choice).</summary>
    public const int NameChars = 200;

    /// <summary>A branch's length at most, and an evidence address's.</summary>
    public const int BranchChars = 250;

    public const int UrlChars = 2000;

    /// <summary>A full commit id: SHA-1's 40 or SHA-256's 64 lowercase hex digits (§C4: <c>&lt;full id&gt;</c>).</summary>
    private static readonly Regex FullCommit = new("^[0-9a-f]{40}(?:[0-9a-f]{24})?$", RegexOptions.CultureInvariant);

    /// <summary>A failure's code, as a step's <c>transient</c> codes are written (UNIT1d): lowercase letters, digits and dashes.</summary>
    private static readonly Regex CodeShape = new("^[a-z0-9][a-z0-9-]{0,39}$", RegexOptions.CultureInvariant);

    /// <summary>An instant as the wire writes it: ISO 8601 with its offset or <c>Z</c>, so no reader takes it for local time.</summary>
    private static readonly Regex Instant =
        new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant);

    // ——— Daoris's messages

    /// <summary><see cref="HookPoints.Watch"/>'s params: the whole awaited set, the generation, and whether to send again (§C4).</summary>
    public static JsonObject Watch(WorkWatchFrame frame) => new()
    {
        ["generation"] = frame.Generation,
        ["resync"] = frame.Resync,
        ["units"] = new JsonArray([.. frame.Units.Select(unit => (JsonNode)new JsonObject
        {
            ["repository"] = unit.Repository,
            ["workspace"] = unit.Workspace,
            ["root"] = unit.Root,
            ["branch"] = unit.Branch,
            ["tip"] = unit.Tip,
            ["line"] = unit.Line,
            ["pullRequest"] = unit.PullRequest,
            ["awaiting"] = Strings(unit.Awaiting),
        })]),
    };

    /// <summary><see cref="HookPoints.Perform"/>'s params (§D6), as its example writes them.</summary>
    public static JsonObject Perform(WorkPerformCall call) => new()
    {
        ["unit"] = call.Unit,
        ["step"] = call.Step,
        ["stepVersion"] = call.StepVersion,
        ["invocation"] = call.Invocation,
        ["idempotencyKey"] = call.IdempotencyKey,
        ["subjectRevision"] = call.SubjectRevision,
        ["inputs"] = Strings(call.Inputs),
        ["deadlineAt"] = Time(call.DeadlineAt),
    };

    /// <summary><c>work/reconcile</c>'s params (§D6): <c>{ unit, step, invocation, idempotencyKey, subjectRevision }</c>.</summary>
    public static JsonObject Reconcile(WorkReconcileCall call) => new()
    {
        ["unit"] = call.Unit,
        ["step"] = call.Step,
        ["invocation"] = call.Invocation,
        ["idempotencyKey"] = call.IdempotencyKey,
        ["subjectRevision"] = call.SubjectRevision,
    };

    // ——— a plugin's messages: what a stand-in and the kit's scaffold write

    /// <summary>
    /// A <see cref="HookPoints.Step"/> event as a whole frame (§C4): a notification, with no id. Its outcome's fields sit beside the step's,
    /// as §C4's frame writes <c>state</c> and <c>evidence</c>; a part not said is not written.
    /// </summary>
    public static JsonObject Step(WorkStepFrame step)
    {
        var @params = new JsonObject { ["step"] = step.Step };
        if (step.Repository is not null) @params["repository"] = step.Repository;
        if (step.Branch is not null) @params["branch"] = step.Branch;
        if (step.Commit is not null) @params["commit"] = step.Commit;
        if (step.Invocation is not null) @params["invocation"] = step.Invocation;
        Outcome(@params, step.Outcome);
        @params["at"] = Time(step.At);
        @params["id"] = step.Id;
        return new JsonObject { ["jsonrpc"] = "2.0", ["method"] = StepMethod, ["params"] = @params };
    }

    /// <summary><see cref="HookPoints.Perform"/>'s answer (§D6): receipt of this invocation, and no more. The outcome comes later as an event.</summary>
    public static JsonObject PerformAnswer(string invocation) => new() { ["invocation"] = invocation };

    /// <summary><c>work/reconcile</c>'s answer (§D6): its state, and its outcome where it has one.</summary>
    public static JsonObject ReconcileAnswer(WorkReconcileAnswer answer)
    {
        var written = new JsonObject { ["state"] = answer.State };
        if (answer.Outcome is { } outcome) written["outcome"] = Outcome([], outcome);
        return written;
    }

    // ——— reading what a plugin sends

    /// <summary>
    /// A <see cref="HookPoints.Step"/> event's params read by shape (§C4's fifth rule), or null with the first broken field named: the
    /// step, its outcome, its time and its id always; a repository, a branch and a full commit where it binds to no invocation. A field
    /// this build does not know (a <c>plugin</c> above all, §C4's first rule) is passed over.
    /// </summary>
    public static WorkStepFrame? ReadStep(JsonElement @params, out string? field)
    {
        field = "params";
        if (@params.ValueKind != JsonValueKind.Object) return null;

        field = "step";
        if (Text(@params, "step") is not { } step || !IsStep(step)) return null;

        var outcome = ReadOutcome(@params, out field);
        if (outcome is null) return null;

        field = "at";
        if (Text(@params, "at") is not { } written || ReadTime(written) is not { } at) return null;
        field = "id";
        if (Name(@params, "id", NameChars) is not { } id) return null;

        field = "invocation";
        if (!Optional(@params, "invocation", NameChars, out var invocation)) return null;

        // An observed step binds to a commit (§C4's fourth rule); a completion binds to the attempt it names (§D6).
        var bound = invocation is null;
        field = "repository";
        if (!Optional(@params, "repository", NameChars, out var repository) || (bound && repository is null)) return null;
        field = "branch";
        if (!Optional(@params, "branch", BranchChars, out var branch) || (bound && branch is null)) return null;
        field = "commit";
        if (!Optional(@params, "commit", 64, out var commit) || (commit is not null && !FullCommit.IsMatch(commit)) || (bound && commit is null))
        {
            return null;
        }

        field = null;
        return new WorkStepFrame(step, outcome, at, id, repository, branch, commit, invocation);
    }

    /// <summary>Whether <paramref name="answer"/> acknowledges receipt of <paramref name="invocation"/>: <c>{ "invocation": "…" }</c>, the same one.</summary>
    public static bool ReadPerformAnswer(JsonElement answer, string invocation) =>
        answer.ValueKind == JsonValueKind.Object && Text(answer, "invocation") == invocation;

    /// <summary>
    /// <c>work/reconcile</c>'s answer read by shape (§D6), or null: <c>done</c> carries an outcome reached or failed, <c>failed</c> one
    /// failed, and the other three none. Its <c>resources</c> are PERFORM1d's, and passed over here.
    /// </summary>
    public static WorkReconcileAnswer? ReadReconcileAnswer(JsonElement answer)
    {
        if (answer.ValueKind != JsonValueKind.Object || Text(answer, "state") is not { } state || !ReconcileStates.All.Contains(state)) return null;

        WorkOutcome? outcome = null;
        if (answer.TryGetProperty("outcome", out var said) && said.ValueKind != JsonValueKind.Null)
        {
            if (said.ValueKind != JsonValueKind.Object || (outcome = ReadOutcome(said, out _)) is null) return null;
        }

        var fits = state switch
        {
            ReconcileStates.Done => outcome?.State is WorkStates.Reached or WorkStates.Failed,
            ReconcileStates.Failed => outcome?.State == WorkStates.Failed,
            _ => outcome is null,
        };
        return fits ? new WorkReconcileAnswer(state, outcome) : null;
    }

    // ——— reading what Daoris sends: a stand-in's and the kit's

    /// <summary><see cref="HookPoints.Watch"/>'s params read back, or null where they are not its shape.</summary>
    public static WorkWatchFrame? ReadWatch(JsonElement @params)
    {
        if (@params.ValueKind != JsonValueKind.Object || Whole(@params, "generation") is not { } generation
            || !@params.TryGetProperty("resync", out var resync) || resync.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !@params.TryGetProperty("units", out var units) || units.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var read = new List<WatchedUnit>();
        foreach (var unit in units.EnumerateArray())
        {
            if (unit.ValueKind != JsonValueKind.Object
                || Text(unit, "repository") is not { } repository || Text(unit, "workspace") is not { } workspace
                || Text(unit, "root") is not { } root || Text(unit, "branch") is not { } branch || Text(unit, "tip") is not { } tip
                || !Optional(unit, "line", int.MaxValue, out var line) || !Optional(unit, "pullRequest", int.MaxValue, out var pullRequest)
                || StringList(unit, "awaiting") is not { } awaiting)
            {
                return null;
            }

            read.Add(new WatchedUnit(repository, workspace, root, branch, tip, line, pullRequest, awaiting));
        }

        return new WorkWatchFrame(generation, resync.GetBoolean(), read);
    }

    /// <summary><see cref="HookPoints.Perform"/>'s params read back, or null where they are not its shape.</summary>
    public static WorkPerformCall? ReadPerform(JsonElement @params) =>
        @params.ValueKind == JsonValueKind.Object
        && Text(@params, "unit") is { } unit && Text(@params, "step") is { } step && Whole(@params, "stepVersion") is { } version
        && Text(@params, "invocation") is { } invocation && Text(@params, "idempotencyKey") is { } key
        && Text(@params, "subjectRevision") is { } revision && StringList(@params, "inputs") is { } inputs
        && Text(@params, "deadlineAt") is { } deadline && ReadTime(deadline) is { } deadlineAt
            ? new WorkPerformCall(unit, step, version, invocation, key, revision, inputs, deadlineAt)
            : null;

    /// <summary><c>work/reconcile</c>'s params read back, or null where they are not its shape.</summary>
    public static WorkReconcileCall? ReadReconcile(JsonElement @params) =>
        @params.ValueKind == JsonValueKind.Object
        && Text(@params, "unit") is { } unit && Text(@params, "step") is { } step && Text(@params, "invocation") is { } invocation
        && Text(@params, "idempotencyKey") is { } key && Text(@params, "subjectRevision") is { } revision
            ? new WorkReconcileCall(unit, step, invocation, key, revision)
            : null;

    // ——— the parts

    /// <summary>An outcome's fields written into <paramref name="into"/>: its state, then each part said.</summary>
    private static JsonObject Outcome(JsonObject into, WorkOutcome outcome)
    {
        into["state"] = outcome.State;
        if (outcome.Code is not null) into["code"] = outcome.Code;
        if (outcome.Evidence is { } evidence)
        {
            var written = new JsonObject();
            if (evidence.Ref is not null) written["ref"] = evidence.Ref;
            if (evidence.Url is not null) written["url"] = evidence.Url;
            if (evidence.Environment is not null) written["environment"] = evidence.Environment;
            if (evidence.Message is not null) written["message"] = evidence.Message;
            into["evidence"] = written;
        }

        if (outcome.Outputs is { Count: > 0 } outputs)
        {
            var written = new JsonObject();
            foreach (var (name, output) in outputs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var one = new JsonObject { ["ref"] = output.Ref };
                if (output.Revision is not null) one["revision"] = output.Revision;
                written[name] = one;
            }

            into["outputs"] = written;
        }

        return into;
    }

    /// <summary>
    /// An outcome's fields read from <paramref name="from"/> by shape, or null with the broken field: a state of the three, a failure's code,
    /// evidence of its four optional parts, and outputs on a reached outcome alone, at most 8, each a reference to a declared kind of thing.
    /// </summary>
    private static WorkOutcome? ReadOutcome(JsonElement from, out string? field)
    {
        field = "state";
        if (Text(from, "state") is not { } state || !WorkStates.All.Contains(state)) return null;

        field = "code";
        if (!Optional(from, "code", 40, out var code) || (code is not null && !CodeShape.IsMatch(code))) return null;

        field = "evidence";
        WorkEvidence? evidence = null;
        if (from.TryGetProperty("evidence", out var said) && said.ValueKind != JsonValueKind.Null)
        {
            if (said.ValueKind != JsonValueKind.Object
                || !Optional(said, "ref", RefChars, out var reference) || !Optional(said, "url", UrlChars, out var url)
                || (url is not null && !IsWebPage(url))
                || !Optional(said, "environment", NameChars, out var environment)
                || !Words(said, "message", MessageChars, out var message))
            {
                return null;
            }

            evidence = new WorkEvidence(reference, url, environment, message);
        }

        field = "outputs";
        Dictionary<string, WorkOutput>? outputs = null;
        if (from.TryGetProperty("outputs", out var named) && named.ValueKind != JsonValueKind.Null)
        {
            if (named.ValueKind != JsonValueKind.Object || state != WorkStates.Reached) return null;
            outputs = new Dictionary<string, WorkOutput>(StringComparer.Ordinal);
            foreach (var output in named.EnumerateObject())
            {
                if (outputs.Count == OutputsAtMost || !PluginSteps.NameShape.IsMatch(output.Name) || output.Value.ValueKind != JsonValueKind.Object
                    || Name(output.Value, "ref", RefChars) is not { } reference || !IsReference(reference)
                    || !Optional(output.Value, "revision", NameChars, out var revision))
                {
                    return null;
                }

                outputs[output.Name] = new WorkOutput(reference, revision);
            }
        }

        field = null;
        return new WorkOutcome(state, code, evidence, outputs);
    }

    /// <summary>A step's name: the plugin's own id, or <c>&lt;plugin&gt;/&lt;id&gt;</c> (the kind, as §D6's example names it).</summary>
    private static bool IsStep(string step)
    {
        var slash = step.LastIndexOf('/');
        return slash < 0
            ? PluginSteps.NameShape.IsMatch(step)
            : PluginCatalog.IsId(step[..slash]) && PluginSteps.NameShape.IsMatch(step[(slash + 1)..]);
    }

    /// <summary>A reference, <c>&lt;schema&gt;:&lt;opaque id&gt;</c> (§D6): a declared schema and an id that is not empty.</summary>
    private static bool IsReference(string reference)
    {
        var colon = reference.IndexOf(':');
        return colon > 0 && colon < reference.Length - 1 && PluginSteps.IsSchema(reference[..colon]);
    }

    /// <summary>Only a web page is an address a person goes to read, as a landing's pull request is (Hooks.cs).</summary>
    private static bool IsWebPage(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var address) && address.Scheme is "https" or "http";

    private static DateTimeOffset? ReadTime(string written) =>
        Instant.IsMatch(written)
        && DateTimeOffset.TryParse(written, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            ? at
            : null;

    private static string Time(DateTimeOffset at) => at.ToString("o", CultureInfo.InvariantCulture);

    private static JsonArray Strings(IEnumerable<string> values) => new([.. values.Select(value => (JsonNode)JsonValue.Create(value))]);

    private static string? Text(JsonElement from, string field) =>
        from.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>A name: words of <paramref name="most"/> characters at most, not empty, and no control character among them.</summary>
    private static string? Name(JsonElement from, string field, int most) =>
        Text(from, field) is { Length: > 0 } text && text.Length <= most && !text.Any(char.IsControl) ? text : null;

    /// <summary>An optional name: absent or null reads as null and fits; anything else must be a name.</summary>
    private static bool Optional(JsonElement from, string field, int most, out string? value)
    {
        value = null;
        if (!from.TryGetProperty(field, out var said) || said.ValueKind == JsonValueKind.Null) return true;
        value = Name(from, field, most);
        return value is not null;
    }

    /// <summary>Optional words a person reads: absent or null, or text of <paramref name="most"/> characters at most.</summary>
    private static bool Words(JsonElement from, string field, int most, out string? value)
    {
        value = null;
        if (!from.TryGetProperty(field, out var said) || said.ValueKind == JsonValueKind.Null) return true;
        value = said.ValueKind == JsonValueKind.String && said.GetString() is { } text && text.Length <= most ? text : null;
        return value is not null;
    }

    private static int? Whole(JsonElement from, string field) =>
        from.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var whole) && whole >= 1
            ? whole
            : null;

    private static IReadOnlyList<string>? StringList(JsonElement from, string field)
    {
        if (!from.TryGetProperty(field, out var list) || list.ValueKind != JsonValueKind.Array) return null;
        var read = new List<string>();
        foreach (var each in list.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.String) return null;
            read.Add(each.GetString()!);
        }

        return read;
    }
}
