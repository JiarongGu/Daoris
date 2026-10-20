using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The shapes a step's field takes (the workflow design §3.2), as the shared table spells them.</summary>
public static class WorkflowFieldTypes
{
    public const string Names = "names";
    public const string Flag = "flag";
    public const string Choice = "choice";
    public const string Environment = "environment";
    public const string Pattern = "pattern";
    public const string PluginOrNone = "plugin-or-none";
    public const string Text = "text";
    public const string Plugin = "plugin";
    public const string Checks = "checks";
    public const string Id = "id";
    public const string Members = "members";
}

/// <summary>One field a kind takes, in the order a version reads it: its shape, whether it must be named, and what absence reads as.</summary>
/// <param name="Default">Text, a switch, or null: what an absent field reads as.</param>
public sealed record WorkflowKindField(string Name, string Type, IReadOnlyList<string>? Choices, bool Required, object? Default);

/// <summary>One kind of step: how much of it this build runs, the limit it is said with (a <see cref="WorkflowLimits"/> code), and its fields.</summary>
/// <param name="Runtime">One of <see cref="WorkflowRuntime"/>'s, or <see cref="WorkflowKindTable.None"/>: never offered.</param>
public sealed record WorkflowKindRow(string Kind, string Runtime, string? Limit, IReadOnlyList<WorkflowKindField> Fields);

/// <summary>
/// The kinds a named workflow may hold, as main runs them (WORKFLOW1d, D157 point 8, the workflow design §3.2, §3.9) — the CLI's
/// <c>WORKFLOW_KINDS</c>, cell for cell. A kind this build runs <see cref="None"/> of is never offered, and a version naming it
/// does not read here. The opinion is <c>partial</c> since XAGENT1f's gate reads its rule, with Current's limit for it.
/// </summary>
public static class WorkflowKindTable
{
    /// <summary>A kind this build does not run: designed, never offered.</summary>
    public const string None = "none";

    /// <summary>At most this many steps in a version, a group's members counted (design §2.5).</summary>
    public const int MaxSteps = 24;

    /// <summary>A workflow keeps its newest this many versions, and every version a kept run names (design §2.6).</summary>
    public const int KeptVersions = 20;

    private static readonly WorkflowKindField Failed = new("failed", WorkflowFieldTypes.Choice, ["wait", "send-back", "stop"], false, "wait");

    public static IReadOnlyList<WorkflowKindRow> Table { get; } =
    [
        new(WorkflowKinds.Work, WorkflowRuntime.Built, null, []),
        new(WorkflowKinds.Opinion, WorkflowRuntime.Partial, WorkflowLimits.OpinionPartial,
        [
            new("reviewers", WorkflowFieldTypes.Names, null, false, null),
            new("required", WorkflowFieldTypes.Flag, null, false, false),
            new("steps", WorkflowFieldTypes.Flag, null, false, false),
            new("recheck", WorkflowFieldTypes.Flag, null, false, true),
        ]),
        new(WorkflowKinds.Look, WorkflowRuntime.Partial, WorkflowLimits.LookPartial,
            [new("environment", WorkflowFieldTypes.Environment, null, false, null)]),
        new(WorkflowKinds.Landing, WorkflowRuntime.Built, null,
        [
            new("form", WorkflowFieldTypes.Choice, [LandingForm.Merge, LandingForm.Branch], true, null),
            new("accept", WorkflowFieldTypes.Choice, ["you", "automatic"], true, null),
            new("pattern", WorkflowFieldTypes.Pattern, null, false, null),
            new("plugin", WorkflowFieldTypes.PluginOrNone, null, false, null),
        ]),
        new(WorkflowKinds.PullRequest, WorkflowRuntime.Partial, WorkflowLimits.PullRequestAtCleanUp, []),
        new(WorkflowKinds.GoAhead, WorkflowRuntime.Partial, WorkflowLimits.GoAheadPartial,
        [
            new("act", WorkflowFieldTypes.Text, null, true, null),
            new("on", WorkflowFieldTypes.Text, null, true, null),
            new("refused", WorkflowFieldTypes.Choice, ["stop", "wait"], false, "stop"),
        ]),
        new(WorkflowKinds.Check, None, null,
        [
            new("plugin", WorkflowFieldTypes.Plugin, null, true, null),
            new("checks", WorkflowFieldTypes.Checks, null, false, "required"),
            Failed,
        ]),
        new(WorkflowKinds.Stage, None, null,
        [
            new("plugin", WorkflowFieldTypes.Plugin, null, true, null),
            new("stage", WorkflowFieldTypes.Id, null, true, null),
            new("start", WorkflowFieldTypes.Choice, ["you", "automatic"], false, "you"),
            Failed,
        ]),
        new(WorkflowKinds.All, None, null, [new("steps", WorkflowFieldTypes.Members, null, true, null), Failed]),
    ];

    /// <summary>A kind's row, or null for a kind nobody has.</summary>
    public static WorkflowKindRow? Of(string kind) => Table.FirstOrDefault(row => row.Kind == kind);
}

