using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// WORKFLOWCLEAN1 1b: no door saves a list of steps or a <c>handed</c> now, yet a file saved so before still reads and binds. A test that needs
/// one writes the file as an older build left it.
/// </summary>
internal static class StoredSteps
{
    /// <summary>A version appended to a workflow's file, or the file made, as an older build wrote it; the version's number.</summary>
    public static int Add(string home, WorkflowVersionAdded add, IReadOnlyCollection<int>? kept = null)
    {
        var file = WorkflowStore.Load(home, add.Id)?.File?.DeepClone().AsObject()
            ?? new JsonObject { ["id"] = add.Id, ["name"] = add.Name ?? add.Id, ["versions"] = new JsonArray() };
        var versions = file["versions"]!.AsArray();
        var number = versions.Count == 0 ? 1 : (int)versions[^1]!["version"]! + 1;
        var version = new JsonObject { ["version"] = number, ["at"] = add.At, ["door"] = add.Door };
        if (add.Graph is not null)
        {
            version["schema"] = WorkflowNamed.GraphSchema;
            version["graph"] = add.Graph.DeepClone();
        }
        else
        {
            version["steps"] = add.Steps!.DeepClone();
        }

        versions.Add(version);
        var path = WorkflowStore.PathOf(home, add.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, WorkflowNamed.Text(file), new UTF8Encoding(false));
        return number;
    }
}
