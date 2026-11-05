using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A saved workflow whose newest version lands by a push or a pull request, as the steps it is now (G8; D241 point 4, <see cref="PushedGraphs"/>):
/// the conversion appends the next version, as the save does, and leaves older versions as they were. A workflow with nothing old is not touched,
/// so a home already in steps converts to itself. A version the save refuses is left as it is, said.
/// </summary>
public sealed class PushStepsStep : IHomeMigrationStep
{
    private const string Door = "terminal";

    public string Name => "push-steps";

    public void Run(HomeMigrationRun run)
    {
        var store = WorkflowPorts.Store(run.Home);
        foreach (var found in store.List().ToList())
        {
            if (found.Read is not { Problem: null, Verifies: false, Versions: [.., var newest] } read) continue;
            if (found.File?["versions"]?.AsArray().FirstOrDefault(each => (int?)each?["version"] == newest.Version) is not JsonObject version
                || version["graph"] is not JsonObject graph || PushedGraphs.Convert(graph, out _) is not { } converted) continue;

            run.Keep(WorkflowStore.PathOf(run.Home, found.Id));
            try
            {
                var planned = WorkflowOperations.PlanGraph(run.Home, found, found.Id, null, read.Name, Door, PlannedWorkflows.At, converted);
                store.Write(found.Id, planned.Plan.Result!);
                run.Said(WorkflowStore.PathOf(run.Home, found.Id));
            }
            catch (ChoiceWords)
            {
                // The save refuses the converted steps: the version stays as it was, and the workflow editor is where it is mended.
            }
        }
    }
}
