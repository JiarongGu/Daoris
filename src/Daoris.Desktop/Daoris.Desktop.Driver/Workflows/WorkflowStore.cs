using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Where named workflows live and how a version is added (WORKFLOW1d, D157 point 5, the workflow design §2.4–§2.6): one file
/// per workflow, <c>&lt;home&gt;/workflows/&lt;id&gt;.json</c>, the home's (D63), written atomically, BOM-less and LF, and
/// machine-local. A version is whole and never edited: saving adds the next number, keeping the file's other fields and every
/// version as written, then the newest 20 and every version a kept run names.
/// </summary>
/// <remarks>
/// <para>A TWIN with the CLI's <c>planAddVersion</c> (<c>namedworkflows.ts</c>) and its door (<c>workflowdoor.ts</c>): the plan,
/// <see cref="WorkflowNamed.PlanAdd"/>, is held to this suite's tests' <c>fixtures/workflow-named.json</c> <c>store</c>, cell for
/// cell. A run binds a named workflow's newest version at its first start (WORKFLOW1e, <see cref="WorkflowRunBindings"/>), which
/// every gate reads (WORKFLOW1f, <see cref="WorkflowProcesses"/>); the screen's editor (WORKFLOW1g) and Ask Daoris (WORKFLOW1h)
/// write through here.</para>
///
/// <para><b>Layered</b> (STRUCT3b, D166): the store's use cases, written against <see cref="IWorkflowStore"/> and
/// <see cref="IWorkflowRunStore"/>; a caller that holds only the home reaches them by it, as every module did before.</para>
/// </remarks>
public static class WorkflowStore
{
    /// <summary>The folder under the home that holds the workflows.</summary>
    public const string Folder = "workflows";

    /// <summary>The folder of a home's workflows.</summary>
    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows);

    /// <summary>A workflow's file: its id, which <see cref="WorkflowNamed.IsId"/> has passed, so it names no path.</summary>
    public static string PathOf(string home, string id) =>
        WorkflowNamed.IsId(id) ? Path.Combine(FolderOf(home), id + ".json") : throw new DriverException(WorkflowNamed.IdProblem(id)!);

    /// <summary>A workflow's file under the home, as found: null where there is none; its problem where it is not JSON.</summary>
    public static WorkflowFileFound? Load(string home, string id) => WorkflowPorts.Store(home).Load(id);

    /// <summary>Every workflow under the home, by its file's name in ordinal order: each file named as an id, read or not.</summary>
    public static IReadOnlyList<WorkflowFileFound> List(string home) => WorkflowPorts.Store(home).List();

    /// <summary>
    /// A version added and written, atomically: the plan, then the file whole. Refused, naming why, where the plan writes nothing.
    /// </summary>
    /// <param name="kept">The versions a kept run names; absent, those this home's runs name (WORKFLOW1e, <see cref="WorkflowRunBindings.KeptVersions(string, string)"/>).</param>
    /// <returns>The version's number.</returns>
    public static int Add(string home, WorkflowVersionAdded add, IReadOnlyCollection<int>? kept = null) =>
        Add(WorkflowPorts.Store(home), WorkflowPorts.Runs(home), add, kept);

    /// <summary>A version added and written through the ports: the plan over the file the store holds, then the file whole.</summary>
    /// <param name="kept">The versions a kept run names; absent, those <paramref name="runs"/> name.</param>
    /// <returns>The version's number.</returns>
    public static int Add(IWorkflowStore store, IWorkflowRunStore runs, WorkflowVersionAdded add, IReadOnlyCollection<int>? kept = null)
    {
        if (WorkflowNamed.IdProblem(add.Id) is { } idProblem) throw new DriverException(idProblem);
        var found = store.Load(add.Id);
        if (found is { File: null }) throw new DriverException($"nothing was written: the workflow's file cannot be read — {found.Read.Problem}");
        var plan = WorkflowNamed.PlanAdd(found?.File, add, kept ?? runs.KeptVersions(add.Id));
        if (plan.Problem is not null) throw new DriverException(plan.Problem);
        store.Write(add.Id, plan.Result!);
        return plan.Version!.Value;
    }
}
