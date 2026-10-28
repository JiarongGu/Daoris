namespace Daoris.Driver;

/// <summary>
/// The look's half of carrying a failed session's work on (RETRYWORK1, D80's note): how many commits the tree of each open quest's
/// last session here holds beyond the commit it opened at, where that session was cut off before it took the quest. The planner reads
/// the count (<see cref="Snapshot.Committed"/>) and carries the quest on in that tree, never building it again in a new one.
/// </summary>
/// <remarks>
/// Asked only of the quests it could change: an open quest whose last session here failed, or whose stop was not the person's, with
/// its tree and its start commit on record. Nothing else asks git, so a look with no such quest starts no git at all.
/// </remarks>
public static class CommittedWork
{
    /// <summary>
    /// How many commits a tree holds past <c>before</c>, or null where that cannot be read: the tree is gone or is not a checkout's
    /// top level, the commit is not one, or git refused.
    /// </summary>
    public delegate Task<int?> Count(string tree, string before, CancellationToken ct);

    /// <summary>Whether a record ended so its quest would be retried (D80): failed, or stopped by the sweep or a shutdown (D104).</summary>
    internal static bool CutOff(PriorSession last) =>
        string.Equals(last.State, "failed", StringComparison.OrdinalIgnoreCase) || last is { State: "stopped", Interrupted: true };

    /// <summary>
    /// The snapshot with each such quest's count beyond its start, as it was where none is asked. A count that cannot be read is
    /// left out, and the quest is planned as a first start, as it was before.
    /// </summary>
    public static async Task<Snapshot> LookAsync(Snapshot snapshot, Count count, CancellationToken ct = default)
    {
        var committed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var quest in snapshot.Quests.Where(quest => quest.Status == "Open"))
        {
            if (!snapshot.LastRun.TryGetValue(quest.Id, out var last)
                || last.Teammate
                || !CutOff(last)
                || last is not { Tree: { Length: > 0 } tree, BaseCommit: { Length: > 0 } before }) continue;

            if (await count(tree, before, ct).ConfigureAwait(false) is { } commits) committed[quest.Id] = commits;
        }

        return committed.Count == 0 ? snapshot : snapshot with { Committed = committed };
    }

    /// <summary>git's count of <c>before..HEAD</c> in the tree, where the tree is its own checkout's top level (never the one above).</summary>
    public static async Task<int?> CountAsync(string tree, string before, CancellationToken ct)
    {
        if (!WorkingTree.IsCommitId(before) || !await WorkingTree.IsTopLevelAsync(tree, ct).ConfigureAwait(false)) return null;
        var (code, stdout, _) = await WorkingTree.GitAsync(tree, ["rev-list", "--count", $"{before}..HEAD"], ct).ConfigureAwait(false);
        return code == 0 && int.TryParse(stdout.Trim(), out var commits) ? commits : null;
    }
}
