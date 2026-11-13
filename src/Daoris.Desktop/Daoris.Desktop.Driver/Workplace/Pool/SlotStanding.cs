namespace Daoris.Driver;

/// <summary>
/// The row a settling claim leaves its slot with, read from the slot's disk and never from a phase or the row before it: the claim's line
/// where the slot is on the claim's branch and the claim has made it that line's, the line the slot served where it stands where the claim
/// found it, and no line otherwise, so no reader takes the slot's folder for a line its files are not. A slot settled <c>stuck</c> is first
/// detached where it stands, which writes no file, so no slot out of the pool keeps a branch checked out; only git's own lock file stops
/// that, and the words then name the branch it holds.
/// </summary>
internal sealed record SlotStanding(KeptTree Slot, TreeClaim Claim)
{
    /// <summary>The claim has made the slot its line's: an open that landed, a switching return from its clearing on, a claim in place.</summary>
    internal bool Made { get; init; }

    internal string State { get; init; } = KeptStates.Kept;

    internal string Said { get; init; } = "";

    internal long Bytes { get; init; } = Slot.Bytes;

    internal string Toolchain { get; init; } = Slot.Toolchain;

    /// <summary>The slot's row as its disk shows it now.</summary>
    internal async Task<SlotSettled> ReadAsync(CancellationToken ct)
    {
        var branch = await WorkingTree.BranchAsync(Slot.Path, ct).ConfigureAwait(false) ?? "";
        var head = await WorkingTree.HeadAsync(Slot.Path, ct).ConfigureAwait(false) ?? "";
        var line = LineOf(Claim, Made, branch, head);
        return new SlotSettled(Slot.Registration, Slot.Path, line, line.Length > 0 ? "" : head, State, Said, Bytes, Toolchain);
    }

    /// <summary>The row of the slot settled <c>stuck</c> with <paramref name="why"/>, detached where it stands first unless git's lock file stands.</summary>
    internal async Task<SlotSettled> StuckAsync(PoolSite site, PoolGit git, string why, CancellationToken ct)
    {
        var said = why;
        if (await WorkingTree.BranchAsync(Slot.Path, ct).ConfigureAwait(false) is { Length: > 0 } branch)
        {
            if (GitLockFile(Slot.Path) is not null)
            {
                said = $"{why}; it holds {branch} checked out";
            }
            else
            {
                var detached = await git.Repository.SwitchAsync(Slot.Path, new GitSwitch(null, HooksPath: TreeOpenings.HooksPath(site.Home)), ct).ConfigureAwait(false);
                if (!detached.Ok) said = $"{why}; git would not detach it, so it holds {branch} checked out: {TreeGit.FirstLine(detached.Error)}";
            }
        }

        return await (this with { State = KeptStates.Stuck, Said = said }).ReadAsync(ct).ConfigureAwait(false);
    }

    /// <summary>The line a slot on <paramref name="branch"/> at <paramref name="head"/> serves, as the claim leaves it.</summary>
    internal static string LineOf(TreeClaim claim, bool made, string branch, string head) =>
        made && claim.Line.Length > 0 && branch == claim.Branch ? claim.Line
        : claim.ExpectLine.Length > 0 && head.Length > 0 && head == claim.Expect && branch == claim.ExpectBranch ? claim.ExpectLine
        : "";

    /// <summary>The lock file git leaves where a process of it stopped mid-write, or null; while it stands no git writes the slot.</summary>
    internal static string? GitLockFile(string slot) =>
        GitBranches.GitDirectory(slot) is { } git ? new[] { "index.lock", "HEAD.lock" }.FirstOrDefault(name => File.Exists(Path.Combine(git, name))) : null;
}
