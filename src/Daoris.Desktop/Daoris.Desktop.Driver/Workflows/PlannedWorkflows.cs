using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The home's workflows as a plan reads them, with the copies a set-up would save added in memory: nothing is written, so
/// the plan's after reads the version it would choose.
/// </summary>
internal sealed class PlannedWorkflows : IWorkflowStore
{
    private readonly IWorkflowStore _inner;
    private readonly Dictionary<string, JsonNode> _files = new(StringComparer.Ordinal);

    public PlannedWorkflows(IWorkflowStore inner, string home)
    {
        _inner = inner;
        Home = home;
    }

    public PlannedWorkflows(string home) : this(WorkflowPorts.Store(home), home)
    {
    }

    public string Home { get; }

    /// <summary>The moment a planned copy is stamped with: the plan's own, and not a part of its digest.</summary>
    public static string At => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    public WorkflowFileFound? Load(string id) => _files.TryGetValue(id, out var file) ? Found(id, file) : _inner.Load(id);

    public IReadOnlyList<WorkflowFileFound> List() =>
        [.. _inner.List().Where(each => !_files.ContainsKey(each.Id)).Concat(_files.Select(pair => Found(pair.Key, pair.Value))).OrderBy(each => each.Id, StringComparer.Ordinal)];

    public void Write(string id, JsonNode file) => _files[id] = file;

    private static WorkflowFileFound Found(string id, JsonNode file) =>
        new(id, file, WorkflowNamed.Read(WorkflowNamed.ElementOf(file)));

    /// <summary>
    /// The id of a saved workflow whose newest version is a copy of <paramref name="shipped"/> with the gates a variant asks for, the home's
    /// first by id; null where there is none.
    /// </summary>
    public string? CopyOf(WorkflowDefault shipped, bool opinion, bool look, string? plugin = null, string? pattern = null)
    {
        // The newest version's digest is the variant's own: a copy the person edited is no copy of it.
        var wanted = NodeGraphs.Digest(NodeGraphs.Read(WorkflowDefaults.Variant(shipped, opinion, look, plugin, pattern).ToJsonString()).Graph!);
        return List().FirstOrDefault(each => each.Read is { Problem: null, Versions: [.., { From: { } from, Digest: { } digest }] }
            && from.Default == shipped.Id && digest == wanted)?.Id;
    }
}
