namespace Daoris.Driver;

/// <summary>One check of an operation at the head's tip: the merge commits in the replayed range and every member path accounted.</summary>
/// <param name="Merges">The merge commits from the line's tip to the head's: a landing's replay drops a merge's resolution.</param>
/// <param name="Unread">Git could not say something the accounting needed: nothing is proved.</param>
internal sealed record TogetherCheck(IReadOnlyList<string> Merges, IReadOnlyList<TogetherPath> Paths, bool Unread)
{
    /// <summary>What fails it: unread, a merge, a missing change, then an unproven one; null where every change is accounted.</summary>
    public string? Failed =>
        Unread ? TogetherChecks.NotRead
        : Merges.Count > 0 ? TogetherChecks.Merge
        : Paths.Any(each => each.How == TogetherAccounts.Missing) ? TogetherAccounts.Missing
        : Paths.Any(each => !TogetherAccounts.Proves(each.How)) ? TogetherAccounts.Unproven
        : null;
}

/// <summary>
/// <i>Checking</i> (LINESTOGETHER1d, the review-flow design §4): every member change accounted from the head's tip, never the agent's
/// claim nor ancestry. A commit the tip holds by content or patch (<see cref="LineHolds.JudgeAsync"/>) is held. Each other is tried alone
/// onto the tip, as <see cref="LineHolds.TrialTreeAsync"/> tries a step but past a conflict, git writing a tree even then, so a conflict
/// hides no later clean change: each path it changes is contained, settled by the line's rules, missing or unproven. An unproven path
/// is superseded only where a head commit since the press changed it and names the commit in a <c>Supersedes:</c> trailer.
/// </summary>
/// <remarks>Objects only: it writes no ref, no index and no tree on disk.</remarks>
internal static class TogetherChecks
{
    public const string Merge = "merge";
    public const string NotRead = "not-read";

    /// <param name="lineRules">The line rules accepted for integration, whose files are settled by rule.</param>
    public static async Task<TogetherCheck> AccountAsync(
        string root, TogetherOperation operation, string headTip, string lineTip, IReadOnlyList<IntegrationMergeRule>? lineRules,
        CancellationToken ct)
    {
        var (mergesCode, mergesOut, _) = await GitAsync(root, ["rev-list", "--merges", $"{lineTip}..{headTip}"], ct).ConfigureAwait(false);
        if (mergesCode != 0) return new([], [], true);
        var merges = Lines(mergesOut);
        if (await SupersedingAsync(root, operation.Head.PressTip, headTip, ct).ConfigureAwait(false) is not { } superseding) return new(merges, [], true);

        BacklogMerge.Declared? declared = null;
        var paths = new List<TogetherPath>();
        foreach (var member in operation.Members)
        {
            if (await LineHolds.JudgeAsync(root, member.Tip, headTip, null, ct).ConfigureAwait(false) is not { } judged) return new(merges, paths, true);
            paths.AddRange(judged.LeftOut.Select(held => new TogetherPath(member.Session, held.Sha, "", TogetherAccounts.Held)));
            foreach (var commit in judged.Kept)
            {
                if (await TryAloneAsync(root, headTip, commit.Sha, ct).ConfigureAwait(false) is not { } tried) return new(merges, paths, true);
                foreach (var path in tried.Changed)
                {
                    var settles = !tried.Differs.Contains(path) && !tried.Conflicts.Contains(path) ? false
                        : await SettledByRuleAsync(root, path, headTip, lineRules, declared ??= await BacklogMerge.DeclaredTablesAsync(root, headTip, ct)
                            .ConfigureAwait(false), ct).ConfigureAwait(false);
                    var how = tried.Conflicts.Contains(path)
                        ? settles ? TogetherAccounts.Settled
                        : Supersedes(superseding, commit.Sha, path) ? TogetherAccounts.Superseded : TogetherAccounts.Unproven
                        : !tried.Differs.Contains(path) ? TogetherAccounts.Contained
                        : settles ? TogetherAccounts.Settled : TogetherAccounts.Missing;
                    paths.Add(new TogetherPath(member.Session, commit.Sha, path, how));
                }
            }
        }

        return new(merges, paths, false);
    }

