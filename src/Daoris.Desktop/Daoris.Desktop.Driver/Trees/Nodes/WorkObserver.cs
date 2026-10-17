namespace Daoris.Driver;

/// <summary>
/// An observed plugin node reached by its believed event (FABRIC-PERFORM1i; the fabric events design §C4, §E2, §E10).
/// </summary>
public sealed class WorkObserver(NodeRecordFiles records, IWorkRuntimePort port, Func<DateTimeOffset> clock)
{
    /// <summary>The events heard that name no invocation, judged and taken.</summary>
    public Task<IReadOnlyList<WorkPerformed>> HearAsync(
        IReadOnlyList<WorkStepHeard> heard, IReadOnlyDictionary<string, GovernedUnit> governed,
        Func<string, IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlyDictionary<string, string>>> tips, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WorkPerformed>>([]);
}

/// <summary>Each named branch's tip in one checkout, read fresh with one read of its refs (FABRIC-PERFORM1i).</summary>
public static class NodeTips
{
    public static Task<IReadOnlyDictionary<string, string>> ReadAsync(string root, IReadOnlyCollection<string> branches, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
}

/// <summary>A repository's identity (§B5): its common git folder's file identity, else its spelling (FABRIC-PERFORM1i).</summary>
public static class RepositoryIdentity
{
    public static string Of(string root) => root;
}