/// <summary>
/// One step as a version reads it: its id and kind, and every field its kind takes, in order, absence read as its default. A
/// value is text, null, a switch, a list of names, or a group's members.
/// </summary>
public sealed record NamedStep(string Id, string Kind, IReadOnlyList<WorkflowSetting> Fields)
{
    /// <summary>A field's value as the step reads it.</summary>
    public object? Field(string name) => Fields.FirstOrDefault(each => each.Name == name)?.Value;

    /// <summary>A group's members, or none.</summary>
    public IReadOnlyList<NamedStep> Members => Kind == WorkflowKinds.All && Field("steps") is IReadOnlyList<NamedStep> members ? members : [];
}

/// <summary>
/// One version as read: its number, when and through which door it was saved, and its steps and digest, or its first problem. A graph
/// version (schema 2, FABRIC-GRAPH1b) holds <see cref="Graph"/> and no steps, and its digest is the graph's own
/// (<see cref="NodeGraphs.Digest"/>), which a run bound to it binds.
/// </summary>
public sealed record NamedVersion(
    int Version, string? At, string? Door, IReadOnlyList<NamedStep>? Steps, string? Problem, string? Digest, NodeGraph? Graph = null)
{
    /// <summary>Why the steps-driven runtime cannot run it, or null where it can: a graph version reads, and is not run until the cutover.</summary>
    public string? Unrun => Steps is not null ? null : Problem ?? $"v{Version}: it is a graph (schema 2), which this build reads and does not run yet.";
}

/// <summary>A workflow file as read: its id and name and every version, each readable or not; or the file's own first problem.</summary>
public sealed record NamedWorkflowRead(string? Id, string? Name, IReadOnlyList<NamedVersion> Versions, string? Problem)
{
    /// <summary>
    /// The file's mark (FABRIC-PIPELINE1a, <see cref="WorkflowPipelines"/>): <c>development</c> where it says none, null where the file
    /// does not read.
    /// </summary>
    public string? Pipeline { get; init; }

    /// <summary>Whether it is marked a verification pipeline, which only the <c>landed</c> route names.</summary>
    public bool Verifies => Pipeline == WorkflowPipelines.Verification;
}

/// <summary>
/// Named workflows read (WORKFLOW1d, D157 points 5, 6 and 8, the workflow design §2.5, §2.6, §3.2, §3.3, §3.9): what a workflow
/// file and each of its versions may say, the first problem in what does not, and a version's digest. Choosing one is WORKFLOW1e's,
/// and every gate reads the version a run bound (WORKFLOW1f, <see cref="WorkflowProcesses"/>).
/// </summary>
/// <remarks>
/// A TWIN with the CLI's <c>namedworkflows.ts</c>: both hold one table, this suite's tests' <c>fixtures/workflow-named.json</c>,
/// cell for cell, every sentence and every digest included (<c>WorkflowNamedTests</c> and <c>namedworkflows.test.ts</c>).
/// </remarks>
public static class WorkflowNamed
{
    private const string IdRule = "lower-case letters, digits and dashes, at most 40";
    private const string KindList = "`work`, `opinion`, `look`, `landing`, `pull-request`, `go-ahead`, `check`, `stage` or `all`";
    private const string FileShape = "a workflow is a JSON object: its `id`, its `name` and its `versions`.";
    private const string IdSentence = $"a workflow's `id` is {IdRule}, such as `docs-to-pr`.";
    private const string Current = "`current` names Current, the workflow drawn from the rules as they stand, so no saved workflow takes it.";
    private const string NameSentence = "a workflow's `name` is your words for it, 1 to 60 characters.";
    private const string VersionsSentence = "a workflow's `versions` is a list of at least one.";
    private const string VersionShape = "each of its versions is a JSON object with its `version`, `at`, `door` and `steps`.";
    private const string VersionNumber = "each version's `version` is a whole number from 1, each greater than the one before it.";
    private const string PipelineSentence = "a workflow's `pipeline` is `development` or `verification`, as it was made; absent is `development`.";

    /// <summary>The kinds a verification pipeline's graph never holds: each observes a chain's work, or lands it (the pipeline design §2.1).</summary>
    private static readonly string[] NotVerified =
        [NodeKinds.Claimed, NodeKinds.Opinion, NodeKinds.Look, NodeKinds.GoAhead, NodeKinds.Handed, NodeKinds.QuestClose];

    /// <summary>A workflow's or a step's id: lower-case letters, digits and dashes, at most 40.</summary>
    public static bool IsId(string? value) => value is { Length: > 0 and <= 40 } && SafeNames.IsKebabId(value);

    /// <summary>What is wrong with a workflow's id, or null. <c>current</c> is Current's (design §2.5).</summary>
    public static string? IdProblem(string? value)
    {
        if (string.IsNullOrEmpty(value)) return IdSentence;
        if (!IsId(value)) return $"`{value}` is not a workflow's id — {IdRule}, such as `docs-to-pr`.";
        return value == "current" ? Current : null;
    }

