using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A kind as a level's choice holds it (WORKFLOW1e, the workflow design §2.5, §4.2): for a workspace, its label in the person's
/// words, the paths work of that kind keeps to, and the workflow it maps to; for a repository, only the workflow.
/// </summary>
/// <param name="Workflow">A workflow's id, <c>current</c>, or null where the kind maps to nothing (the level's default).</param>
public sealed record WorkflowKindChoice(string? Label, IReadOnlyList<string> Paths, string? Workflow);

/// <summary>
/// The landed route (FABRIC-PIPELINE1a, D192 point 2, the pipeline design §1.2): the verification pipeline for work that reaches the
/// line, and the gates the person accepted for it to run unasked, by git's blob id of <c>daoris.gates.json</c> at the line's tip.
/// </summary>
/// <param name="Workflow">A verification pipeline's id, or <c>current</c>: no verification, today's.</param>
/// <param name="Gates">The accepted blob id, <c>none</c> where the line held no declaration, or null where none was accepted yet.</param>
public sealed record WorkflowLanded(string Workflow, string? Gates);

/// <summary>
/// One level's choice of workflow (WORKFLOW1e, design §2.5): its default and its kinds, in the order written, and its landed route
/// (FABRIC-PIPELINE1a). A repository's kinds map the workspace's; a workspace's also declare them.
/// </summary>
/// <param name="Default">A workflow's id, <c>current</c>, or null for none.</param>
public sealed record WorkflowChoice(string? Default, IReadOnlyList<KeyValuePair<string, WorkflowKindChoice>> Kinds)
{
    /// <summary>The landed route, or null where this level names none.</summary>
    public WorkflowLanded? Landed { get; init; }

    /// <summary>The kind by its id, or null where this choice has none.</summary>
    public WorkflowKindChoice? KindOf(string kind) =>
        Kinds.Where(pair => string.Equals(pair.Key, kind, StringComparison.Ordinal)).Select(pair => pair.Value).FirstOrDefault();

    /// <summary>
    /// Whether it names a development route, a default or a kind: what makes a repository's choice replace its workspace's. A landed
    /// route alone does not (FABRIC-PIPELINE1a, the pipeline design §1.2).
    /// </summary>
    public bool NamesDevelopment => Default is not null || Kinds.Count > 0;

    /// <summary>A choice that names no default, no kind and no landed route says nothing, and is not kept.</summary>
    public bool NamesNothing => !NamesDevelopment && Landed is null;
}

/// <summary>
/// The workflow choices as <c>driver.json</c> keeps them, <c>workflows</c> by repository and <c>workspaceWorkflows</c> by workspace
/// (WORKFLOW1e, D157 point 10; FOUNDATION1 mod-iii): the key's shape, its limits and the sentences that say what does not read, read
/// and written by the settings' own parser. What a choice means, its resolution, edit and the sentences of an edit, stays with the
/// selection (<see cref="WorkflowSelection"/>), which answers through this for what it shares.
/// </summary>
public static class WorkflowKey
{
    /// <summary>The most kinds one choice holds (design §4.2).</summary>
    public const int MaxKinds = 12;

    /// <summary>The longest a kind's id is.</summary>
    public const int KindIdLimit = 24;

    /// <summary>The longest a kind's label is.</summary>
    public const int LabelLimit = 60;

    /// <summary>The most paths one kind keeps to.</summary>
    public const int MaxPaths = 20;

    /// <summary>The longest one of a kind's paths is.</summary>
    public const int PathLimit = 200;

    public const string ShapeProblem = "a workflow choice is an object: its `default`, its `kinds`, or both.";

    public const string DefaultProblem =
        "its `default` names a workflow: its id, lower-case letters, digits and dashes, at most 40, or `current`.";

    public const string KindsProblem = "its `kinds` is an object, each kind by its id.";

    public const string ManyProblem = "a choice holds at most 12 kinds.";

    public static string KindIdProblem(string kind) =>
        $"`{kind}` is not a kind's id: lower-case letters, digits and dashes, at most 24, such as `docs`.";

    public static string MappedProblem(string kind) => $"kind `{kind}` maps to a workflow: its id, or `current`.";

    public static string DeclaredProblem(string kind) =>
        $"kind `{kind}` is an object: its `label`, and its `paths` and `workflow` where it has them.";

