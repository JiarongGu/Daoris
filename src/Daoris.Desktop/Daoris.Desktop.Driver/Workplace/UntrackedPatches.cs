using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A session tree's untracked files as the patches that add them, in two git starts whatever their count: the files are added as
/// intents to a private index of the read's own, and one <c>diff</c> of that index against the tree is split by its files' headers
/// into the fold, in status's order, as it comes.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The tree's own index is never touched.</b> The private index is named by <c>GIT_INDEX_FILE</c> beside it, in the tree's
/// own git directory, and starts empty, so the diff holds the intents alone and needs no path on its command line. It is removed with
/// its lock when the read ends, whatever git did, and <c>core.splitIndex=false</c> writes nothing else there. The paths are handed on
/// git's input, NUL-ended and literal (<c>GIT_LITERAL_PATHSPECS</c>), so no command line bounds them and no glob or magic is read in a
/// name.</para>
///
/// <para><b>Each patch is the one a file's own read gave</b> (<c>diff --no-index /dev/null &lt;path&gt;</c>), byte for byte: the same
/// header, mode and index lines, the clean filter and textconv run alike, a binary file with no patch. A file over
/// <see cref="TreeDiffs.UntrackedPatchLimit"/>, a folder (a nested repository's) and one gone since the status are listed with no
/// read; once the patch budget is spent git is stopped and every later file is listed with no patch.</para>
///
/// <para><b>In status's order.</b> git writes the files in its index's order, which is status's (both sort the paths' bytes), and
/// <c>-O/dev/null</c> holds it against a configured order file; so a file git wrote nothing for is answered with no patch as soon as a
/// later one begins. Where git ends of itself its last file is whole; where it did not start or answer, that file is dropped.</para>
/// </remarks>
internal sealed class UntrackedPatches : IDisposable
{
    private const string Header = "diff --git ";

    private readonly string _root;
    private readonly IReadOnlyList<string> _paths;
    private readonly UntrackedFold _fold;
    private readonly bool[] _read;
    private readonly (string? Patch, int? Added)?[] _answers;
    private readonly Dictionary<string, int> _headers = new(StringComparer.Ordinal);
    private readonly string? _index;
    private int _next;
    private int _expected;

    // The diff, read a line at a time: a line's start held while it may be a header, the current file's patch, its added lines.
    private readonly StringBuilder _held = new();
    private readonly StringBuilder _patch = new();
    private readonly StringBuilder _lead = new();
    private bool _lineStart = true;
    private int _current = -1;
    private int _added;

    /// <param name="root">The session's own tree, its top.</param>
    /// <param name="paths">Its untracked paths, in status's order.</param>
    /// <param name="fold">The fold each file is taken into, W's patches already in it.</param>
    internal UntrackedPatches(string root, IReadOnlyList<string> paths, UntrackedFold fold)
    {
        _root = root;
        _paths = paths;
        _fold = fold;
        _read = new bool[paths.Count];
        _answers = new (string?, int?)?[paths.Count];
        _index = RefFiles.GitDirOf(root) is { } gitDir ? Path.Combine(gitDir, $"index.daoris-review-{Guid.NewGuid():N}") : null;
        for (var at = 0; at < paths.Count; at++)
        {
            _read[at] = _index is not null && Readable(root, paths[at]);
            if (_read[at]) _headers.TryAdd($"{Header}{TreeDiffs.Quoted("a/", paths[at])} {TreeDiffs.Quoted("b/", paths[at])}", at);
        }
    }

    /// <summary>Whether git is asked at all: a file is to be read and the patch budget is unspent.</summary>
    internal bool Asks => !_fold.Spent && _headers.Count > 0;

    /// <summary>The intents (A): each file to be read added to the private index; false where git refused them all.</summary>
    internal async Task<bool> IntentsAsync(CancellationToken ct)
    {
        using (Tail())
        {
            var input = new StringBuilder();
            for (var at = 0; at < _paths.Count; at++)
            {
                if (_read[at]) input.Append(_paths[at]).Append('\0');
            }

            var (code, _, _) = await WorkingTree.GitAsync(
                _root, ["-c", "core.splitIndex=false", "add", "--intent-to-add", "--pathspec-from-file=-", "--pathspec-file-nul"],
                Private(literal: true), input.ToString(), ct).ConfigureAwait(false);
            // 1: git refused a path (one ignored since the status) and added the rest.
            return code is 0 or 1;
        }
    }