    private sealed record Tried(IReadOnlyList<string> Changed, IReadOnlySet<string> Conflicts, IReadOnlySet<string> Differs);

    /// <summary>
    /// <paramref name="commit"/> merged alone onto <paramref name="tip"/> with its parent as the base: the paths it changes, those in
    /// conflict, and those whose merged copy differs from the tip's. Null where git cannot say.
    /// </summary>
    private static async Task<Tried?> TryAloneAsync(string root, string tip, string commit, CancellationToken ct)
    {
        var (changedCode, changedOut, _) = await GitAsync(
            root, ["-c", "core.quotePath=false", "diff-tree", "--no-commit-id", "--name-only", "-r", $"{commit}^", commit], ct).ConfigureAwait(false);
        if (changedCode != 0) return null;
        var changed = Lines(changedOut);
        var (code, output, _) = await GitAsync(
            root, ["-c", "core.quotePath=false", "merge-tree", "--write-tree", "--name-only", "--messages", $"--merge-base={commit}^", tip, commit], ct)
            .ConfigureAwait(false);
        var lines = output.Split('\n', StringSplitOptions.TrimEntries);
        if (code is not (0 or 1) || lines.Length == 0 || lines[0].Length == 0) return null;
        var conflicts = code == 0 ? [] : lines.Skip(1).TakeWhile(each => each.Length > 0).ToHashSet(StringComparer.Ordinal);
        if (changed.Count == 0) return new(changed, conflicts, new HashSet<string>());
        var (diffCode, diffOut, _) = await GitAsync(
            root, ["-c", "core.quotePath=false", "diff-tree", "--name-only", "-r", tip, lines[0], "--", .. changed], ct).ConfigureAwait(false);
        return diffCode != 0 ? null : new(changed, conflicts, Lines(diffOut).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// The head's commits since the press, each with the commits its <c>Supersedes:</c> trailers name and the paths it changed; null
    /// where git cannot list them.
    /// </summary>
    private static async Task<IReadOnlyList<(IReadOnlyList<string> Names, IReadOnlySet<string> Paths)>?> SupersedingAsync(
        string root, string pressTip, string headTip, CancellationToken ct)
    {
        var (code, output, _) = await GitAsync(
            root,
            ["-c", "core.quotePath=false", "log", "--no-merges", "--format=%x1e%(trailers:key=Supersedes,valueonly,separator=%x1f)%x1d", "--name-only",
                $"{pressTip}..{headTip}"],
            ct).ConfigureAwait(false);
        if (code != 0) return null;
        return [.. output.Split('\u001e', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Split('\u001d', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => ((IReadOnlyList<string>)[.. parts[0].Split('\u001f', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(name => name.Length >= 7)],
                (IReadOnlySet<string>)Lines(parts[1]).ToHashSet(StringComparer.Ordinal)))
            .Where(each => each.Item1.Count > 0)];
    }

    private static bool Supersedes(IReadOnlyList<(IReadOnlyList<string> Names, IReadOnlySet<string> Paths)> superseding, string commit, string path) =>
        superseding.Any(each => each.Paths.Contains(path)
            && each.Names.Any(name => commit.StartsWith(name, StringComparison.OrdinalIgnoreCase)));

    /// <summary>A path the line's rules settle: a file marked generated, the declared backlog, a row table or an accepted line rule's.</summary>
    private static async Task<bool> SettledByRuleAsync(
        string root, string path, string tip, IReadOnlyList<IntegrationMergeRule>? lineRules, BacklogMerge.Declared declared, CancellationToken ct) =>
        (declared with { Lines = lineRules ?? [] }).Settles(path)
        || await GeneratedPaths.AllAsync(root, [path], ct, source: tip).ConfigureAwait(false) is true;

    private static IReadOnlyList<string> Lines(string text) =>
        [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static Task<(int Code, string Stdout, string Stderr)> GitAsync(string root, string[] args, CancellationToken ct) =>
        WorkingTree.GitAsync(root, ["--no-optional-locks", .. args], ct);
}
