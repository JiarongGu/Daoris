using System.Text;
using static Daoris.Driver.WorkingTree;

namespace Daoris.Driver;

/// <summary>A tree's committed and uncommitted work read as a diff: the files always, the patches within the bound (REVIEW3, REVIEWDIRTY1-a).</summary>
internal static class TreeDiffs
{
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

        return await RangeAsync(root, before, $"{before}..HEAD", CommittedRest, git, ct).ConfigureAwait(false);
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

        var tracked = await RangeAsync(root, "HEAD", "HEAD", UncommittedRest, git, ct).ConfigureAwait(false);
        if (tracked is null) return null;

        var status = new StringBuilder();
        var code = await git(root, UncommittedStatus, piece => { status.Append(piece.Span); return true; }, ct).ConfigureAwait(false);
        if (code != 0) return null;

        // GITAPP1a-ii: the fold is the graph's too (ReviewChoices, UntrackedFold), so the two read the same files alike.
        var fold = new UntrackedFold(tracked);
        foreach (var path in ReviewChoices.UntrackedPaths(status.ToString()))
        {
            if (fold.Spent)
            {
                fold.Drop(path);
                continue;
            }

            var (patch, added) = await UntrackedReadAsync(root, path, git, ct).ConfigureAwait(false);
            fold.Take(path, patch, added);
        }

        return fold.End(UncommittedRest);
    }

    /// <summary>Where the rest of a tree's uncommitted work is, when the bound drops some.</summary>
    internal const string UncommittedRest = "`git diff HEAD` in the tree has all of it.";

    /// <summary>The status of a session's own tree, as the uncommitted read asks it (REVIEWLOCK1: no <c>index.lock</c> taken).</summary>
    internal static readonly string[] UncommittedStatus = ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all"];

    /// <summary>One untracked file's read, counted in the review's untracked budget where the review asks it (GITAPP1a-i).</summary>
    internal static async Task<(string? Patch, int? Added)> UntrackedReadAsync(string root, string path, GitRead git, CancellationToken ct)
    {
        // GITAPP1a-i: a review's untracked reads are counted in a budget of their own, its tail (the git-app design P1.4).
        using (GitLaunch.Budget == GitBudget.Review ? GitLaunch.Within(GitBudget.Untracked) : null)
        {
            return await UntrackedPatchAsync(root, path, git, ct).ConfigureAwait(false);
        }
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

        return await RangeAsync(root, from, $"{from}..{to}", BetweenRest(from, to), git, ct).ConfigureAwait(false);
    }

    /// <summary>Where the rest of a range of two commits is, when the bound drops some (Review's graph says it alike, GITAPP1a-ii).</summary>
    internal static string BetweenRest(string from, string to) => $"`git diff {Short(from)} {Short(to)}` in the repository's checkout has all of it.";

    /// <summary>Where the rest of a tree's committed range is, when the bound drops some.</summary>
    internal const string CommittedRest = "`git diff` in the tree has all of it.";

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
}