    public static string LabelProblem(string kind) => $"kind `{kind}`'s `label` is your words for it, 1 to 60 characters.";

    public static string PathsProblem(string kind) =>
        $"kind `{kind}`'s `paths` is a list of at most 20 paths in a repository, each with `/` between its parts, none absolute, "
        + "none with `..` or spaces around it, none twice, each at most 200 characters.";

    public static string KindWorkflowProblem(string kind) => $"kind `{kind}`'s `workflow` names a workflow: its id, or `current`.";

    public const string LandedProblem =
        "its `landed` is an object: its `workflow`, a verification pipeline's id or `current`, and its `gates` where they were accepted.";

    public const string GatesProblem =
        "its `landed` route's `gates` is the blob id git gives the `daoris.gates.json` you accepted, 40 or 64 lower-case hex, or `none` "
        + "where the line held none.";

    /// <summary>Gates accepted where the line held no declaration.</summary>
    public const string NoGates = "none";

    /// <summary>
    /// The gates a landed route keeps: git's blob id of the <c>daoris.gates.json</c> the person accepted, 40 lower-case hex (SHA-1) or
    /// 64 (SHA-256), or <c>none</c>; the CLI's <c>isGates</c>.
    /// </summary>
    public static bool IsGates(string? value) =>
        value == NoGates || (value is { Length: 40 or 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));

    /// <summary>A workflow's or a step's id: lower-case letters, digits and dashes, at most 40.</summary>
    public static bool IsId(string? value) => value is { Length: > 0 and <= 40 } && SafeNames.IsKebabId(value);

    /// <summary>A kind's id: lower-case letters, digits and dashes, at most 24.</summary>
    public static bool IsKindId(string? value) => value is { Length: > 0 and <= KindIdLimit } && SafeNames.IsKebabId(value);

    /// <summary>A kind's label: the person's words, not blank, at most 60 characters, kept as written.</summary>
    public static bool IsLabel(string? value) => value is { Length: <= LabelLimit } && value.Trim().Length > 0;

    /// <summary>
    /// What is wrong with a kind's paths, or nothing: at most 20, each relative with <c>/</c> between its parts, no <c>..</c> part,
    /// no spaces around it and no control character, at most 200 characters, and none twice.
    /// </summary>
    public static bool ArePaths(IReadOnlyList<string> paths) =>
        paths.Count <= MaxPaths
        && paths.All(path => path.Length is >= 1 and <= PathLimit && path == path.Trim() && !path.StartsWith('/') && !path.Contains('\\')
                             && !path.Split('/').Contains("..") && !path.Any(c => c < ' '))
        && paths.Distinct(StringComparer.Ordinal).Count() == paths.Count;

    /// <summary>
    /// An entry read (design §2.5): its first problem, or the choice; a choice naming nothing is no problem. A repository's kind maps
    /// to a workflow; a workspace's is an object, its label first. Null is absent, and a field a kind has no name for is not kept.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="workspace">Whether the entry is a workspace's, whose kinds are declared here.</param>
    public static (WorkflowChoice? Choice, string? Problem) Read(JsonElement entry, bool workspace)
    {
        if (entry.ValueKind != JsonValueKind.Object) return (null, ShapeProblem);

        string? chosen = null;
        if (entry.TryGetProperty("default", out var value) && value.ValueKind != JsonValueKind.Null)
        {
            chosen = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (!IsId(chosen)) return (null, DefaultProblem);
        }

        var kinds = new List<KeyValuePair<string, WorkflowKindChoice>>();
        if (entry.TryGetProperty("kinds", out var map) && map.ValueKind != JsonValueKind.Null)
        {
            if (map.ValueKind != JsonValueKind.Object) return (null, KindsProblem);
            if (map.EnumerateObject().Count() > MaxKinds) return (null, ManyProblem);
            foreach (var property in map.EnumerateObject())
            {
                var (kind, problem) = KindRead(property.Name, property.Value, workspace);
                if (problem is not null) return (null, problem);
                kinds.Add(new(property.Name, kind!));
            }
        }

        // The landed route (FABRIC-PIPELINE1a), after the kinds: its workflow an id or `current`, its gates a blob id or `none` where accepted.
        WorkflowLanded? landed = null;
        if (entry.TryGetProperty("landed", out var route) && route.ValueKind != JsonValueKind.Null)
        {
            var workflow = route.ValueKind == JsonValueKind.Object ? Json.Text(route, "workflow") : null;
            if (!IsId(workflow)) return (null, LandedProblem);
            string? gates = null;
            if (route.TryGetProperty("gates", out var accepted) && accepted.ValueKind != JsonValueKind.Null)
            {
                gates = accepted.ValueKind == JsonValueKind.String ? accepted.GetString() : null;
                if (!IsGates(gates)) return (null, GatesProblem);
            }

            landed = new WorkflowLanded(workflow!, gates);
        }

        return (new WorkflowChoice(chosen, kinds) { Landed = landed }, null);
    }

