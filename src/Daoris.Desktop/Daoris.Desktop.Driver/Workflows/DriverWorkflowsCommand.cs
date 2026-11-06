using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris workflow …</c> (D194, the alone-path design §5; D157, the workflow design §4.7): the terminal's door to workflows; a
/// run's <c>run</c>, <c>keep</c>, <c>skip</c>, <c>stop</c> and <c>changes</c> are the host's. <c>show --repository|--workspace [--kind]</c> draws what
/// work there follows: Current, read from the rules as they stand, or the named workflow a choice gives. The rest are named workflows,
/// one file each under the home's <c>workflows</c>: <c>list</c>, <c>show &lt;id&gt;[@&lt;version&gt;]</c>, <c>new</c>, <c>edit</c>,
/// <c>apply</c>, <c>export</c> and <c>import</c>; and the choice in <c>driver.json</c>, <c>use</c> and <c>kind</c>. A writer says its
/// change as Ask Daoris's card draws it and saves a new version, never editing one; <c>--plan</c> says it and saves nothing.
/// </summary>
/// <remarks>
/// <para>A write is the person's press: sent to the running Daoris's route (<c>WORKFLOW_SAVE</c>, <c>WORKFLOW_USE</c>, <c>WORKFLOW_KIND</c>,
/// <c>WORKFLOW_LANDED</c>) and confirmed in its window, else done on the terminal's word, refused from a session's terminal, and
/// holding the home's lease (<see cref="VerbLeases"/>); a read holds nothing. <c>new</c>, <c>use</c> and <c>kind</c> run the typed operations
/// (<see cref="WorkflowOperations"/>) the window's routes run, and say their result. The landed route, <c>use … --landed</c>, accepts
/// the gates at the line's tip as the window's <c>WORKFLOW_LANDED</c> does, and is that route while Daoris runs. A file imported goes a
/// version at a time, each confirmed.</para>
/// <para>Exit codes are the CLI's: 1 where a version would not read or a choice is refused, 2 where the words cannot be done.</para>
/// </remarks>
[CommandGroup("workflow", "show", "list", "defaults", "new", "edit", "apply", "export", "import", "use", "kind", "run", "keep", "skip", "stop",
    "changes", "done", "approve", "reject")]
public static class DriverWorkflowsCommand
{
    /// <summary>The sub-verbs that write a workflow or the choice; with <c>--plan</c> each only says what it would write.</summary>
    public static IReadOnlySet<string> Writes { get; } = ChoiceGrammar.WorkflowWrites;

    /// <summary>The sub-verbs the usage names; <c>edit</c> is still answered, with the refusal that names the editor and <c>apply</c>.</summary>
    public static IReadOnlyList<string> Verbs { get; } = ["list", "defaults", "show", "use", "kind", "new", "apply", "export", "import"];

    public const string Usage = "`driver workflow` takes list; defaults [<default> [--to <file>]]; show <id>[@<version>]; show --repository <name>|--workspace <name> [--kind <kind>]; "
        + "new <id> --from <default>|<id>[@<version>] [--with-opinion] [--with-look] [--name \"…\"]; "
        + "apply <id> <file> [--base <version>]; export <id> [--to <file>]; import <file>; "
        + "use <id>|--clear --repository <name>|--workspace <name> [--kind <kind>|--landed] (current only with --landed: no verification); "
        + "kind <workspace> <kind> --label \"…\" [--paths <path,…>]|--drop. "
        + "new, apply, import, use and kind print their change, and with --plan save nothing — e.g. "
        + "`daoris workflow new docs --from merge-after-accept --plan`.";

    /// <summary>Whether the words (after <c>workflow</c>) write: a writing sub-verb with no <c>--plan</c>.</summary>
    public static bool Changes(IReadOnlyList<string> args) => ChoiceGrammar.WorkflowChanges(args);

    /// <summary>
    /// The verb's run over <paramref name="choices"/>, the <c>driver.json</c> this terminal read: said, and written where it writes. A
    /// refusal of the words is <see cref="ChoiceWords"/>, said by the caller.
    /// </summary>
    /// <param name="argv">The words from <c>workflow</c> on.</param>
    public static Task<int> RunAsync(IReadOnlyList<string> argv, DriverConfig choices, DriverChoicesWorld world, CancellationToken ct) =>
        new WorkflowTerminal(argv, choices, world, ct).Go();

    /// <summary>
    /// The graph a file's version or a file holds (FABRIC-GRAPH1f): a version of schema 2's <c>graph</c>, or an object that is a graph
    /// itself, its <c>nodes</c> a list or its <c>schemaVersion</c> named, with no <c>steps</c>; else null, and the file is read for steps.
    /// </summary>
    internal static bool GraphIn(JsonNode? value, out JsonNode? graph)
    {
        graph = null;
        if (value is not JsonObject record) return false;
        if (record["schema"] is JsonValue schema && schema.TryGetValue<double>(out var number) && number == WorkflowNamed.GraphSchema
            && record.TryGetPropertyValue("graph", out graph))
        {
            return true;
        }

        graph = record;
        return !record.ContainsKey("steps") && (record["nodes"] is JsonArray || record.ContainsKey("schemaVersion"));
    }

    /// <summary>Steps as a version reads them, or null where they do not read.</summary>
    internal static IReadOnlyList<NamedStep>? StepsOf(JsonNode? steps, int number, IReadOnlyList<NamedVersion>? prior = null) =>
        WorkflowNamed.ReadSteps(steps is null ? null : WorkflowNamed.ElementOf(steps), number, prior).Steps;

    /// <summary>The versions a workflow keeps, as a refusal names them.</summary>
    internal static string Kept(NamedWorkflowRead read) => string.Join(", ", read.Versions.Select(each => $"v{each.Version}"));

    /// <summary>Whether a checkout is the repository named, in any case.</summary>
    internal static bool InScope(string? repository, string name) => WorkflowOperations.InScope(repository, name);

    /// <summary>A level's choice by its name in any case, or null.</summary>
    internal static WorkflowChoice? ChoiceAt(IReadOnlyDictionary<string, WorkflowChoice> map, string name) => WorkflowOperations.ChoiceAt(map, name);

    /// <summary>A choice as the file writes it, to tell an edit that changes nothing.</summary>
    internal static string Shape(WorkflowChoice? choice, bool workspace) => WorkflowOperations.Shape(choice, workspace);
}
