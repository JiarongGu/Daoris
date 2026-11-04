using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Daoris.Driver.NamedStepReading;

namespace Daoris.Driver;

/// <summary>
/// Named workflows read (WORKFLOW1d, D157 points 5, 6 and 8, the workflow design §2.5, §2.6, §3.2, §3.3, §3.9): what a workflow
/// file and each of its versions may say, the first problem in what does not, and a version's digest. Choosing one is WORKFLOW1e's,
/// and every gate reads the version a run bound (WORKFLOW1f, <see cref="WorkflowProcesses"/>).
/// </summary>
/// <remarks>
/// One table, this suite's tests' <c>fixtures/workflow-named.json</c>, cell for cell, every sentence and every digest included
/// (<c>WorkflowNamedTests</c>); the CLI's twin, <c>namedworkflows.ts</c>, left with the driver family's TypeScript (PUSH1f2b), and
/// <c>daoris driver workflow</c> is <see cref="DriverWorkflowsCommand"/>.
/// </remarks>
public static class WorkflowNamed
{
    internal const string IdRule = "lower-case letters, digits and dashes, at most 40";
    internal const string KindList = "`work`, `opinion`, `look`, `landing`, `pull-request`, `go-ahead`, `check`, `stage` or `all`";
    internal const string FileShape = "a workflow is a JSON object: its `id`, its `name` and its `versions`.";
    internal const string IdSentence = $"a workflow's `id` is {IdRule}, such as `docs-to-pr`.";
    internal const string Current = "`current` names Current, the workflow drawn from the rules as they stand, so no saved workflow takes it.";
    internal const string NameSentence = "a workflow's `name` is your words for it, 1 to 60 characters.";
    internal const string VersionsSentence = "a workflow's `versions` is a list of at least one.";
    internal const string VersionShape = "each of its versions is a JSON object with its `version`, `at`, `door` and `steps`.";
    internal const string VersionNumber = "each version's `version` is a whole number from 1, each greater than the one before it.";
    internal const string PipelineSentence = "a workflow's `pipeline` is `development` or `verification`, as it was made; absent is `development`.";

    /// <summary>The kinds a verification pipeline's graph never holds: each observes a chain's work, or lands it (the pipeline design §2.1).</summary>
    private static readonly string[] NotVerified =
        [NodeKinds.Claimed, NodeKinds.Opinion, NodeKinds.Look, NodeKinds.GoAhead, NodeKinds.Land, NodeKinds.Review, NodeKinds.Integrate, NodeKinds.WorkRequests, NodeKinds.QuestClose];

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
    /// <param name="onSave">True where the version is being added to a file (INTEGRATE1n's F47 is the save's alone: a stored version that read before still reads).</param>
    /// <param name="id">The workflow a version is being saved to, said in the refusal of a list of steps.</param>
    public static NamedVersion ReadVersion(JsonElement version, int number, IReadOnlyList<NamedVersion> prior, string pipeline = WorkflowPipelines.Development, bool onSave = false, string? id = null)
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

