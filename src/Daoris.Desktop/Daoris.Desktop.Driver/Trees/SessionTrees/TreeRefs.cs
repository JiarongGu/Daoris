using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// The sync's reads of a checkout's refs (PART10j, out of <c>SessionTrees.Sync.cs</c>): where a revision is, whether one holds
/// another, how far apart, where the line stands, whether a branch is on its remote, the fetch and when origin was last heard
/// from, and the bound over many checkouts at once. Static, since none reads anything of the trees' context.
/// </summary>
internal static class TreeRefs
{
    /// <summary>
    /// A credential prompt with nobody at a terminal would wait for ever: git is told to fail instead. The person's
    /// credential helper still answers, since the fetch runs as them.
    /// </summary>
    private static readonly Dictionary<string, string> NoPrompt = new(StringComparer.Ordinal) { ["GIT_TERMINAL_PROMPT"] = "0" };

    /// <summary>
    /// When this checkout last heard from origin (WSR7): the newer of <c>FETCH_HEAD</c>'s time, where it holds a fetch,
    /// and the time <c>origin/&lt;line&gt;</c> last moved here, from its log; null where neither says. Daoris's own fetch
    /// writes no <c>FETCH_HEAD</c>, so its time is the person's own last fetch.
    /// </summary>
    internal static async Task<DateTimeOffset?> LastFetchAsync(string root, string? line, CancellationToken ct)
    {
        DateTimeOffset? fetchHead = null;
        var (pathCode, pathOut, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--git-path", "FETCH_HEAD"], ct).ConfigureAwait(false);
        if (pathCode == 0 && pathOut.Trim() is { Length: > 0 } relative)
        {
            var file = new FileInfo(Path.IsPathRooted(relative) ? relative : Path.Combine(root, relative));
            // An empty one is a fetch that failed before it heard anything: it says when a fetch was tried, not heard.
            if (file.Exists && file.Length > 0) fetchHead = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
        }

        DateTimeOffset? moved = null;
        if (line is not null)
        {
            // `origin/main@{1790781297}`: the log's newest entry, its time in seconds.
            var (logCode, logOut, _) = await WorkingTree.GitAsync(
                root, ["log", "-g", "-1", "--date=unix", "--format=%gd", $"refs/remotes/origin/{line}"], ct).ConfigureAwait(false);
            var at = logOut.LastIndexOf("@{", StringComparison.Ordinal);
            if (logCode == 0 && at >= 0 && long.TryParse(logOut[(at + 2)..].TrimEnd().TrimEnd('}'), out var seconds))
            {
                moved = DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
        }

        return fetchHead is null ? moved : moved is null || fetchHead > moved ? fetchHead : moved;
    }

    /// <summary>How origin is reached, from its address (WSR7): `ssh`, `https`, `http`, `git` or `file`; null for none.</summary>
    /// <remarks>An address with no scheme is ssh's `[user@]host:path` where a colon comes before any slash, after a host of
    /// more than one letter: `C:\repos` is a folder on a drive, not a host.</remarks>
    internal static string? ReachOf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var text = url.Trim();
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme > 0)
        {
            var name = text[..scheme].ToLowerInvariant();
            return name.Contains("ssh", StringComparison.Ordinal) ? "ssh" : name;
        }

        var colon = text.IndexOf(':');
        var slash = text.IndexOfAny(['/', '\\']);
        return colon > 1 && (slash < 0 || colon < slash) ? "ssh" : "file";
    }

