using System.Text;
using static Daoris.Driver.WorkingTree;

namespace Daoris.Driver;

/// <summary>A tree's committed and uncommitted work read as a diff: the files always, the patches within the bound.</summary>
internal static class TreeDiffs
{
    /// <summary>What a surface will render before a person stops reading and git on disk has the rest.</summary>
    /// <remarks>A diff has no upper size, so the bound is stated, not hidden: whole patches are dropped, never one cut mid-hunk (half a
    /// hunk reads as a diff and is not one), and the file list survives intact, so a person always learns that a file changed.</remarks>
    public const int PatchBudget = 400_000;

    /// <summary>The per-file cap, so one generated file cannot spend the whole budget alone.</summary>
    public const int PatchCap = 80_000;

    /// <summary>What landed in this tree since <paramref name="before"/> — the reviewable half, as a diff.</summary>
    /// <remarks>
    /// <para><b>Committed work only.</b> The range is `before..HEAD`, exactly what the evidence string counts, so the two never
    /// disagree about what the session did. A chat may open on a dirty tree, so what is uncommitted is not knowably the session's,
    /// and giving somebody else's work in flight to an agent is the `reaching-in` mistake in a read-only disguise. Only a session's
    /// OWN tree, which it ran in alone, has its uncommitted work read, and apart: <see cref="UncommittedDiffAsync(string, CancellationToken)"/>.</para>
    ///
    /// <para>Null when git cannot answer — no repository, an unknown base, a tree that has moved
    /// away. A caller says so rather than showing an empty diff, which reads as "it changed
    /// nothing".</para>
    /// </remarks>
    public static Task<TreeDiff?> DiffAsync(string root, string before, CancellationToken ct = default) =>
        DiffAsync(root, before, ReadGitAsync, ct);

    /// <inheritdoc cref="DiffAsync(string, string, CancellationToken)"/>
    /// <param name="git">How the review reads git: the seam its tests count processes through.</param>
    internal static async Task<TreeDiff?> DiffAsync(string root, string before, GitRead git, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(before)) return null;

        if (!await IsTopLevelAsync(root, git, ct).ConfigureAwait(false)) return null;

