namespace Daoris.Driver;

/// <summary>
/// A workflow store that reads a file once for as long as its stamp (its write time and length) stands: a pass that asks the version of
/// every run of a home asks the few workflow files they share, and parsing each again for each run grew with the runs.
/// </summary>
internal sealed class StampedWorkflows(IWorkflowStore inner, string home) : IWorkflowStore
{
    private readonly object _gate = new();

    private readonly Dictionary<string, (DateTime? Written, long Length, WorkflowFileFound? Found)> _kept = new(StringComparer.Ordinal);

    public WorkflowFileFound? Load(string id)
    {
        var file = new FileInfo(WorkflowStore.PathOf(home, id));
        DateTime? written = file.Exists ? file.LastWriteTimeUtc : null;
        var length = file.Exists ? file.Length : -1;
        lock (_gate)
        {
            if (_kept.TryGetValue(id, out var kept) && kept.Written == written && kept.Length == length) return kept.Found;
        }

        var found = inner.Load(id);
        lock (_gate) _kept[id] = (written, length, found);
        return found;
    }

    public IReadOnlyList<WorkflowFileFound> List() => inner.List();

    public void Write(string id, System.Text.Json.Nodes.JsonNode file)
    {
        inner.Write(id, file);
        lock (_gate) _kept.Remove(id);
    }
}
