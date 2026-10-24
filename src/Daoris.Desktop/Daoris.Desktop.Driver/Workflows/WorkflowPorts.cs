using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The named workflows a home keeps (WORKFLOW1d, the workflow design §2.4–§2.6): one file per workflow,
/// <c>&lt;home&gt;/workflows/&lt;id&gt;.json</c>, read as found and written whole. A port (STRUCT3b, D166; the code-structure
/// design §3): <see cref="WorkflowStore"/>'s use cases are written against it, and <c>WorkflowFiles</c> is the files behind it.
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
/// <see cref="WorkflowStore"/>'s use cases are written against it, and the files are behind it.
/// </summary>
public interface IWorkflowRunStore
{
    /// <summary>Whether a run is bound here: its id is a run's, and its file is there.</summary>
    bool Bound(string run);

    /// <summary>A run's binding, or null where it has none here, or its file does not read as one.</summary>
    WorkflowRunBinding? Read(string run);

    /// <summary>
    /// A run's binding kept where none is: a run already bound keeps the version it started on. False where it was bound already, a
    /// bind racing it included: of two binds of one run at once exactly one is kept (FABRIC-CUTOVER1g).
    /// </summary>
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
/// What a run's read reads on this machine beside the service (WORKFLOW1c, the workflow design §5.1): the landing record and the
/// due list under the home, the acceptances each session's conversation record keeps, and whether a tree still stands. A port
/// (STRUCT3b): <see cref="WorkflowRunReader"/> is written against it, and the reads are behind it.
/// </summary>
public interface IWorkflowRunRecords
{
    /// <summary>The landing record's entries (<see cref="LandedBranch"/>), read once per read of several runs.</summary>
    IReadOnlyList<LandedBranch> Landings();

    /// <summary>The automatic landings due and closed (<see cref="AutoLanding"/>), read once per read of several runs.</summary>
    IReadOnlyList<AutoLanding> AutoLandings();

    /// <summary>
    /// When each session's work was accepted into its line (D100): the newest acceptance its conversation's record keeps, by its id.
    /// A teammate's record is on its machine, and a record that does not read keeps none.
    /// </summary>
    IReadOnlyDictionary<string, DateTimeOffset> Acceptances(IReadOnlyList<TracedSession> sessions);

    /// <summary>Whether a session's tree still stands on this machine.</summary>
    bool Stands(string tree);

    /// <summary>
    /// Whether <paramref name="tree"/> is one this home opened (<see cref="SessionTrees.Holds"/>), standing or not: a chat's own tree, where
    /// a chat whose tree it is not works in the person's checkout (CHATFLOW1a).
    /// </summary>
    bool Holds(string tree);

    /// <summary>
    /// A run's node record, <c>run:&lt;run&gt;</c>, read and never written (FABRIC-CUTOVER1e): an empty one where Daoris has not judged the run
    /// yet, null where its file does not read.
    /// </summary>
    NodeRecord? Record(string unit);

    /// <summary>This machine's session branches (<see cref="SessionBranches"/>), which name a branch unit by its branch (FABRIC-CUTOVER1e).</summary>
    IReadOnlyList<GrownBranch> Branches();
}

/// <summary>
/// The ports over a home's files, for a caller that holds only the home (STRUCT3b; D166's STRUCT3b note): the one place Workflows'
/// use cases name the files behind them. Every module, the loop and the hosts reach Workflows by a
/// home, as they did before the ports; a caller that holds the ports passes them to the use case itself.
/// </summary>
internal static class WorkflowPorts
{
    /// <summary>The named workflows under <paramref name="home"/>.</summary>
    public static IWorkflowStore Store(string home) => new WorkflowFiles(home);

    /// <summary>The runs' bindings under <paramref name="home"/>.</summary>
    public static IWorkflowRunStore Runs(string home) => new WorkflowRunFiles(home);

    /// <summary>What a run's read reads under <paramref name="home"/> beside the service, and the trees on this machine.</summary>
    public static IWorkflowRunRecords Records(string home) => new WorkflowRunRecords(home);
}