    /// <summary>What is wrong with a workflow's name, or null: the person's words, 1 to 60 characters (content, D142).</summary>
    public static string? NameProblem(string? value) =>
        value is not null && value.Trim().Length > 0 && value.Length <= 60 ? null : NameSentence;

    /// <summary>
    /// A workflow file read whole (design §2.5, §2.6): the file's own problem first (its shape, id, name, and its versions'
    /// numbers, each greater than the one before), then each version on its own, so a version a newer build or a hand edit
    /// wrote is kept and unreadable here while the others read. A step's id an earlier readable version used for another kind
    /// makes the later version unreadable.
    /// </summary>
    public static NamedWorkflowRead Read(JsonElement file)
    {
        static NamedWorkflowRead Unread(string problem) => new(null, null, [], problem);
        if (file.ValueKind != JsonValueKind.Object) return Unread(FileShape);
        var id = Json.Text(file, "id");
        if (IdProblem(id) is { } idProblem) return Unread(idProblem);
        var name = Json.Text(file, "name");
        if (NameProblem(name) is { } nameProblem) return Unread(nameProblem);

        // The mark (FABRIC-PIPELINE1a): absent or null is a development pipeline; a value this build does not know reads nothing.
        var pipeline = WorkflowPipelines.Development;
        if (file.TryGetProperty("pipeline", out var mark) && mark.ValueKind != JsonValueKind.Null)
        {
            pipeline = mark.ValueKind == JsonValueKind.String ? mark.GetString()! : "";
            if (pipeline is not (WorkflowPipelines.Development or WorkflowPipelines.Verification)) return Unread(PipelineSentence);
        }

        if (!file.TryGetProperty("versions", out var versions) || versions.ValueKind != JsonValueKind.Array || versions.GetArrayLength() == 0)
        {
            return Unread(VersionsSentence);
        }

        var last = 0;
        foreach (var version in versions.EnumerateArray())
        {
            if (version.ValueKind != JsonValueKind.Object) return Unread(VersionShape);
            if (WholeOf(version, "version") is not { } number || number <= last) return Unread(VersionNumber);
            last = number;
        }

        var read = new List<NamedVersion>();
        foreach (var version in versions.EnumerateArray())
        {
            read.Add(ReadVersion(version, WholeOf(version, "version")!.Value, [.. read.Where(each => each.Steps is not null)], pipeline));
        }

        return new NamedWorkflowRead(id, name, read, null) { Pipeline = pipeline };
    }

    /// <summary>One version read (design §2.5): its <c>at</c>, its <c>door</c>, then its steps, the first problem said with its number.</summary>
    /// <param name="prior">The versions before it that read, which a step's id is held to: an id is never used for another kind.</param>
    /// <param name="pipeline">
    /// The mark its workflow's file carries (FABRIC-PIPELINE1a): a development pipeline's graph lands its work once, and a verification
    /// pipeline's versions are graphs holding its checks.
    /// </param>
    public static NamedVersion ReadVersion(JsonElement version, int number, IReadOnlyList<NamedVersion> prior, string pipeline = WorkflowPipelines.Development)
    {
        var at = Json.Text(version, "at");
        var door = Json.Text(version, "door");
        NamedVersion Unread(string problem) => new(number, at, door, null, problem, null);
        if (!IsMoment(at)) return Unread($"v{number}: its `at` is a moment as ISO 8601 writes it, such as `2026-10-09T09:12:00Z`.");
        if (door is null || !(door is "screen" or "terminal" || IsAskDoor(door)))
        {
            return Unread($"v{number}: its `door` is `screen`, `terminal` or `ask-daoris:<proposal>`.");
        }

        // A version is its steps (schema 1: no `schema`, or 1) or its graph (schema 2), never both and never in part (FABRIC-GRAPH1b). An
        // older build reads `steps` alone and no other key, so a graph version, which holds none, is unreadable there as it is here.
        var hasSteps = version.TryGetProperty("steps", out var raw) && raw.ValueKind != JsonValueKind.Null;
        if (version.TryGetProperty("schema", out var schema) && schema.ValueKind != JsonValueKind.Null)
        {
            if (!(schema.ValueKind == JsonValueKind.Number && schema.TryGetInt32(out var named) && named is 1 or GraphSchema))
            {
                return Unread($"v{number}: its `schema` is 1, its steps, or {GraphSchema}, its graph; this build reads no other.");
            }

            if (named == GraphSchema)
            {
                if (hasSteps) return Unread(BothSentence(number));
                if (!version.TryGetProperty("graph", out var graphJson) || graphJson.ValueKind != JsonValueKind.Object)
                {
                    return Unread($"v{number}: its schema is {GraphSchema}, so its `graph` is the graph's JSON object.");
                }

                var (graph, problems) = NodeGraphs.Read(graphJson.GetRawText());
                if (graph is null) return Unread($"v{number}: its graph does not hold: {problems[0].Words}");
                var shape = pipeline == WorkflowPipelines.Verification ? VerificationProblem(graph, number) : OneLandingProblem(graph, number);
                return shape is { } landing
                    ? Unread(landing)
                    : new NamedVersion(number, at, door, null, null, NodeGraphs.Digest(graph), graph);
            }
        }

        // A verification pipeline's versions are graphs (the steps engine runs no `check`, D190 §10).
        if (pipeline == WorkflowPipelines.Verification)
        {
            return Unread($"v{number}: a verification pipeline's versions are graphs (schema {GraphSchema}) holding its checks, never steps.");
        }

        var (steps, problem) = ReadSteps(hasSteps ? raw : null, number, prior);
        return steps is null ? Unread(problem!) : new NamedVersion(number, at, door, steps, null, Digest(steps));
    }

