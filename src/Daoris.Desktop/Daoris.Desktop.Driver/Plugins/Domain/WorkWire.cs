using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What a plugin reports of a step: reached, failed, or not known.</summary>
public static class WorkStates
{
    public const string Reached = "reached";

    public const string Failed = "failed";

    public const string Unknown = "unknown";

    public static readonly IReadOnlyList<string> All = [Reached, Failed, Unknown];
}

/// <summary>What <c>work/reconcile</c> answers of an attempt, each with what Daoris then does.</summary>
public static class ReconcileStates
{
    public const string Done = "done";

    public const string NotStarted = "not-started";

    public const string Running = "running";

    public const string Failed = "failed";

    public const string Unknown = "unknown";

    public static readonly IReadOnlyList<string> All = [Done, NotStarted, Running, Failed, Unknown];
}

/// <summary>One unit a plugin is told about at <see cref="HookPoints.Watch"/>: where it is, its tip, and which of the plugin's steps it awaits.</summary>
public sealed record WatchedUnit(
    string Repository, string Workspace, string Root, string Branch, string Tip, string? Line, string? PullRequest, IReadOnlyList<string> Awaiting)
{
    /// <summary>
    /// The canonical unit it is told for: the host's half of the frame, never written on the wire, so an event bound to
    /// the frame actually sent is bound to the unit that frame named, never looked up by its repository's name alone.
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>The repository's identity: its common git folder's file identity, else its spelling. The host's alone, as <see cref="Unit"/> is.</summary>
    public string? Identity { get; init; }

    /// <summary>
    /// Whether it tells a landed branch at a landing's tip: an event is judged against <see cref="Tip"/>,
    /// which the landing's record keeps, never the branch's live tip, which a later landing moves on. The host's alone, as <see cref="Unit"/> is.
    /// </summary>
    public bool Landing { get; init; }
}

/// <summary>
/// A <see cref="HookPoints.Watch"/> frame: the whole awaited set, the generation of the process it is sent to, and whether the plugin
/// sends again what it knows of each awaited step (after a start, a restart or any drop).
/// </summary>
public sealed record WorkWatchFrame(int Generation, bool Resync, IReadOnlyList<WatchedUnit> Units);

/// <summary>An event's evidence, the plugin's word: each part optional, the message 300 characters at most.</summary>
public sealed record WorkEvidence(string? Ref, string? Url, string? Environment, string? Message);

/// <summary>An output a reached step names: a reference <c>&lt;schema&gt;:&lt;id&gt;</c>, never its payload, and its revision.</summary>
public sealed record WorkOutput(string Ref, string? Revision);

/// <summary>
/// What a plugin says it created: a declared schema and the opaque id the plugin
/// issued for it, which only the plugin reads. Never a path: Daoris hands it back at <c>work/cleanup</c> and runs nothing for it.
/// </summary>
public sealed record WorkResource(string Schema, string Id);

/// <summary>An outcome as a plugin reports it: its state, a failure's code, its evidence, and a reached step's outputs.</summary>
public sealed record WorkOutcome(
    string State, string? Code = null, WorkEvidence? Evidence = null, IReadOnlyDictionary<string, WorkOutput>? Outputs = null)
{
    /// <summary>
    /// What the attempt created, whatever its state: a failed deployment may still have made something. Taken as
    /// receipts on the unit's resource record before the outcome is applied, so a crash between the two converges on the replay.
    /// </summary>
    public IReadOnlyList<WorkResource> Resources { get; init; } = [];

    /// <summary>The route a reached decision took, on the wire as <c>outcome</c>: one of its step's declared outcomes. Null on any other.</summary>
    public string? Decision { get; init; }

    /// <summary>A failure's fix: one of its plugin's declared commands, which the person runs to put it right. Judged against the declaration where the run is read.</summary>
    public string? Needs { get; init; }
}

/// <summary>
/// A <see cref="HookPoints.Step"/> event: an observed step bound to a repository, a branch and a full commit, or a performed
/// step's completion bound to its invocation. The step is the plugin's own id, or <c>&lt;plugin&gt;/&lt;id&gt;</c>.
/// </summary>
public sealed record WorkStepFrame(
    string Step, WorkOutcome Outcome, DateTimeOffset At, string Id,
    string? Repository = null, string? Branch = null, string? Commit = null, string? Invocation = null);

