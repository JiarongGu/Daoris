using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOWCLEAN1 1b: a list of steps is no longer saved by any door, yet a file saved as steps before still reads and binds. A test that
/// needs one writes the file as an older build left it, which no door of this build can.
/// </summary>
internal static class StoredSteps
{
    /// <summary>A version of steps appended to a workflow's file, or the file made, written as an older build wrote it; the version's number.</summary>
    public static int Add(IWorkflowStore store, string id, string? name, string at, JsonNode steps, string door = "terminal")
    {
        var found = store.Load(id);
        var file = found?.File?.DeepClone().AsObject() ?? new JsonObject { ["id"] = id, ["name"] = name ?? id, ["versions"] = new JsonArray() };
        var versions = file["versions"]!.AsArray();
        var number = versions.Count == 0 ? 1 : (int)versions[^1]!["version"]! + 1;
        versions.Add(new JsonObject { ["version"] = number, ["at"] = at, ["door"] = door, ["steps"] = steps.DeepClone() });
        store.Write(id, file);
        return number;
    }

    /// <summary>The same through the home, as <see cref="WorkflowStore.Add(string, WorkflowVersionAdded, IReadOnlyCollection{int}?)"/> takes a version.</summary>
    public static int Add(string home, WorkflowVersionAdded add) => Add(WorkflowPorts.Store(home), add.Id, add.Name, add.At, add.Steps!, add.Door);

    /// <summary>Whether <paramref name="add"/> is a version of steps, which only this seam writes.</summary>
    public static bool Holds(WorkflowVersionAdded add) => add.Steps is not null && add.Graph is null;
}
