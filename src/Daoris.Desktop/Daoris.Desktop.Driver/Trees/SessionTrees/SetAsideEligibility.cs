namespace Daoris.Driver;

/// <summary>
/// A set-aside folder's class (KEEPSIZE1b, the keep-size design §3.4), read from the manifest beside it: what every removal door reads, the
/// hour's purge, the days' purge and the press (and the allowance, KEEPSIZE1d).
/// </summary>
public static class SetAsideClass
{
    /// <summary>A finished tree at <c>recorded</c>: its branch deleted, its record kept. Goes an hour after its record, the holding check passing.</summary>
    public const string Recorded = "recorded";

    /// <summary>A finished tree at <c>deleted</c>: its branch deleted, its record not yet kept. Waits for its record, past its days.</summary>
    public const string Awaiting = "awaiting";

    /// <summary>A finished tree at <c>judged</c> or <c>moved</c>: stopped between steps. Resumed, else its days.</summary>
    public const string Stopped = "stopped";

    /// <summary>A finished tree at <c>refused</c>: the move done, the branch's delete refused, the branch standing. Resumed, else its days.</summary>
    public const string Refused = "refused";

    /// <summary>No manifest, one of schema 1, one that does not read, or a finished one whose holding check failed: its days, as built.</summary>
    public const string Unmarked = "unmarked";
}

/// <summary>
/// One judgement of a set-aside folder (KEEPSIZE1b, the keep-size design §3.4): its class, the moment a write after which keeps it, and when
/// a rule takes it. Read and never written; the holding check, which reads git, is <see cref="HoldingAsync"/>, asked by the record pass
/// before the purge.
/// </summary>
/// <param name="Threshold">The manifest's <c>read</c> for a finished tree (§3.5's race), else the moment in the folder's name.</param>
/// <param name="Due">When a rule takes it: an hour after <c>recorded</c>, else its days after the move; null where it waits for its record.</param>
/// <param name="Hour">It goes by the hour's rule, not its days.</param>
internal sealed record SetAsideEligibility(string Class, DateTimeOffset Threshold, DateTimeOffset? Due, bool Hour, SlimManifest? Manifest)
{
    /// <summary>The holding check's words where the branch stands: the one the press lists a stopped or refused folder by as standing.</summary>
    internal const string BranchStands = "its branch stands";

    /// <summary>The folder a removal renames its folder into first (§3.4), under <c>.tidied</c>: every other reader passes over it.</summary>
    internal const string Going = ".going";

    /// <summary>The workspaces' folders under one place's <c>.tidied</c>, <see cref="Going"/> passed over.</summary>
    internal static IEnumerable<string> Workspaces(string root) =>
        Directory.EnumerateDirectories(root).Where(folder => !string.Equals(Path.GetFileName(folder), Going, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// <paramref name="folder"/>, moved aside at <paramref name="moved"/>, judged from its manifest under the policy's days and
    /// <c>whole</c>. A <c>recorded</c> folder under <c>whole</c>, set at the move or now, keeps its days.
    /// <para>🔴 A recorded folder's hour is due only where the holding check passed at or after it (<see cref="SlimManifest.Held"/>), and, read
    /// at <paramref name="now"/>, within the last hour (the second maker's read, finding 1): a purge whose record pass did not run, or ran
    /// before the hour, or a press with no service to resolve the repository, takes none. Where it is not, the folder waits, its class kept.</para>
    /// </summary>
    internal static SetAsideEligibility Of(string folder, DateTimeOffset moved, TimeSpan days, bool whole, DateTimeOffset? now = null)
    {
        var manifest = SlimManifest.Read(SlimManifest.PathFor(folder)).Manifest;
        if (manifest?.Finished is not { } finished)
        {
            return new(SetAsideClass.Unmarked, moved, moved + days, false, manifest);
        }

        return manifest.State switch
        {
            SlimStates.Recorded when manifest.Recorded is { } recorded && !whole && !finished.Whole =>
                new(SetAsideClass.Recorded, finished.Read, HeldSince(manifest, recorded + KeepKinds.SetAsideSettle, now), true, manifest),
            SlimStates.Recorded => new(SetAsideClass.Recorded, finished.Read, moved + days, false, manifest),
            SlimStates.Deleted => new(SetAsideClass.Awaiting, finished.Read, null, false, manifest),
            SlimStates.Judged or SlimStates.Moved => new(SetAsideClass.Stopped, finished.Read, moved + days, false, manifest),
            SlimStates.Refused => new(SetAsideClass.Refused, finished.Read, moved + days, false, manifest),
            _ => new(SetAsideClass.Unmarked, finished.Read, moved + days, false, manifest),
        };
    }

    /// <summary>The hour, where a holding check passed at or after it and, read at <paramref name="now"/>, within the last hour; else null.</summary>
    private static DateTimeOffset? HeldSince(SlimManifest manifest, DateTimeOffset hour, DateTimeOffset? now) =>
        manifest.Held is { } held && held >= hour && (now is not { } at || at - held <= KeepKinds.SetAsideSettle) ? hour : null;

    /// <summary>
    /// The holding check (§3.4), read-only and outside every hold, in the repository's root the registry gives: the branch is absent by git's
    /// own word, and the tip is an ancestor of the manifest's ref, or of <c>refs/heads/&lt;line&gt;</c> where it names none. Null where it
    /// passes; else why, in a clause. A root not given or not read fails it, as does any answer git does not define.
    /// </summary>
    internal static async Task<string?> HoldingAsync(string? root, SlimManifest manifest, CancellationToken ct)
    {
        if (manifest.Finished is not { } finished) return "it has no finished tree's manifest";
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return "the registry names no checkout of its repository on this machine";
        // Only git's own "missing", show-ref --exists exiting 2, says the branch is absent (git-ref-absence.md); 0 says it stands.
        var (code, _, err) = await WorkingTree.GitAsync(root, ["show-ref", "--exists", $"refs/heads/{manifest.Branch}"], ct).ConfigureAwait(false);
        if (code == 0) return BranchStands;
        if (code != 2) return $"git could not say whether its branch stands: {TreeGit.FirstLine(err)}";
        if ((finished.Ref ?? (finished.Line is { } line ? $"refs/heads/{line}" : null)) is not { } holder) return "nothing names what holds its tip";
        // A holder dropped by hand is git's own "missing", not an answer merge-base defines.
        (code, _, _) = await WorkingTree.GitAsync(root, ["show-ref", "--exists", holder], ct).ConfigureAwait(false);
        if (code == 2) return $"{holder} is gone, so nothing holds its tip";
        (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", finished.Tip, holder], ct).ConfigureAwait(false);
        return code switch
        {
            0 => null,
            1 => $"{holder} no longer holds its tip",
            _ => $"git could not say whether {holder} holds its tip",
        };
    }
}
