using Daoris.Ports.Repositories;

namespace Daoris.Driver;

/// <summary>
/// A step's commits kept on refs of its run: <c>refs/daoris/steps/&lt;run&gt;/&lt;node&gt;/&lt;attempt&gt;</c>, one per attempt, so a later step, a
/// restart and git's collection all find a commit an attempt reads. Kept until the run has ended with no attempt unsettled, and only the
/// run's end-keeping removes them.
/// </summary>
internal static class StepRefs
{
    /// <summary>The namespace the steps' refs are under.</summary>
    public const string Prefix = "refs/daoris/steps/";

    /// <summary>The ref of <paramref name="node"/>'s <paramref name="attempt"/> in <paramref name="run"/>.</summary>
    public static string Of(string run, string node, int attempt) =>
        $"{Prefix}{run}/{node}/{attempt.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    public static string EditsOf(string run, string node) => $"{Prefix}{run}/{node}/edits";

    /// <summary>The attempt's commit kept on its ref in one transaction, made only where the ref is absent.</summary>
    public static Task<GitRefRun> KeepAsync(string root, string run, string node, int attempt, string commit, CancellationToken ct) =>
        new RefTransaction().Create(Of(run, node, attempt), commit).RunAsync(root, ct);

    /// <summary>Every step ref of <paramref name="run"/> with its commit, by name, as a restart finds them; null where git did not answer, which releases nothing.</summary>
    public static async Task<IReadOnlyList<(string Ref, string Commit)>?> OfRunAsync(string root, string run, CancellationToken ct)
    {
        if (await MachinePorts.Repository.RefsAsync(root, [$"{Prefix}{run}/"], null, null, ct).ConfigureAwait(false) is not { } refs) return null;
        return [.. refs.Select(each => (each.Name, each.Commit)).Order()];
    }

    /// <summary>Whether <paramref name="record"/>'s run is at its settled end: ended, with no attempt unsettled or waiting on a retry.</summary>
    public static bool Releases(NodeRecord record) =>
        record.Ended is not null && record.Attempts.All(attempt => attempt.State == AttemptStates.Settled && !record.RetryPending(attempt));

    /// <summary>Every step ref of <paramref name="run"/> deleted in one transaction, each at the commit it holds; done where it holds none.</summary>
    public static async Task<GitRefRun> ReleaseAsync(string root, string run, CancellationToken ct)
    {
        if (await OfRunAsync(root, run, ct).ConfigureAwait(false) is not { } held) return new GitRefRun(false, "the run's step refs are unread");
        if (held.Count == 0) return new GitRefRun(true, "no step refs");
        var release = new RefTransaction();
        foreach (var (reference, commit) in held) release.Delete(reference, commit);
        return await release.RunAsync(root, ct).ConfigureAwait(false);
    }
}