    /// <summary>
    /// Why a named version's graph is not one landing, or null (FABRIC-CUTOVER1b; D190 point 4, the cutover design §4): it holds one
    /// <c>handed</c>, and at most one <c>opinion</c> and one <c>look</c>, since the process it is read as holds one of each. Judged after
    /// the graph holds, in that order, so a save is refused and a stored version is kept as written and read as unreadable. A TWIN with
    /// the CLI's <c>oneLandingProblem</c> (<c>namedworkflows.ts</c>), on <c>workflow-graph-versions.json</c>.
    /// </summary>
    private static string? OneLandingProblem(NodeGraph graph, int number)
    {
        string Named(string kind) => string.Join(" and ", graph.Nodes.Where(node => node.Kind == kind).Select(node => $"`{node.Id}`"));
        var landings = graph.Nodes.Count(node => node.Kind == NodeKinds.Handed);
        if (landings == 0) return $"v{number}: its graph holds no landing (`{NodeKinds.Handed}`), and a named workflow's graph lands its work once.";
        if (landings > 1) return $"v{number}: its graph holds {landings} landings, {Named(NodeKinds.Handed)}, and a named workflow's graph lands its work once.";
        foreach (var (kind, what) in new[] { (NodeKinds.Opinion, "opinions"), (NodeKinds.Look, "looks") })
        {
            var count = graph.Nodes.Count(node => node.Kind == kind);
            if (count > 1) return $"v{number}: its graph holds {count} {what}, {Named(kind)}, and a named workflow's graph holds at most one.";
        }

        return null;
    }

    /// <summary>
    /// Why a verification pipeline's graph is not one, or null (FABRIC-PIPELINE1a, D192 point 3, the pipeline design §2.1): it holds none
    /// of <c>claimed</c>, <c>opinion</c>, <c>look</c>, <c>go-ahead</c>, <c>handed</c> or <c>quest.close</c>, whose subjects (a chain's
    /// quests, its branch, its ask) a landed run has none of, the first in node order named; then at least one <c>check</c>. A TWIN with
    /// the CLI's <c>verificationProblem</c> (<c>namedworkflows.ts</c>), on <c>workflow-graph-versions.json</c>.
    /// </summary>
    private static string? VerificationProblem(NodeGraph graph, int number)
    {
        if (graph.Nodes.FirstOrDefault(node => NotVerified.Contains(node.Kind, StringComparer.Ordinal)) is { } held)
        {
            return $"v{number}: its graph holds `{held.Id}`, a `{held.Kind}`, and a verification pipeline's graph lands nothing and observes no "
                + "chain's work: it holds no `claimed`, `opinion`, `look`, `go-ahead`, `handed` or `quest.close`.";
        }

        return graph.Nodes.Any(node => node.Kind == NodeKinds.Check)
            ? null
            : $"v{number}: its graph holds no `{NodeKinds.Check}`, and a verification pipeline's graph runs at least one.";
    }

    /// <summary>The schema of a version that is a graph (FABRIC-GRAPH1b); a version of steps is schema 1, named or not.</summary>
    public const int GraphSchema = 2;

    private static string BothSentence(int number) => $"v{number}: a version is its `steps` or its `graph`, never both.";

    /// <summary>
    /// A version's steps read (design §2.5, §3.2, §3.3, §3.9), the first problem said: the list and its bound, then each step's
    /// shape in order (a group's members inside it), then the order the runtime keeps, then whether this build runs each kind.
    /// </summary>
    public static (IReadOnlyList<NamedStep>? Steps, string? Problem) ReadSteps(JsonElement? value, int number, IReadOnlyList<NamedVersion>? prior = null)
    {
        var (steps, problem) = ReadShape(value, number, prior);
        if (steps is null) return (null, problem);
        foreach (var each in steps.SelectMany(step => new[] { step }.Concat(step.Members)))
        {
            if (WorkflowKindTable.Of(each.Kind)!.Runtime == WorkflowKindTable.None)
            {
                return (null, $"v{number}, step `{each.Id}`: this build does not run a step of kind `{each.Kind}` yet.");
            }
        }

        return (steps, null);
    }

