using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A kind as a level's choice holds it (WORKFLOW1e, the workflow design §2.5, §4.2): for a workspace, its label in the person's
/// words, the paths work of that kind keeps to, and the workflow it maps to; for a repository, only the workflow.
/// </summary>
/// <param name="Workflow">A workflow's id, <c>current</c>, or null where the kind maps to nothing (the level's default).</param>
public sealed record WorkflowKindChoice(string? Label, IReadOnlyList<string> Paths, string? Workflow);

/// <summary>
/// One level's choice of workflow (WORKFLOW1e, design §2.5): its default and its kinds, in the order written. A repository's
/// kinds map the workspace's; a workspace's also declare them.
/// </summary>
/// <param name="Default">A workflow's id, <c>current</c>, or null for none.</param>
public sealed record WorkflowChoice(string? Default, IReadOnlyList<KeyValuePair<string, WorkflowKindChoice>> Kinds)
{
    /// <summary>The kind by its id, or null where this choice has none.</summary>
    public WorkflowKindChoice? KindOf(string kind) =>
        Kinds.Where(pair => string.Equals(pair.Key, kind, StringComparison.Ordinal)).Select(pair => pair.Value).FirstOrDefault();

    /// <summary>A choice that names no default and no kind says nothing, and is not kept.</summary>
    public bool NamesNothing => Default is null && Kinds.Count == 0;
}

/// <summary>The task's own choice (design §4.1 row 1): its ask's latest, a kind and a workflow, each null where it names none.</summary>
public sealed record WorkflowTaskChoice(string? Kind, string? Workflow);

/// <summary>
/// The workflow a chain's work in a repository follows, and why (design §4.1): the workflow (an id, or <c>current</c>), the level
/// that chose it, the kind read with its label and paths, and a kind the task named that the workspace does not declare.
/// </summary>
/// <param name="Level">One of <see cref="WorkflowLevels"/>.</param>
/// <param name="Undeclared">The task's kind where its workspace does not declare it, which is then read as none; else null.</param>
public sealed record WorkflowSelected(
    string Workflow, string Level, string? Kind, string? Label, IReadOnlyList<string> Paths, string? Undeclared);

/// <summary>Which level of §4.1's table chose a workflow, as a code a door words and a run keeps (D143).</summary>
public static class WorkflowLevels
{
    /// <summary>Row 1: the task's own choice, its ask's.</summary>
    public const string Task = "task";

    /// <summary>Row 2: the repository's choice for the task's kind.</summary>
    public const string RepositoryKind = "repository-kind";

    /// <summary>Row 3: the repository's default, Current where a repository that chooses names none.</summary>
    public const string Repository = "repository";

    /// <summary>Row 4: the workspace's choice for the task's kind.</summary>
    public const string WorkspaceKind = "workspace-kind";

    /// <summary>Row 5: the workspace's default.</summary>
    public const string Workspace = "workspace";

    /// <summary>Row 6: nothing names a workflow, so Current.</summary>
    public const string Current = "current";
}

/// <summary>An edit to the choices: a workflow used for a level or its kind, a kind declared, or a kind dropped.</summary>
public abstract record WorkflowChoiceEdit;

/// <summary>
/// A workflow chosen, or a choice cleared (design §4.7's <c>use</c>), for a repository or a workspace, by kind where one is named.
/// </summary>
/// <param name="Workflow">A workflow's id, <c>current</c>, or null to clear.</param>
public sealed record WorkflowUse(string? Workflow, string? Repository, string? Workspace, string? Kind) : WorkflowChoiceEdit
{
    /// <summary>Whether the repository's workspace is known, so its kind is checked against the kinds it declares.</summary>
    public bool InKnown { get; init; }

    /// <summary>The repository's workspace, where <see cref="InKnown"/>; null for none, read as the default workspace.</summary>
    public string? In { get; init; }
}

