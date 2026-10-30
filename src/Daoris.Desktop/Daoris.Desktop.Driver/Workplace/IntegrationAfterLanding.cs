using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A commit made on a unit's branch after its request froze, as the settle judges it against the promoted line (INTEGRATE1e part e2; the
/// integration design §5.7, §5.2 step 9).
/// </summary>
/// <param name="Disposition">One of <see cref="AfterLandingDispositions"/>.</param>
public sealed record AfterLandingCommit(string Sha, string Subject, string Disposition)
{
    /// <summary>For a contained commit, why (<see cref="LeftOutWhy"/>: content, patch, or an empty replay); null otherwise.</summary>
    public string? Why { get; init; }

    /// <summary>For a superseded commit, the line's commit outside the unit's work that changed the line's side of its conflict; null otherwise.</summary>
    public string? By { get; init; }

    /// <summary>The files its replay conflicts in, for a superseded commit and for a new one that conflicts; none otherwise.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];
}

/// <summary>The three judgements of §5.7.</summary>
public static class AfterLandingDispositions
{
    /// <summary>The line holds it by content or patch, or its replay is empty: said, and never offered again.</summary>
    public const string Contained = "contained";

    /// <summary>
    /// Its replay conflicts, and in every conflicted hunk the line's side was changed after the frozen base by a commit outside the unit's work:
    /// kept on a recovery ref and offered as superseded or rebase it, visible until one is answered (row e4's settle).
    /// </summary>
    public const string Superseded = "superseded";

    /// <summary>Anything else: offered by §4.6's rule for its request.</summary>
    public const string New = "new";
}

