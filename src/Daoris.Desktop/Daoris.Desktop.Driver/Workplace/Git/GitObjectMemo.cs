using System.Collections.Concurrent;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The answers of git reads that only object ids decide (<see cref="GitObjectArguments"/>: is-ancestor, merge-base, a count over ids, the names two
/// commits differ in, a patch's equivalence, a commit's listing), kept under the git service by the repository's common folder and the
/// read's words, in memory and in the home, so a read asked once is never asked again: a hit starts no git, takes no place and opens no
/// library. A commit never changes, so an answer has no age; only the size bounds it, the least recently used going first.
/// </summary>
/// <remarks>
/// <para><b>Only what git answered whole.</b> An exit 0 with nothing on the error stream is kept, and an exit 1 where it is an answer
/// (<c>merge-base</c>'s no and its no base, a search that found nothing); a failure, a timeout and an answer bigger than
/// <see cref="Largest"/> characters are asked again.</para>
/// <para><b>Nothing a ref, HEAD, the index or a working tree decides.</b> A read that names one never qualifies. A repository where a
/// replacement can stand, or a shallow or grafted one (its history is another from one fetch to the next), keeps nothing.</para>
/// <para><b>Only where a home is named</b> (<see cref="ProgramHome"/>): a unit's process with no home keeps nothing, so a scripted git stays the test's own.</para>
/// <para><b>Written in batches.</b> A kept answer marks the memo dirty and one write follows <see cref="Batch"/> later, whatever came between;
/// <see cref="Flush"/> and the process's end write what is left. Never a write for an ask. The file is <c>memo/git-objects.json</c>.</para>
/// </remarks>
internal sealed class GitObjectMemo
{
    /// <summary>What the kept answers may weigh together (<see cref="GitReadCache"/>'s estimate): the file is about a quarter of it.</summary>
    internal const long Bound = 32L * 1024 * 1024;

    /// <summary>The longest answer kept, in characters.</summary>
    internal const int Largest = 256 * 1024;

    /// <summary>How long after the first unwritten answer the memo is written.</summary>
    internal static readonly TimeSpan Batch = TimeSpan.FromSeconds(20);

    private static readonly ConcurrentDictionary<string, GitObjectMemo> Homes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly AsyncLocal<string?> s_home = new();

    private readonly GitReadCache _kept = new(Bound);
    private readonly string? _path;
    private readonly object _gate = new();
    private Timer? _timer;
    private bool _dirty;

    static GitObjectMemo() => AppDomain.CurrentDomain.ProcessExit += (_, _) =>
    {
        foreach (var memo in Homes.Values) memo.Flush();
    };

    private GitObjectMemo(string folder)
    {
        _path = Path.Combine(folder, "memo", "git-objects.json");
        try
        {
            if (!File.Exists(_path) || JsonSerializer.Deserialize<List<Row>>(AtomicFile.ReadText(_path)) is not { } rows) return;
            foreach (var row in rows) _kept.Keep(row.K, new Kept(row.C, row.O), Weight(row.K, row.O));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // A file that cannot be read keeps nothing.
        }
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

    /// <summary>The commit <paramref name="id"/> peels to (itself where it is one), once a read said so; null where none did.</summary>
    internal static string? PeeledCommit(string common, string id) =>
        Current?._kept.TryGet<Kept>(PeelKey(common, id), out var kept) == true ? kept!.Stdout : null;

    /// <summary>Keeps that <paramref name="id"/> peels to <paramref name="commit"/>: an id names the same object for good.</summary>
    internal static void LearnPeeled(string common, string id, string commit) => Current?.Keep(PeelKey(common, id), 0, commit);

    private static string PeelKey(string common, string id) => "peel\n" + common.ToLowerInvariant() + "\n" + id;

    /// <summary>The repository's common folder and the read's words; null where the repository keeps nothing.</summary>
    internal static string? KeyOf(string root, IReadOnlyList<string> arguments)
    {
        var common = TreeLock.CommonDirOf(root);
        if (GitReplacements.Unfixed(common)) return null;
        var words = arguments.Where(word => word != "--no-optional-locks");
        return common.ToLowerInvariant() + "\n" + string.Join('\0', words);
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
        if (answer.Code == 0) return true;
        var verb = GitLaunch.Command(arguments)[0];
        return answer.Code == 1 && verb is "merge-base" or "grep";
    }

    private void Keep(string key, int code, string stdout)
    {
        _kept.Keep(key, new Kept(code, stdout), Weight(key, stdout));
        lock (_gate)
        {
            _dirty = true;
            _timer ??= new Timer(_ => Flush(), null, Batch, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Writes the kept answers, least recently used first, unless none is new since the last write.</summary>
    internal void Flush()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            if (!_dirty) return;
            _dirty = false;
            try
            {
                var rows = _kept.Entries().Where(entry => entry.Value is Kept).Select(entry => new Row(entry.Key, ((Kept)entry.Value).Code, ((Kept)entry.Value).Stdout)).ToList();
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                AtomicFile.WriteText(_path, JsonSerializer.Serialize(rows));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Kept in memory alone this time; the next kept answer writes again.
                _dirty = true;
            }
        }
    }

    private static GitObjectMemo Of(string home) => Homes.GetOrAdd(Path.GetFullPath(home), full => new GitObjectMemo(full));

    private static long Weight(string key, string stdout) => 2L * (key.Length + stdout.Length) + 96;

    private sealed record Kept(int Code, string Stdout);

    private sealed record Row(string K, int C, string O);

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