/// <summary>A kind a workspace declares (design §4.7's <c>kind</c>), with its label and paths; declared again, both are replaced.</summary>
public sealed record WorkflowDeclare(string Kind, string Workspace, string Label, IReadOnlyList<string> Paths) : WorkflowChoiceEdit;

/// <summary>A kind a workspace no longer declares (design §4.7's <c>kind … --drop</c>).</summary>
public sealed record WorkflowDrop(string Kind, string Workspace) : WorkflowChoiceEdit;

/// <summary>
/// Which workflow work follows (WORKFLOW1e, D157 point 10, the workflow design §2.5, §4.1–§4.3): <c>workflows</c> by repository and
/// <c>workspaceWorkflows</c> by workspace in <c>driver.json</c>, each a default and kinds, a workspace's kinds declared with a label
/// and the paths work of that kind keeps to; read, written, edited, and resolved in §4.1's order.
/// </summary>
/// <remarks>
/// <para>A TWIN with the CLI's <c>workflowchoice.ts</c>: both are held, cell for cell, to ONE table, this suite's
/// <c>fixtures/workflow-selection.json</c>, which <c>driverconfig.test.ts</c> reads too (<c>.claude/knowledge/twins.md</c>).</para>
///
/// <para><b>A repository's choice replaces its workspace's whole</b> (§4.1), as its landing, review and opinion rules do: a
/// repository that names any workflow follows its own kinds and its own default, Current where it names none, and the
/// workspace's mapping of a kind does not reach it. <c>current</c> at any level is a choice like any other.</para>
///
/// <para><b>Additive</b>: two keys <c>driver.json</c> never had, written only when set, and nothing else in the file is read
/// again. With neither, every repository's work follows Current, which is today's behaviour.</para>
/// </remarks>
public static partial class WorkflowSelection
{
    /// <summary>The workflow drawn from the rules as they stand (design §2.7), which no saved workflow's id may take.</summary>
    public const string Current = "current";

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

    public const string NamedProblem = "a choice names the repository or the workspace it is for.";

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

    public static string WorkflowProblem(string workflow) =>
        $"`{workflow}` is not a workflow's id: lower-case letters, digits and dashes, at most 40, such as `docs-to-pr`; or `current`.";

    public static string UndeclaredInWorkspace(string workspace, string kind) =>
        $"workspace `{workspace}` declares no kind `{kind}`: a kind is declared first, with its label.";

    public static string UndeclaredForRepository(string repository, string workspace, string kind) =>
        $"`{repository}`'s workspace, `{workspace}`, declares no kind `{kind}`: a kind is declared first, with its label.";

    public static string NoneToDrop(string workspace, string kind) =>
        $"workspace `{workspace}` declares no kind `{kind}`, so there is none to drop.";

    /// <summary>A kind's id: lower-case letters, digits and dashes, at most 24.</summary>
    public static bool IsKindId(string? value) => value is { Length: > 0 and <= KindIdLimit } && KindShape().IsMatch(value);

