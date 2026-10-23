using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>Where a checkout stands in its repository's history — what a feed's claim is made of.</summary>
/// <param name="Commit">The full SHA of HEAD.</param>
/// <param name="CommittedAt">The committer date, which is what orders two points in one history.</param>
/// <param name="Branch">The line this checkout is on; `(detached)` where it is on none.</param>
public sealed record TreeProvenance(string Commit, DateTimeOffset CommittedAt, string Branch)
{
    /// <summary>The form a person reads in a note.</summary>
    public string ShortCommit => TextBounds.ShortCommit(Commit, 8);
}

/// <summary>How this checkout's commit stands to one a deployment holds, as git answered (SYNC5a).</summary>
public enum TreeRelation
{
    /// <summary>This checkout's commit has the held one in its history: a fast-forward.</summary>
    Descends,

    /// <summary>The held commit has this checkout's in its history: the checkout is behind.</summary>
    Behind,

    /// <summary>Neither is in the other's history — two lines from a common past.</summary>
    Diverged,

    /// <summary>This checkout does not have the held commit, so git cannot say.</summary>
    Unknown,
}

/// <summary>
/// The questions the driver asks a working tree: is it clean enough to spawn into, what landed while
/// a session ran, and — since D48 §6 — where in the repository's history this checkout stands. Asked
/// of git itself, because git's answer is the one that matters.
/// </summary>
/// <remarks>
/// <para>A session spawns only onto a clean tree (D46 §3): uncommitted changes are somebody's work in
/// flight — the lesson `reaching-in` was written from — and a tree that is not a git repository at
/// all cannot make that promise, so it refuses too.</para>
///
/// <para>The provenance half exists because two checkouts of one repository are two points in its
/// history, and a deployment fed by both must decide which one speaks. Only this side can answer it:
/// the deployment has no checkout and cannot ask git anything.</para>
/// </remarks>
public static class WorkingTree
{
    /// <summary>
    /// Where this checkout stands — or null when git cannot say, which is a tree that feeds nothing.
    /// </summary>
    public static async Task<TreeProvenance?> ProvenanceAsync(string root, CancellationToken ct = default)
    {
        // One call for both fields, so the commit and its time can never come from different commits —
        // a session landing work between two calls would otherwise produce a provenance describing
        // neither. %H is the SHA and %cI the committer date; %n separates them, because neither field
        // can contain a newline (unlike a commit message, which is deliberately not asked for here).
        var (code, stdout, _) = await GitAsync(root, ["log", "-1", "--format=%H%n%cI"], ct).ConfigureAwait(false);
        if (code != 0) return null;

        var lines = stdout.Trim().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != 2
            || Moments.Read(lines[1]) is not { } committedAt)
        {
            return null;
        }

        var (branchCode, branchOut, _) = await GitAsync(root, ["rev-parse", "--abbrev-ref", "HEAD"], ct)
            .ConfigureAwait(false);
        var branch = branchCode == 0 ? branchOut.Trim() : "";