/// <summary>A <see cref="HookPoints.Perform"/> request's params: one attempt, with the key the plugin deduplicates it by.</summary>
public sealed record WorkPerformCall(
    string Unit, string Step, int StepVersion, string Invocation, string IdempotencyKey, string SubjectRevision,
    IReadOnlyList<string> Inputs, DateTimeOffset DeadlineAt)
{
    /// <summary>
    /// The performed node's options as its graph writes them (<see cref="NodeDeclaration.Options"/>): each name with its JSON value's
    /// text. The host fills them at each send from the declaration it performs, so they are never kept on the attempt's record.
    /// </summary>
    public IReadOnlyDictionary<string, string> Options { get; init; } = None;

    private static readonly IReadOnlyDictionary<string, string> None = ImmutableSortedDictionary<string, string>.Empty;
}

/// <summary>A <c>work/reconcile</c> request's params: what became of one attempt.</summary>
public sealed record WorkReconcileCall(string Unit, string Step, string Invocation, string IdempotencyKey, string SubjectRevision);

/// <summary><c>work/reconcile</c>'s answer: its state, and the outcome where it is done or failed.</summary>
public sealed record WorkReconcileAnswer(string State, WorkOutcome? Outcome = null)
{
    /// <summary>What the attempt created so far, whatever its state: taken as its receipts.</summary>
    public IReadOnlyList<WorkResource> Resources { get; init; } = [];
}

/// <summary>What <c>work/cleanup</c> answers of one receipt: three are terminal, and <see cref="Failed"/> is tried again on a back-off.</summary>
public static class CleanupDispositions
{
    public const string Removed = "removed";

    /// <summary>The plugin had already removed it: a second delivery of the same key never fails for having succeeded.</summary>
    public const string AlreadyGone = "already-gone";

    /// <summary>The plugin keeps it, with its reason (a reused environment, say).</summary>
    public const string Retained = "retained";

    public const string Failed = "failed";

    public static readonly IReadOnlyList<string> All = [Removed, AlreadyGone, Retained, Failed];

    /// <summary>Whether a disposition ends the receipt: everything but a failure.</summary>
    public static bool Terminal(string disposition) => disposition is Removed or AlreadyGone or Retained;
}

/// <summary>
/// A <c>work/cleanup</c> request's params: the receipt Daoris keeps, the plugin's own opaque id for what it created,
/// and the key it deduplicates by, the same at every delivery.
/// </summary>
public sealed record WorkCleanupCall(string Receipt, string Resource, string IdempotencyKey);

/// <summary><c>work/cleanup</c>'s answer: a disposition of <see cref="CleanupDispositions"/>, its proof and the plugin's words.</summary>
public sealed record WorkCleanupAnswer(string Disposition, string? Proof = null, string? Message = null);

/// <summary>
/// A step event as the hook set heard it: the plugin and the generation of the process it is read from, never what the
/// frame says of its sender, and the frame read by shape. What is believed of it is <see cref="WorkBelief"/>'s.
/// </summary>
public sealed record WorkStepHeard(string Plugin, int Generation, WorkStepFrame Step);

/// <summary>
/// The work wire's contract, <c>work.perform@1</c>: every message Daoris sends at the
/// work points and every message a plugin sends back, written and read here once, so the hook set, a stand-in plugin and the kit's
/// scaffold say the same thing. Every name is spelled here rather than left to a serializer's policy, as
/// <see cref="HookFrames"/>'s are.
/// </summary>
public static class WorkWire
{
    /// <summary>The capability a performer's handshake names. <c>protocolVersion</c> stays 1.</summary>
    public const string Capability = "work.perform@1";

    public const string WatchMethod = "hook/" + HookPoints.Watch;

    public const string PerformMethod = "hook/" + HookPoints.Perform;

    public const string ReconcileMethod = "hook/" + PluginSteps.ReconcilePoint;

    /// <summary>The cleanup adapter's request: one receipt's removal asked of the plugin that created it.</summary>
    public const string CleanupMethod = "hook/" + PluginSteps.CleanupPoint;

    /// <summary>How many resources one outcome or reconciliation names at most, as many as its outputs.</summary>
    public const int ResourcesAtMost = 8;