    /// <summary><paramref name="work"/> over each item, at most <paramref name="atOnce"/> at a time, the answers in the items' order (WSR7).</summary>
    internal static async Task<IReadOnlyList<TResult>> AtMostAsync<T, TResult>(
        IReadOnlyList<T> items, int atOnce, Func<T, CancellationToken, Task<TResult>> work, CancellationToken ct)
    {
        var answers = new TResult[items.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, items.Count),
            new ParallelOptions { MaxDegreeOfParallelism = atOnce, CancellationToken = ct },
            async (index, token) => answers[index] = await work(items[index], token).ConfigureAwait(false)).ConfigureAwait(false);
        return answers;
    }

    /// <summary>
    /// Fetch the line from <c>origin</c> (WSR6), as the person, with their credentials: the one network step, and it
    /// moves only origin's own refs here. Null where it fetched; otherwise why not, in git's words — and origin's address.
    /// </summary>
    /// <remarks>
    /// <para><b>It writes no <c>FETCH_HEAD</c></b> (WSR7): a fetch that fails empties that file and stamps it with the failure's
    /// time, which made the person's own last fetch unreadable, and only origin's refs are the look's to move.</para>
    /// <para><b>One at a time per line</b> (FABRIC-END6c): a fetch of the line already in flight, whichever path started it, is joined
    /// rather than started again beside it (<see cref="RemoteFetches.OnceAsync"/>).</para>
    /// </remarks>
    internal static async Task<(string? Failed, string? Url)> FetchAsync(string root, string? line, CancellationToken ct) =>
        line is null ? (null, null) : await RemoteFetches.OnceAsync(root, line, token => FetchingAsync(root, line, token), ct).ConfigureAwait(false);

    private static async Task<(string? Failed, string? Url)> FetchingAsync(string root, string line, CancellationToken ct)
    {
        var (remote, url, _) = await WorkingTree.GitAsync(root, ["remote", "get-url", "origin"], ct).ConfigureAwait(false);
        if (remote != 0) return ("there is no `origin` remote here", null);
        var (code, _, err) = await WorkingTree.GitAsync(
            root, ["fetch", "--quiet", "--no-write-fetch-head", "origin", line], NoPrompt, SyncBounds.Fetch, ct).ConfigureAwait(false);
        // FABRIC-END6: every fetch of the line counted, made or failed, so a moment inside its minute fetches nothing more (§A4.3).
        RemoteFetches.Fetched(root, line, DateTimeOffset.UtcNow);
        return (code == 0 ? null : Failure(err), url.Trim());
    }

    /// <summary>
    /// The line's latest as this checkout knows it once fetched (REVIEWBASE2-i): origin's where the line here is behind it or absent,
    /// which is what WSR6's pull would take; else the line here. Null where neither is here. A landing's branch form builds on it
    /// (LANDSYNC1, D87's 2026-10-15 amendment point 3).
    /// </summary>
    internal static async Task<string?> LatestAsync(string root, string line, CancellationToken ct, RefSnapshot? refs = null)
    {
        var here = await CommitOfAsync(root, $"refs/heads/{line}", ct, refs).ConfigureAwait(false);
        var origin = await CommitOfAsync(root, $"refs/remotes/origin/{line}", ct, refs).ConfigureAwait(false);
        if (here is null || origin is null) return here ?? origin;
        return await IsAncestorAsync(root, here, origin, ct).ConfigureAwait(false) ? origin : here;
    }

    /// <summary>Where the line stands as the rest of Daoris reads it — the local branch, else origin's — as a commit, or null.</summary>
    internal static async Task<string?> OntoAsync(string root, string? line, CancellationToken ct, RefSnapshot? refs = null)
    {
        if (line is null || await ComparableAsync(root, line, ct, refs).ConfigureAwait(false) is not { } name) return null;
        return await CommitOfAsync(root, name, ct, refs).ConfigureAwait(false);
    }

    /// <summary>The commit <paramref name="revision"/> names, or null; from <paramref name="refs"/> where it holds the answer (PROC1d-ii).</summary>
    internal static async Task<string?> CommitOfAsync(string root, string revision, CancellationToken ct, RefSnapshot? refs = null)
    {
        if (refs is not null && await refs.CommitAsync(revision, ct).ConfigureAwait(false) is { Known: true } read) return read.Commit;
        var (code, sha, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{revision}^{{commit}}"], ct).ConfigureAwait(false);
        return code == 0 ? sha.Trim() : null;
    }

    internal static async Task<bool> IsAncestorAsync(string root, string ancestor, string of, CancellationToken ct) =>
        (await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", ancestor, of], ct).ConfigureAwait(false)).Code == 0;

    internal static async Task<int> CountAsync(string root, string range, CancellationToken ct)
    {
        var (_, count, _) = await WorkingTree.GitAsync(root, ["rev-list", "--count", range], ct).ConfigureAwait(false);
        return int.TryParse(count.Trim(), out var n) ? n : 0;
    }

    /// <summary>
    /// Whether a branch is on a remote: a remote-tracking branch of its own name, as its upstream or on
    /// <c>origin</c>. An upstream a new branch borrowed from its start point (the line's) is not its own.
    /// </summary>
    internal static async Task<bool> OnRemoteAsync(string root, string branch, CancellationToken ct, RefSnapshot? refs = null)
    {
        if (await CommitOfAsync(root, $"refs/remotes/origin/{branch}", ct, refs).ConfigureAwait(false) is not null) return true;
        // `--symbolic-full-name` answers only an upstream that is there: none configured, or one whose ref is gone, fails.
        if (refs is not null && await refs.UpstreamAsync(branch, ct).ConfigureAwait(false) is { Known: true } read)
        {
            if (read.Full is null) return false;
            if (await refs.HasAsync(read.Full, ct).ConfigureAwait(false) is { } there)
            {
                return there && read.Short!.EndsWith($"/{branch}", StringComparison.Ordinal);
            }
        }

        var (code, upstream, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--abbrev-ref", "--symbolic-full-name", $"{branch}@{{upstream}}"], ct).ConfigureAwait(false);
        return code == 0 && upstream.Trim().EndsWith($"/{branch}", StringComparison.Ordinal);
    }
}