    /// <summary>The patches (U): the private index's diff, folded as it comes and stopped once the budget is spent; false where git did not answer.</summary>
    internal async Task<bool> ReadAsync(CancellationToken ct)
    {
        using (Tail())
        {
            // A file's own read's flags; `-O/dev/null` holds git's order against a configured order file.
            var code = await WorkingTree.ReadGitAsync(
                _root,
                [
                    "--no-optional-locks", "-c", "diff.autoRefreshIndex=false", "-c", "core.quotePath=false", "diff",
                    "--no-color", "--no-ext-diff", "-O/dev/null", "--src-prefix=a/", "--dst-prefix=b/",
                ],
                Private(literal: false), piece => Take(piece.Span), ct).ConfigureAwait(false);
            if (code >= 0 && !_fold.Spent)
            {
                if (_held.Length > 0) Keep(_held.ToString());
                Close();
            }

            _current = -1;
            return code >= 0;
        }
    }

    /// <summary>Every file not yet answered is answered with no patch, and the fold is taken to its end.</summary>
    internal void Settle()
    {
        for (var at = _next; at < _paths.Count; at++)
        {
            if (_read[at]) _answers[at] ??= (null, null);
        }

        Advance();
    }

    /// <summary>The private index and its lock removed, whatever git did.</summary>
    public void Dispose()
    {
        if (_index is null) return;
        foreach (var file in new[] { _index, _index + ".lock" })
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>The private index named for git; with <paramref name="literal"/>, each pathspec read as the path it is (git's <c>--literal-pathspecs</c>).</summary>
    private Dictionary<string, string> Private(bool literal)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["GIT_INDEX_FILE"] = _index! };
        if (literal) environment["GIT_LITERAL_PATHSPECS"] = "1";
        return environment;
    }

    // A review's untracked starts are counted in a budget of their own, its tail.
    private static IDisposable? Tail() => GitLaunch.Budget == GitBudget.Review ? GitLaunch.Within(GitBudget.Untracked) : null;

    private static bool Readable(string root, string path)
    {
        if (path.EndsWith('/')) return false;
        try
        {
            return new FileInfo(Path.Combine(root, path)) is { Exists: true, Length: <= TreeDiffs.UntrackedPatchLimit };
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The fold taken as far as the answers reach, in status's order; every file past the budget dropped.</summary>
    private void Advance()
    {
        for (; _next < _paths.Count; _next++)
        {
            var path = _paths[_next];
            if (_fold.Spent) _fold.Drop(path);
            else if (!_read[_next]) _fold.Take(path, null, null);
            else if (_answers[_next] is { } answer) _fold.Take(path, answer.Patch, answer.Added);
            else return;
        }
    }

    /// <summary>The next piece of git's answer; false once the budget is spent and nothing more is wanted.</summary>
    private bool Take(ReadOnlySpan<char> piece)
    {
        while (piece.Length > 0 && !_fold.Spent)
        {
            if (_lineStart)
            {
                var c = piece[0];
                piece = piece[1..];
                _held.Append(c);
                if (_held.Length > Header.Length)
                {
                    if (c == '\n') Heading();
                }
                else if (c != Header[_held.Length - 1])
                {
                    Keep(_held.ToString());
                    _held.Clear();
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

        return !_fold.Spent;
    }

    /// <summary>A <c>diff --git</c> line: the file before it whole, every file asked between them answered with none, and the next begun.</summary>
    private void Heading()
    {
        var line = _held.ToString(0, _held.Length - 1);
        _held.Clear();
        Close();
        if (_headers.TryGetValue(line, out var at) && at >= _expected)
        {
            for (; _expected < at; _expected++)
            {
                if (_read[_expected]) _answers[_expected] ??= (null, null);
            }

            _expected = at + 1;
            _current = at;
        }

        Advance();
        Keep(line + "\n");
    }

    /// <summary>Text of the current file's: kept to one character past the cap, its added lines counted over all of it as its own read counted them.</summary>
    private void Keep(ReadOnlySpan<char> text)
    {
        if (_current < 0 || _fold.Spent) return;
        var room = TreeDiffs.PatchCap + 1 - _patch.Length;
        if (room > 0) _patch.Append(text[..Math.Min(room, text.Length)]);
        var ends = text.Length > 0 && text[^1] == '\n';
        var body = ends ? text[..^1] : text;
        if (_lead.Length < 3) _lead.Append(body[..Math.Min(3 - _lead.Length, body.Length)]);
        if (ends) EndLine();
    }

    private void EndLine()
    {
        if (_lead.Length > 0 && _lead[0] == '+' && !(_lead.Length == 3 && _lead[1] == '+' && _lead[2] == '+')) _added++;
        _lead.Clear();
    }

    /// <summary>The current file answered: its patch, or none where git said it is binary.</summary>
    private void Close()
    {
        if (_current < 0) return;
        if (_lead.Length > 0) EndLine();
        var patch = _patch.ToString();
        _answers[_current] = patch.Contains("\nBinary files ", StringComparison.Ordinal) || patch.Contains("\nGIT binary patch", StringComparison.Ordinal)
            ? (null, null)
            : (TreeDiffs.Capped(patch), _added);
        _current = -1;
        _patch.Clear();
        _added = 0;
    }
}
