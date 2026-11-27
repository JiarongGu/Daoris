namespace Daoris.Driver;

/// <summary>What moving a pin onto an ask's branch did (<see cref="TrainPins"/>).</summary>
public sealed record TrainPinned(string Kind, string Said);

/// <summary>The kinds of <see cref="TrainPinned"/>.</summary>
public static class TrainPinKinds
{
    /// <summary>A commit of the pin's own is made on the ask's branch.</summary>
    public const string Written = "written";

    /// <summary>The ask's branch pins that commit already: nothing moves.</summary>
    public const string Current = "current";

    /// <summary>The branch's lock is no lock a pin can be moved in: it is left as it is, for a person to say.</summary>
    public const string Refused = "refused";

    /// <summary>git cannot say, the branch is not there, or the move loses to another writer: nothing is written, and it is asked again at the next look.</summary>
    public const string Unread = "unread";
}

/// <summary>
/// A consumer's pin of a producer moved onto the consumer's ask branch, as a commit of its own, so the consumer part's landing carries it. Made from objects alone (<c>hash-object</c>, <c>mktree</c>, <c>commit-tree</c>, one <c>update-ref</c> verifying the old
/// value), so no working tree is touched, the branch is never checked out, and a lost race leaves the branch where it stood.
/// </summary>
public static class TrainPins
{
    public static async Task<TrainPinned> WriteAsync(string root, string ask, string producer, string commit, CancellationToken ct)
    {
        var line = TrainBranch.Of(ask);
        var tip = await MachinePorts.Repository.CommitOfAsync(root, $"refs/heads/{line}", ct).ConfigureAwait(false);
        if (tip is null) return new(TrainPinKinds.Unread, $"`{line}` is not there to take the pin of {producer}: it is asked again at the next look.");

        var text = await MachinePorts.Repository.TextAtAsync(root, tip, FamilyLock.FileName, ct).ConfigureAwait(false);
        IReadOnlyList<(string Producer, string Commit)> pins = [];
        if (text is not null)
        {
            if (FamilyLock.Entries(text) is not { } read) return new(TrainPinKinds.Refused, $"`{FamilyLock.FileName}` on `{line}` is not a lock, so the pin of {producer} is not moved in it.");
            pins = read;
        }

        if (pins.Any(pin => string.Equals(pin.Producer, producer, StringComparison.OrdinalIgnoreCase) && string.Equals(pin.Commit, commit, StringComparison.OrdinalIgnoreCase)))
        {
            return new(TrainPinKinds.Current, $"`{line}` pins {producer} at {Short(commit)} already.");
        }

        var (hashed, blob, hashErr) = await WorkingTree.GitAsync(root, ["hash-object", "-w", "--stdin"], FamilyLock.With(pins, producer, commit), ct).ConfigureAwait(false);
        if (hashed != 0) return new(TrainPinKinds.Unread, $"git could not keep the lock ({ContentHolds.Said(hashErr)}): it is asked again at the next look.");

        var (listed, output, listErr) = await WorkingTree.GitAsync(root, ["ls-tree", "-z", tip], ct).ConfigureAwait(false);
        if (listed != 0) return new(TrainPinKinds.Unread, $"git could not read `{line}`'s tree ({ContentHolds.Said(listErr)}): it is asked again at the next look.");
        var entries = output.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(entry => !entry.EndsWith($"\t{FamilyLock.FileName}", StringComparison.Ordinal)).ToList();
        entries.Add($"100644 blob {blob.Trim()}\t{FamilyLock.FileName}");
        var (made, tree, treeErr) = await WorkingTree.GitAsync(root, ["mktree", "-z"], string.Join('\0', entries) + "\0", ct).ConfigureAwait(false);
        if (made != 0) return new(TrainPinKinds.Unread, $"git could not make the tree of the pin ({ContentHolds.Said(treeErr)}): it is asked again at the next look.");

        var words = $"chore(pin): move the pin of {producer} to {Short(commit)}\n\nThe commit {commit} is where {producer}'s part of this ask landed on its line.";
        var (committed, moved, commitErr) = await WorkingTree.GitAsync(
            root, ["-c", "user.name=Daoris", "-c", "user.email=daoris@localhost", "-c", "commit.gpgsign=false", "commit-tree", tree.Trim(), "-p", tip, "-m", words], ct).ConfigureAwait(false);
        if (committed != 0) return new(TrainPinKinds.Unread, $"git could not commit the pin ({ContentHolds.Said(commitErr)}): it is asked again at the next look.");

        var (updated, _, updateErr) = await WorkingTree.GitAsync(root, ["update-ref", $"refs/heads/{line}", moved.Trim(), tip], ct).ConfigureAwait(false);
        return updated == 0
            ? new(TrainPinKinds.Written, $"`{line}` pins {producer} at {Short(commit)}, where its part of the ask landed.")
            : new(TrainPinKinds.Unread, $"`{line}` moved while its pin was made ({ContentHolds.Said(updateErr)}): it is asked again at the next look.");
    }

    private static string Short(string commit) => commit.Length > 12 ? commit[..12] : commit;
}
