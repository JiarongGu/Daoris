namespace Daoris.Driver;

/// <summary>
/// A set-aside folder's class (KEEPSIZE1b, the keep-size design §3.4), read from the manifest beside it: what every removal door reads, the
/// hour's purge, the days' purge and the press (and the allowance, KEEPSIZE1d).
/// </summary>
public static class SetAsideClass
{
    /// <summary>A finished tree at <c>recorded</c>: its branch deleted, its record kept. Goes an hour after its record, its holding check passing then.</summary>
    public const string Recorded = "recorded";

    /// <summary>A finished tree at <c>deleted</c>: its branch deleted, its record not yet kept. Waits for its record, past its days.</summary>
    public const string Awaiting = "awaiting";

    /// <summary>A finished tree at <c>judged</c> or <c>moved</c>: stopped between steps. Resumed, else its days.</summary>
    public const string Stopped = "stopped";

    /// <summary>A finished tree at <c>refused</c>: the move done, the branch's delete refused, the branch standing. Resumed, else its days.</summary>
    public const string Refused = "refused";

    /// <summary>
    /// A manifest beside it that does not read, or of a schema this build does not know (Codex's read of the design, finding 3): what it is
    /// cannot be told, so no rule takes it; it waits, said, for a build or a hand that can read it, and the press lists it.
    /// </summary>
    public const string Unread = "unread";

    /// <summary>
    /// No manifest, one of schema 1, or a finished one whose holding check said for certain that nothing holds its tip: a stray, a pass's
    /// leftover, a folder from before this build, or one whose ref was dropped by hand: its days, as built.
    /// </summary>
    public const string Unmarked = "unmarked";
}

/// <summary>
/// One judgement of a set-aside folder (KEEPSIZE1b, the keep-size design §3.4): its class, the moment a write after which keeps it, and when
/// a rule may take it. Read and never written. <see cref="Due"/> is a schedule, never an authorization: a recorded folder's hour is taken only
/// on a holding check run right before its rename (<see cref="HoldingAsync"/>, Codex's read of the branch, finding 1).
/// </summary>
/// <param name="Threshold">The manifest's <c>read</c> for a finished tree (§3.5's race), else the moment in the folder's name.</param>
/// <param name="Due">When a rule takes it: an hour after <c>recorded</c>, else its days after the move; null where it waits for its record, or cannot be read.</param>
/// <param name="Hour">It goes by the hour's rule, not its days, so its holding check runs right before its removal.</param>
internal sealed record SetAsideEligibility(string Class, DateTimeOffset Threshold, DateTimeOffset? Due, bool Hour, SlimManifest? Manifest)
{
    /// <summary>The holding check's words where the branch stands: the one the press lists a stopped or refused folder by as standing.</summary>
    internal const string BranchStands = "its branch stands";

    /// <summary>
    /// Why a folder waits where the registry read and names no checkout of its repository: a recorded one's check cannot run, an awaiting one's
    /// commits cannot be read, so neither goes nor is posted, and the card counts it with this reason, never as next (the last fixes' check,
    /// findings 2 and 7).
    /// </summary>
    internal const string NoCheckout = "the registry names no checkout of its repository on this machine";

    /// <summary>The folder a removal renames its folder into first (§3.4), under <c>.tidied</c>: every other reader passes over it.</summary>
    internal const string Going = ".going";

