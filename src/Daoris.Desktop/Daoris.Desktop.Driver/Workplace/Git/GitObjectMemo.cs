using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The answers of git reads that only object ids decide (<see cref="GitObjectArguments"/>: is-ancestor, merge-base, a count over ids, the names two
/// commits differ in, a patch's equivalence, a commit's listing), kept under the git service by the repository's common folder and the
/// read's words, in memory and in the home, so a read asked once is never asked again: a hit starts no git, takes no place and opens no
/// library. A commit never changes, so an answer has no age; only the size bounds it, the least recently used going first.
/// </summary>
/// <remarks>
/// <para><b>Only what git answered whole, and pure.</b> An exit 0 with nothing on the error stream is kept, and <c>merge-base</c>'s exit 1; a
/// failure, a timeout and an answer bigger than <see cref="Largest"/> characters are asked again. Every kept kind reads objects alone: no
/// ref, HEAD, index, working tree, <c>.gitattributes</c> or configuration decides it, so one key for the repository serves every tree of it.</para>
/// <para><b>Nothing where the files cannot say.</b> A folder whose own <c>.git</c> is not there (a subfolder), a replacement that can stand, a
/// shallow or grafted history and a process with no home named (<see cref="ProgramHome"/>) keep nothing.</para>
/// <para><b>Appended in batches.</b> A kept answer waits for one append <see cref="Batch"/> after the first unwritten one, so the disk takes
/// the new answers' bytes and no more; the file is rewritten whole only past <see cref="CompactAfter"/> appended bytes and when a start reads
/// it that large. A first line names the format <see cref="Version"/>: another one, a row that does not read and a file that cannot be read
/// are dropped, never thrown. The file is <c>memo/git-objects.json</c>, one row a line.</para>
/// </remarks>
internal sealed class GitObjectMemo
{
    /// <summary>What the kept answers may weigh together (<see cref="GitReadCache"/>'s estimate): the file is about half of it.</summary>
    internal const long Bound = 8L * 1024 * 1024;

    /// <summary>The longest answer kept, in characters.</summary>
    internal const int Largest = 64 * 1024;

    /// <summary>The file's format; a file of another is discarded at its load.</summary>
    internal const int Version = 2;

    /// <summary>How many bytes may be appended before the file is written again whole.</summary>
    internal const long CompactAfter = 4L * 1024 * 1024;

    /// <summary>How long after the first unwritten answer the memo is appended.</summary>
    internal static readonly TimeSpan Batch = TimeSpan.FromSeconds(20);

    private static readonly ConcurrentDictionary<string, GitObjectMemo> Homes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly AsyncLocal<string?> s_home = new();

    private readonly GitReadCache _kept = new(Bound);
    private readonly string _path;
    private readonly object _gate = new();
    private readonly List<Row> _pending = [];
    private Timer? _timer;
    private long _appended;

    static GitObjectMemo() => AppDomain.CurrentDomain.ProcessExit += (_, _) =>
    {
        foreach (var memo in Homes.Values) memo.Flush();
    };

    private GitObjectMemo(string folder)
    {
        _path = Path.Combine(folder, "memo", "git-objects.json");
        Load();
    }

    /// <summary>The memo of the home this flow acts for: the one <see cref="Within"/> names, else the program's; none where no home is named, which keeps nothing.</summary>
    internal static GitObjectMemo? Current => (s_home.Value ?? ProgramHome.Named) is { } home ? Of(home) : null;

    /// <summary>Until disposed, this flow's reads are kept in <paramref name="home"/>'s memo: a test's own home.</summary>
    internal static IDisposable Within(string home)
    {
        var before = s_home.Value;
        s_home.Value = Path.GetFullPath(home);
        return new Restore(() => s_home.Value = before);
    }

    /// <summary>Writes what <paramref name="home"/>'s memo holds and forgets it, so the next ask reads the file: the tests' restart.</summary>
    internal static void Restart(string home)
    {
        if (Homes.TryRemove(Path.GetFullPath(home), out var memo)) memo.Flush();
    }

    /// <summary>
    /// The answer to <paramref name="arguments"/> in <paramref name="root"/>: the kept one, or <paramref name="run"/>'s, kept where git
    /// answered it whole; null where the read is not one ids alone decide, and the caller asks as before.
    /// </summary>
    internal static Task<(int Code, string Stdout, string Stderr)>? Asked(
        string root, IReadOnlyList<string> arguments, Func<CancellationToken, Task<(int Code, string Stdout, string Stderr)>> run, CancellationToken ct)
    {
        if (Current is not { } memo || !GitObjectArguments.Qualifies(arguments) || KeyOf(root, arguments) is not { } key) return null;
        return memo.AskAsync(key, arguments, run, ct);
    }

    /// <summary>Whether <paramref name="id"/> is a commit, once a read said so; null where none did.</summary>
    internal static string? PeeledCommit(string common, string id) =>
        Current?._kept.TryGet<Kept>(PeelKey(common, id), out var kept) == true ? kept!.Stdout : null;

    /// <summary>Keeps that <paramref name="id"/> is a commit: an id names the same object for good.</summary>
    internal static void LearnPeeled(string common, string id, string commit) => Current?.Keep(PeelKey(common, id), 0, commit);

    private static string PeelKey(string common, string id) => "peel\n" + common.ToLowerInvariant() + "\n" + id;

    /// <summary>The repository's common folder and the read's words; null where the repository keeps nothing.</summary>
    internal static string? KeyOf(string root, IReadOnlyList<string> arguments)
    {
        if (GitLibraryAdmission.DirsOf(root) is not { } dirs || GitReplacements.Unfixed(dirs.Common)) return null;
        var words = arguments.Where(word => word != "--no-optional-locks");
        return dirs.Common.ToLowerInvariant() + "\n" + string.Join('\0', words);
    }

    private async Task<(int Code, string Stdout, string Stderr)> AskAsync(
        string key, IReadOnlyList<string> arguments, Func<CancellationToken, Task<(int Code, string Stdout, string Stderr)>> run, CancellationToken ct)
    {
        var (caller, kind) = (GitAskers.Current, GitReadKinds.Of(arguments));
        if (_kept.TryGet<Kept>(key, out var known))
        {
            GitAskTally.Shared.RecordMemo(caller, kind, hit: true);
            return (known.Code, known.Stdout, "");
        }

        GitAskTally.Shared.RecordMemo(caller, kind, hit: false);
        var answer = await run(ct).ConfigureAwait(false);
        if (Whole(arguments, answer)) Keep(key, answer.Code, answer.Stdout);
        return answer;
    }

    private static bool Whole(IReadOnlyList<string> arguments, (int Code, string Stdout, string Stderr) answer)
    {
        if (answer.Stderr.Length > 0 || answer.Stdout.Length > Largest) return false;
        return answer.Code == 0 || answer.Code == 1 && GitLaunch.Command(arguments)[0] == "merge-base";
    }

    private void Keep(string key, int code, string stdout)
    {
        _kept.Keep(key, new Kept(code, stdout), Weight(key, stdout));
        lock (_gate)
        {
            _pending.Add(new Row(key, code, stdout));
            _timer ??= new Timer(_ => Flush(), null, Batch, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Appends the answers kept since the last write; the file is written whole where it is gone or has grown past <see cref="CompactAfter"/>.</summary>
    internal void Flush()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            if (_pending.Count == 0) return;
            var rows = _pending.ToList();
            _pending.Clear();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                if (!File.Exists(_path) || _appended > CompactAfter)
                {
                    Rewrite();
                    return;
                }

                var text = string.Concat(rows.Select(row => JsonSerializer.Serialize(row) + "\n"));
                File.AppendAllText(_path, text);
                _appended += Encoding.UTF8.GetByteCount(text);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                _pending.InsertRange(0, rows);
            }
        }
    }

    private void Rewrite()
    {
        var text = new StringBuilder(JsonSerializer.Serialize(new FormatLine(Version))).Append('\n');
        foreach (var (key, value) in _kept.Entries())
        {
            if (value is Kept kept) text.Append(JsonSerializer.Serialize(new Row(key, kept.Code, kept.Stdout))).Append('\n');
        }

        AtomicFile.WriteText(_path, text.ToString());
        _appended = Encoding.UTF8.GetByteCount(text.ToString());
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var lines = AtomicFile.ReadText(_path).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0 || !Versioned(lines[0]))
            {
                File.Delete(_path);
                return;
            }

            foreach (var line in lines.Skip(1))
            {
                try
                {
                    if (JsonSerializer.Deserialize<Row>(line) is { K: { } key, O: { } stdout } row) _kept.Keep(key, new Kept(row.C, stdout), Weight(key, stdout));
                }
                catch (JsonException)
                {
                    // A row that does not read is dropped.
                }
            }

            _appended = new FileInfo(_path).Length;
            if (_appended > CompactAfter) Rewrite();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // A file that cannot be read keeps nothing.
        }
    }

    private static bool Versioned(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<FormatLine>(line)?.V == Version;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static GitObjectMemo Of(string home) => Homes.GetOrAdd(Path.GetFullPath(home), full => new GitObjectMemo(full));

    private static long Weight(string key, string stdout) => 2L * (key.Length + stdout.Length) + 96;

    private sealed record Kept(int Code, string Stdout);

    private sealed record Row(string K, int C, string O);

    private sealed record FormatLine(int V);

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
