using Daoris.Ports.Repositories;

namespace Daoris.Testing;

/// <summary>
/// The two ways a memory repository moves a checkout's own branch to another commit without a switch: a fast-forward, which refuses work in
/// its way as git does, and a hard reset, which takes every tracked file to the target's and keeps what is untracked.
/// </summary>
internal sealed class MemoryAdvance(MemoryHistory history)
{
    internal GitDone FastForward(MemoryTree tree, MemoryCommit? now, MemoryCommit target)
    {
        if (now is null) return GitDone.Not("Cannot fast-forward an unborn branch.");
        if (target.Id == now.Id) return GitDone.Done;
        if (!history.Reach(target).Contains(now.Id)) return GitDone.Not("Not possible to fast-forward, aborting.");
        if (MemorySwitch.Blocked(tree, now.Tree, target.Tree, keepIgnored: false) is { } path)
        {
            return GitDone.Not($"Your local changes to the following files would be overwritten by merge: {path}");
        }

        Move(tree, now, target);
        return GitDone.Done;
    }

    internal GitDone ResetHard(MemoryTree tree, MemoryCommit? now, MemoryCommit target)
    {
        foreach (var (path, blob) in now?.Tree ?? new Dictionary<string, MemoryBlob>()) if (blob.Mode == MemoryBlob.File) tree.Files[path] = blob.Content;
        Move(tree, now, target);
        return GitDone.Done;
    }

    private void Move(MemoryTree tree, MemoryCommit? now, MemoryCommit target)
    {
        MemorySwitch.Place(tree, now?.Tree ?? new Dictionary<string, MemoryBlob>(), target);
        if (tree.Branch is { } branch) history.Refs[$"refs/heads/{branch}"] = target.Id;
        else tree.Detached = target.Id;
    }
}
