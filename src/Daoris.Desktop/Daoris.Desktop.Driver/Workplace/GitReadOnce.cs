namespace Daoris.Driver;

/// <summary>
/// Within one read that writes nothing (a review's), each question of git is asked once: a question asked again is handed the first
/// answer; where a folder's working tree is, the repository it shares and the branch it holds are read in one <c>rev-parse</c>; and the
/// commit a branch or origin's copy of one points at is read from one listing of the folder's refs. A review made 34 to 42 git starts,
/// a third of them a question another of its stages had asked, or one of those three.
/// </summary>
/// <remarks>
/// <para><b>Reads only.</b> A command that may move a ref (<see cref="GitCommands.Writes"/>) is asked as written and drops what is
/// kept, and only an answer git gave is kept: a git that did not start, a read cancelled or one that threw is asked again.</para>
/// <para><b>One moment.</b> Two stages that ask one question see one answer, where each saw its own a moment apart before. A ref
/// answered from the listing is answered as <c>rev-parse --verify --quiet</c> would: its commit, or nothing with code 1; a name the
/// listing holds only blind to case, or one that is no commit, is asked of git.</para>
/// </remarks>
internal sealed class GitReadOnce
{
    private static readonly AsyncLocal<GitReadOnce?> Current = new();

    /// <summary>The one question that answers the three layout reads below, line by line, in their order.</summary>
    internal static readonly string[] Layout = ["rev-parse", "--path-format=absolute", "--git-common-dir", "--show-toplevel", "--abbrev-ref", "HEAD"];

    private static readonly string[][] LayoutParts =
    [
        ["rev-parse", "--path-format=absolute", "--git-common-dir"],
        ["rev-parse", "--show-toplevel"],
        ["rev-parse", "--abbrev-ref", "HEAD"],
    ];

    private const string CommitSuffix = "^{commit}";

    private readonly object _gate = new();
    private Dictionary<string, Task<(int Code, string Stdout, string Stderr)>> _asked = new(StringComparer.Ordinal);
    private volatile bool _closed;

    private static GitReadOnce? Live => Current.Value is { _closed: false } once ? once : null;

    private CancellationToken _flow;

    /// <summary>Until disposed, this flow asks each question of git once, under <paramref name="flow"/>. Within a flow that already does, this is that one's.</summary>
    internal static IDisposable Within(CancellationToken flow = default)
    {
        if (Live is not null) return Nothing.Instance;
        var once = new GitReadOnce { _flow = flow };
        Current.Value = once;
        return new Ending(once);
    }

    /// <summary>
    /// <paramref name="ask"/>'s answer, or the one this flow already holds, or one read from a question that holds it; null where this
    /// flow keeps none, and the caller asks git as before.
    /// </summary>
    internal static Task<(int Code, string Stdout, string Stderr)>? Asked(
        string root, IReadOnlyList<string> arguments, Func<CancellationToken, Task<(int Code, string Stdout, string Stderr)>> ask, CancellationToken ct)
    {
        if (Live is not { } once) return null;
        if (GitCommands.Writes(arguments))
        {
            once.Drop();
            return null;
        }

        var folder = GitFacts.Folder(root);
        var flow = GitMemoAsks.Under(ask, once._flow);
        if (PartOf(arguments) is var part and >= 0) return GitMemoAsks.Joined(once.LayoutAsync(root, folder, part, flow, ct), ct);
        if (BranchCommit(arguments) is { } reference) return GitMemoAsks.Joined(once.CommitAsync(root, folder, arguments, reference, flow, ct), ct);
        return GitMemoAsks.Joined(once.AskAsync(Key(folder, arguments), flow), ct);
    }

    /// <summary>
    /// The streamed read of where a folder's working tree is, handed the kept answer; null where this flow keeps none or the question
    /// is another, and the caller reads git as before.
    /// </summary>
    internal static Task<int>? Streamed(string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        if (Live is null || PartOf(arguments) != 1) return null;
        return StreamedAsync();

        async Task<int> StreamedAsync()
        {
            var (code, stdout, _) = await WorkingTree.GitAsync(root, arguments, ct).ConfigureAwait(false);
            if (code < 0) return code;
            return take(stdout.AsMemory()) ? code : 0;
        }
    }

    private static int PartOf(IReadOnlyList<string> arguments)
    {
        for (var at = 0; at < LayoutParts.Length; at++)
        {
            if (arguments.SequenceEqual(LayoutParts[at], StringComparer.Ordinal)) return at;
        }

        return -1;
    }

    private static string? BranchCommit(IReadOnlyList<string> arguments) =>
        arguments is ["rev-parse", "--verify", "--quiet", var asked] && asked.EndsWith(CommitSuffix, StringComparison.Ordinal)
        && RefSnapshot.Covers(asked[..^CommitSuffix.Length])
            ? asked[..^CommitSuffix.Length]
            : null;

