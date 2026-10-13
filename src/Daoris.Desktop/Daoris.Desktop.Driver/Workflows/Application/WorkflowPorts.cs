using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The named workflows a home keeps (WORKFLOW1d, the workflow design §2.4–§2.6): one file per workflow,
/// <c>&lt;home&gt;/workflows/&lt;id&gt;.json</c>, read as found and written whole. A port (STRUCT3b, D166; the code-structure
/// design §3): <see cref="WorkflowStore"/>'s use cases are written against it, and Workflows' Infrastructure is the files behind it.
/// </summary>
public interface IWorkflowStore
{
    /// <summary>A workflow's file as found: null where there is none; its problem where it is not JSON, or names another id.</summary>
    WorkflowFileFound? Load(string id);

    /// <summary>Every workflow, by its file's name in ordinal order: each file named as an id, read or not.</summary>
    IReadOnlyList<WorkflowFileFound> List();

    /// <summary>A workflow's file written whole, atomically, BOM-less and LF (<see cref="WorkflowNamed.Text"/>), its folder made where it is not.</summary>
    void Write(string id, JsonNode file);
}

/// <summary>
/// The runs' bindings a home keeps (WORKFLOW1e, the workflow design §4.5, §5.1): one file per run,
/// <c>&lt;home&gt;/workflows/runs/&lt;run&gt;.json</c>, machine-local. A port (STRUCT3b): <see cref="WorkflowRunBindings"/>' and
/// <see cref="WorkflowStore"/>'s use cases are written against it, and Workflows' Infrastructure is the files behind it.
/// </summary>
public interface IWorkflowRunStore
{
    /// <summary>Whether a run is bound here: its id is a run's, and its file is there.</summary>
    bool Bound(string run);

    /// <summary>A run's binding, or null where it has none here, or its file does not read as one.</summary>
    WorkflowRunBinding? Read(string run);

    /// <summary>A run's binding kept where none is: a run already bound keeps the version it started on. False where it was bound already.</summary>
    bool Bind(WorkflowRunBinding binding);

    /// <summary>A run's binding written over its file, whole: the person's keep (WORKFLOW1f) and nothing else.</summary>
    void Write(string run, WorkflowRunBinding binding);

    /// <summary>
    /// Whether any run here is bound to a named workflow in <paramref name="repository"/>: a file that does not read cannot rule one
    /// out, so it answers yes.
    /// </summary>
    bool AnyNamed(string repository);

    /// <summary>The versions of <paramref name="workflow"/> the runs here name (<see cref="WorkflowRunBindings.KeptVersions(IEnumerable{System.Text.Json.JsonElement}, string)"/>).</summary>
    IReadOnlyList<int> KeptVersions(string workflow);
}

/// <summary>
/// The ports over a home's files, for a caller that holds only the home (STRUCT3b; D166's STRUCT3b note): the one place Workflows'
/// Application names its own Infrastructure. Every module, the loop and the hosts reach Workflows through its Application by a
/// home, as they did before it was layered; a caller that holds the ports passes them to the use case itself.
/// </summary>
internal static class WorkflowPorts
{
    /// <summary>The named workflows under <paramref name="home"/>.</summary>
    public static IWorkflowStore Store(string home) => new WorkflowFiles(home);

    /// <summary>The runs' bindings under <paramref name="home"/>.</summary>
    public static IWorkflowRunStore Runs(string home) => new WorkflowRunFiles(home);
}