    /// <summary>
    /// A version's steps read as <see cref="ReadSteps"/> reads them, all but whether this build runs each kind: Ask Daoris's judge
    /// (WORKFLOW1h-b, design §8.1) says a kind this build does not run as what the card needs first, where the editor refuses it.
    /// </summary>
    public static (IReadOnlyList<NamedStep>? Steps, string? Problem) ReadShape(JsonElement? value, int number, IReadOnlyList<NamedVersion>? prior = null)
    {
        var v = $"v{number}";
        if (value is not { ValueKind: JsonValueKind.Array } list || list.GetArrayLength() == 0) return (null, $"{v}: its `steps` is a list of at least one.");
        var count = list.GetArrayLength() + list.EnumerateArray().Sum(each =>
            each.ValueKind == JsonValueKind.Object && Json.Text(each, "kind") == WorkflowKinds.All
            && each.TryGetProperty("steps", out var members) && members.ValueKind == JsonValueKind.Array
                ? members.GetArrayLength()
                : 0);
        if (count > WorkflowKindTable.MaxSteps)
        {
            return (null, $"{v}: it has {count} steps, counting each group's; a workflow holds at most {WorkflowKindTable.MaxSteps}.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var steps = new List<NamedStep>();
        var position = 0;
        foreach (var raw in list.EnumerateArray())
        {
            var (step, problem) = ReadStep(raw, ++position, null, seen, prior ?? [], v);
            if (problem is not null) return (null, problem);
            steps.Add(step!);
        }

        return OrderProblem(steps, v) is { } order ? (null, order) : (steps, null);
    }

    /// <summary>One step's shape read (design §3.2): an object, its id once, its kind, a group's rule, its fields; a group's members inside it.</summary>
    private static (NamedStep? Step, string? Problem) ReadStep(
        JsonElement raw, int position, string? group, HashSet<string> seen, IReadOnlyList<NamedVersion> prior, string v)
    {
        var at = group is null ? $"{v}, step {position}: " : $"{v}, step {position} in `{group}`: ";
        if (raw.ValueKind != JsonValueKind.Object) return (null, $"{at}a step is a JSON object with its `id` and `kind`.");
        var id = Json.Text(raw, "id");
        if (!IsId(id)) return (null, $"{at}its `id` is {IdRule}, such as `land`.");
        if (!seen.Add(id!)) return (null, $"{at}`{id}` is an earlier step's id too; each step's id is its own.");

        var p = group is null ? $"{v}, step `{id}`: " : $"{v}, step `{id}` in `{group}`: ";
        if (Json.Text(raw, "kind") is not { } kind) return (null, $"{p}its `kind` is one of {KindList}.");
        if (WorkflowKindTable.Of(kind) is not { } row) return (null, $"{p}`{kind}` is no kind of step — one of {KindList}.");
        if (group is not null && kind == WorkflowKinds.All) return (null, $"{p}a group holds no group: one level of grouping.");
        if (group is not null && kind is not (WorkflowKinds.Check or WorkflowKinds.Stage))
        {
            return (null, $"{p}a group holds checks and stages, side by side; `{kind}` is neither.");
        }

        foreach (var earlier in prior)
        {
            var was = earlier.Steps!.Concat(earlier.Steps!.SelectMany(step => step.Members)).FirstOrDefault(step => step.Id == id);
            if (was is not null && was.Kind != kind)
            {
                return (null, $"{p}`{id}` was a step of kind `{was.Kind}` in v{earlier.Version}; an id is never used for another kind.");
            }
        }

        foreach (var property in raw.EnumerateObject())
        {
            if (property.Name is not ("id" or "kind") && row.Fields.All(each => each.Name != property.Name))
            {
                return (null, $"{p}a step of kind `{kind}` takes no `{property.Name}` — it takes {FieldList(row.Fields)}.");
            }
        }

        var fields = new List<WorkflowSetting>();
        foreach (var each in row.Fields)
        {
            var (value, problem) = ReadField(each, raw.TryGetProperty(each.Name, out var named) ? named : null);
            if (problem is not null) return (null, p + problem);
            fields.Add(new WorkflowSetting(each.Name, value));
        }

        var step = new NamedStep(id!, kind, fields);
        if (kind == WorkflowKinds.Landing && (string?)step.Field("form") == LandingForm.Merge)
        {
            // A merge writes into the person's checkout (D145 point 1, D51 rule 6), and makes no branch for a plugin (D100).
            if ((string?)step.Field("accept") == "automatic")
            {
                return (null, $"{p}only a branch lands with no press — a merge writes into your checkout, and with no press nothing would "
                    + "stand between the work and the line.");
            }

            if (step.Field("pattern") is not null || step.Field("plugin") is not null)
            {
                return (null, $"{p}a merge makes no branch, so it names no `pattern` and no `plugin`.");
            }
        }

        if (kind == WorkflowKinds.All)
        {
            var members = new List<NamedStep>();
            var index = 0;
            foreach (var member in raw.GetProperty("steps").EnumerateArray())
            {
                var (read, problem) = ReadStep(member, ++index, id, seen, prior, v);
                if (problem is not null) return (null, problem);
                members.Add(read!);
            }

            step = step with { Fields = [.. fields.Select(each => each.Name == "steps" ? each with { Value = members } : each)] };
        }

        return (step, null);
    }

    /// <summary>A field's value read, or why not: a sentence after the step's prefix. Absent and JSON null are both absence.</summary>
    private static (object? Value, string? Problem) ReadField(WorkflowKindField field, JsonElement? raw)
    {
        (object?, string?) wrong = (null, $"its `{field.Name}` is {What(field)}.");
        if (raw is not { } value || value.ValueKind == JsonValueKind.Null) return field.Required ? wrong : (field.Default, null);
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        switch (field.Type)
        {
            case WorkflowFieldTypes.Names:
                return Names(value) is { } names ? (names, null) : wrong;
            case WorkflowFieldTypes.Checks:
                if (text == "required") return (text, null);
                return Names(value) is { } checks ? (checks, null) : wrong;
            case WorkflowFieldTypes.Flag:
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? (value.GetBoolean(), null) : wrong;
            case WorkflowFieldTypes.Choice:
                return text is not null && field.Choices!.Contains(text, StringComparer.Ordinal) ? (text, null) : wrong;
            case WorkflowFieldTypes.Environment:
                return text is { Length: > 0 and <= 32 } && SafeNames.IsKebabId(text) ? (text, null) : wrong;
            case WorkflowFieldTypes.Id:
                return IsId(text) ? (text, null) : wrong;
            case WorkflowFieldTypes.Text:
                return text is not null && text.Trim().Length > 0 && text.Length <= 300 ? (text, null) : wrong;
            case WorkflowFieldTypes.Plugin:
                return text is not null && PluginCatalog.IsId(text) ? (text, null) : wrong;
            case WorkflowFieldTypes.PluginOrNone:
                return text is not null && (text == "none" || PluginCatalog.IsId(text)) ? (text, null) : wrong;
            case WorkflowFieldTypes.Pattern:
                if (text is null) return wrong;
                // The landing twin's own check, asked of its one owner, so a step and a rule refuse one pattern in the same words.
                return LandingRules.Problem(text) is { } problem ? (null, $"its `pattern` will not do: {problem}") : (text, null);
            case WorkflowFieldTypes.Members:
                return value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0 ? (new List<NamedStep>(), null) : wrong;
            default:
                throw new InvalidOperationException($"`{field.Type}` is no field type.");
        }
    }

    /// <summary>Names as a list reads them: at least one, each text that is not blank, kept without the spaces around it, none twice in any case.</summary>
    private static List<string>? Names(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0) return null;
        var kept = new List<string>();
        foreach (var each in value.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.String || each.GetString()!.Trim().Length == 0) return null;
            var name = each.GetString()!.Trim();
            if (kept.Contains(name, StringComparer.OrdinalIgnoreCase)) return null;
            kept.Add(name);
        }

        return kept;
    }

