namespace Daoris.Driver;

/// <summary>
/// A named version read as a graph (FABRIC-WORKFLOW2): its number, its identity (<see cref="WorkflowNamed.Digest"/>), and its
/// graph with the digest a run on it binds; or, with no graph, why it is unreadable.
/// </summary>
/// <param name="GraphDigest">What a run on the decoded graph binds (<see cref="WireRun.GraphDigest"/>): the graph version's own digest.</param>
/// <param name="Unreadable">The reason it does not decode, said whole: the version's own problem, or why it holds no graph.</param>
public sealed record DecodedVersion(int Version, string? Identity, NodeGraph? Graph, string? GraphDigest, string? Unreadable)
{
    public bool Readable => Graph is not null;
}

/// <summary>
/// The version a run is bound to, read as a graph (§E7, <i>one governs a run, by its binding</i>). Pure: it reads a version as
/// <see cref="WorkflowNamed"/> read it and writes nothing. A version that is not a graph is unreadable.
/// </summary>
public static class WorkflowGraphs
{
    /// <summary>
    /// A stored version decoded: one that does not read in this build (<see cref="NamedVersion.Problem"/>) or holds no graph is
    /// unreadable with that reason, kept as it is; a graph version is a graph already, judged where it was read, and its identity is its
    /// own digest, which is what a run on it binds.
    /// </summary>
    public static DecodedVersion Decode(NamedVersion version) =>
        version.Graph is { } graph && version.Digest is { } digest
            ? new DecodedVersion(version.Version, digest, graph, digest, null)
            : new DecodedVersion(version.Version, null, null, null, version.Problem ?? $"v{version.Version}: it does not read.");

    /// <summary>
    /// The decoded version a run is bound to (D157 §5.1): null under Current. Its identity is the binding's digest; a file that does not
    /// read, a version it no longer holds, or one whose identity is not the binding's reads as unreadable, and the run cannot start on it
    /// (D157 §5.2).
    /// </summary>
    public static DecodedVersion? ForRun(NamedWorkflowRead workflow, WorkflowRunBinding binding)
    {
        if (binding.IsCurrent) return null;
        var number = binding.Version ?? 0;
        DecodedVersion Unread(string why) => new(number, null, null, null, why);
        if (binding.Version is null) return Unread(binding.Problem ?? $"the run `{binding.Run}` is bound to no version of `{binding.Workflow}`.");
        if (workflow.Problem is { } problem) return Unread(problem);
        if (workflow.Versions.FirstOrDefault(each => each.Version == number) is not { } stored)
        {
            return Unread($"the run `{binding.Run}` is bound to v{number}, which the workflow's file no longer holds.");
        }

        var decoded = Decode(stored);
        return decoded.Readable && decoded.Identity != binding.Digest
            ? Unread($"the run `{binding.Run}` is bound to v{number} at `{binding.Digest}`, and the file's v{number} reads as `{decoded.Identity}`: a version is "
                + "never edited, so its file is not the one the run started on.")
            : decoded;
    }
}