/// <summary>
/// The settle's judgement of a branch's commits after its frozen tip (§5.7): judged against the promoted line by patch id and content (D87
/// point 1, <see cref="LineHolds"/>), and by a replay over trees alone, never by the session branch's ancestry, since a replayed member's
/// merge holds replayed commits (F27). Reads git and writes objects only, never a ref or a tree on disk. The settle
/// calls it.
/// </summary>
/// <remarks>
/// <para><b>Superseded, hunk by hunk.</b> A conflicted file's hunks are where the commit's change and the line's change since the commit's
/// parent meet, touching lines included, as git's merge counts a conflict. The line's side of each is read by <c>git blame</c> on the
/// promoted line: the hunk is superseded where a line of it was last changed by a commit after the frozen base that is not the unit's own
/// landed work. The commit is superseded only where every such hunk is, by the first such commit found. A hunk whose line's side is a
/// removal, which blame cannot read, and a file git cannot diff as text, are read by the file: some commit outside the unit's work after the
/// base changed it.</para>
/// <para><b>In order.</b> Each commit is replayed onto what the ones before it made, as the settle would offer them, so a contained or
/// superseded one is left out of what the next is replayed onto.</para>
/// </remarks>
public static class IntegrationAfterLanding
{
    private static readonly Regex Hunk = new(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@", RegexOptions.CultureInvariant);

    private static readonly Regex Blamed = new("^([0-9a-f]{40}) \\d+ \\d+", RegexOptions.CultureInvariant);

    /// <summary>
    /// Judge each commit in <paramref name="frozenTip"/>..<paramref name="branchTip"/>, oldest first, merges aside, against
    /// <paramref name="promoted"/>; null where git could not say for one of them, which is asked again rather than guessed.
    /// </summary>
    /// <param name="line">The line's name, for the judgement by content.</param>
    /// <param name="frozenBase">The line's commit the unit's request was judged on: what the line changed after it is the line's side.</param>
    /// <param name="own">The line's commits that are the unit's own landed work (its replayed commits and its merge), which supersede nothing.</param>
    public static async Task<IReadOnlyList<AfterLandingCommit>?> JudgeAsync(
        string root, string line, string promoted, string frozenBase, string frozenTip, string branchTip, IReadOnlyCollection<string> own,
        CancellationToken ct = default)
    {
        if (await LineHolds.CommitsAsync(root, $"{frozenTip}..{branchTip}", ct).ConfigureAwait(false) is not { } commits) return null;
        if (commits.Count == 0) return [];
        if (await LineHolds.JudgeAsync(root, branchTip, promoted, line, ct).ConfigureAwait(false) is not { } held) return null;
        var leftOut = held.LeftOut.ToDictionary(each => each.Sha, StringComparer.OrdinalIgnoreCase);
        var (treeCode, treeOut, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{promoted}^{{tree}}"], ct).ConfigureAwait(false);
        if (treeCode != 0) return null;

        var current = treeOut.Trim();
        IReadOnlySet<string>? outside = null;
        var judged = new List<AfterLandingCommit>();
        foreach (var commit in commits)
        {
            if (leftOut.TryGetValue(commit.Sha, out var left))
            {
                judged.Add(new AfterLandingCommit(commit.Sha, commit.Subject, AfterLandingDispositions.Contained) { Why = left.Why });
                continue;
            }

            var (tree, conflicts) = await LineHolds.TrialTreeAsync(root, current, [commit], ct).ConfigureAwait(false);
            if (tree is not null)
            {
                var empty = string.Equals(tree, current, StringComparison.OrdinalIgnoreCase);
                judged.Add(empty
                    ? new AfterLandingCommit(commit.Sha, commit.Subject, AfterLandingDispositions.Contained) { Why = LeftOutWhy.Empty }
                    : new AfterLandingCommit(commit.Sha, commit.Subject, AfterLandingDispositions.New));
                current = tree;
                continue;
            }

            if (conflicts is null) return null;
            if (await ConflictedAsync(root, current, commit.Sha, ct).ConfigureAwait(false) is not { Count: > 0 } files) return null;
            outside ??= await OutsideAsync(root, frozenBase, promoted, own, ct).ConfigureAwait(false);
            if (outside is null) return null;
            var (by, unread) = await SupersededByAsync(root, promoted, commit.Sha, files, outside, frozenBase, ct).ConfigureAwait(false);
            if (unread) return null;
            judged.Add(by is not null
                ? new AfterLandingCommit(commit.Sha, commit.Subject, AfterLandingDispositions.Superseded) { By = by, Files = files }
                : new AfterLandingCommit(commit.Sha, commit.Subject, AfterLandingDispositions.New) { Files = files });
        }

        return judged;
    }

    /// <summary>The files replaying <paramref name="commit"/> onto <paramref name="onto"/> conflicts in, as git's merge over trees names them; null where git could not say.</summary>
    private static async Task<IReadOnlyList<string>?> ConflictedAsync(string root, string onto, string commit, CancellationToken ct)
    {
        var (code, output, _) = await WorkingTree.GitAsync(
            root, ["-c", "core.quotePath=false", "merge-tree", "--write-tree", "--name-only", "--no-messages", $"--merge-base={commit}^", onto, commit], ct)
            .ConfigureAwait(false);
        if (code != 1) return null;
        return [.. output.Split('\n', StringSplitOptions.TrimEntries).Skip(1).TakeWhile(each => each.Length > 0).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The line's commits after <paramref name="frozenBase"/> up to <paramref name="promoted"/> that are not the unit's own work; null where git could not list them.</summary>
    private static async Task<IReadOnlySet<string>?> OutsideAsync(string root, string frozenBase, string promoted, IReadOnlyCollection<string> own, CancellationToken ct)
    {
        var (code, output, _) = await WorkingTree.GitAsync(root, ["rev-list", $"{frozenBase}..{promoted}"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        var mine = own.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(each => !mine.Contains(each))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The line's commit outside the unit's work that changed the line's side of every conflicted hunk of <paramref name="commit"/>'s replay,
    /// the first found; none where some conflicted hunk's line side is the unit's own or older than the base, or no hunk meets the line's change.
    /// </summary>
    private static async Task<(string? By, bool Unread)> SupersededByAsync(
        string root, string promoted, string commit, IReadOnlyList<string> files, IReadOnlySet<string> outside, string frozenBase, CancellationToken ct)
    {
        string? by = null;
        foreach (var file in files)
        {
            var lineSide = await HunksAsync(root, $"{commit}^", promoted, file, ct).ConfigureAwait(false);
            var theirs = await HunksAsync(root, $"{commit}^", commit, file, ct).ConfigureAwait(false);
            if (lineSide is null || theirs is null || lineSide.Count == 0 || theirs.Count == 0)
            {
                // Not text git can diff by lines (a binary, a file one side removed or renamed): read by the file.
                var (touched, unread) = await TouchedByAsync(root, frozenBase, promoted, file, outside, ct).ConfigureAwait(false);
                if (unread) return (null, true);
                if (touched is null) return (null, false);
                by ??= touched;
                continue;
            }

            var met = lineSide.Where(ours => theirs.Any(change => Meets(ours, change))).ToList();
            if (met.Count == 0) return (null, false);
            foreach (var hunk in met)
            {
                string? changed;
                if (hunk.NewCount == 0)
                {
                    var (touched, unread) = await TouchedByAsync(root, frozenBase, promoted, file, outside, ct).ConfigureAwait(false);
                    if (unread) return (null, true);
                    changed = touched;
                }
                else
                {
                    var (blamed, unread) = await BlameAsync(root, promoted, file, hunk.NewStart, hunk.NewCount, ct).ConfigureAwait(false);
                    if (unread) return (null, true);
                    changed = blamed!.FirstOrDefault(outside.Contains);
                }

                if (changed is null) return (null, false);
                by ??= changed;
            }
        }

        return (by, false);
    }

    /// <summary>One hunk of <c>git diff -U0</c>: its lines in the old file and in the new.</summary>
    private sealed record DiffHunk(int OldStart, int OldCount, int NewStart, int NewCount)
    {
        /// <summary>The boundaries between the old file's lines it spans: lines a..a+b-1 span [a-1, a+b-1], an insertion after line a spans [a, a].</summary>
        public (int From, int To) Span => OldCount == 0 ? (OldStart, OldStart) : (OldStart - 1, OldStart + OldCount - 1);
    }

    /// <summary>Whether two hunks over one old file meet: overlap or touch, as git's merge counts a conflict.</summary>
    private static bool Meets(DiffHunk a, DiffHunk b) => Math.Max(a.Span.From, b.Span.From) <= Math.Min(a.Span.To, b.Span.To);

    /// <summary>The hunks from <paramref name="from"/> to <paramref name="to"/> in one file, none where it is the same; null where git could not diff it as text.</summary>
    private static async Task<IReadOnlyList<DiffHunk>?> HunksAsync(string root, string from, string to, string file, CancellationToken ct)
    {
        var (code, output, _) = await WorkingTree.GitAsync(
            root, ["--no-optional-locks", "-c", "core.quotePath=false", "diff", "-U0", "--no-color", "--no-ext-diff", "--no-renames", "--no-textconv", from, to, "--", file], ct)
            .ConfigureAwait(false);
        if (code != 0) return null;
        var lines = output.Split('\n');
        if (lines.Any(each => each.StartsWith("Binary files ", StringComparison.Ordinal))) return null;
        if (lines.Any(each => each.StartsWith("new file mode", StringComparison.Ordinal) || each.StartsWith("deleted file mode", StringComparison.Ordinal))) return null;
        var hunks = new List<DiffHunk>();
        foreach (var line in lines)
        {
            if (Hunk.Match(line) is not { Success: true } match) continue;
            static int Count(Group group) => group.Success ? int.Parse(group.Value, System.Globalization.CultureInfo.InvariantCulture) : 1;
            hunks.Add(new DiffHunk(
                int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), Count(match.Groups[2]),
                int.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture), Count(match.Groups[4])));
        }

        return hunks;
    }

    /// <summary>The commits that last changed lines <paramref name="start"/> to <paramref name="start"/>+<paramref name="count"/>-1 of a file at <paramref name="at"/>.</summary>
    private static async Task<(IReadOnlyList<string>? Commits, bool Unread)> BlameAsync(string root, string at, string file, int start, int count, CancellationToken ct)
    {
        var (code, output, _) = await WorkingTree.GitAsync(root, ["blame", "--porcelain", "-L", $"{start},+{count}", at, "--", file], ct).ConfigureAwait(false);
        if (code != 0) return (null, true);
        return ([.. output.Split('\n').Select(each => Blamed.Match(each)).Where(match => match.Success).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal)], false);
    }

    /// <summary>The newest commit outside the unit's work after the base that changed <paramref name="file"/>; none where none did.</summary>
    private static async Task<(string? By, bool Unread)> TouchedByAsync(
        string root, string frozenBase, string promoted, string file, IReadOnlySet<string> outside, CancellationToken ct)
    {
        var (code, output, _) = await WorkingTree.GitAsync(root, ["log", "--format=%H", $"{frozenBase}..{promoted}", "--", file], ct).ConfigureAwait(false);
        if (code != 0) return (null, true);
        return (output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(outside.Contains), false);
    }
}