    /// <summary>The plugin's notification: the one method a plugin may send unasked, and only to the lease's loop.</summary>
    public const string StepMethod = HookPoints.Step;

    /// <summary>An evidence message's length at most.</summary>
    public const int MessageChars = 300;

    /// <summary>How many outputs a reached step names at most, and a reference's length at most.</summary>
    public const int OutputsAtMost = 8;

    public const int RefChars = 200;

    /// <summary>A name's length at most: a repository, an id, an invocation, a key, a revision, an environment.</summary>
    public const int NameChars = 200;

    /// <summary>A branch's length at most, and an evidence address's.</summary>
    public const int BranchChars = 250;

    public const int UrlChars = 2000;

    /// <summary>A full commit id: SHA-1's 40 or SHA-256's 64 lowercase hex digits.</summary>
    private static readonly Regex FullCommit = new("^[0-9a-f]{40}(?:[0-9a-f]{24})?$", RegexOptions.CultureInvariant);

    /// <summary>A failure's code, as a step's <c>transient</c> codes are written: lowercase letters, digits and dashes.</summary>
    private static readonly Regex CodeShape = new("^[a-z0-9][a-z0-9-]{0,39}$", RegexOptions.CultureInvariant);

    /// <summary>An instant as the wire writes it: ISO 8601 with its offset or <c>Z</c>, so no reader takes it for local time.</summary>
    private static readonly Regex Instant =
        new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant);

    // ——— Daoris's messages

    /// <summary><see cref="HookPoints.Watch"/>'s params: the whole awaited set, the generation, and whether to send again.</summary>
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

    /// <summary>
    /// <see cref="HookPoints.Perform"/>'s params, as its example writes them. The step's <c>options</c> follow where it has any: an object
    /// of each option's JSON value, or its text as written where that is not JSON.
    /// </summary>
    public static JsonObject Perform(WorkPerformCall call)
    {
        var written = new JsonObject
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
        if (call.Options.Count > 0)
        {
            var options = new JsonObject();
            foreach (var (name, text) in call.Options.OrderBy(pair => pair.Key, StringComparer.Ordinal)) options[name] = OptionValue(text);
            written["options"] = options;
        }

        return written;
    }

    /// <summary><c>work/reconcile</c>'s params: <c>{ unit, step, invocation, idempotencyKey, subjectRevision }</c>.</summary>
    public static JsonObject Reconcile(WorkReconcileCall call) => new()
    {
        ["unit"] = call.Unit,
        ["step"] = call.Step,
        ["invocation"] = call.Invocation,
        ["idempotencyKey"] = call.IdempotencyKey,
        ["subjectRevision"] = call.SubjectRevision,
    };

    /// <summary><c>work/cleanup</c>'s params: <c>{ receipt, resource, idempotencyKey }</c>.</summary>
    public static JsonObject Cleanup(WorkCleanupCall call) => new()
    {
        ["receipt"] = call.Receipt,
        ["resource"] = call.Resource,
        ["idempotencyKey"] = call.IdempotencyKey,
    };

    // ——— a plugin's messages: what a stand-in and the kit's scaffold write

    /// <summary>
    /// A <see cref="HookPoints.Step"/> event as a whole frame: a notification, with no id. Its outcome's fields sit beside the step's,
    /// as the frame writes <c>state</c> and <c>evidence</c>; a part not said is not written.
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

    /// <summary><see cref="HookPoints.Perform"/>'s answer: receipt of this invocation, and no more. The outcome comes later as an event.</summary>
    public static JsonObject PerformAnswer(string invocation) => new() { ["invocation"] = invocation };

    /// <summary><c>work/reconcile</c>'s answer: its state, and its outcome where it has one.</summary>
    public static JsonObject ReconcileAnswer(WorkReconcileAnswer answer)
    {
        var written = new JsonObject { ["state"] = answer.State };
        if (answer.Outcome is { } outcome) written["outcome"] = Outcome([], outcome);
        if (answer.Resources.Count > 0) written["resources"] = Resources(answer.Resources);
        return written;
    }

    /// <summary><c>work/cleanup</c>'s answer: its disposition, and its proof and words where it has them.</summary>
    public static JsonObject CleanupAnswer(WorkCleanupAnswer answer)
    {
        var written = new JsonObject { ["disposition"] = answer.Disposition };
        if (answer.Proof is not null) written["proof"] = answer.Proof;
        if (answer.Message is not null) written["message"] = answer.Message;
        return written;
    }

    // ——— reading what a plugin sends

    /// <summary>
    /// A <see cref="HookPoints.Step"/> event's params read by shape, or null with the first broken field named: the
    /// step, its outcome, its time and its id always; a repository, a branch and a full commit where it binds to no invocation. A field
    /// this build does not know (a <c>plugin</c> above all) is passed over.
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

        // An observed step binds to a commit; a completion binds to the attempt it names.
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
    /// <c>work/reconcile</c>'s answer read by shape, or null: <c>done</c> carries an outcome reached or failed, <c>failed</c> one
    /// failed, and the other three none. Its <c>resources</c> are read as an outcome's are, whatever its state.
    /// </summary>
    public static WorkReconcileAnswer? ReadReconcileAnswer(JsonElement answer)
    {
        if (answer.ValueKind != JsonValueKind.Object || Text(answer, "state") is not { } state || !ReconcileStates.All.Contains(state)) return null;

        WorkOutcome? outcome = null;
        if (answer.TryGetProperty("outcome", out var said) && said.ValueKind != JsonValueKind.Null)
        {
            if (said.ValueKind != JsonValueKind.Object || (outcome = ReadOutcome(said, out _)) is null) return null;
        }

        if (ReadResources(answer) is not { } resources) return null;
        var fits = state switch
        {
            ReconcileStates.Done => outcome?.State is WorkStates.Reached or WorkStates.Failed,
            ReconcileStates.Failed => outcome?.State == WorkStates.Failed,
            _ => outcome is null,
        };
        return fits ? new WorkReconcileAnswer(state, outcome) { Resources = resources } : null;
    }

    /// <summary>
    /// <c>work/cleanup</c>'s answer read by shape, or null: a disposition of the four, a proof of a name's length and
    /// words a person reads of an evidence message's.
    /// </summary>
    public static WorkCleanupAnswer? ReadCleanupAnswer(JsonElement answer) =>
        answer.ValueKind == JsonValueKind.Object
        && Text(answer, "disposition") is { } disposition && CleanupDispositions.All.Contains(disposition)
        && Optional(answer, "proof", RefChars, out var proof) && Words(answer, "message", MessageChars, out var message)
            ? new WorkCleanupAnswer(disposition, proof, message)
            : null;

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
        && ReadOptions(@params) is { } options
            ? new WorkPerformCall(unit, step, version, invocation, key, revision, inputs, deadlineAt) { Options = options }
            : null;

    /// <summary>A perform call's <c>options</c> read back as each value's JSON text: absent or null is none; null where it is not an object.</summary>
    private static IReadOnlyDictionary<string, string>? ReadOptions(JsonElement @params)
    {
        if (!@params.TryGetProperty("options", out var said) || said.ValueKind == JsonValueKind.Null) return ImmutableSortedDictionary<string, string>.Empty;
        if (said.ValueKind != JsonValueKind.Object) return null;
        return said.EnumerateObject().ToDictionary(option => option.Name, option => option.Value.GetRawText(), StringComparer.Ordinal);
    }

    /// <summary>An option's text as its JSON value, or the text itself where it is not JSON: <c>true</c> is a boolean, <c>squash</c> the word.</summary>
    private static JsonNode? OptionValue(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text);
        }
    }

    /// <summary><c>work/reconcile</c>'s params read back, or null where they are not its shape.</summary>
    public static WorkReconcileCall? ReadReconcile(JsonElement @params) =>
        @params.ValueKind == JsonValueKind.Object
        && Text(@params, "unit") is { } unit && Text(@params, "step") is { } step && Text(@params, "invocation") is { } invocation
        && Text(@params, "idempotencyKey") is { } key && Text(@params, "subjectRevision") is { } revision
            ? new WorkReconcileCall(unit, step, invocation, key, revision)
            : null;

    /// <summary><c>work/cleanup</c>'s params read back, or null where they are not its shape.</summary>
    public static WorkCleanupCall? ReadCleanup(JsonElement @params) =>
        @params.ValueKind == JsonValueKind.Object
        && Text(@params, "receipt") is { } receipt && Text(@params, "resource") is { } resource && Text(@params, "idempotencyKey") is { } key
            ? new WorkCleanupCall(receipt, resource, key)
            : null;

    // ——— the parts

    /// <summary>An outcome's fields written into <paramref name="into"/>: its state, then each part said.</summary>
    private static JsonObject Outcome(JsonObject into, WorkOutcome outcome)
    {
        into["state"] = outcome.State;
        if (outcome.Code is not null) into["code"] = outcome.Code;
        if (outcome.Needs is not null) into["needs"] = outcome.Needs;
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

        if (outcome.Decision is not null) into["outcome"] = outcome.Decision;
        if (outcome.Resources.Count > 0) into["resources"] = Resources(outcome.Resources);
        return into;
    }

    /// <summary>Resources as the wire writes them: <c>[{ schema, id }]</c>, in the order said.</summary>
    private static JsonArray Resources(IEnumerable<WorkResource> resources) =>
        new([.. resources.Select(resource => (JsonNode)new JsonObject { ["schema"] = resource.Schema, ["id"] = resource.Id })]);

    /// <summary>
    /// The <c>resources</c> beside an outcome or a reconciliation read by shape: absent or null is none; else at most
    /// <see cref="ResourcesAtMost"/>, each a schema's name and an id of a reference's length, none twice. Null where any is not.
    /// </summary>
    private static IReadOnlyList<WorkResource>? ReadResources(JsonElement from)
    {
        if (!from.TryGetProperty("resources", out var said) || said.ValueKind == JsonValueKind.Null) return [];
        if (said.ValueKind != JsonValueKind.Array) return null;
        var read = new List<WorkResource>();
        foreach (var each in said.EnumerateArray())
        {
            if (read.Count == ResourcesAtMost || each.ValueKind != JsonValueKind.Object
                || Text(each, "schema") is not { } schema || !PluginSteps.IsSchema(schema) || Name(each, "id", RefChars) is not { } id
                || read.Contains(new WorkResource(schema, id)))
            {
                return null;
            }

            read.Add(new WorkResource(schema, id));
        }

        // None said reads as none, the one empty list, so an outcome read back equals the one written.
        return read.Count == 0 ? [] : read;
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

        field = "needs";
        if (!Optional(from, "needs", 40, out var needs) || (needs is not null && (state != WorkStates.Failed || !CodeShape.IsMatch(needs)))) return null;

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
                if (outputs.Count == OutputsAtMost || !PluginIds.NameShape.IsMatch(output.Name) || output.Value.ValueKind != JsonValueKind.Object
                    || Name(output.Value, "ref", RefChars) is not { } reference || !IsReference(reference)
                    || !Optional(output.Value, "revision", NameChars, out var revision))
                {
                    return null;
                }

                outputs[output.Name] = new WorkOutput(reference, revision);
            }
        }

        field = "outcome";
        string? decision = null;
        if (from.TryGetProperty("outcome", out var chosen) && chosen.ValueKind != JsonValueKind.Null)
        {
            if (state != WorkStates.Reached || chosen.ValueKind != JsonValueKind.String || chosen.GetString() is not { } route || !NodeOutcomes.Shape.IsMatch(route)) return null;
            decision = route;
        }

        field = "resources";
        if (ReadResources(from) is not { } resources) return null;
        field = null;
        return new WorkOutcome(state, code, evidence, outputs) { Resources = resources, Decision = decision, Needs = needs };
    }

    /// <summary>A step's name: the plugin's own id, or <c>&lt;plugin&gt;/&lt;id&gt;</c> (the kind).</summary>
    private static bool IsStep(string step)
    {
        var slash = step.LastIndexOf('/');
        return slash < 0
            ? PluginIds.NameShape.IsMatch(step)
            : PluginCatalog.IsId(step[..slash]) && PluginIds.NameShape.IsMatch(step[(slash + 1)..]);
    }

    /// <summary>A reference, <c>&lt;schema&gt;:&lt;opaque id&gt;</c>: a declared schema and an id that is not empty.</summary>
    private static bool IsReference(string reference)
    {
        var colon = reference.IndexOf(':');
        return colon > 0 && colon < reference.Length - 1 && PluginSteps.IsSchema(reference[..colon]);
    }

    /// <summary>Only a web page is an address a person goes to read, as a landing's pull request is (HookPeer.cs).</summary>
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