    private static (WorkflowKindChoice? Kind, string? Problem) KindRead(string id, JsonElement value, bool workspace)
    {
        if (!IsKindId(id)) return (null, KindIdProblem(id));
        if (!workspace)
        {
            var mapped = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return IsId(mapped) ? (new WorkflowKindChoice(null, [], mapped), null) : (null, MappedProblem(id));
        }

        if (value.ValueKind != JsonValueKind.Object) return (null, DeclaredProblem(id));
        var label = value.TryGetProperty("label", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString() : null;
        if (!IsLabel(label)) return (null, LabelProblem(id));

        IReadOnlyList<string> paths = [];
        if (value.TryGetProperty("paths", out var list) && list.ValueKind != JsonValueKind.Null)
        {
            if (list.ValueKind != JsonValueKind.Array || list.EnumerateArray().Any(path => path.ValueKind != JsonValueKind.String))
            {
                return (null, PathsProblem(id));
            }

            paths = [.. list.EnumerateArray().Select(path => path.GetString()!)];
            if (!ArePaths(paths)) return (null, PathsProblem(id));
        }

        string? workflow = null;
        if (value.TryGetProperty("workflow", out var maps) && maps.ValueKind != JsonValueKind.Null)
        {
            workflow = maps.ValueKind == JsonValueKind.String ? maps.GetString() : null;
            if (!IsId(workflow)) return (null, KindWorkflowProblem(id));
        }

        return (new WorkflowKindChoice(label, paths, workflow), null);
    }

    /// <summary>
    /// A map of choices as <c>driver.json</c> holds it: each name trimmed, a blank one not read; an entry with a problem, and one
    /// naming nothing, not read; a name written twice in any case read where first readable. A map that is not one is none.
    /// </summary>
    internal static IReadOnlyDictionary<string, WorkflowChoice> Map(JsonElement root, string name, bool workspace)
    {
        var map = new Dictionary<string, WorkflowChoice>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || map.ContainsKey(named)) continue;
            if (Read(property.Value, workspace) is not ({ NamesNothing: false } choice, null)) continue;
            map[named] = choice;
        }

        return map;
    }

    /// <summary>A map written as <c>driver.json</c> keeps it, only when set, its names in ordinal order and each kind where it stands.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, WorkflowChoice> map, bool workspace)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, choice) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(key);
            Write(writer, choice, workspace);
        }

        writer.WriteEndObject();
    }

    /// <summary>One choice as kept: its default where it names one, then its kinds where it has any.</summary>
    public static void Write(Utf8JsonWriter writer, WorkflowChoice choice, bool workspace)
    {
        writer.WriteStartObject();
        if (choice.Default is { } chosen) writer.WriteString("default", chosen);
        if (choice.Kinds.Count > 0)
        {
            writer.WriteStartObject("kinds");
            foreach (var (id, kind) in choice.Kinds)
            {
                if (!workspace)
                {
                    writer.WriteString(id, kind.Workflow);
                    continue;
                }

                writer.WriteStartObject(id);
                writer.WriteString("label", kind.Label);
                if (kind.Paths.Count > 0)
                {
                    writer.WriteStartArray("paths");
                    foreach (var path in kind.Paths) writer.WriteStringValue(path);
                    writer.WriteEndArray();
                }

                if (kind.Workflow is { } workflow) writer.WriteString("workflow", workflow);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        if (choice.Landed is { } landed)
        {
            writer.WriteStartObject("landed");
            writer.WriteString("workflow", landed.Workflow);
            if (landed.Gates is { } gates) writer.WriteString("gates", gates);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}