    /// <summary>What a choice may name: a saved workflow's id, or <c>current</c>, which has an id's shape.</summary>
    public static bool IsWorkflow(string? value) => WorkflowNamed.IsId(value);

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
    /// <param name="workspace">Whether the entry is a workspace's, whose kinds are declared here.</param>
    public static (WorkflowChoice? Choice, string? Problem) Read(JsonElement entry, bool workspace)
    {
        if (entry.ValueKind != JsonValueKind.Object) return (null, ShapeProblem);

        string? chosen = null;
        if (entry.TryGetProperty("default", out var value) && value.ValueKind != JsonValueKind.Null)
        {
            chosen = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (!IsWorkflow(chosen)) return (null, DefaultProblem);
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

        return (new WorkflowChoice(chosen, kinds), null);
    }

    /// <summary>The first problem in an entry, or null: <see cref="Read"/>'s.</summary>
    public static string? Problem(JsonElement entry, bool workspace) => Read(entry, workspace).Problem;

    private static (WorkflowKindChoice? Kind, string? Problem) KindRead(string id, JsonElement value, bool workspace)
    {
        if (!IsKindId(id)) return (null, KindIdProblem(id));
        if (!workspace)
        {
            var mapped = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return IsWorkflow(mapped) ? (new WorkflowKindChoice(null, [], mapped), null) : (null, MappedProblem(id));
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
            if (!IsWorkflow(workflow)) return (null, KindWorkflowProblem(id));
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

        writer.WriteEndObject();
    }

    /// <summary>
    /// The workflow a chain's work in <paramref name="repository"/> follows (design §4.1): the task's choice, then the repository's
    /// for the task's kind, its default, the workspace's for the kind, its default, then Current. The first level that names a
    /// workflow decides, and a repository that names any replaces its workspace's whole. The task's kind is read only where its
    /// workspace declares it, and said otherwise.
    /// </summary>
    /// <param name="repository">The repository, or null for the workspace's own choice, which no repository's reaches.</param>
    /// <param name="workspace">The repository's workspace, or null for none, read as the default workspace.</param>
    public static WorkflowSelected Resolve(DriverConfig config, string? repository, string? workspace, WorkflowTaskChoice? task)
    {
        config.WorkspaceWorkflows.TryGetValue(RemoteTarget.Workspace(workspace), out var shared);
        var declared = task?.Kind is { } asked ? shared?.KindOf(asked) : null;
        var kind = declared is null ? null : task!.Kind;
        var undeclared = declared is null ? task?.Kind : null;
        WorkflowSelected Pick(string workflow, string level) => new(workflow, level, kind, declared?.Label, declared?.Paths ?? [], undeclared);

        if (task?.Workflow is { } chosen) return Pick(chosen, WorkflowLevels.Task);
        if (repository is not null && config.Workflows.TryGetValue(repository.Trim(), out var own))
        {
            return kind is not null && own.KindOf(kind)?.Workflow is { } mapped
                ? Pick(mapped, WorkflowLevels.RepositoryKind)
                : Pick(own.Default ?? Current, WorkflowLevels.Repository);
        }

        if (declared?.Workflow is { } sharedKind) return Pick(sharedKind, WorkflowLevels.WorkspaceKind);
        return shared?.Default is { } fallback ? Pick(fallback, WorkflowLevels.Workspace) : Pick(Current, WorkflowLevels.Current);
    }

    /// <summary>
    /// An edit applied to the choices (design §4.7): a workflow used or cleared for a repository or a workspace, by kind; a kind
    /// declared or dropped. A choice left naming nothing is removed, so its workspace's reaches the repository again; a name is
    /// kept under the spelling first written.
    /// </summary>
    /// <exception cref="DriverException">The edit's first problem, in the table's words: nothing is changed.</exception>
    public static DriverConfig Apply(DriverConfig config, WorkflowChoiceEdit edit) => edit switch
    {
        WorkflowUse use => Use(config, use),
        WorkflowDeclare declare => Declare(config, declare),
        WorkflowDrop drop => Drop(config, drop),
        _ => throw new DriverException("an edit to the workflow choices is a use, a declare or a drop."),
    };

    private static DriverConfig Use(DriverConfig config, WorkflowUse use)
    {
        var forWorkspace = use.Repository is null;
        var name = (use.Repository ?? use.Workspace)?.Trim() ?? "";
        if (name.Length == 0) throw new DriverException(NamedProblem);
        if (use.Workflow is { } workflow && !IsWorkflow(workflow)) throw new DriverException(WorkflowProblem(workflow));
        if (use.Kind is { } kind && !IsKindId(kind)) throw new DriverException(KindIdProblem(kind));

        var map = forWorkspace ? config.WorkspaceWorkflows : config.Workflows;
        map.TryGetValue(name, out var held);
        var kinds = held?.Kinds.ToList() ?? [];
        var chosen = held?.Default;
        if (use.Kind is not { } named)
        {
            chosen = use.Workflow;
        }
        else if (forWorkspace)
        {
            // A kind is declared before it maps to anything; clearing one that is not declared changes nothing.
            var index = kinds.FindIndex(pair => pair.Key == named);
            if (index == -1 && use.Workflow is not null) throw new DriverException(UndeclaredInWorkspace(name, named));
            if (index != -1) kinds[index] = new(named, kinds[index].Value with { Workflow = use.Workflow });
        }
        else
        {
            // Checked where the repository's workspace is known; a mapping cleared is never refused, so one left by a kind
            // its workspace has since dropped can go.
            if (use.InKnown && use.Workflow is not null)
            {
                var circle = RemoteTarget.Workspace(use.In);
                config.WorkspaceWorkflows.TryGetValue(circle, out var theirs);
                if (theirs?.KindOf(named) is null) throw new DriverException(UndeclaredForRepository(name, circle, named));
            }

            var index = kinds.FindIndex(pair => pair.Key == named);
            if (use.Workflow is null)
            {
                if (index != -1) kinds.RemoveAt(index);
            }
            else if (index != -1)
            {
                kinds[index] = new(named, new WorkflowKindChoice(null, [], use.Workflow));
            }
            else
            {
                if (kinds.Count >= MaxKinds) throw new DriverException(ManyProblem);
                kinds.Add(new(named, new WorkflowKindChoice(null, [], use.Workflow)));
            }
        }

        var next = With(map, name, new WorkflowChoice(chosen, kinds));
        return forWorkspace ? config with { WorkspaceWorkflows = next } : config with { Workflows = next };
    }

    private static DriverConfig Declare(DriverConfig config, WorkflowDeclare declare)
    {
        var name = declare.Workspace.Trim();
        if (name.Length == 0) throw new DriverException(NamedProblem);
        if (!IsKindId(declare.Kind)) throw new DriverException(KindIdProblem(declare.Kind));
        if (!IsLabel(declare.Label)) throw new DriverException(LabelProblem(declare.Kind));
        if (!ArePaths(declare.Paths)) throw new DriverException(PathsProblem(declare.Kind));

        config.WorkspaceWorkflows.TryGetValue(name, out var held);
        var kinds = held?.Kinds.ToList() ?? [];
        var index = kinds.FindIndex(pair => pair.Key == declare.Kind);
        if (index != -1)
        {
            kinds[index] = new(declare.Kind, kinds[index].Value with { Label = declare.Label, Paths = [.. declare.Paths] });
        }
        else
        {
            if (kinds.Count >= MaxKinds) throw new DriverException(ManyProblem);
            kinds.Add(new(declare.Kind, new WorkflowKindChoice(declare.Label, [.. declare.Paths], null)));
        }

        return config with { WorkspaceWorkflows = With(config.WorkspaceWorkflows, name, new WorkflowChoice(held?.Default, kinds)) };
    }

    private static DriverConfig Drop(DriverConfig config, WorkflowDrop drop)
    {
        var name = drop.Workspace.Trim();
        if (name.Length == 0) throw new DriverException(NamedProblem);
        config.WorkspaceWorkflows.TryGetValue(name, out var held);
        if (held?.KindOf(drop.Kind) is null) throw new DriverException(NoneToDrop(name, drop.Kind));
        var kinds = held.Kinds.Where(pair => pair.Key != drop.Kind).ToList();
        return config with { WorkspaceWorkflows = With(config.WorkspaceWorkflows, name, new WorkflowChoice(held.Default, kinds)) };
    }

    /// <summary>The map with a name's choice set under the spelling first written, or removed where it names nothing.</summary>
    private static IReadOnlyDictionary<string, WorkflowChoice> With(IReadOnlyDictionary<string, WorkflowChoice> map, string name, WorkflowChoice choice)
    {
        var next = new Dictionary<string, WorkflowChoice>(map, StringComparer.OrdinalIgnoreCase);
        var key = next.Keys.FirstOrDefault(each => string.Equals(each, name, StringComparison.OrdinalIgnoreCase)) ?? name;
        if (choice.NamesNothing) next.Remove(key);
        else next[key] = choice;
        return next;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex KindShape();
}
