using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Where named workflows live and how a version is added: one file
/// per workflow, <c>&lt;home&gt;/workflows/&lt;id&gt;.json</c>, the home's, written atomically, BOM-less and LF, and
/// machine-local. A version is whole and never edited: saving adds the next number, keeping the file's other fields and every
/// version as written, then the newest 20 and every version a kept run names.
/// </summary>
/// <remarks>
/// <para>The plan, <see cref="WorkflowNamed.PlanAdd"/>, is held to this suite's tests' <c>fixtures/workflow-named.json</c>
/// <c>store</c>, cell for cell. A run binds a named workflow's newest version at its first start
/// (<see cref="WorkflowRunBindings"/>), which every gate reads (<see cref="WorkflowProcesses"/>); the screen's editor,
/// Ask Daoris and <c>daoris workflow</c> (<see cref="DriverWorkflowsCommand"/>) write through here.</para>
///
/// <para><b>Layered</b>: the store's use cases, written against <see cref="IWorkflowStore"/> and
/// <see cref="IWorkflowRunStore"/>; a caller that holds only the home reaches them by it.</para>
/// </remarks>
public static class WorkflowStore
{
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
    /// <param name="kept">The versions a kept run names; absent, those this home's runs name (<see cref="WorkflowRunBindings.KeptVersions(string, string)"/>).</param>
    /// <returns>The version's number.</returns>
    public static int Add(string home, WorkflowVersionAdded add, IReadOnlyCollection<int>? kept = null) =>
        Add(WorkflowPorts.Store(home), WorkflowPorts.Runs(home), add, kept);

    /// <summary>A version added and written through the ports: the plan over the file the store holds, then the file whole.</summary>
    /// <param name="kept">The versions a kept run names; absent, those <paramref name="runs"/> name.</param>
    /// <returns>The version's number.</returns>
    public static int Add(IWorkflowStore store, IWorkflowRunStore runs, WorkflowVersionAdded add, IReadOnlyCollection<int>? kept = null)
    {
        if (WorkflowNamed.IdProblem(add.Id) is { } idProblem) throw new DriverException(idProblem);
        lock (Writing)
        {
            var found = store.Load(add.Id);
            if (found is { File: null }) throw new DriverException($"nothing was written: the workflow's file cannot be read — {found.Read.Problem}");
            var plan = WorkflowNamed.PlanAdd(found?.File, add, kept ?? runs.KeptVersions(add.Id));
            if (plan.Problem is not null) throw new DriverException(plan.Problem);
            store.Write(add.Id, plan.Result!);
            return plan.Version!.Value;
        }
    }

    /// <summary>Held from a workflow's read to its write: two saves of one workflow never plan the same number.</summary>
    internal static readonly object Writing = new();

    /// <summary>Whether <paramref name="id"/>'s newest saved version is still <paramref name="version"/>, the one a save planned against.</summary>
    internal static bool NewestIs(IWorkflowStore store, string id, int version) =>
        store.Load(id) is { File: not null, Read.Problem: null } now && now.Read.Versions[^1].Version == version;

    /// <summary>Writes <paramref name="file"/> as the workflow's first version where the store holds no file for <paramref name="id"/>; false, nothing written, where one has appeared.</summary>
    internal static bool WriteIfAbsent(IWorkflowStore store, string id, JsonNode file)
    {
        lock (Writing)
        {
            if (store.Load(id) is not null) return false;
            store.Write(id, file);
            return true;
        }
    }

    /// <summary>Writes <paramref name="file"/> where the workflow's newest version is still <paramref name="version"/>; false, nothing written, where it is not.</summary>
    internal static bool WriteIfNewest(IWorkflowStore store, string id, int version, JsonNode file)
    {
        lock (Writing)
        {
            if (!NewestIs(store, id, version)) return false;
            store.Write(id, file);
            return true;
        }
    }
}
