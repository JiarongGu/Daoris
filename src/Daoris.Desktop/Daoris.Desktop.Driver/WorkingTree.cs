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
    public string ShortCommit => Commit.Length <= 8 ? Commit : Commit[..8];
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
            || !DateTimeOffset.TryParse(
                lines[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out var committedAt))
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
            var (exists, _, _) = await GitAsync(
                root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{candidate}"], ct).ConfigureAwait(false);
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
        var (code, stdout, stderr) = await GitAsync(root, ["status", "--porcelain"], ct).ConfigureAwait(false);
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
        var (code, stdout, _) = await GitAsync(root, ["status", "--porcelain"], ct).ConfigureAwait(false);
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
        var (code, stdout, _) = await GitAsync(root, ["status", "--porcelain", "--", path], ct).ConfigureAwait(false);
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
    /// flight to an agent is the `reaching-in` mistake in a read-only disguise.</para>
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

    private static string Short(string commit) => commit.Length > 8 ? commit[..8] : commit;

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
        return (info, null);
    }

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
    internal static async Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, TimeSpan? timeout,
        CancellationToken ct)
    {
        var (info, refusal) = GitStart(root, arguments, DaorisHome.Resolve());
        // A git the person chose that cannot run is git's answer, said in the resolution's words (TOOLS5): never PATH's.
        if (info is null) return (-1, "", refusal!);
        if (environment is not null)
        {
            foreach (var (name, value) in environment) info.Environment[name] = value;
        }

        using var bound = timeout is not null ? CancellationTokenSource.CreateLinkedTokenSource(ct) : null;
        if (timeout is { } limit) bound!.CancelAfter(limit);
        var waitOn = bound?.Token ?? ct;
        // Not `using` inside the try: a process disposed before the catch runs could not be stopped there.
        Process? process = null;
        try
        {
            process = Process.Start(info) ?? throw new DriverException("git did not start");
            var stdout = process.StandardOutput.ReadToEndAsync(waitOn);
            var stderr = process.StandardError.ReadToEndAsync(waitOn);
            await process.WaitForExitAsync(waitOn).ConfigureAwait(false);
            return (process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (bound is not null && !ct.IsCancellationRequested)
        {
            try
            {
                process?.Kill(entireProcessTree: true);
            }
            catch (Exception kill) when (kill is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Gone already: nothing is left to stop.
            }

            return (-1, "", $"git did not answer within {timeout!.Value.TotalSeconds:0} seconds, and was stopped.");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return (-1, "", error.Message);
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// The first <paramref name="limit"/> bytes git writes, as bytes (REVIEW2): a blob read for a preview, which may be
    /// binary and may be larger than any preview shows. Null where git failed or would not start.
    /// </summary>
    /// <remarks>
    /// Bytes rather than <see cref="GitAsync(string, IReadOnlyList{string}, CancellationToken)"/>'s text, since a blob
    /// decoded as UTF-8 on its way in is no longer the blob; and bounded, since a file has no upper size. Once the bound
    /// is read, git is stopped rather than drained: the rest is only counted, from what git said of the blob's size.
    /// </remarks>
    internal static async Task<(byte[] Bytes, int Count)?> GitBytesAsync(
        string root, IReadOnlyList<string> arguments, int limit, CancellationToken ct)
    {
        // The blob is read as bytes off the stream beneath; git's own words on the error stream are UTF-8.
        var (info, _) = GitStart(root, arguments, DaorisHome.Resolve());
        if (info is null) return null;

        Process? process = null;
        try
        {
            process = Process.Start(info) ?? throw new DriverException("git did not start");
            var stderr = process.StandardError.ReadToEndAsync(ct);
            var buffer = new byte[Math.Max(0, limit)];
            var count = 0;
            var stream = process.StandardOutput.BaseStream;
            while (count < buffer.Length)
            {
                var got = await stream.ReadAsync(buffer.AsMemory(count), ct).ConfigureAwait(false);
                if (got == 0) break;
                count += got;
            }

            // More than the bound is waiting: git would block on a full pipe, so it is stopped. What it wrote is whole.
            var bounded = count == buffer.Length && await stream.ReadAsync(new byte[1], ct).ConfigureAwait(false) > 0;
            if (bounded)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception kill) when (kill is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Gone already.
                }
            }

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            return bounded || process.ExitCode == 0 ? (buffer, count) : null;
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
    /// </remarks>
    internal static async Task<int> ReadGitAsync(
        string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        var (info, _) = GitStart(root, arguments, DaorisHome.Resolve());
        if (info is null) return -1;

        Process? process = null;
        try
        {
            process = Process.Start(info) ?? throw new DriverException("git did not start");
            var stderr = process.StandardError.ReadToEndAsync(ct);
            var buffer = new char[16 * 1024];
            var stopped = false;
            int got;
            while (!stopped && (got = await process.StandardOutput.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
            {
                stopped = !take(buffer.AsMemory(0, got));
            }

            if (stopped)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception kill) when (kill is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Gone already.
                }
            }

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            return stopped ? 0 : process.ExitCode;
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
