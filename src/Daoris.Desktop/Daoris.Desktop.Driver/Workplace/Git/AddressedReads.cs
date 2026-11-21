using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Within one act of the pool, a read of git's objects named by full ids alone (a commit's listing, a search of its files, a file's bytes at it)
/// is asked once and every later asker in the act is handed the answer: the file rules are read four times in a take, each reading the same
/// commit's listing, search and two files again beside the ignored tops, which are the only part that the slot's files decide.
/// </summary>
/// <remarks>
/// <para><b>Objects never change,</b> so a write drops nothing (a switch moves HEAD, never what an id names), and a read that names HEAD, a
/// ref, a working tree or an index is never kept: <see cref="Addressed"/> and <see cref="AddressedBytes"/> admit nothing else. Only an
/// answer git gave is kept: a git that did not start, a read cancelled or one that threw is asked again.</para>
/// <para>Flow-scoped like <see cref="GitReadOnce"/>: a flow beside the act shares nothing with it.</para>
/// </remarks>
internal sealed class AddressedReads
{
    private static readonly AsyncLocal<AddressedReads?> Current = new();

    private static readonly Regex Whole = new(@"^([0-9a-f]{40}|[0-9a-f]{64})$", RegexOptions.CultureInvariant);

    private static readonly Regex Question = new(@"^([0-9a-f]{40}|[0-9a-f]{64})(:.*)?$", RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private readonly object _gate = new();
    private readonly Dictionary<string, Task<(int Code, string Stdout, string Stderr)>> _text = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<(byte[] Bytes, int Count)?>> _bytes = new(StringComparer.Ordinal);
    private CancellationToken _flow;
    private volatile bool _closed;

    private static AddressedReads? Live => Current.Value is { _closed: false } reads ? reads : null;

    /// <summary>Until disposed, this flow asks each read of objects by their ids once, under <paramref name="flow"/>; within a flow that already does, that one's.</summary>
    internal static IDisposable Within(CancellationToken flow = default)
    {
        if (Live is not null) return Nothing.Instance;
        var reads = new AddressedReads { _flow = flow };
        Current.Value = reads;
        return new Ending(reads);
    }

    /// <summary>Whether <paramref name="arguments"/> list or search the files of one commit named by its full id, and nothing the working tree decides.</summary>
    internal static bool Addressed(IReadOnlyList<string> arguments)
    {
        var at = 0;
        while (at < arguments.Count && arguments[at].StartsWith('-')) at += arguments[at] is "-c" or "-C" ? 2 : 1;
        if (at >= arguments.Count) return false;
        var rest = arguments.Skip(at + 1).ToList();
        return arguments[at] switch
        {
            "ls-tree" => rest.Count(each => !each.StartsWith('-')) == 1 && rest.All(each => each is "-r" or "-z" or "--full-tree" || Whole.IsMatch(each)),
            "grep" => SearchOf(rest),
            _ => false,
        };
    }

    /// <summary>A search's words: its options, its patterns, the one commit by its full id, and the paths after <c>--</c>.</summary>
    private static bool SearchOf(List<string> rest)
    {
        var commits = 0;
        for (var at = 0; at < rest.Count; at++)
        {
            var word = rest[at];
            if (word == "--") return commits == 1;
            if (word == "-e") at++;
            else if (Whole.IsMatch(word)) commits++;
            else if (word is not ("-I" or "-F" or "-z" or "--no-color")) return false;
        }

        return commits == 1;
    }

    /// <summary>Whether a <c>cat-file --batch</c> question is lines that each name an object by its full id, or a path at one.</summary>
    internal static bool AddressedBytes(IReadOnlyList<string> arguments, string? input) =>
        arguments.SequenceEqual(["cat-file", "--batch"], StringComparer.Ordinal) && !string.IsNullOrEmpty(input)
        && input.Split('\n', StringSplitOptions.RemoveEmptyEntries).All(line => Question.IsMatch(line));

    /// <summary>
    /// <paramref name="ask"/>'s answer, or the one this act already holds; null where no act holds reads or the ask is not one of objects by their ids.
    /// </summary>
    internal static Task<(int Code, string Stdout, string Stderr)>? Asked(
        string root, IReadOnlyList<string> arguments, Func<CancellationToken, Task<(int Code, string Stdout, string Stderr)>> ask, CancellationToken ct)
    {
        if (Live is not { } reads || !Addressed(arguments)) return null;
        // A search that found nothing exits 1: git's answer, not a failure.
        var searches = GitLaunch.Command(arguments)[0] == "grep";
        return GitMemoAsks.Joined(
            Kept(reads._gate, reads._text, KeyOf(root, arguments, null), GitMemoAsks.Under(ask, reads._flow), done => done.Code == 0 || searches && done.Code == 1), ct);
    }

    /// <summary>The bytes <paramref name="ask"/> read, or the ones this act already holds for the same question and limit.</summary>
    internal static Task<(byte[] Bytes, int Count)?>? AskedBytes(
        string root, IReadOnlyList<string> arguments, int limit, string? input, Func<CancellationToken, Task<(byte[] Bytes, int Count)?>> ask, CancellationToken ct)
    {
        if (Live is not { } reads || !AddressedBytes(arguments, input)) return null;
        return GitMemoAsks.Joined(Kept(reads._gate, reads._bytes, KeyOf(root, arguments, $"{limit}\n{input}"), GitMemoAsks.Under(ask, reads._flow), done => done is not null), ct);
    }

    private static string KeyOf(string root, IReadOnlyList<string> arguments, string? input) =>
        GitFacts.Folder(root) + "\n" + string.Join('\0', arguments) + "\n" + input;

    private static Task<T> Kept<T>(object gate, Dictionary<string, Task<T>> table, string key, Func<Task<T>> ask, Func<T, bool> keeps)
    {
        Task<T> task;
        lock (gate)
        {
            if (table.TryGetValue(key, out var known) && (!known.IsCompleted || known.IsCompletedSuccessfully && keeps(known.Result))) return known;
            table[key] = task = ask();
        }

        _ = task.ContinueWith(done =>
        {
            if (done.IsCompletedSuccessfully && keeps(done.Result)) return;
            lock (gate)
            {
                if (table.TryGetValue(key, out var now) && ReferenceEquals(now, task)) table.Remove(key);
            }
        }, TaskScheduler.Default);
        return task;
    }

    private sealed class Ending(AddressedReads reads) : IDisposable
    {
        public void Dispose()
        {
            reads._closed = true;
            Current.Value = null;
        }
    }

    private sealed class Nothing : IDisposable
    {
        public static readonly Nothing Instance = new();

        public void Dispose()
        {
        }
    }
}