        return await RangeAsync(root, before, $"{before}..HEAD", CommittedRest, git, ct).ConfigureAwait(false);
    }

    /// <summary>The size above which an untracked file is listed with no patch and git is not asked for one: a page of text no surface renders whole.</summary>
    public const int UntrackedPatchLimit = 200 * 1024;

    /// <summary>
    /// What a session's OWN tree holds that no commit does: the tracked changes, staged and
    /// unstaged together, against <c>HEAD</c>, and each untracked file as an addition. Null where git cannot answer.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Only for a tree the session ran in alone.</b> What is uncommitted in the person's checkout is not knowably the session's;
    /// the caller asks only of a tree this home made. The tree's own index is only read (its untracked files' intents go to a private one,
    /// <see cref="UntrackedPatches"/>), never refreshed, and no <c>index.lock</c> is taken where its session may be writing. The files
    /// are bounded as the committed list is: <see cref="PatchBudget"/> and <see cref="PatchCap"/>.
    /// </remarks>
    public static Task<TreeDiff?> UncommittedDiffAsync(string root, CancellationToken ct = default) =>
        UncommittedDiffAsync(root, ReadGitAsync, ct);

    /// <inheritdoc cref="UncommittedDiffAsync(string, CancellationToken)"/>
    /// <param name="git">How the review reads git, its tests' seam; the untracked files' starts name a private index (<see cref="UntrackedPatches"/>).</param>
    internal static async Task<TreeDiff?> UncommittedDiffAsync(string root, GitRead git, CancellationToken ct)
    {
        if (!await IsTopLevelAsync(root, git, ct).ConfigureAwait(false)) return null;

        var tracked = await RangeAsync(root, "HEAD", "HEAD", UncommittedRest, git, ct).ConfigureAwait(false);
        if (tracked is null) return null;

        var status = new StringBuilder();
        var code = await git(root, UncommittedStatus, piece => { status.Append(piece.Span); return true; }, ct).ConfigureAwait(false);
        if (code != 0) return null;

        // The fold is the graph's too (ReviewChoices, UntrackedFold), so the two read the same files alike.
        var fold = new UntrackedFold(tracked);
        using var untracked = new UntrackedPatches(root, ReviewChoices.UntrackedPaths(status.ToString()), fold);
        if (untracked.Asks && await untracked.IntentsAsync(ct).ConfigureAwait(false)) await untracked.ReadAsync(ct).ConfigureAwait(false);
        untracked.Settle();
        return fold.End(UncommittedRest);
    }

    /// <summary>Where the rest of a tree's uncommitted work is, when the bound drops some.</summary>
    internal const string UncommittedRest = "`git diff HEAD` in the tree has all of it.";

    /// <summary>The status of a session's own tree, as the uncommitted read asks it (no <c>index.lock</c> taken).</summary>
    internal static readonly string[] UncommittedStatus = ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all"];

    /// <summary>A file's patch as a review keeps it: whole up to <see cref="PatchCap"/>, cut there with a line that says so.</summary>
    internal static string Capped(string patch) =>
        patch.Length > PatchCap ? patch[..PatchCap] + "\n… this file's patch is longer than the surface renders." : patch;

    /// <summary>
    /// What changed between two commits of the repository at <paramref name="root"/>: a landed
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
    /// <param name="git">How the review reads git: the seam its tests count processes through.</param>
    internal static async Task<TreeDiff?> DiffBetweenAsync(string root, string from, string to, GitRead git, CancellationToken ct)
    {
        if (!IsCommitId(from) || !IsCommitId(to)) return null;

        if (!await IsTopLevelAsync(root, git, ct).ConfigureAwait(false)) return null;

        return await RangeAsync(root, from, $"{from}..{to}", BetweenRest(from, to), git, ct).ConfigureAwait(false);
    }

    /// <summary>Where the rest of a range of two commits is, when the bound drops some (Review's graph says it alike).</summary>
    internal static string BetweenRest(string from, string to) => $"`git diff {Short(from)} {Short(to)}` in the repository's checkout has all of it.";

    /// <summary>Where the rest of a tree's committed range is, when the bound drops some.</summary>
    internal const string CommittedRest = "`git diff` in the tree has all of it.";

    private static string Short(string commit) => TextBounds.ShortCommit(commit, 8);

    /// <summary>
    /// The files and patches of one range, bounded, from ONE git process; <paramref name="rest"/> says where the
    /// rest is when the bound drops some.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>A read of two commits, or of a tree's committed range, and nothing else.</b> A diff of a range compares
    /// trees git already holds: no working tree and no index is read, whatever state they are in. Null where git cannot
    /// answer (an unknown commit, git failing or not starting), which a caller says rather than showing an empty diff.</para>
    ///
    /// <para><b>One process, not one per file</b>, as a start on a large repository costs about a second: <c>--raw</c> (with
    /// <c>--name-status</c> git drops the counts and the patch), <c>--numstat</c> and <c>-p</c> together, the lists NUL-ended under
    /// <c>-z</c> with every path verbatim, an empty field ending them before the patch. Each file's patch starts at a
    /// <c>diff --git</c> line, which no other line of a patch starts with, and is matched to its file by that line exactly as git
    /// writes it for the file's paths. A typechange's two halves carry the same line, and both are its file's.</para>
    ///
    /// <para><b>A rename is git's rename.</b> With the whole range in view, git pairs a renamed file with where it came
    /// from: its patch is the rename and the lines that changed, and its counts are found under its new path. A read of the
    /// new path alone cannot see the old one, so it would show the whole file as added and find no counts.</para>
    ///
    /// <para><b>The bound is applied as the answer comes</b>, exactly as each file's own read applied it, in the files'
    /// order: a patch is kept while less than <see cref="PatchBudget"/> is spent, cut at <see cref="PatchCap"/>; once the
    /// budget is spent every later file is counted, and git is stopped there rather than read to the end.</para>
    ///
    /// <para>A commit's page and a compare read through this reader too. A caller proves <paramref name="root"/> is a repository's top first.</para>
    /// </remarks>
    internal static async Task<TreeDiff?> RangeAsync(
        string root, string before, string range, string rest, GitRead git, CancellationToken ct)
    {
        var split = new RangeSplit();
        var code = await git(
            root,
            [
                // A diff against the working tree (`HEAD` alone) takes the tree's `index.lock` to write the stat data it refreshed,
                // whatever `--no-optional-locks` says (2.53); `diff.autoRefreshIndex=false` keeps it from refreshing, and RangeSplit
                // drops the files that leaves listed with nothing changed. A range of two commits reads no tree.
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
    /// git's answer to <c>diff -z --raw --numstat -p</c>, read as it comes: the files and their counts first,
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

        /// <summary>Whether the budget is spent and the rest of git's answer is not wanted.</summary>
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
                // Every file from here is past the budget when its turn comes.
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
            // (`diff.autoRefreshIndex=false`), and nothing else: no count and no patch. A file that changed is always
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
            _patches[index] = Capped(patch);
            _spent += _patches[index]!.Length;
        }
    }

    /// <summary>
    /// A path as git writes it on a <c>diff --git</c> line under <c>core.quotePath=false</c> (git's <c>quote_c_style</c>):
    /// as itself after its prefix, or in double quotes with the prefix inside them where it holds a double quote, a
    /// backslash or a control character, each escaped.
    /// </summary>
    internal static string Quoted(string prefix, string path)
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