        return new(
            lines[0],
            committedAt,
            // A detached HEAD is on no line at all. Named rather than blank, because the deployment's
            // refusal quotes it back — and "fed from ``" is a sentence nobody can act on.
            branch is "" or "HEAD" ? "(detached)" : branch);
    }

    /// <summary>
    /// The branch this checkout stands on, by its short name: what the composer's tree chip says (NEW1e-a). Null where HEAD is
    /// detached, which is no branch at all; a branch with no commit yet answers its name. A git that fails is thrown, because a
    /// tree git cannot read has no head to name, and "detached" would be a claim about it.
    /// </summary>
    public static async Task<string?> BranchAsync(string root, CancellationToken ct = default)
    {
        // `-q` makes a detached HEAD exit 1 with nothing said, which is told apart from a real failure by its silence.
        var (code, stdout, stderr) = await GitAsync(root, ["symbolic-ref", "--short", "-q", "HEAD"], ct).ConfigureAwait(false);
        if (code == 0 && stdout.Trim() is { Length: > 0 } branch) return branch;
        if (code == 1 && string.IsNullOrWhiteSpace(stderr)) return null;
        throw new DriverException($"git could not name this checkout's branch: {stderr.Trim()}");
    }

    /// <summary>
    /// Which line this repository treats as canonical, as far as this checkout knows (D48 §6).
    /// </summary>
    /// <remarks>
    /// <para>Asked of the remote's own HEAD first, which is what a clone records and therefore the
    /// repository's own answer rather than this machine's guess. A checkout made with
    /// `git remote add` has no such ref, so the conventional names are tried in turn.</para>
    ///
    /// <para>Null when none of them exists — and null is PERMISSIVE at the deployment: a repository
    /// that never said which line is canonical has not asked for its branches to be judged, and
    /// refusing every feed from it would silence a real repository over a missing ref.</para>
    /// </remarks>
    public static async Task<string?> DefaultBranchAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(
            root, ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"], ct).ConfigureAwait(false);
        if (code == 0 && stdout.Trim() is { Length: > 0 } pointed)
        {
            var slash = pointed.IndexOf('/');
            return slash < 0 ? pointed : pointed[(slash + 1)..];
        }

        foreach (var candidate in new[] { "main", "master" })
        {
            // Spelled as every read of a branch's commit is (LANDGIT1c), so one ref is one command wherever it is asked.
            var (exists, _, _) = await GitAsync(
                root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{candidate}^{{commit}}"], ct).ConfigureAwait(false);
            if (exists == 0) return candidate;
        }

        return null;
    }

    /// <summary>
    /// How <paramref name="head"/> stands to <paramref name="held"/> — the question a deployment
    /// cannot ask, because it has no checkout (SYNC5a).
    /// </summary>
    /// <remarks>
    /// Whether the commit is here is asked first and on its own: `merge-base` fails the same way for a
    /// commit it does not have as for anything else, and an unknown commit reported as diverged would
    /// let commit time replace a history this machine has never seen.
    /// </remarks>
    public static async Task<TreeRelation> RelationAsync(
        string root, string held, string head, CancellationToken ct = default)
    {
        // The held commit is the REMOTE's answer and becomes a git argument, so it must be a commit id
        // and nothing else — a value starting with `-` would be read as an option.
        if (!IsCommitId(held) || !IsCommitId(head)) return TreeRelation.Unknown;

        var (known, _, _) = await GitAsync(root, ["cat-file", "-e", $"{held}^{{commit}}"], ct).ConfigureAwait(false);
        if (known != 0) return TreeRelation.Unknown;

        // `--is-ancestor` answers 0 for yes and 1 for no; anything else is git failing to answer.
        var (descends, _, _) = await GitAsync(root, ["merge-base", "--is-ancestor", held, head], ct).ConfigureAwait(false);
        if (descends == 0) return TreeRelation.Descends;
        if (descends != 1) return TreeRelation.Unknown;

        var (behind, _, _) = await GitAsync(root, ["merge-base", "--is-ancestor", head, held], ct).ConfigureAwait(false);
        return behind switch
        {
            0 => TreeRelation.Behind,
            1 => TreeRelation.Diverged,
            _ => TreeRelation.Unknown,
        };
    }

    /// <summary>A full or abbreviated commit id: hex, and long enough to mean one commit.</summary>
    internal static bool IsCommitId(string text) =>
        text.Length is >= 7 and <= 64 && text.All(char.IsAsciiHexDigit);

    /// <summary>Clean means: a git repository, with nothing uncommitted.</summary>
    public static async Task<(bool Clean, string Detail)> CleanAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout, stderr) = await GitAsync(root, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
        if (code != 0)
        {
            return (false, $"not a spawnable tree — git status failed: {FirstLine(stderr)}");
        }

        return string.IsNullOrWhiteSpace(stdout)
            ? (true, "clean")
            : (false, $"the working tree has uncommitted changes ({stdout.Trim().Split('\n').Length} paths) — "
                      + "somebody's work in flight; the driver holds rather than entangling a session with it.");
    }

    /// <summary>
    /// What a tree holds uncommitted, as git's own short lines (<c>M path</c>, <c>?? path</c>), at most
    /// <paramref name="limit"/> of them with a last line counting the rest. Empty when the tree is clean
    /// or git cannot say. What a session carrying on after a cut-off is handed (D80): it may not run
    /// <c>git status</c> itself, and a timeout lands mid-change.
    /// </summary>
    public static async Task<IReadOnlyList<string>> UncommittedAsync(
        string root, int limit = 60, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(root, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
        if (code != 0) return [];

        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Trim().Length > 0)
            .ToList();
        return lines.Count <= limit
            ? lines
            : [.. lines.Take(limit), $"… and {lines.Count - limit} more"];
    }

    /// <summary>
    /// Whether one file is exactly as HEAD has it — tracked, and nothing staged, changed or untracked
    /// about it. What a registration asks of the manifest before it names the commit (SYNC5b).
    /// </summary>
    public static async Task<bool> UnmodifiedAsync(string root, string path, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(root, ["--no-optional-locks", "status", "--porcelain", "--", path], ct).ConfigureAwait(false);
        return code == 0 && string.IsNullOrWhiteSpace(stdout);
    }

    /// <summary>Where HEAD is now — the "before" a session's evidence is measured from.</summary>
    public static async Task<string?> HeadAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(root, ["rev-parse", "HEAD"], ct).ConfigureAwait(false);
        return code == 0 ? stdout.Trim() : null;
    }

    /// <summary>What landed since — the reviewable half of the record (D46 §4).</summary>
    public static async Task<string> CommitsSinceAsync(string root, string? before, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(root, ["log", "--oneline", Since(before)], ct).ConfigureAwait(false);
        return Landed(code, stdout);
    }

    /// <inheritdoc cref="CommitsSinceAsync(string, string?, CancellationToken)"/>
    /// <param name="git">How git is read: the review's seam (REVIEW3), through which the sweep reads a lost session's (EVID1b).</param>
    internal static async Task<string> CommitsSinceAsync(string root, string? before, GitRead git, CancellationToken ct)
    {
        var stdout = new StringBuilder();
        var code = await git(root, ["log", "--oneline", Since(before)], piece =>
        {
            stdout.Append(piece.Span);
            return true;
        }, ct).ConfigureAwait(false);
        return Landed(code, stdout.ToString());
    }

    private static string Since(string? before) => before is null ? "HEAD" : $"{before}..HEAD";

    private static string Landed(int code, string stdout)
    {
        if (code != 0) return "no commits readable";

        var commits = stdout.Trim();
        return commits.Length == 0 ? "no commits landed" : $"commits landed:\n{commits}";
    }

    /// <summary>One file in a session's landed work, and the patch that changed it.</summary>
    /// <param name="Path">Repository-relative, as git spells it.</param>
    /// <param name="Status">`added`, `modified`, `deleted`, `renamed` — git's letter, said in words.</param>
    /// <param name="Added">Lines added, or null where git counted none because the file is binary.</param>
    /// <param name="Removed">Lines removed, same.</param>
    /// <param name="Patch">The unified diff for this file, or null when it was dropped by the bound.</param>
    public sealed record DiffFile(
        string Path, string Status, int? Added, int? Removed, string? Patch);

    /// <summary>
    /// A session's landed work: which files changed between the commit the tree stood at when the
    /// spawn began and where it stands now, with the patch for each.
    /// </summary>
    /// <param name="Files">Every changed file, always — the LIST is never truncated, only the patches.</param>
    /// <param name="Truncated">What the bound dropped, said out loud, or null when nothing was.</param>
    /// <param name="Base">The commit the range is measured from.</param>
    public sealed record TreeDiff(
        IReadOnlyList<DiffFile> Files, string? Truncated, string Base);

    /// <summary>What a surface will render before a person stops reading and git on disk has the rest.</summary>
    /// <remarks>
    /// The console's rule, applied again (design §5): a diff has no upper size, so the bound is stated
    /// rather than hidden. It drops whole PATCHES rather than cutting one mid-hunk — half a hunk reads
    /// as a diff and is not one — and the file list survives intact, so the person always learns that
    /// a file changed even when they cannot read how.
    /// </remarks>
    public const int PatchBudget = 400_000;

    /// <summary>The per-file cap, so one generated file cannot spend the whole budget alone.</summary>
    public const int PatchCap = 80_000;

    /// <summary>
    /// What landed in this tree since <paramref name="before"/> — the reviewable half, as a diff.
    /// </summary>
    /// <remarks>
    /// <para><b>Committed work only.</b> The range is `before..HEAD`, which is exactly what the
    /// evidence string counts, so the two can never disagree about what the session did. Uncommitted
    /// changes in the tree are deliberately not here: a chat may open on a dirty tree (D49 §3), so
    /// what is uncommitted is not knowably the session's, and attributing somebody else's work in
    /// flight to an agent is the `reaching-in` mistake in a read-only disguise. Only a session's OWN
    /// tree, which it ran in alone, has its uncommitted work read, and apart: <see cref="UncommittedDiffAsync(string, CancellationToken)"/>
    /// (REVIEWDIRTY1-a, D164 point 4).</para>
    ///
    /// <para>Null when git cannot answer — no repository, an unknown base, a tree that has moved
    /// away. A caller says so rather than showing an empty diff, which reads as "it changed
    /// nothing".</para>
    /// </remarks>
    public static Task<TreeDiff?> DiffAsync(string root, string before, CancellationToken ct = default) =>
        DiffAsync(root, before, ReadGitAsync, ct);

    /// <inheritdoc cref="DiffAsync(string, string, CancellationToken)"/>
    /// <param name="git">How the review reads git: the seam its tests count processes through (REVIEW3).</param>
    internal static async Task<TreeDiff?> DiffAsync(string root, string before, GitRead git, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(before)) return null;

        if (!await IsTopLevelAsync(root, git, ct).ConfigureAwait(false)) return null;

        return await RangeAsync(root, before, $"{before}..HEAD", "`git diff` in the tree has all of it.", git, ct).ConfigureAwait(false);
    }

    /// <summary>The size above which an untracked file is listed with no patch (REVIEWDIRTY1-a): a read of one costs a git start and a page of text.</summary>
    public const int UntrackedPatchLimit = 200 * 1024;

    /// <summary>
    /// What a session's OWN tree holds that no commit does (REVIEWDIRTY1-a, D164 point 4): the tracked changes, staged and
    /// unstaged together, against <c>HEAD</c>, and each untracked file as an addition. Null where git cannot answer.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Only for a tree the session ran in alone.</b> A conversation in the person's checkout may have opened on a dirty
    /// tree (D49 §3), so what is uncommitted there is not knowably the session's and is never read through this: the caller
    /// asks only of a tree this home made. Reads only (<c>git diff</c>, <c>git status</c>); the index is not refreshed to disk, and
    /// no <c>index.lock</c> is taken in a tree its session may be writing in that moment (REVIEWLOCK1).
    /// The files are bounded as the committed list is: <see cref="PatchBudget"/> and <see cref="PatchCap"/>.
    /// </remarks>
    public static Task<TreeDiff?> UncommittedDiffAsync(string root, CancellationToken ct = default) =>
        UncommittedDiffAsync(root, ReadGitAsync, ct);

    /// <inheritdoc cref="UncommittedDiffAsync(string, CancellationToken)"/>
    /// <param name="git">How the review reads git: the seam its tests count processes through (REVIEW3).</param>
    internal static async Task<TreeDiff?> UncommittedDiffAsync(string root, GitRead git, CancellationToken ct)
    {
        if (!await IsTopLevelAsync(root, git, ct).ConfigureAwait(false)) return null;

        const string rest = "`git diff HEAD` in the tree has all of it.";
        var tracked = await RangeAsync(root, "HEAD", "HEAD", rest, git, ct).ConfigureAwait(false);
        if (tracked is null) return null;

        var status = new StringBuilder();
        var code = await git(
            root, ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all"], piece => { status.Append(piece.Span); return true; }, ct)
            .ConfigureAwait(false);
        if (code != 0) return null;

        var files = new List<DiffFile>(tracked.Files);
        var spent = tracked.Files.Sum(file => file.Patch?.Length ?? 0);
        var dropped = 0;
        var entries = status.ToString().Split('\0');
        for (var at = 0; at < entries.Length; at++)
        {
            var entry = entries[at];
            if (entry.Length < 4) continue;
            // A rename's or a copy's source follows its entry, and is no path of its own.
            if (entry[0] is 'R' or 'C' || entry[1] is 'R' or 'C') at++;
            if (!entry.StartsWith("?? ", StringComparison.Ordinal)) continue;

            var path = entry[3..];
            if (spent >= PatchBudget)
            {
                dropped++;
                files.Add(new DiffFile(path, "added", null, null, null));
                continue;
            }

            // GITAPP1a-i: a review's untracked reads are counted in a budget of their own, its tail (the git-app design P1.4).
            string? patch;
            int? added;
            using (GitLaunch.Budget == GitBudget.Review ? GitLaunch.Within(GitBudget.Untracked) : null)
            {
                (patch, added) = await UntrackedPatchAsync(root, path, git, ct).ConfigureAwait(false);
            }

            spent += patch?.Length ?? 0;
            // The word the committed list uses for an addition, so the page renders both alike.
            files.Add(new DiffFile(path, "added", added, added is null ? null : 0, patch));
        }

        var truncated = tracked.Truncated ?? (dropped == 0
            ? null
            : $"{dropped} more untracked file{(dropped == 1 ? "" : "s")}; their patches are not shown here. {rest}");
        return new TreeDiff(files, truncated, "HEAD");
    }

    /// <summary>An untracked file as the patch that adds it, with the lines it adds; none for a binary, an oversize or an unreadable one.</summary>
    private static async Task<(string? Patch, int? Added)> UntrackedPatchAsync(string root, string path, GitRead git, CancellationToken ct)
    {
        try
        {
            if (new FileInfo(Path.Combine(root, path)) is { Exists: true, Length: > UntrackedPatchLimit }) return (null, null);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return (null, null);
        }

        var text = new StringBuilder();
        var code = await git(
            root,
            [
                "--no-optional-locks", "-c", "core.quotePath=false", "diff", "--no-index", "--no-color", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/",
                "--", "/dev/null", path,
            ],
            piece => { text.Append(piece.Span); return true; },
            ct).ConfigureAwait(false);
        // --no-index exits 1 where the files differ, which a new file always does.
        if (code is not (0 or 1) || text.Length == 0) return (null, null);

        var patch = text.ToString();
        if (patch.Contains("\nBinary files ", StringComparison.Ordinal) || patch.Contains("\nGIT binary patch", StringComparison.Ordinal)) return (null, null);

        var added = patch.Split('\n').Count(line => line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal));
        return (patch.Length > PatchCap ? patch[..PatchCap] + "\n… this file's patch is longer than the surface renders." : patch, added);
    }

    /// <summary>
    /// What changed between two commits of the repository at <paramref name="root"/> (REVIEW2, D113): a landed
    /// session's work, read from its branch in the repository's own checkout once its tree is gone.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>A read of two commits, and nothing else.</b> <c>git diff &lt;from&gt; &lt;to&gt;</c> compares two trees
    /// git already holds: it reads no working tree and no index, so the person's checkout is untouched whatever state
    /// it is in. Both ends must be commit ids, since the record's words become git's arguments. Null where git
    /// cannot answer, and where <paramref name="root"/> is not the top of a repository of its own (git walks UP).
    /// </remarks>
    public static Task<TreeDiff?> DiffBetweenAsync(string root, string from, string to, CancellationToken ct = default) =>
        DiffBetweenAsync(root, from, to, ReadGitAsync, ct);

    /// <inheritdoc cref="DiffBetweenAsync(string, string, string, CancellationToken)"/>
    /// <param name="git">How the review reads git: the seam its tests count processes through (REVIEW3).</param>
    internal static async Task<TreeDiff?> DiffBetweenAsync(string root, string from, string to, GitRead git, CancellationToken ct)
    {
        if (!IsCommitId(from) || !IsCommitId(to)) return null;

        if (!await IsTopLevelAsync(root, git, ct).ConfigureAwait(false)) return null;

        return await RangeAsync(
                root, from, $"{from}..{to}", $"`git diff {Short(from)} {Short(to)}` in the repository's checkout has all of it.", git, ct)
            .ConfigureAwait(false);
    }

    private static string Short(string commit) => TextBounds.ShortCommit(commit, 8);

    /// <summary>
    /// The files and patches of one range, bounded, from ONE git process (REVIEW3); <paramref name="rest"/> says where the
    /// rest is when the bound drops some.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>A read of two commits, or of a tree's committed range, and nothing else.</b> A diff of a range compares
    /// trees git already holds: no working tree and no index is read, whatever state they are in. Null where git cannot
    /// answer (an unknown commit, git failing or not starting), which a caller says rather than showing an empty diff.</para>
    ///
    /// <para><b>One process, not one per file.</b> A git start on a large repository costs about a second, and this read
    /// once asked git again for each file's patch: a review of 61 files took 52.7 s on an install. Now <c>--raw</c> (the
    /// status: <c>--name-status</c> would make git drop the counts and the patch from the same answer), <c>--numstat</c>
    /// and <c>-p</c> come back together. With <c>-z</c> the two lists are NUL-ended fields with every path verbatim, an
    /// empty field ends them, and the patch follows. Each file's patch starts at a <c>diff --git</c> line, which no other
    /// line of a patch can start with (a hunk's lines start with a space, <c>+</c>, <c>-</c> or <c>\</c>), and is matched
    /// to its file by that line exactly as git writes it for the file's paths: a part of the answer is never filed under
    /// a file whose line it does not carry. A typechange's two halves carry the same line, and both are its file's.</para>
    ///
    /// <para><b>A rename is git's rename.</b> With the whole range in view, git pairs a renamed file with where it came
    /// from: its patch is the rename and the lines that changed, and its counts are found under its new path. A read of the
    /// new path alone could not see the old one, so it showed the whole file as added and found no counts.</para>
    ///
    /// <para><b>The bound is applied as the answer comes</b>, exactly as each file's own read applied it, in the files'
    /// order: a patch is kept while less than <see cref="PatchBudget"/> is spent, cut at <see cref="PatchCap"/>; once the
    /// budget is spent every later file is counted, and git is stopped there rather than read to the end.</para>
    ///
    /// <para>Internal since GIT1b: a commit's page and a compare read their changes through this same reader, over the range
    /// they name, rather than through a second one. A caller proves <paramref name="root"/> is a repository's top first.</para>
    /// </remarks>
    internal static async Task<TreeDiff?> RangeAsync(
        string root, string before, string range, string rest, GitRead git, CancellationToken ct)
    {
        var split = new RangeSplit();
        var code = await git(
            root,
            [
                // A diff against the working tree (`HEAD` alone, a session's uncommitted work) takes the tree's `index.lock` to
                // write the stat data it refreshed, which a session writing there may need that moment (REVIEWLOCK1). git's diff
                // does so whatever `--no-optional-locks` says (2.53); `diff.autoRefreshIndex=false` keeps it from refreshing,
                // and RangeSplit drops the files that leaves listed with nothing changed. A range of two commits reads no tree.
                "--no-optional-locks",
                "-c", "diff.autoRefreshIndex=false",
                // A path outside ASCII as itself on the `diff --git` line, as `-z` gives it in the lists.
                "-c", "core.quotePath=false",
                "diff",
                // What a person's settings would change in the answer's shape: colour codes (`color.ui=always`), a diff
                // program (`diff.external`), and the prefixes on the line a patch is matched by (`diff.noprefix`,
                // `diff.mnemonicPrefix`, `diff.srcPrefix`): each named here wins over the setting.
                "--no-color", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/",
                "-M", "-z", "--raw", "--numstat", "-p", range,
            ],
            piece => split.Take(piece.Span),
            ct).ConfigureAwait(false);
        if (code != 0 && !split.Stopped) return null;

        return split.End(before, rest);
    }

    /// <summary>
    /// git's answer to <c>diff -z --raw --numstat -p</c>, read as it comes (REVIEW3): the files and their counts first,
    /// then each file's patch, bounded as it is read.
    /// </summary>
    private sealed class RangeSplit
    {
        private const string Header = "diff --git ";

        private readonly List<(char Status, string Old, string New)> _files = [];
        private readonly Dictionary<string, (int? Added, int? Removed)> _counts = new(StringComparer.Ordinal);

        // The lists: a record is its NUL-ended fields, and how many it has is known from its first.
        private readonly StringBuilder _field = new();
        private readonly List<string> _record = [];
        private int _fields;
        private bool _inPatch;

        // The patch: which file each `diff --git` line is, and what has been kept of each.
        private readonly Dictionary<string, int> _headers = new(StringComparer.Ordinal);
        private string?[] _patches = [];
        private readonly StringBuilder _line = new();
        private bool _lineStart = true;
        private readonly StringBuilder _patch = new();
        private int _current = -1;
        private int _next;
        private int _spent;
        private int _dropped;

        /// <summary>Whether the budget was spent and the rest of git's answer is not wanted.</summary>
        public bool Stopped { get; private set; }

        /// <summary>Read the next piece of git's answer; false once nothing more is wanted.</summary>
        public bool Take(ReadOnlySpan<char> piece)
        {
            while (piece.Length > 0 && !Stopped)
            {
                if (_inPatch)
                {
                    piece = Patch(piece);
                    continue;
                }

                var end = piece.IndexOf('\0');
                if (end < 0)
                {
                    _field.Append(piece);
                    break;
                }

                _field.Append(piece[..end]);
                piece = piece[(end + 1)..];
                var field = _field.ToString();
                _field.Clear();
                Field(field);
            }

            return !Stopped;
        }

        /// <summary>The range as a review shows it, once git's answer has ended or been stopped.</summary>
        public TreeDiff End(string before, string rest)
        {
            if (!_inPatch) BeginPatch();
            if (Stopped)
            {
                // Every file from here was past the budget when its turn came.
                _dropped += _files.Count - _next;
            }
            else
            {
                if (_line.Length > 0) Keep(_line.ToString());
                Close();
                while (_next < _files.Count) Settle(_next++, "");
            }

            var files = new List<DiffFile>(_files.Count);
            for (var i = 0; i < _files.Count; i++)
            {
                var (letter, _, path) = _files[i];
                var status = letter switch
                {
                    'A' => "added",
                    'D' => "deleted",
                    'R' => "renamed",
                    'C' => "copied",
                    _ => "modified",
                };
                var (added, removed) = _counts.TryGetValue(path, out var count) ? count : (null, null);
                files.Add(new DiffFile(path, status, added, removed, _patches[i]));
            }

            var truncated = _dropped == 0
                ? null
                : $"{_dropped} more file{(_dropped == 1 ? "" : "s")} changed; their patches are not shown here. {rest}";
            return new TreeDiff(files, truncated, before);
        }

        /// <summary>
        /// One field of the lists. A <c>--raw</c> record is <c>:modes ids STATUS</c> and its path, or two for a rename or a
        /// copy; a <c>--numstat</c> record is <c>added\tremoved\tpath</c>, or an empty path and then two. An empty field
        /// where a record would start ends the lists.
        /// </summary>
        private void Field(string field)
        {
            if (_record.Count == 0)
            {
                if (field.Length == 0)
                {
                    BeginPatch();
                    return;
                }

                _fields = field[0] == ':'
                    ? (StatusOf(field) is 'R' or 'C' ? 3 : 2)
                    : (field.Split('\t', 3) is [_, _, ""] ? 3 : 1);
            }

            _record.Add(field);
            if (_record.Count < _fields) return;

            var head = _record[0];
            if (head[0] == ':')
            {
                // A rename carries two paths; the one that exists now is the last.
                _files.Add((StatusOf(head), _record[1], _record[^1]));
            }
            else if (head.Split('\t', 3) is [var added, var removed, var path])
            {
                // git writes `-` for a binary file rather than a count. Null, not zero: "not counted"
                // and "counted nothing" are different answers.
                _counts[path.Length > 0 ? path : _record[^1]] = (
                    int.TryParse(added, out var a) ? a : (int?)null,
                    int.TryParse(removed, out var r) ? r : (int?)null);
            }

            _record.Clear();
        }

        private static char StatusOf(string raw) => raw[(raw.LastIndexOf(' ') + 1)..] is { Length: > 0 } status ? status[0] : 'M';

        private void BeginPatch()
        {
            _inPatch = true;
            // A tracked file whose stat data alone moved is listed `M` by `--raw` when the index is not refreshed
            // (`diff.autoRefreshIndex=false`, REVIEWLOCK1), and nothing else: no count and no patch. A file that changed is always
            // counted, a binary one with `-`, so one with no count changed nothing and is no file of the answer.
            _files.RemoveAll(file => file.Status == 'M' && !_counts.ContainsKey(file.New));
            _patches = new string?[_files.Count];
            for (var i = 0; i < _files.Count; i++)
            {
                var header = $"{Header}{Quoted("a/", _files[i].Old)} {Quoted("b/", _files[i].New)}";
                // Two files git would head alike could not be told apart: neither is handed a patch that may be the other's.
                if (!_headers.TryAdd(header, i)) _headers[header] = -1;
            }
        }

        /// <summary>
        /// The patch, a line at a time: a line's start is held while it may still be a <c>diff --git</c> line, which is
        /// read whole; any other line is the current file's, and is copied through to its end.
        /// </summary>
        private ReadOnlySpan<char> Patch(ReadOnlySpan<char> piece)
        {
            while (piece.Length > 0 && !Stopped)
            {
                if (_lineStart)
                {
                    var c = piece[0];
                    piece = piece[1..];
                    _line.Append(c);
                    if (_line.Length > Header.Length)
                    {
                        if (c == '\n') Section();
                    }
                    else if (c != Header[_line.Length - 1])
                    {
                        Keep(_line.ToString());
                        _line.Clear();
                        _lineStart = c == '\n';
                    }

                    continue;
                }

                var newline = piece.IndexOf('\n');
                var through = newline < 0 ? piece.Length : newline + 1;
                Keep(piece[..through]);
                piece = piece[through..];
                _lineStart = newline >= 0;
            }

            return piece;
        }

        /// <summary>
        /// A <c>diff --git</c> line: the next part of the current file (a typechange's second half), or the start of a
        /// later file's, where every file between that had no part of its own is settled with none.
        /// </summary>
        private void Section()
        {
            var line = _line.ToString();
            _line.Clear();
            var at = _headers.TryGetValue(line[..^1], out var index) ? index : -1;
            if (at < 0 || at != _current)
            {
                Close();
                if (at >= _next)
                {
                    while (_next < at) Settle(_next++, "");
                    if (_spent >= PatchBudget)
                    {
                        Stopped = true;
                        return;
                    }

                    _current = at;
                }
            }

            Keep(line);
        }

        /// <summary>Keep text for the current file, up to one character past the cap: enough to know it is longer.</summary>
        private void Keep(ReadOnlySpan<char> text)
        {
            if (_current < 0) return;
            var room = PatchCap + 1 - _patch.Length;
            if (room > 0) _patch.Append(text[..Math.Min(room, text.Length)]);
        }

        private void Close()
        {
            if (_current < 0) return;
            Settle(_current, _patch.ToString());
            _next = _current + 1;
            _current = -1;
            _patch.Clear();
        }

        /// <summary>The rule each file's own read applied, in the files' order: kept while the budget lasts, cut at the cap.</summary>
        private void Settle(int index, string patch)
        {
            if (_spent >= PatchBudget)
            {
                _dropped++;
                return;
            }

            if (patch.Length == 0) return;
            _patches[index] = patch.Length > PatchCap
                ? patch[..PatchCap] + "\n… this file's patch is longer than the surface renders."
                : patch;
            _spent += _patches[index]!.Length;
        }
    }

    /// <summary>
    /// A path as git writes it on a <c>diff --git</c> line under <c>core.quotePath=false</c> (git's <c>quote_c_style</c>):
    /// as itself after its prefix, or in double quotes with the prefix inside them where it holds a double quote, a
    /// backslash or a control character, each escaped.
    /// </summary>
    private static string Quoted(string prefix, string path)
    {
        var escaped = new StringBuilder(path.Length + 8);
        var any = false;
        foreach (var c in path)
        {
            var escape = c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\a' => "\\a",
                '\b' => "\\b",
                '\t' => "\\t",
                '\n' => "\\n",
                '\v' => "\\v",
                '\f' => "\\f",
                '\r' => "\\r",
                < ' ' or '\x7f' => "\\" + Convert.ToString(c, 8).PadLeft(3, '0'),
                _ => null,
            };
            any |= escape is not null;
            if (escape is null) escaped.Append(c);
            else escaped.Append(escape);
        }

        return any ? $"\"{prefix}{escaped}\"" : prefix + path;
    }

    /// <summary>The files a tree holds, for a person to `@` one (CONV4d).</summary>
    /// <param name="Files">Tree-relative, with forward slashes, in ordinal order.</param>
    /// <param name="Unlisted">How many more the bound left out — counted, so the page can say so.</param>
    public sealed record TreeFiles(IReadOnlyList<string> Files, int Unlisted);

    /// <summary>How many paths a completion list is handed before the rest are only counted.</summary>
    /// <remarks>
    /// The console's rule again (design §5): a tree has no upper size, so the bound is stated. A path
    /// the list leaves out can still be typed, and both doors expand it as typed.
    /// </remarks>
    public const int FileLimit = 20_000;

    /// <summary>
    /// What a person may mention in this tree: every file git tracks that is still there, and every new
    /// one it does not ignore — or null when git cannot say.
    /// </summary>
    /// <remarks>
    /// <para><b>Untracked files are in.</b> A file the agent wrote a minute ago is as mentionable as one
    /// from last year, and the harness expands either. What git ignores is out, which is what keeps a
    /// dependency folder from burying the tree's own files.</para>
    ///
    /// <para><b>A tracked file deleted from the tree is out</b>: it is still in the index, and no
    /// harness can expand a file that is not there.</para>
    ///
    /// <para>`-z` rather than lines, because git quotes a name outside ASCII as octal escapes
    /// otherwise, and a completion would then offer a file nobody has.</para>
    /// </remarks>
    public static async Task<TreeFiles?> FilesAsync(string root, CancellationToken ct = default) =>
        await FilesAsync(root, FileLimit, ct).ConfigureAwait(false);

    /// <inheritdoc cref="FilesAsync(string, CancellationToken)"/>
    public static async Task<TreeFiles?> FilesAsync(string root, int limit, CancellationToken ct = default)
    {
        if (!await IsTopLevelAsync(root, ct).ConfigureAwait(false)) return null;

        var (code, stdout, _) = await GitAsync(
            root, ["ls-files", "-z", "--cached", "--others", "--exclude-standard"], ct).ConfigureAwait(false);
        if (code != 0) return null;

        var (deletedCode, deletedOut, _) = await GitAsync(root, ["ls-files", "-z", "--deleted"], ct).ConfigureAwait(false);
        var deleted = deletedCode == 0
            ? new HashSet<string>(deletedOut.Split('\0', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal)
            : [];

        // A conflicted file is in the index once per stage, so the set is taken before the order.
        var files = stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(path => !deleted.Contains(path))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return files.Count <= limit
            ? new TreeFiles(files, 0)
            : new TreeFiles(files[..limit], files.Count - limit);
    }

    /// <summary>
    /// 🔴 Whether <paramref name="root"/> is the top of a repository of its own, which is what every
    /// question about a session's tree has to confirm before git's answer means anything.
    /// </summary>
    /// <remarks>
    /// git WALKS UP. Run it in a directory that is not a repository and it answers for whatever
    /// repository encloses it — so a session tree that has since been deleted would quietly return the
    /// diff of the parent checkout, presented as that session's work. Found by a test that pointed at
    /// an empty directory inside this repository and got a clean exit code back. Requiring the top level
    /// to BE this path also rejects a subdirectory, which the driver never passes and which would
    /// silently narrow the answer if it ever did.
    /// </remarks>
    internal static Task<bool> IsTopLevelAsync(string root, CancellationToken ct) => IsTopLevelAsync(root, WholeAsync, ct);

    /// <inheritdoc cref="IsTopLevelAsync(string, CancellationToken)"/>
    /// <param name="git">How git is read: the review's seam (REVIEW3), so its tests count this start with the range's, and
    /// evidence's (EVID1b).</param>
    internal static async Task<bool> IsTopLevelAsync(string root, GitRead git, CancellationToken ct)
    {
        // A folder that is not there: git would refuse to start in it, and nothing here is asked of the one above.
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return false;
        var stdout = new StringBuilder();
        var code = await git(
            root,
            ["rev-parse", "--show-toplevel"],
            piece =>
            {
                stdout.Append(piece.Span);
                return true;
            },
            ct).ConfigureAwait(false);
        return code == 0 && SamePath(stdout.ToString().Trim(), root);
    }

    /// <summary>git's whole answer handed on at once: how every caller but the review asks, through <see cref="GitAsync(string, IReadOnlyList{string}, CancellationToken)"/> as before.</summary>
    private static async Task<int> WholeAsync(
        string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        var (code, stdout, _) = await GitAsync(root, arguments, ct).ConfigureAwait(false);
        take(stdout.AsMemory());
        return code;
    }

    /// <summary>
    /// Whether two paths name the same directory. git answers in forward slashes even on Windows,
    /// and a trailing separator or a differently-cased drive letter is the same place.
    /// </summary>
    private static bool SamePath(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0) return false;

        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            // An unrooted or malformed path is not the one we are standing in.
            return false;
        }
    }

    /// <summary>
    /// How Daoris's own git starts (TOOLS5, D121 §2.4): the file Tools resolves for git, by its whole path, with the
    /// tools' environment — or the refusal of a git the person chose that cannot run, which never falls back to
    /// <c>PATH</c>. Every git call here and in <see cref="SessionTrees"/> starts through this.
    /// </summary>
    /// <param name="home">The Daoris home whose <c>tools.json</c> says which git; null is a machine with no home, whose
    /// git is the system's, as before.</param>
    internal static (ProcessStartInfo? Info, string? Refusal) GitStart(string root, IReadOnlyList<string> arguments, string? home)
    {
        var read = home is null ? null : Tools.Read(home);
        var git = read is null
            ? new ToolResolution("git", ToolWay.System, null, CommandPresence.Resolve("git", startable: true), false, null)
            : Tools.Resolve(read, home!, "git");
        if (git.Refused) return (null, git.Problem);

        var info = new ProcessStartInfo
        {
            // The system's git PATH does not find keeps the bare name, so the start fails in the system's own words, as before.
            FileName = git.File ?? "git",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true, // git runs every tick; from a window it must not flash a console (Adapters.Shell)
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // 🔴 Long paths, for every call (2026-09-28): a session tree's prefix is longer than its root's, so
        // a file that fits under the root can pass Windows' 260 characters in a tree. Without this git
        // cannot open it, and says so as a change it cannot read: the first real workspace's clean-up kept
        // two trees for "uncommitted work" that was three committed files, and a removal failed half done.
        // Said on the command line, so nothing in the repository's own configuration changes; Daoris's own
        // need, so it stays here rather than in the git the person's sessions share (§2.4).
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("core.longpaths=true");
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        if (read is not null) Tools.Hand(info, read, home!);
        else GitWiring.Hand(info); // with no home there is no tools' file, and still nothing may prompt (GITAUTH1a)
        // GITSTART1: the system's git, where it is Git for Windows' launcher, starts as the git behind it with the
        // launcher's variables, one process where it was two. A managed git is laid out alike (its record names
        // cmd/git.exe) and goes the same way (GITSTART5). A git the person named as a file starts as it is.
        if (git.Way is ToolWay.System or ToolWay.Managed) GitForWindows.Direct(info);
        return (info, null);
    }

    /// <summary>
    /// The git Daoris's own git runs, as a held remote names it (GITAUTH1a, the git-auth design §5.2): its file, and when that file
    /// was last written, so a git the person chooses in Tools, or one updated in place, is another git and a held remote is tried
    /// again. Null where no git resolves.
    /// </summary>
    internal static string? GitInUse(string? home)
    {
        var read = home is null ? null : Tools.Read(home);
        var git = read is null
            ? new ToolResolution("git", ToolWay.System, null, CommandPresence.Resolve("git", startable: true), false, null)
            : Tools.Resolve(read, home!, "git");
        if (git.Refused || git.File is not { } path) return null;
        var file = new FileInfo(path);
        return file.Exists ? $"{file.FullName}|{file.LastWriteTimeUtc.Ticks}" : file.FullName;
    }

    /// <summary>
    /// The test seam (PROC1d-i): called with the arguments of each git this flow starts, through <see cref="GitAsync(string, IReadOnlyList{string}, CancellationToken)"/>,
    /// <see cref="ReadGitAsync"/> or <see cref="GitBytesAsync"/> (GITAPP1a-i: raised by <see cref="GitLaunch"/>, beneath all three),
    /// so a test counts the product's starts. Null in production; an async local, so classes running in parallel never see
    /// each other's starts. A callback must be safe to call from several threads, as a flow's fetches run side by side.
    /// </summary>
    internal static readonly AsyncLocal<Action<IReadOnlyList<string>>?> Started = new();

    /// <summary>
    /// The test seam for git's answer (PROOF-UNREAD1): where it returns a reply for a start's arguments, that reply stands in for
    /// git, so a missing git, a ref git cannot read and a timeout are each one fast case. Null in production, and an async local.
    /// </summary>
    internal static readonly AsyncLocal<Func<IReadOnlyList<string>, (int Code, string Stdout, string Stderr)?>?> Answered = new();

    /// <summary>
    /// The test seam for git's run (PARALLEL1e): where set, it stands in for the runner, handed the arguments, the bound the call
    /// runs under and the caller's token, so a test sees the bound a kind is given and the token that reaches git, and answers a
    /// run that never ended in it. Null in production, and an async local.
    /// </summary>
    internal static readonly AsyncLocal<Func<IReadOnlyList<string>, TimeSpan, CancellationToken, Task<ChildRun>>?> Runs = new();

    // Internal rather than private since D51: SessionTrees asks git the same way for the same reason —
    // one process-spawning implementation, not two that differ in encoding or error shape.
    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, CancellationToken ct) =>
        GitAsync(root, arguments, environment: null, timeout: null, ct);

    /// <summary>
    /// git with variables of its own and a bound on how long it may take (WSR6): a fetch reaches the network as
    /// the person, and one that waits on a credential nobody is there to type must end rather than hold a press.
    /// </summary>
    /// <remarks>On the bound, git and whatever it started are stopped, and the answer is a failure that says why.</remarks>
    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, TimeSpan? timeout,
        CancellationToken ct) =>
        GitAsync(root, arguments, environment, timeout, input: null, ct);

    /// <summary>
    /// git reading <paramref name="input"/> as its standard input (FABRIC-END4a, second review): <c>update-ref --stdin</c> takes a
    /// transaction there, so a branch's delete and the refs its proof rested on are checked together under git's own locks.
    /// </summary>
    /// <remarks>Written as BOM-less UTF-8, LF as given: git reads a byte-order mark as part of the first command.</remarks>
    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, string input, CancellationToken ct) =>
        GitAsync(root, arguments, environment: null, timeout: null, input, ct);

    /// <summary>
    /// git with variables of its own reading <paramref name="input"/> (COAGENT1b): the piece keeper's <c>update-index --index-info</c>
    /// fills a temporary index named by <c>GIT_INDEX_FILE</c>, never the repository's own.
    /// </summary>
    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment, string input, CancellationToken ct) =>
        GitAsync(root, arguments, environment, timeout: null, input, ct);

    private static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, TimeSpan? timeout,
        string? input, CancellationToken ct)
    {
        // LANDGIT1: within a landing, a read of a ref or a repository is asked of git once; PROC1d-iv: within a judgement, a question
        // of commits by their ids is asked of its checkout's git once. A call with a variable of its own or a bound (a fetch), one
        // that writes git's input (END4a's `update-ref --stdin`) and a test's stand-in answer are always asked as written.
        var run = () => RunGitAsync(root, arguments, environment, timeout, input, ct);
        return environment is null && timeout is null && input is null && Answered.Value is null
            ? GitFacts.Asked(root, arguments, run) ?? RefSnapshot.Asked(root, arguments, run) ?? run()
            : run();
    }

    private static Task<(int Code, string Stdout, string Stderr)> RunGitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, TimeSpan? timeout,
        string? input, CancellationToken ct)
    {
        // LANDGIT1: a git that may move a ref drops what this flow was told, before it starts as well as after it ends.
        GitFacts.Ran(arguments);
        // GITAPP1a-i: counted, classified and timed at the one seam, which raises Started; the arguments go on as they came.
        return GitLaunch.LaunchAsync(
            GitLaunch.KindOf(arguments), arguments, GitLaunch.Budget, GitRunner.Whole,
            () => RunLaunchedAsync(root, arguments, environment, timeout, input, ct));
    }

    private static async Task<(int Code, string Stdout, string Stderr)> RunLaunchedAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, TimeSpan? timeout,
        string? input, CancellationToken ct)
    {
        if (Answered.Value?.Invoke(arguments) is { } answered) return answered;
        // PARALLEL1e (D181 point 4): no git runs unbounded. The caller's bound where it passed one (a fetch, a replay), else
        // its kind's; past it git's tree is ended and the answer is a failure saying so, which every caller reads as a read
        // that failed: its subject unknown until the next wake asks again.
        var bound = GitBounds.Of(arguments, timeout);
        if (Runs.Value is { } stand)
        {
            try
            {
                return Answer(await stand(arguments, bound, ct).ConfigureAwait(false), bound);
            }
            finally
            {
                RefSnapshot.Ran(arguments);
                GitFacts.Ran(arguments);
            }
        }

        var (info, refusal) = GitStart(root, arguments, DaorisHome.Resolve());
        // A git the person chose that cannot run is git's answer, said in the resolution's words (TOOLS5): never PATH's.
        if (info is null) return (-1, "", refusal!);
        if (environment is not null)
        {
            foreach (var (name, value) in environment) info.Environment[name] = value;
        }

        if (input is not null)
        {
            info.RedirectStandardInput = true;
            info.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        // DEDUP4b: the run, the wait and the guarded tree kill are ChildProcess's. On the caller's own cancellation it now
        // ends git's tree and throws; git was once left to finish on its own.
        try
        {
            var run = input is null
                ? await ChildProcess.RunAsync(info, bound, ct, closeInput: false).ConfigureAwait(false)
                : await ChildProcess.RunAsync(info, bound, ct, input, ProcessStarts.Shared).ConfigureAwait(false);
            return Answer(run, bound);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return (-1, "", error.Message);
        }
        finally
        {
            // PROC1d-ii: a git that may have moved a ref, finished, stopped or failed, drops the refs this flow read at once.
            RefSnapshot.Ran(arguments);
            GitFacts.Ran(arguments);
        }
    }

    /// <summary>A run's end as git's answer: its code and streams, or a failure saying it did not start or did not answer in time.</summary>
    private static (int Code, string Stdout, string Stderr) Answer(ChildRun run, TimeSpan bound)
    {
        if (!run.Started) return (-1, "", run.Error?.Message ?? "git did not start");
        if (run.TimedOut) return (-1, "", $"git did not answer within {bound.TotalSeconds:0} seconds, and was stopped.");
        return (run.ExitCode!.Value, run.Stdout, run.Stderr);
    }

    /// <summary>
    /// The first <paramref name="limit"/> bytes git writes, as bytes (REVIEW2): a blob read for a preview, which may be
    /// binary and may be larger than any preview shows. Null where git failed or would not start.
    /// </summary>
    /// <remarks>
    /// Bytes rather than <see cref="GitAsync(string, IReadOnlyList{string}, CancellationToken)"/>'s text, since a blob
    /// decoded as UTF-8 on its way in is no longer the blob; and bounded, since a file has no upper size. Once the bound
    /// is read, git is stopped rather than drained: the rest is only counted, from what git said of the blob's size.
    /// PARALLEL1e: bounded in time too, by its kind (<see cref="GitBounds"/>); past it, or on the caller's cancellation, git's
    /// tree is ended, and the answer is null, or the cancellation thrown.
    /// </remarks>
    internal static Task<(byte[] Bytes, int Count)?> GitBytesAsync(
        string root, IReadOnlyList<string> arguments, int limit, CancellationToken ct) =>
        // GITAPP1a-i: through the one seam, which raises Started for this runner too (once RunGitAsync's alone).
        GitLaunch.LaunchAsync(
            GitLaunch.KindOf(arguments), arguments, GitLaunch.Budget, GitRunner.Bytes, () => BytesLaunchedAsync(root, arguments, limit, ct));

    private static async Task<(byte[] Bytes, int Count)?> BytesLaunchedAsync(
        string root, IReadOnlyList<string> arguments, int limit, CancellationToken ct)
    {
        // The blob is read as bytes off the stream beneath; git's own words on the error stream are UTF-8.
        var (info, _) = GitStart(root, arguments, DaorisHome.Resolve());
        if (info is null) return null;

        Process? process = null;
        Task<string>? stderr = null;
        using var within = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            // Through the process-wide gate, off the caller's thread (UIFREEZE1), as the background's (PARALLEL1d).
            process = await ProcessStarts.Shared.StartAsync(info, StartClass.Background, ct).ConfigureAwait(false)
                      ?? throw new DriverException("git did not start");
            within.CancelAfter(GitBounds.Of(arguments));
            using var ending = Ending(within.Token, process);
            stderr = process.StandardError.ReadToEndAsync(within.Token);
            var buffer = new byte[Math.Max(0, limit)];
            var count = 0;
            var stream = process.StandardOutput.BaseStream;
            while (count < buffer.Length)
            {
                var got = await stream.ReadAsync(buffer.AsMemory(count), within.Token).ConfigureAwait(false);
                if (got == 0) break;
                count += got;
            }

            // More than the bound is waiting: git would block on a full pipe, so it is stopped. What it wrote is whole.
            var bounded = count == buffer.Length && await stream.ReadAsync(new byte[1], within.Token).ConfigureAwait(false) > 0;
            if (bounded)
            {
                ChildProcess.KillTree(process);
            }

            await process.WaitForExitAsync(within.Token).ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            // A read the kill below ended answers as git's end would: past the bound, it is none.
            within.Token.ThrowIfCancellationRequested();
            return bounded || process.ExitCode == 0 ? (buffer, count) : null;
        }
        catch (OperationCanceledException) when (process is not null)
        {
            await EndAsync(process, stderr).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return null;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// A streamed git ended as its bound passes or its caller cancels (PARALLEL1e): a read of a pipe does not always hear its
    /// token, and the kill closes the pipe under it, so the read ends with git.
    /// </summary>
    private static CancellationTokenRegistration Ending(CancellationToken within, Process process) =>
        within.Register(() => ChildProcess.KillTree(process));

    /// <summary>
    /// A streamed git past its bound or its caller's cancellation (PARALLEL1e): its tree ended, never left running unowned as
    /// a cancelled read once left it, and its error stream's read let finish unobserved, the answer already "none".
    /// </summary>
    private static async Task EndAsync(Process process, Task<string>? stderr)
    {
        ChildProcess.KillTree(process);
        if (stderr is not null) await stderr.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
    }

    /// <summary>
    /// How a review reads git (REVIEW3): <c>take</c> is handed git's answer as it comes and answers false once it has what
    /// it needs, and git is stopped there. The answer is git's exit code, 0 where the reader stopped it, -1 where git did
    /// not start or could not be read.
    /// </summary>
    /// <remarks>The seam a review's tests count its git processes through; production passes <see cref="ReadGitAsync"/>.</remarks>
    internal delegate Task<int> GitRead(
        string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct);

    /// <summary>
    /// git, read as it answers (REVIEW3): each piece of what it writes is handed to <paramref name="take"/>, decoded as
    /// UTF-8, and once <paramref name="take"/> answers false git is stopped rather than drained, since the rest is not
    /// wanted. 0 where it was stopped so, git's own code otherwise, -1 where git did not start or could not be read.
    /// </summary>
    /// <remarks>
    /// <see cref="GitBytesAsync"/>'s shape for text: a range's patch has no upper size, and a review that has spent its
    /// bound reads no further. git's words on the error stream are drained beside it, so a full pipe never holds git.
    /// PARALLEL1e: bounded in time by its kind (<see cref="GitBounds"/>); past it git's tree is ended and the answer is -1, a
    /// read that failed, and on the caller's cancellation its tree is ended and the cancellation thrown.
    /// </remarks>
    internal static Task<int> ReadGitAsync(
        string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct) =>
        // GITAPP1a-i: through the one seam, which raises Started for this runner too (once RunGitAsync's alone).
        GitLaunch.LaunchAsync(
            GitLaunch.KindOf(arguments), arguments, GitLaunch.Budget, GitRunner.Streamed, () => ReadLaunchedAsync(root, arguments, take, ct));

    private static async Task<int> ReadLaunchedAsync(
        string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        var (info, _) = GitStart(root, arguments, DaorisHome.Resolve());
        if (info is null) return -1;

        Process? process = null;
        Task<string>? stderr = null;
        using var within = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            // Through the process-wide gate, off the caller's thread (UIFREEZE1), as the background's (PARALLEL1d).
            process = await ProcessStarts.Shared.StartAsync(info, StartClass.Background, ct).ConfigureAwait(false)
                      ?? throw new DriverException("git did not start");
            within.CancelAfter(GitBounds.Of(arguments));
            using var ending = Ending(within.Token, process);
            stderr = process.StandardError.ReadToEndAsync(within.Token);
            var buffer = new char[16 * 1024];
            var stopped = false;
            int got;
            while (!stopped && (got = await process.StandardOutput.ReadAsync(buffer.AsMemory(), within.Token).ConfigureAwait(false)) > 0)
            {
                stopped = !take(buffer.AsMemory(0, got));
            }

            if (stopped)
            {
                ChildProcess.KillTree(process);
            }

            await process.WaitForExitAsync(within.Token).ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            // A read the kill ended answers as git's end would: past the bound, it is a read that failed.
            within.Token.ThrowIfCancellationRequested();
            return stopped ? 0 : process.ExitCode;
        }
        catch (OperationCanceledException) when (process is not null)
        {
            await EndAsync(process, stderr).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return -1;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return -1;
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static string FirstLine(string text)
    {
        var trimmed = text.Trim();
        var newline = trimmed.IndexOf('\n');
        return newline < 0 ? trimmed : trimmed[..newline].Trim();
    }
}