    /// <summary>What a field's value is, as its refusal says it.</summary>
    private static string What(WorkflowKindField field) => field.Type switch
    {
        WorkflowFieldTypes.Names => "a list of names, at least one, none twice in any case",
        WorkflowFieldTypes.Flag => "true or false",
        WorkflowFieldTypes.Choice => Either(field.Choices!),
        WorkflowFieldTypes.Environment => "an environment's name: lower-case letters, digits and dashes, at most 32",
        WorkflowFieldTypes.Pattern => "a branch pattern, such as `work/{quest}-{slug}`",
        WorkflowFieldTypes.PluginOrNone => "`none` or a plugin's id: lowercase letters, digits, dots and dashes",
        WorkflowFieldTypes.Text => "your words, 1 to 300 characters",
        WorkflowFieldTypes.Plugin => "a plugin's id: lowercase letters, digits, dots and dashes",
        WorkflowFieldTypes.Checks => "`required` or a list of check names, at least one, none twice in any case",
        WorkflowFieldTypes.Id => IdRule,
        WorkflowFieldTypes.Members => "a list of the checks and stages it runs side by side, at least one",
        _ => throw new InvalidOperationException($"`{field.Type}` is no field type."),
    };

    /// <summary><c>a</c>, <c>a or b</c>, <c>a, b or c</c>: a field's choices, as its sentence names them.</summary>
    private static string Either(IReadOnlyList<string> choices)
    {
        var named = choices.Select(choice => $"`{choice}`").ToList();
        return named.Count == 1 ? named[0] : $"{string.Join(", ", named.Take(named.Count - 1))} or {named[^1]}";
    }

    /// <summary>The fields a kind takes, as its refusal names them: none, <c>a</c>, <c>a and b</c>, <c>a, b and c</c>.</summary>
    private static string FieldList(IReadOnlyList<WorkflowKindField> fields)
    {
        var named = fields.Select(each => $"`{each.Name}`").ToList();
        if (named.Count == 0) return "none";
        return named.Count == 1 ? named[0] : $"{string.Join(", ", named.Take(named.Count - 1))} and {named[^1]}";
    }

