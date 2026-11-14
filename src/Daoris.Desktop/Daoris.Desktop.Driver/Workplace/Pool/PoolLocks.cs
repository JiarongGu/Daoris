namespace Daoris.Driver;

/// <summary>
/// The pool's own lock reasons on a slot: <c>daoris kept tree</c> while free and <c>daoris kept tree (claimed by run &lt;id&gt;)</c> through a
/// claim, which no session's or opening's reading takes as its own.
/// </summary>
internal static class PoolLocks
{
    internal const string Free = "daoris kept tree";

    private const string ClaimedPrefix = Free + " (claimed by run ";

    internal static string Claimed(string run) => $"{ClaimedPrefix}{run})";

    internal static bool IsOwn(string? reason) => reason is { } said && (said == Free || (said.StartsWith(ClaimedPrefix, StringComparison.Ordinal) && said.EndsWith(')')));

    /// <summary>How a slot's lock reads to the judgement: only the pool's reasons, a session's whose record no longer holds it, or none, pass.</summary>
    internal static SlotLocks Read(GitWorktree entry) =>
        !entry.Locked ? SlotLocks.None
        : IsOwn(entry.LockReason) ? SlotLocks.Free
        : SessionLocks.SessionOf(entry.LockReason) is not null ? SlotLocks.SessionNoRecord
        : SlotLocks.Other;

    internal static Task<GitDone> UnlockAsync(PoolGit git, string slot, CancellationToken ct) => git.Repository.UnlockAsync(slot, slot, ct);

    /// <summary>
    /// <paramref name="slot"/> locked with <paramref name="reason"/>, a lock of the pool's or a session's put in the new one's place; null
    /// where it is, otherwise git's words. A person's or an opening's lock is never changed.
    /// </summary>
    internal static async Task<string?> SetAsync(PoolGit git, string slot, string reason, CancellationToken ct)
    {
        if (await TreeWorktrees.EntriesAsync(slot, ct).ConfigureAwait(false) is not { } entries
            || entries.FirstOrDefault(entry => TreeGit.SamePath(entry.Path, slot)) is not { } entry)
        {
            return $"git lists no tree at {slot}";
        }

        if (entry.Locked)
        {
            if (entry.LockReason == reason) return null;
            if (Read(entry) == SlotLocks.Other) return $"the slot is locked for another reason ({entry.LockReason})";
            if (await git.Repository.UnlockAsync(slot, entry.Path, ct).ConfigureAwait(false) is { Ok: false } unlocked) return TreeGit.FirstLine(unlocked.Error);
        }

        return await git.Repository.LockAsync(slot, entry.Path, reason, ct).ConfigureAwait(false) is { Ok: false } locked ? TreeGit.FirstLine(locked.Error) : null;
    }
}
