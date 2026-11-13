namespace Daoris.Driver;

/// <summary>What bringing the line's tip into the ask's branch did (<see cref="TrainBases"/>).</summary>
public sealed record TrainBased(string Kind, string Said);

/// <summary>The kinds of <see cref="TrainBased"/>.</summary>
public static class TrainBaseKinds
{
    /// <summary>The ask's branch holds the line's tip already, or is made at it: nothing moves.</summary>
    public const string Current = "current";

    /// <summary>The line's tip is merged into the ask's branch.</summary>
    public const string Merged = "merged";

    /// <summary>The line's tip and the ask's branch conflict: the branch stays where it stands.</summary>
    public const string Conflict = "conflict";

    /// <summary>git cannot say, or the move loses to another writer: nothing is written, and it is asked again at the next look.</summary>
    public const string Unread = "unread";
}

/// <summary>
/// The line's tip merged into the ask's branch before each batch, so the branch never drifts more than one train from the line. The merge is
/// made from objects alone (<c>merge-tree</c>, <c>commit-tree</c>, one <c>update-ref</c> verifying the old value), so no working tree is
/// touched and a conflict leaves the branch where it stands; the batch is then replayed onto the merged branch as on any line.
/// </summary>
public static class TrainBases
{
    public static async Task<TrainBased> PrepareAsync(string root, string ask, string baseLine, CancellationToken ct)
    {
        var (baseCode, baseOut, baseErr) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{baseLine}^{{commit}}"], ct).ConfigureAwait(false);
        if (baseCode != 0 || baseOut.Trim().Length == 0)
        {
            return new(TrainBaseKinds.Unread, $"git could not read where the line `{baseLine}` stands ({ContentHolds.Said(baseErr)}): the ask's branch is not brought up to it.");
        }

        var line = TrainBranch.Of(ask);
        var @base = baseOut.Trim();
        if (!await TrainBranch.EnsureAsync(root, ask, @base, ct).ConfigureAwait(false))
        {
            return new(TrainBaseKinds.Unread, $"git would not make `{line}` at `{baseLine}`: it is asked again at the next look.");
        }

        var (tipCode, tipOut, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{line}^{{commit}}"], ct).ConfigureAwait(false);
        if (tipCode != 0) return new(TrainBaseKinds.Unread, $"git could not read where `{line}` stands: it is asked again at the next look.");
        var tip = tipOut.Trim();

        var (held, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", @base, tip], ct).ConfigureAwait(false);
        if (held == 0) return new(TrainBaseKinds.Current, $"`{line}` holds `{baseLine}`'s tip already.");

        var (merged, tree, treeErr) = await WorkingTree.GitAsync(root, ["merge-tree", "--write-tree", "--no-messages", tip, @base], ct).ConfigureAwait(false);
        if (merged == 1)
        {
            return new(TrainBaseKinds.Conflict, $"`{baseLine}` and `{line}` conflict, so the ask's branch is left as it was and its parts land on it unmerged.");
        }

        if (merged != 0) return new(TrainBaseKinds.Unread, $"git could not merge `{baseLine}` into `{line}` ({ContentHolds.Said(treeErr)}): it is asked again at the next look.");

        var (made, commit, makeErr) = await WorkingTree.GitAsync(
            root, ["-c", "user.name=Daoris", "-c", "user.email=daoris@localhost", "commit-tree", tree.Trim().Split('\n')[0], "-p", tip, "-p", @base, "-m", $"Merge {baseLine} into {line}"], ct)
            .ConfigureAwait(false);
        if (made != 0) return new(TrainBaseKinds.Unread, $"git could not commit the merge of `{baseLine}` into `{line}` ({ContentHolds.Said(makeErr)}): it is asked again at the next look.");

        var (moved, _, moveErr) = await WorkingTree.GitAsync(root, ["update-ref", $"refs/heads/{line}", commit.Trim(), tip], ct).ConfigureAwait(false);
        return moved == 0
            ? new(TrainBaseKinds.Merged, $"`{baseLine}`'s tip is merged into `{line}`.")
            : new(TrainBaseKinds.Unread, $"`{line}` moved while `{baseLine}` was merged into it ({ContentHolds.Said(moveErr)}): it is asked again at the next look.");
    }
}