                var (graph, problems) = NodeGraphs.Read(graphJson.GetRawText(), onSave);
                if (graph is null) return Unread($"v{number}: its graph does not hold: {problems[0].Words}");
                var shape = pipeline == WorkflowPipelines.Verification ? VerificationProblem(graph, number) : OneLandingProblem(graph, number);
                return shape is { } landing
                    ? Unread(landing)
                    : new NamedVersion(number, at, door, null, null, NodeGraphs.Digest(graph), graph) { From = FromOf(version) };
            }
        }

        // A verification pipeline's versions are graphs (the steps engine runs no `check`, D190 §10).
        if (pipeline == WorkflowPipelines.Verification)
        {
            return Unread($"v{number}: a verification pipeline's versions are graphs (schema {GraphSchema}) holding its checks, never steps.");
        }

        // WORKFLOWCLEAN1 1b: a list of steps is no longer saved; a stored one reads as before.
        if (onSave && hasSteps) return Unread(StepsRefused(id));

        var (steps, problem) = ReadSteps(hasSteps ? raw : null, number, prior);
        return steps is null ? Unread(problem!) : new NamedVersion(number, at, door, steps, null, Digest(steps)) { From = FromOf(version) };
    }

    /// <summary>The code a save of steps is refused by (WORKFLOWCLEAN1 1b), which the page keys its words on.</summary>
    public const string StepsRefusedCode = "steps-refused";

    /// <summary>What every door says to a list of steps (schema 1) being saved.</summary>
    public static string StepsRefused(string? id) =>
        $"`{id ?? "the workflow"}` is not saved: a workflow is saved as a graph of steps now, and a list of steps (schema 1) is no longer saved. "
        + $"Compose it in the workflow editor, or save a graph with `daoris workflow apply {id ?? "<id>"} <file>`. "
        + "A workflow saved as steps before is converted by the conversion plan: `daoris convert --plan`.";

    /// <summary>A version's <c>from</c>, or null where it names none or names one in another shape: where it came from never makes it unreadable.</summary>
    private static WorkflowFrom? FromOf(JsonElement version) =>
        version.TryGetProperty("from", out var from) && from.ValueKind == JsonValueKind.Object
        && Json.Text(from, "default") is { } named && IsId(named) && Json.Text(from, "digest") is { Length: > 0 } digest
            ? new WorkflowFrom(named, digest)
            : null;

    /// <summary>
    /// Why a named version's graph is not one landing, or null (FABRIC-CUTOVER1b; D190 point 4, the cutover design §4): it holds one
    /// <c>land</c>, and at most one <c>opinion</c> and one <c>look</c>, since the process it is read as holds one of each. Judged after
    /// the graph holds, in that order, so a save is refused and a stored version is kept as written and read as unreadable. Held to
    /// <c>workflow-graph-versions.json</c>.
    /// </summary>
    private static string? OneLandingProblem(NodeGraph graph, int number)
    {
        string Named(string kind) => string.Join(" and ", graph.Nodes.Where(node => node.Kind == kind).Select(node => $"`{node.Id}`"));
        var landings = graph.Nodes.Count(node => NodeKinds.IsLanding(node.Kind));
        if (landings == 0) return $"v{number}: its graph holds no landing (`{NodeKinds.Land}`), and a named workflow's graph lands its work once.";
        if (landings > 1) return $"v{number}: its graph holds {landings} landings, {Named(NodeKinds.Land)}, and a named workflow's graph lands its work once.";
        foreach (var (kind, what) in new[] { (NodeKinds.Opinion, "opinions"), (NodeKinds.Look, "looks") })
        {
            var count = graph.Nodes.Count(node => node.Kind == kind);
            if (count > 1) return $"v{number}: its graph holds {count} {what}, {Named(kind)}, and a named workflow's graph holds at most one.";
        }

        return null;
    }

    /// <summary>
    /// Why a verification pipeline's graph is not one, or null (FABRIC-PIPELINE1a, D192 point 3, the pipeline design §2.1): it holds none
    /// of <c>claimed</c>, <c>opinion</c>, <c>look</c>, <c>go-ahead</c>, <c>land</c>, <c>integrate</c> (INTEGRATE1n: its subject is a chain's
    /// landing), <c>work.requests</c> (INTEGRATE1o: it holds a chain's work on the quests its sessions published) or <c>quest.close</c>, whose subjects (a chain's
    /// quests, its branch, its ask) a landed run has none of, the first in node order named; then at least one <c>check</c>. Held to
    /// <c>workflow-graph-versions.json</c>.
    /// </summary>
    private static string? VerificationProblem(NodeGraph graph, int number)
    {
        if (graph.Nodes.FirstOrDefault(node => NotVerified.Contains(node.Kind, StringComparer.Ordinal)) is { } held)
        {
            return $"v{number}: its graph holds `{held.Id}`, a `{held.Kind}`, and a verification pipeline's graph lands nothing and observes no "
                + "chain's work: it holds no `claimed`, `opinion`, `look`, `go-ahead`, `land`, `integrate`, `work.requests` or `quest.close`.";
        }

        return graph.Nodes.Any(node => node.Kind == NodeKinds.Check)
            ? null
            : $"v{number}: its graph holds no `{NodeKinds.Check}`, and a verification pipeline's graph runs at least one.";
    }

    /// <summary>The schema of a version that is a graph (FABRIC-GRAPH1b); a version of steps is schema 1, named or not.</summary>
    public const int GraphSchema = 2;

    private static string BothSentence(int number) => $"v{number}: a version is its `steps` or its `graph`, never both.";

    public static (IReadOnlyList<NamedStep>? Steps, string? Problem) ReadSteps(JsonElement? value, int number, IReadOnlyList<NamedVersion>? prior = null) =>
        NamedStepReading.ReadSteps(value, number, prior);

    public static (IReadOnlyList<NamedStep>? Steps, string? Problem) ReadShape(JsonElement? value, int number, IReadOnlyList<NamedVersion>? prior = null) =>
        NamedStepReading.ReadShape(value, number, prior);

    public static (IReadOnlyList<NamedStep>? Steps, string? Problem) GateSteps(
        NodeGraph graph, int number, LandingRule landing, IReadOnlyDictionary<string, string>? bound = null) =>
        NamedStepReading.GateSteps(graph, number, landing, bound);

    internal static (object? Value, string? Problem) ReadField(WorkflowKindField field, JsonElement? raw) => NamedStepReading.ReadField(field, raw);


    public static string Canonical(IReadOnlyList<NamedStep> steps) => NamedStepCanon.Canonical(steps);

    public static string Digest(IReadOnlyList<NamedStep> steps) => NamedStepCanon.Digest(steps);


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

    internal const string AskDoorPrefix = "ask-daoris:";

    /// <summary>The Ask Daoris door: its prefix and a plain id (DEDUP6).</summary>
    private static bool IsAskDoor(string door) =>
        door.StartsWith(AskDoorPrefix, StringComparison.Ordinal) && SafeNames.IsPlainId(door[AskDoorPrefix.Length..]);

    // ---- Adding a version (WORKFLOW1d, D157 point 5, the workflow design §2.4–§2.6) ----
    // Planned and not written: the store's use case (WorkflowStore's Add) writes the plan through its port. A version is
    // whole and never edited: saving adds the next number, keeping the file's other fields and every version as written,
    // then the newest 20 and every version a kept run names. The plan is held to the driver suite's fixtures/workflow-named.json
    // `store`, cell for cell (the CLI's planAddVersion left in PUSH1f2b).

    /// <summary>
    /// A version added to a workflow's file, planned and not written (design §2.6): the next number after the last, the file's
    /// other fields and every version kept as written, a version that does not read here included, then the newest 20 kept with
    /// every version a kept run names. A file that does not read is never written over, and steps that do not read write nothing.
    /// </summary>
    /// <param name="file">The file as it stands, or null for a new workflow.</param>
    /// <param name="kept">The versions a kept run names.</param>
    /// <param name="from">The default the version is copied from, kept in it as <c>from</c>; null writes none.</param>
    public static WorkflowAddPlan PlanAdd(JsonNode? file, WorkflowVersionAdded add, IReadOnlyCollection<int> kept, WorkflowFrom? from = null)
    {
        static WorkflowAddPlan Refused(string problem) => new(null, null, problem);
        // A graph is the version's schema 2 and its steps are none: both is refused whole, by the reader below, with the number it would take.
        var version = new JsonObject { ["version"] = 0, ["at"] = add.At, ["door"] = add.Door };
        if (from is not null) version["from"] = new JsonObject { ["default"] = from.Default, ["digest"] = from.Digest };
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
            var first = ReadVersion(ElementOf(version), 1, [], verification ? WorkflowPipelines.Verification : WorkflowPipelines.Development, onSave: true, id: add.Id);
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
        var candidate = ReadVersion(ElementOf(version), number, [.. read.Versions.Where(each => each.Steps is not null)], read.Pipeline!, onSave: true, id: add.Id);
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