    /// <summary>
    /// The order the runtime keeps (design §2.2, §2.5, §3.3, §3.6): the work first and once; at most one opinion and one look
    /// before the landing, the opinion first, and a go-ahead before them; one landing; after it, the pull request straight after
    /// a landing on a branch whose plugin opens one, then checks, stages, groups and go-aheads, none straight before a stage the
    /// person starts.
    /// </summary>
    private static string? OrderProblem(IReadOnlyList<NamedStep> steps, string v)
    {
        NamedStep? landing = null, opinion = null, look = null, pullRequest = null;
        var landingAt = -1;
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var p = $"{v}, step `{step.Id}`: ";
            if (index == 0 && step.Kind != WorkflowKinds.Work) return $"{v}: the first step is the `work`: the agent's work comes first, once.";
            switch (step.Kind)
            {
                case WorkflowKinds.Work:
                    if (index > 0) return $"{p}there is one `work`, the first step.";
                    break;
                case WorkflowKinds.Landing:
                    if (landing is not null) return $"{p}there is one `landing` already, `{landing.Id}`.";
                    landing = step;
                    landingAt = index;
                    break;
                case WorkflowKinds.Opinion:
                    if (landing is not null) return $"{p}a step of kind `opinion` comes before the landing, in the order the runtime keeps.";
                    if (opinion is not null) return $"{p}there is one `opinion` already, `{opinion.Id}`.";
                    if (look is not null) return $"{p}the second opinion is read before your look, so the `opinion` comes first.";
                    opinion = step;
                    break;
                case WorkflowKinds.Look:
                    if (landing is not null) return $"{p}a step of kind `look` comes before the landing, in the order the runtime keeps.";
                    if (look is not null) return $"{p}there is one `look` already, `{look.Id}`.";
                    look = step;
                    break;
                case WorkflowKinds.GoAhead:
                    if (landing is null && (opinion is not null || look is not null))
                    {
                        return $"{p}a go-ahead before the landing sits with the work, before the second opinion and your look.";
                    }

                    if (landing is not null && index + 1 < steps.Count && steps[index + 1] is { Kind: WorkflowKinds.Stage } next
                        && (string?)next.Field("start") == "you")
                    {
                        return $"{p}a go-ahead straight before a stage you start is not needed: starting it is your go-ahead.";
                    }

                    break;
                case WorkflowKinds.PullRequest:
                    if (landing is null) return $"{p}a step of kind `pull-request` comes after the landing.";
                    if (pullRequest is not null) return $"{p}there is one `pull-request` already, `{pullRequest.Id}`.";
                    if (index - 1 != landingAt) return $"{p}the pull request comes straight after the landing.";
                    if ((string?)landing.Field("form") == LandingForm.Merge)
                    {
                        return $"{p}a pull request needs a landing on a branch whose plugin opens one; `{landing.Id}` merges.";
                    }

                    if ((string?)landing.Field("plugin") == "none")
                    {
                        return $"{p}a pull request needs a landing on a branch whose plugin opens one; `{landing.Id}` names no plugin.";
                    }

                    pullRequest = step;
                    break;
                default:
                    if (landing is null) return $"{p}a step of kind `{step.Kind}` comes after the landing.";
                    break;
            }
        }

