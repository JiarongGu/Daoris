namespace Daoris.Driver;

/// <summary>How a recovery ref's work stands to the repository's line.</summary>
public enum RecoveryHolding
{
    /// <summary>Every commit of the ref is on the line by patch (<c>git cherry</c>), or the ref is on the line already.</summary>
    ByPatch,

    /// <summary>Some commit is not on the line by patch, but every file the ref changed holds on the line what the ref holds, or the line has moved past it.</summary>
    ByContent,

    /// <summary>The ref holds work the line lacks.</summary>
    Lacks,

    /// <summary>Git did not answer, so nothing is known of the ref's work.</summary>
    Unread,
}

/// <summary>
/// What one recovery ref's work is to the line: <see cref="Commits"/> are the commits the line does not hold by ancestry, and
/// <see cref="Files"/> (for <see cref="RecoveryHolding.Lacks"/>) the files whose change the line does not hold.
/// </summary>
public sealed record RecoveryWork(string Ref, string Line, RecoveryHolding Holding, int Commits, int Files, string? Unread = null)
{
    /// <summary>Whether the line holds the ref's work, by patch or by content: the only work a clean-up may drop unasked.</summary>
    public bool OnLine => Holding is RecoveryHolding.ByPatch or RecoveryHolding.ByContent;

    /// <summary>The ref's work to the line, in a sentence both the plan and the kept list say.</summary>
    public string Words => Holding switch
    {
        RecoveryHolding.ByPatch => Commits == 0
            ? $"its commits are all on `{Line}`"
            : $"every commit is on `{Line}` by patch",
        RecoveryHolding.ByContent => $"every file it changed holds on `{Line}` what the ref holds, or `{Line}` has moved past it",
        RecoveryHolding.Lacks => $"it holds work `{Line}` lacks: {Count(Commits, "commit")} and {Count(Files, "file")}",
        _ => $"git could not compare it with `{Line}` ({Unread}), so its work is not known",
    };

    internal static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";
}

/// <summary>
/// **What a recovery ref's work is to the repository's line**: the census beside each unheld ref, so the person tells a ref whose
/// work reached the line from one that holds the only copy. By patch first (<c>git cherry</c> against the line), else by content: the ref's
/// tip against the line, per file the ref changed since their merge base, a file held when the line carries the ref's content or has changed
/// the file itself since (moved past it). It reads and changes nothing.
/// </summary>
public static class RecoveryWorks
{
    /// <summary>
    /// The repository's line in this checkout: <paramref name="set"/> where the person set one (<see cref="CanonicalLine.Choose"/>), else
    /// the conventional <c>main</c>, else <c>master</c>. Never a remote's <c>HEAD</c>, which may point at a feature branch. Null where
    /// the branch is no branch or remote-tracking branch here.
    /// </summary>
    public static async Task<string?> LineAsync(string root, string? set, CancellationToken ct)
    {
        foreach (var candidate in set is { Length: > 0 } ? [set] : ReviewChoices.Conventional)
        {
            if (await TipAsync(root, candidate, ct).ConfigureAwait(false) is not null) return candidate;
        }

        return null;
    }

    /// <summary>The commit <paramref name="line"/> names here: the local branch, else origin's; null where neither exists.</summary>
    private static async Task<string?> TipAsync(string root, string line, CancellationToken ct)
    {
        foreach (var name in new[] { $"refs/heads/{line}", $"refs/remotes/origin/{line}" })
        {
            var (code, tip, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{name}^{{commit}}"], ct).ConfigureAwait(false);
            if (code == 0 && WorkingTree.IsCommitId(tip.Trim())) return tip.Trim();
        }

        return null;
    }

    /// <summary>What <paramref name="reference"/>, a recovery ref's (or its commit's) work, is to <paramref name="line"/>.</summary>
    public static async Task<RecoveryWork> ReadAsync(string root, string line, string reference, CancellationToken ct)
    {
        RecoveryWork Unread(string why) => new(reference, line, RecoveryHolding.Unread, 0, 0, why);
        if (await TipAsync(root, line, ct).ConfigureAwait(false) is not { } tip) return Unread($"`{line}` names no commit here");

        var (code, cherry, err) = await WorkingTree.GitAsync(root, ["cherry", tip, reference], ct).ConfigureAwait(false);
        if (code != 0) return Unread(ContentHolds.Said(err));
        var (counted, ahead, aheadErr) = await WorkingTree.GitAsync(root, ["rev-list", "--count", $"{tip}..{reference}"], ct).ConfigureAwait(false);
        if (counted != 0 || !int.TryParse(ahead.Trim(), out var commits)) return Unread(ContentHolds.Said(aheadErr));

        var missing = cherry.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Count(row => row.StartsWith('+'));
        if (missing == 0) return new(reference, line, RecoveryHolding.ByPatch, commits, 0);

        var (based, bases, _) = await WorkingTree.GitAsync(root, ["merge-base", tip, reference], ct).ConfigureAwait(false);
        var origin = based == 0 && WorkingTree.IsCommitId(bases.Trim()) ? bases.Trim() : null;
        // With no common ancestor every file the ref holds is its work, and the line has changed none of them since.
        var changed = origin is null
            ? await ListAsync(root, ["ls-tree", "-r", "--name-only", "-z", reference], ct).ConfigureAwait(false)
            : await ListAsync(root, ["--no-optional-locks", "diff", "--name-only", "-z", "--no-renames", "--no-ext-diff", origin, reference], ct).ConfigureAwait(false);
        if (changed is null) return Unread("git could not list the files it changed");
        var moved = origin is null
            ? new HashSet<string>()
            : await ListAsync(root, ["--no-optional-locks", "diff", "--name-only", "-z", "--no-renames", "--no-ext-diff", origin, tip], ct).ConfigureAwait(false);
        if (moved is null) return Unread("git could not list the files the line changed");

        // A file the line changed since the base either carries the ref's content or has moved past it; one it left (or put back) as the
        // base had it does not hold the ref's change.
        var lacking = changed.Count(each => !moved.Contains(each));
        return lacking == 0
            ? new(reference, line, RecoveryHolding.ByContent, commits, 0)
            : new(reference, line, RecoveryHolding.Lacks, missing, lacking);
    }

    /// <summary>The NUL-separated paths git answers to <paramref name="arguments"/>; null where git did not answer.</summary>
    private static async Task<HashSet<string>?> ListAsync(string root, string[] arguments, CancellationToken ct)
    {
        var (code, raw, _) = await WorkingTree.GitAsync(root, arguments, ct).ConfigureAwait(false);
        return code == 0 ? raw.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal) : null;
    }

    /// <summary>The plan's age, in days, of a ref dated <paramref name="dated"/>; null where it has no date.</summary>
    internal static string? Age(DateTimeOffset? dated, DateTimeOffset now) => dated is { } at
        ? $"{Math.Max(0, (int)(now - at).TotalDays)} days old"
        : null;
}