    /// <summary>The workspaces' folders under one place's <c>.tidied</c>, <see cref="Going"/> passed over.</summary>
    internal static IEnumerable<string> Workspaces(string root) =>
        Directory.EnumerateDirectories(root).Where(folder => !string.Equals(Path.GetFileName(folder), Going, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// <paramref name="folder"/>, moved aside at <paramref name="moved"/>, judged from its manifest under the policy's days and
    /// <c>whole</c>. A <c>recorded</c> folder under <c>whole</c>, set at the move or now, keeps its days.
    /// </summary>
    internal static SetAsideEligibility Of(string folder, DateTimeOffset moved, TimeSpan days, bool whole)
    {
        var (manifest, problem) = SlimManifest.Read(SlimManifest.PathFor(folder));
        // Codex's read of the design, finding 3: a manifest that is there and does not read is never a known legacy folder.
        if (manifest is null && problem is not null) return new(SetAsideClass.Unread, moved, null, false, null);
        if (manifest?.Finished is not { } finished)
        {
            return new(SetAsideClass.Unmarked, moved, moved + days, false, manifest);
        }

        return manifest.State switch
        {
            SlimStates.Recorded when manifest.Recorded is { } recorded && !whole && !finished.Whole =>
                new(SetAsideClass.Recorded, finished.Read, recorded + KeepKinds.SetAsideSettle, true, manifest),
            SlimStates.Recorded => new(SetAsideClass.Recorded, finished.Read, moved + days, false, manifest),
            SlimStates.Deleted => new(SetAsideClass.Awaiting, finished.Read, null, false, manifest),
            SlimStates.Judged or SlimStates.Moved => new(SetAsideClass.Stopped, finished.Read, moved + days, false, manifest),
            SlimStates.Refused => new(SetAsideClass.Refused, finished.Read, moved + days, false, manifest),
            _ => new(SetAsideClass.Unmarked, finished.Read, moved + days, false, manifest),
        };
    }

    /// <summary>
    /// The holding check (§3.4), read-only and outside every hold, in the repository's root the registry gives: the branch is absent by git's
    /// own word, and the tip is an ancestor of the manifest's ref, or of <c>refs/heads/&lt;line&gt;</c> where it names none.
    /// <see cref="Holding.Passed"/>; <see cref="Holding.Failed"/> only where git said so for certain (the branch stands, the holder is missing,
    /// the tip is no ancestor); <see cref="Holding.Unread"/> for a root not given or any answer git does not define, which keeps the folder
    /// as it is and is asked again (Codex's read of the design, finding 3).
    /// </summary>
    internal static async Task<(Holding Result, string? Why)> HoldingAsync(string? root, SlimManifest manifest, CancellationToken ct)
    {
        if (manifest.Finished is not { } finished) return (Holding.Failed, "it has no finished tree's manifest");
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return (Holding.Unread, NoCheckout);
        // Only git's own "missing", show-ref --exists exiting 2, says the branch is absent (git-ref-absence.md); 0 says it stands.
        var (code, _, err) = await WorkingTree.GitAsync(root, ["show-ref", "--exists", $"refs/heads/{manifest.Branch}"], ct).ConfigureAwait(false);
        if (code == 0) return (Holding.Failed, BranchStands);
        if (code != 2) return (Holding.Unread, $"git could not say whether its branch stands: {TreeGit.FirstLine(err)}");
        if ((finished.Ref ?? (finished.Line is { } line ? $"refs/heads/{line}" : null)) is not { } holder) return (Holding.Failed, "nothing names what holds its tip");
        // A holder dropped by hand is git's own "missing", not an answer merge-base defines.
        (code, _, err) = await WorkingTree.GitAsync(root, ["show-ref", "--exists", holder], ct).ConfigureAwait(false);
        if (code == 2) return (Holding.Failed, $"{holder} is gone, so nothing holds its tip");
        if (code != 0) return (Holding.Unread, $"git could not say whether {holder} stands: {TreeGit.FirstLine(err)}");
        (code, _, err) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", finished.Tip, holder], ct).ConfigureAwait(false);
        return code switch
        {
            0 => (Holding.Passed, null),
            1 => (Holding.Failed, $"{holder} no longer holds its tip"),
            _ => (Holding.Unread, $"git could not say whether {holder} holds its tip: {TreeGit.FirstLine(err)}"),
        };
    }
}

/// <summary>What a holding check found (<see cref="SetAsideEligibility.HoldingAsync"/>).</summary>
internal enum Holding
{
    /// <summary>The branch is gone and a ref that stays holds the tip.</summary>
    Passed,

    /// <summary>git said for certain the folder is not held as its manifest says.</summary>
    Failed,

    /// <summary>It could not be asked or answered: kept as it is, and asked again.</summary>
    Unread,
}