    private static string Key(string folder, IReadOnlyList<string> arguments) => folder + "\n" + string.Join('\0', arguments);

    /// <summary>One line of the layout's one question, where git gave all three; else the part asked of git on its own.</summary>
    private async Task<(int Code, string Stdout, string Stderr)> LayoutAsync(
        string root, string folder, int part, Func<Task<(int Code, string Stdout, string Stderr)>> ask, CancellationToken ct)
    {
        // Asked through the runner, so it is kept here under its own arguments, as the offer's own asking of it is.
        var (code, stdout, _) = await WorkingTree.GitAsync(root, Layout, ct).ConfigureAwait(false);
        var lines = stdout.Split('\n');
        if (code == 0 && lines.Length == LayoutParts.Length + 1 && lines[^1].Length == 0 && lines[..^1].All(line => line.Length > 0))
        {
            return (0, lines[part] + "\n", "");
        }

        return await AskAsync(Key(folder, LayoutParts[part]), ask).ConfigureAwait(false);
    }

    /// <summary>The commit <paramref name="reference"/> points at, read from the folder's one listing of its refs where it answers.</summary>
    private async Task<(int Code, string Stdout, string Stderr)> CommitAsync(
        string root, string folder, IReadOnlyList<string> arguments, string reference,
        Func<Task<(int Code, string Stdout, string Stderr)>> ask, CancellationToken ct)
    {
        // A listing from another folder of this repository reading the same configuration serves as well as one read here.
        var common = GitStamp.ContextOf(root);
        RefSnapshot.Refs? refs;
        lock (_gate) refs = common is null ? null : _listings.FirstOrDefault(each => string.Equals(each.Context, common, StringComparison.Ordinal))?.Refs;
        refs ??= RefSnapshot.ListedFor(root)
            ?? (await RefSnapshot.ReadAsync(root, ct).ConfigureAwait(false) is { } listed ? RefSnapshot.Parse(listed) : null);
        if (refs is not null)
        {
            lock (_gate)
            {
                if (!_listings.Any(each => ReferenceEquals(each.Refs, refs))) _listings.Add(new Listing(common, refs));
            }
            if (refs.ByName.TryGetValue(reference, out var entry))
            {
                if (entry.Type == "commit") return (0, entry.Object + "\n", "");
            }
            else if (!refs.Blind.Contains(reference))
            {
                return (1, "", "");
            }
        }

        return await AskAsync(Key(folder, arguments), ask).ConfigureAwait(false);
    }

    private Task<(int Code, string Stdout, string Stderr)> AskAsync(string key, Func<Task<(int Code, string Stdout, string Stderr)>> ask)
    {
        Task<(int Code, string Stdout, string Stderr)> task;
        Dictionary<string, Task<(int Code, string Stdout, string Stderr)>> held;
        lock (_gate)
        {
            held = _asked;
            if (held.TryGetValue(key, out var known) && (!known.IsCompleted || known.IsCompletedSuccessfully && known.Result.Code >= 0))
            {
                return known;
            }

            held[key] = task = ask();
        }

        _ = task.ContinueWith(done =>
        {
            if (done.IsCompletedSuccessfully && done.Result.Code >= 0) return;
            lock (_gate)
            {
                if (held.TryGetValue(key, out var now) && ReferenceEquals(now, task)) held.Remove(key);
            }
        }, TaskScheduler.Default);
        return task;
    }

    private void Drop()
    {
        lock (_gate)
        {
            _asked = new Dictionary<string, Task<(int Code, string Stdout, string Stderr)>>(StringComparer.Ordinal);
            _listings.Clear();
        }
    }

    // The refs listings this flow read to answer a branch's commit, kept so a commit's tree is read from them (see TreeOf); dropped with a write.
    private readonly List<Listing> _listings = [];

    private sealed record Listing(string? Context, RefSnapshot.Refs Refs);

    /// <summary>
    /// The tree of <paramref name="commit"/> where a listing this flow read in a folder of <paramref name="folder"/>'s configuration has
    /// a ref at exactly it; null where none has, or no read-once flow is live. Nothing is started.
    /// </summary>
    internal static string? TreeOf(string folder, string commit)
    {
        if (Live is not { } once || GitStamp.ContextOf(folder) is not { } context) return null;
        lock (once._gate)
        {
            return once._listings.Where(each => string.Equals(each.Context, context, StringComparison.Ordinal))
                .Select(each => RefSnapshot.TreeIn(each.Refs, commit)).FirstOrDefault(tree => tree is not null);
        }
    }

    private sealed class Ending(GitReadOnce once) : IDisposable
    {
        public void Dispose()
        {
            once._closed = true;
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
