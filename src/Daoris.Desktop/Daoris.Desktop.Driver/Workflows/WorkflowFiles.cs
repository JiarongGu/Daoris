using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The named workflows' files under a home (WORKFLOW1d, D157 point 5, the workflow design §2.4–§2.6): one file per workflow,
/// <c>&lt;home&gt;/workflows/&lt;id&gt;.json</c>, the home's (D63), written atomically, BOM-less and LF, and machine-local. The I/O
/// behind <see cref="IWorkflowStore"/> (STRUCT3b, D166); the layout is <see cref="WorkflowStore.FolderOf"/>'s and
/// <see cref="WorkflowStore.PathOf"/>'s.
/// </summary>
public sealed class WorkflowFiles(string home) : IWorkflowStore
{
    /// <summary>A workflow's file under the home, as found: null where there is none; its problem where it is not JSON.</summary>
    public WorkflowFileFound? Load(string id)
    {
        var path = WorkflowStore.PathOf(home, id);
        if (!File.Exists(path)) return null;
        JsonNode? file;
        try
        {
            file = JsonNode.Parse(AtomicFile.ReadText(path));
        }
        catch (JsonException)
        {
            return new WorkflowFileFound(id, null, new NamedWorkflowRead(null, null, [], $"`{id}.json` is not readable JSON."));
        }

        var read = file is null ? WorkflowNamed.Read(default) : WorkflowNamed.Read(WorkflowNamed.ElementOf(file));
        if (read.Problem is null && read.Id != id)
        {
            read = read with { Problem = $"`{id}.json` holds the workflow `{read.Id}`; a workflow's file is named by its id." };
        }

        return new WorkflowFileFound(id, file, read);
    }

    /// <summary>Every workflow under the home, by its file's name in ordinal order: each file named as an id, read or not.</summary>
    public IReadOnlyList<WorkflowFileFound> List()
    {
        var folder = WorkflowStore.FolderOf(home);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.GetFiles(folder, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(WorkflowNamed.IsId)
                .Order(StringComparer.Ordinal)
                .Select(id => Load(id!)!),
        ];
    }

    /// <summary>A workflow's file written whole, atomically, its folder made where it is not.</summary>
    public void Write(string id, JsonNode file)
    {
        Directory.CreateDirectory(WorkflowStore.FolderOf(home));
        var path = WorkflowStore.PathOf(home, id);
        AtomicFile.WriteText(path, WorkflowNamed.Text(file));
        // This process's own write wakes no landing (FABRIC-END7, EVENT2's rule): a version a terminal saved is heard.
        HomeWrites.Mine(path);
    }
}