        return landing is null ? $"{v}: it has no `landing`; work lands once, after the gates before it." : null;
    }

    /// <summary>
    /// The text a version's digest is of, built by hand so both runtimes write the same bytes — the CLI's
    /// <c>versionCanonical</c>, byte for byte: <c>workflow</c>, then each step's line, its quoted id and its kind, each followed
    /// by every field its kind takes, a line each in order, absence written as its default; a group's members follow it, a step deeper.
    /// </summary>
    public static string Canonical(IReadOnlyList<NamedStep> steps)
    {
        var lines = new List<string> { "workflow" };
        void Write(NamedStep step, string indent)
        {
            lines.Add($"{indent}step {WorkflowCurrent.Quoted(step.Id)} {step.Kind}");
            foreach (var each in step.Fields)
            {
                if (each.Value is IReadOnlyList<NamedStep> members)
                {
                    lines.Add($"{indent}  {each.Name}:");
                    foreach (var member in members) Write(member, indent + "    ");
                }
                else
                {
                    lines.Add($"{indent}  {each.Name}={CanonicalValue(each.Value)}");
                }
            }
        }

        foreach (var step in steps) Write(step, "");
        return string.Join('\n', lines);
    }

    /// <summary>A version's digest: the first 12 hex characters of the SHA-256 of <see cref="Canonical"/>'s text.</summary>
    public static string Digest(IReadOnlyList<NamedStep> steps) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(steps))))[..12];

    private static string CanonicalValue(object? value) => value switch
    {
        null => "-",
        bool flag => flag ? "true" : "false",
        string text => WorkflowCurrent.Quoted(text),
        IEnumerable<string> list => $"[{string.Join(',', list.Select(WorkflowCurrent.Quoted))}]",
        _ => throw new ArgumentException($"a field is text, a switch or a list of names, not {value.GetType().Name}."),
    };

    /// <summary>A whole number from 1, as JSON writes one: a fraction, text or anything past the largest int is none.</summary>
    private static int? WholeOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
        && number >= 1 && number <= int.MaxValue && Math.Floor(number) == number
            ? (int)number
            : null;

    /// <summary>
    /// A moment as ISO 8601 writes it, and only so — the CLI's <c>isoMoment</c>, as <c>AccountCooling</c> reads one: a lenient
    /// parse reads <i>Oct 9</i> as this year's, and a day that does not exist is none.
    /// </summary>
    private static bool IsMoment(string? text) => Moments.ReadStrict(text) is not null;

    private const string AskDoorPrefix = "ask-daoris:";

    /// <summary>The Ask Daoris door: its prefix and a plain id (DEDUP6).</summary>
    private static bool IsAskDoor(string door) =>
        door.StartsWith(AskDoorPrefix, StringComparison.Ordinal) && SafeNames.IsPlainId(door[AskDoorPrefix.Length..]);

    // ---- Adding a version (WORKFLOW1d, D157 point 5, the workflow design §2.4–§2.6) ----
    // Planned and not written: the store's use case (WorkflowStore's Add) writes the plan through its port. A version is
    // whole and never edited: saving adds the next number, keeping the file's other fields and every version as written,
    // then the newest 20 and every version a kept run names. A TWIN with the CLI's planAddVersion (namedworkflows.ts): the
    // plan is held to the driver suite's fixtures/workflow-named.json `store`, cell for cell.

    /// <summary>
    /// A version added to a workflow's file, planned and not written (design §2.6): the next number after the last, the file's
    /// other fields and every version kept as written, a version that does not read here included, then the newest 20 kept with
    /// every version a kept run names. A file that does not read is never written over, and steps that do not read write nothing.
    /// </summary>
    /// <param name="file">The file as it stands, or null for a new workflow.</param>
    /// <param name="kept">The versions a kept run names.</param>
    public static WorkflowAddPlan PlanAdd(JsonNode? file, WorkflowVersionAdded add, IReadOnlyCollection<int> kept)
    {
        static WorkflowAddPlan Refused(string problem) => new(null, null, problem);
        // A graph is the version's schema 2 and its steps are none: both is refused whole, by the reader below, with the number it would take.
        var version = new JsonObject { ["version"] = 0, ["at"] = add.At, ["door"] = add.Door };
        if (add.Graph is not null)
        {
            version["schema"] = GraphSchema;
            version["graph"] = add.Graph.DeepClone();
            if (add.Steps is not null) version["steps"] = add.Steps.DeepClone();
        }
        else
        {
            version["steps"] = add.Steps?.DeepClone();
        }

        if (file is null)
        {
            if ((IdProblem(add.Id) ?? NameProblem(add.Name)) is { } problem) return Refused(problem);
            version["version"] = 1;
            // The mark is written at creation (FABRIC-PIPELINE1a): a verification pipeline's, after the name; a development one's absent.
            var verification = add.Pipeline == WorkflowPipelines.Verification;
            var first = ReadVersion(ElementOf(version), 1, [], verification ? WorkflowPipelines.Verification : WorkflowPipelines.Development);
            if (first.Problem is not null) return Refused(first.Problem);
            var made = new JsonObject { ["id"] = add.Id, ["name"] = add.Name };
            if (verification) made["pipeline"] = WorkflowPipelines.Verification;
            made["versions"] = new JsonArray(version);
            return new WorkflowAddPlan(made, 1, null);
        }

        var read = Read(ElementOf(file));
        if (read.Problem is not null) return Refused($"nothing was written: the workflow's file cannot be read — {read.Problem}");
        if (read.Id != add.Id) return Refused($"nothing was written: the file holds the workflow `{read.Id}`, not `{add.Id}`.");
        if (add.Pipeline is { } asked && asked != read.Pipeline)
        {
            return Refused($"nothing was written: `{add.Id}` is a {read.Pipeline} pipeline, and a workflow keeps the pipeline it was made as: a "
                + $"{asked} pipeline is a new workflow.");
        }

        var number = read.Versions[^1].Version + 1;
        version["version"] = number;
        var candidate = ReadVersion(ElementOf(version), number, [.. read.Versions.Where(each => each.Steps is not null)], read.Pipeline!);
        if (candidate.Problem is not null) return Refused(candidate.Problem);

        var result = file.DeepClone().AsObject();
        var versions = result["versions"]!.AsArray();
        versions.Add(version);
        var numbers = read.Versions.Select(each => each.Version).Append(number).ToList();
        while (versions.Count > WorkflowKindTable.KeptVersions)
        {
            var oldest = numbers.FindIndex(each => each != number && !kept.Contains(each));
            if (oldest == -1) break;
            versions.RemoveAt(oldest);
            numbers.RemoveAt(oldest);
        }

        return new WorkflowAddPlan(result, number, null);
    }

    /// <summary>A workflow's file as written: indented by two, LF, with a last newline, its words as written.</summary>
    public static string Text(JsonNode file) => JsonFile.Serialize(file);

    /// <summary>A file's node as the readers read it, an element of its own.</summary>
    internal static JsonElement ElementOf(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }
}
