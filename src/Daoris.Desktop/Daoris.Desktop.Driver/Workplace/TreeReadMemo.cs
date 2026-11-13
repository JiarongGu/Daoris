using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The room's kept tree judgements: git is asked again where the proof's inputs or the working files moved.
/// </summary>
/// <remarks>
/// <para><b>What a read rests on, read from git's files, no git started.</b> The tree's own: its <c>.git</c> link, its
/// <c>HEAD</c> and the commit of the branch it names. Its repository's: every branch and remote-tracking ref the walk counts as
/// holding work, which is every one but a session's (<c>daoris/</c>), loose and packed. And the home's: the landings record
/// the squash and content proofs read, and the choices the line is read from. A repository whose refs are not files
/// (<c>reftable</c>), a tree that is no linked tree, and a file that cannot be read are walked as before, and nothing is kept.</para>
///
/// <para>The working files and index are tracked by <see cref="TreeReadChanges"/>. An unchanged generation keeps the status
/// and any content-held work too; an edit asks those again without repeating a proof whose refs still stand. A failed status
/// or a change during the read keeps no file generation. This memo serves observations alone: an act still judges afresh.</para>
///
/// <para><b>The line moving on.</b> A landing moves the line, which would walk every tree of the repository again. A tree whose
/// walk counted no commit stays at none where its own stamp is the same and its repository's refs only moved forward since:
/// none went, and each that moved is a descendant of where it stood (one <c>merge-base --is-ancestor</c> a moved ref, a pass),
/// since a commit a ref held is still held then. A tree that counted commits is walked again.</para>
/// </remarks>
internal sealed class TreeReadMemo(IChangeSource? source = null, Func<string, TreeReadStamp?>? stamp = null) : IDisposable
{
    private static readonly ConcurrentDictionary<string, TreeReadMemo> Homes = new(StringComparer.OrdinalIgnoreCase);

    // Each tree's last kept read, by its full path.
    private readonly ConcurrentDictionary<string, Kept> _trees = new(StringComparer.OrdinalIgnoreCase);

    // The refs each kept read is taken at, by their version, so a later pass can tell whether they only moved forward.
    private readonly ConcurrentDictionary<string, CountedRefs> _versions = new(StringComparer.Ordinal);

    internal TreeReadChanges Changes { get; } = new(source ?? new FolderWatcherSource());

    internal static void Release(string home)
    {
        if (Homes.TryRemove(Path.GetFullPath(home), out var memo)) memo.Dispose();
    }

    public void Dispose() => Changes.Dispose();

    /// <summary>The memo of <paramref name="home"/>'s trees, one for the process.</summary>
    internal static TreeReadMemo Of(string home) => Homes.GetOrAdd(Path.GetFullPath(home), _ => new TreeReadMemo());

    /// <summary>One read of some trees through <paramref name="trees"/>: each repository's refs and the home's records read once.</summary>
    internal TreeReadPass Begin(SessionTrees trees, LookTimes? times = null)
    {
        Changes.Prune();
        // What no kept read rests on any more is let go: a gone tree's read, then a version no read is taken at.
        foreach (var tree in _trees.Keys)
        {
            if (!Directory.Exists(tree)) _trees.TryRemove(tree, out _);
        }

        var used = _trees.Values.Select(kept => kept.Refs).ToHashSet(StringComparer.Ordinal);
        foreach (var version in _versions.Keys)
        {
            if (!used.Contains(version)) _versions.TryRemove(version, out _);
        }

        return new TreeReadPass(this, trees, times, stamp);
    }

    internal bool TryLast(string tree, out Kept kept) => _trees.TryGetValue(tree, out kept!);

    internal void Keep(string tree, Kept kept, CountedRefs refs)
    {
        _versions.TryAdd(refs.Version, refs);
        _trees[tree] = kept;
    }

    internal void Forget(string tree) => _trees.TryRemove(tree, out _);

    internal CountedRefs? Version(string version) => _versions.GetValueOrDefault(version);

    /// <summary>A tree's kept read: its own stamp, its repository's refs version and the home's stamp it is taken at, and its work.</summary>
    internal sealed record Kept(string Tree, string Refs, string Home, TreeWork Work)
    {
        internal TreeReadChanges.Version? Files { get; init; }
        /// <summary>A held tree's repository's declared output, once read; it rests on what <see cref="Refs"/> and <see cref="Home"/> hold.</summary>
        internal IReadOnlyList<System.Text.RegularExpressions.Regex>? Declared { get; init; }
    }
}

/// <summary>
/// A repository's refs a tree's walk counts as holding work (<see cref="TreeFollows.UnlandedAsync"/>'s, and the content proof's
/// line and branches): each local branch outside <c>daoris/</c> and each remote-tracking one outside <c>*/daoris/*</c>, with what it
/// holds, and one version for them all. <see cref="Packed"/> is every packed ref, which a tree's own branch may be among.
/// </summary>
internal sealed record CountedRefs(string CommonDir, string Version, IReadOnlyDictionary<string, string> Refs)
{
    internal IReadOnlyDictionary<string, string> Packed { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Whether a walk counts <paramref name="name"/> as holding work, as its <c>--exclude</c>s say.</summary>
    internal static bool Counted(string name) =>
        name.StartsWith("refs/heads/", StringComparison.Ordinal)
            ? !name["refs/heads/".Length..].StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal)
            : name.StartsWith("refs/remotes/", StringComparison.Ordinal)
              && !name["refs/remotes/".Length..].Contains("/" + TreeFollows.SessionPrefix, StringComparison.Ordinal);

    /// <summary>The refs of the repository whose common dir is <paramref name="common"/>, from its files; null where they are not files or cannot be read.</summary>
    internal static CountedRefs? Read(string common)
    {
        try
        {
            if (Directory.Exists(Path.Combine(common, "reftable"))) return null;
            var packed = new Dictionary<string, string>(StringComparer.Ordinal);
            var packedFile = Path.Combine(common, "packed-refs");
            if (File.Exists(packedFile))
            {
                foreach (var line in File.ReadAllLines(packedFile))
                {
                    if (line.Length == 0 || line[0] is '#' or '^') continue;
                    var space = line.IndexOf(' ');
                    if (space > 0) packed[line[(space + 1)..].Trim()] = line[..space];
                }
            }

            var refs = packed.Where(each => Counted(each.Key)).ToDictionary(each => each.Key, each => each.Value, StringComparer.Ordinal);
            foreach (var kind in (string[])["heads", "remotes"])
            {
                var folder = Path.Combine(common, "refs", kind);
                if (!Directory.Exists(folder)) continue;
                foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".lock", StringComparison.Ordinal)) continue;
                    var name = "refs/" + Path.GetRelativePath(Path.Combine(common, "refs"), file).Replace('\\', '/');
                    if (!Counted(name)) continue;
                    // A loose ref is written whole by a rename, so an empty one is a file git is not done with: nothing is kept.
                    if (File.ReadAllText(file).Trim() is not { Length: > 0 } value) return null;
                    refs[name] = value;
                }
            }

            var version = TreeReadPass.Hash(string.Join('\n', refs.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => $"{each.Key} {each.Value}")));
            return new CountedRefs(Path.GetFullPath(common), version, refs) { Packed = packed };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}

internal readonly record struct TreeReadStamp(string Tree, CountedRefs Refs, string GitDir, string Head)
{
    internal string? Branch { get; init; }
}

/// <summary>One read of some trees, sharing the repository and home stamps across its concurrent tree judgements.</summary>
internal sealed class TreeReadPass(TreeReadMemo memo, SessionTrees trees, LookTimes? times = null, Func<string, TreeReadStamp?>? readStamp = null)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CountedRefs?> _repositories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<bool>> _forward = new(StringComparer.Ordinal);
    private string? _home;

    /// <summary>
    /// The kept observation while its inputs stand, else git's judgement.
    /// </summary>
    internal async Task<TreeWork?> WorkAsync(string tree, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string full;
        try
        {
            full = Path.GetFullPath(tree);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return await trees.WorkAsync(tree, ct).ConfigureAwait(false);
        }

        if (!trees.Holds(full) || TreeFolders.TreeGone(full) || (readStamp is null ? Stamp(full) : readStamp(full)) is not { } stamp)
        {
            memo.Forget(full);
            return await trees.WorkAsync(tree, ct).ConfigureAwait(false);
        }

        var home = Home();
        var files = memo.Changes.Read(full, stamp.GitDir);
        var watch = Stopwatch.StartNew();
        if (memo.TryLast(full, out var kept) && kept.Tree == stamp.Tree && kept.Home == home
            && (kept.Refs == stamp.Refs.Version || kept.Work.Commits == 0 && await ForwardAsync(kept.Refs, stamp.Refs, full, ct).ConfigureAwait(false)))
        {
            if (files is not null && kept.Files == files)
            {
                times?.AddTree(full, "kept", watch.ElapsedMilliseconds);
                memo.Keep(full, kept with { Refs = stamp.Refs.Version }, stamp.Refs);
                return kept.Work;
            }

            // No file generation to trust, or files moved: ask git as before.
            // One status: a held tree's own answer lists its paths.
            TreeWork? work = kept.Work;
            var declared = kept.Declared;
            int? uncommitted = null;
            if (kept.Work.Held is { } held)
            {
                var (again, found, paths) = await trees.HeldAgainAsync(full, stamp.Refs.CommonDir, held, declared, ct).ConfigureAwait(false);
                work = again is null ? null : work with { Held = again };
                declared = found;
                uncommitted = paths;
                times?.AddTree(full, "holds", watch.ElapsedMilliseconds);
            }

            if (work is not null && uncommitted is null)
            {
                watch.Restart();
                uncommitted = await trees.StatusCountAsync(full, ct).ConfigureAwait(false);
                times?.AddTree(full, "status", watch.ElapsedMilliseconds);
            }

            if (work is not null && uncommitted is null)
            {
                memo.Forget(full);
                return await trees.WorkAsync(tree, ct).ConfigureAwait(false);
            }

            if (work is not null) work = work with { Uncommitted = uncommitted };

            if (work is not null)
            {
                memo.Keep(full, kept with { Refs = stamp.Refs.Version, Work = work, Declared = declared, Files = Unmoved(full, stamp.GitDir, files) }, stamp.Refs);
                return work;
            }
        }

        watch.Restart();
        // Head and branch from the tree's files: the walk asks its one status last.
        var status = memo.TryLast(full, out kept) && kept.Tree == stamp.Tree && files is not null && kept.Files == files
            && kept.Work.Uncommitted is { } changed
                ? new TreeStatus(changed, WorkingTree.IsCommitId(stamp.Head) ? stamp.Head : null, kept.Work.Branch)
                : WorkingTree.IsCommitId(stamp.Head) ? TreeStatus.Unasked(stamp.Head, stamp.Branch) : null;
        var back = new TreeWalkBack();
        var walked = await trees.WorkWithStatusAsync(tree, status, back, ct).ConfigureAwait(false);
        times?.AddTree(full, "walk", watch.ElapsedMilliseconds);
        if (walked is { Commits: not null })
        {
            // Keep the declaration with the proof, so an edit need only judge what lies beyond its commits.
            var declared = walked.Held is null ? null : back.Declared ?? await DeclaredOrNullAsync(full, stamp.Refs.CommonDir, ct).ConfigureAwait(false);
            memo.Keep(full, new TreeReadMemo.Kept(stamp.Tree, stamp.Refs.Version, home, walked)
            {
                Declared = declared, Files = walked.Uncommitted is null ? null : Unmoved(full, stamp.GitDir, files),
            }, stamp.Refs);
        }
        else memo.Forget(full);
        return walked;
    }

    private TreeReadChanges.Version? Unmoved(string tree, string git, TreeReadChanges.Version? before) =>
        before is not null && memo.Changes.Read(tree, git) == before ? before : null;

    private async Task<IReadOnlyList<System.Text.RegularExpressions.Regex>?> DeclaredOrNullAsync(string tree, string common, CancellationToken ct)
    {
        try
        {
            return await trees.DeclaredAsync(tree, common, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// What the tree's read rests on, from git's files: its link, its <c>HEAD</c> and the commit of the branch it names, with its
    /// repository's counted refs. Null where it is no linked tree, its refs are no files, or a file cannot be read.
    /// </summary>
    private TreeReadStamp? Stamp(string tree)
    {
        try
        {
            var link = Path.Combine(tree, ".git");
            if (!File.Exists(link)) return null;
            var linkText = File.ReadAllText(link).Trim();
            if (!linkText.StartsWith("gitdir:", StringComparison.Ordinal)) return null;
            var gitDir = Path.GetFullPath(Path.Combine(tree, linkText["gitdir:".Length..].Trim()));
            var commonFile = Path.Combine(gitDir, "commondir");
            if (!File.Exists(commonFile)) return null;
            var common = Path.GetFullPath(Path.Combine(gitDir, File.ReadAllText(commonFile).Trim()));
            var head = File.ReadAllText(Path.Combine(gitDir, "HEAD")).Trim();
            if (Repository(common) is not { } refs) return null;
            var at = head;
            string? branch = null;
            if (head.StartsWith("ref:", StringComparison.Ordinal))
            {
                var name = head["ref:".Length..].Trim();
                if (name.StartsWith("refs/heads/", StringComparison.Ordinal)) branch = name["refs/heads/".Length..];
                var loose = Path.Combine(common, name.Replace('/', Path.DirectorySeparatorChar));
                at = File.Exists(loose) ? File.ReadAllText(loose).Trim() : refs.Packed.GetValueOrDefault(name, "none");
                if (at.Length == 0) return null;
            }

            return new TreeReadStamp(string.Join('\n', linkText, head, at), refs, gitDir, at) { Branch = branch };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private CountedRefs? Repository(string common)
    {
        lock (_gate)
        {
            if (!_repositories.TryGetValue(common, out var refs)) _repositories[common] = refs = CountedRefs.Read(common);
            return refs;
        }
    }

    /// <summary>The home's records a walk reads (the landings, the choices the line is read from), as one stamp, read once a pass.</summary>
    private string Home()
    {
        lock (_gate)
        {
            return _home ??= Hash(string.Join('\n', FileStamp(trees.LandingsPath), FileStamp(trees.ConfigPath)));
        }
    }

    private static string FileStamp(string path)
    {
        try
        {
            return File.Exists(path) ? Hash(AtomicFile.ReadText(path)) : "none";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Unread is a stamp of its own, never the last one: a read kept then is never handed on.
            return Guid.NewGuid().ToString("N");
        }
    }

    /// <summary>
    /// Whether the refs of the older version only moved forward to the newer one: none went, and each that moved stands
    /// on a descendant of where it stood, as git says. Asked once a pass for each version.
    /// </summary>
    private Task<bool> ForwardAsync(string was, CountedRefs now, string cwd, CancellationToken ct)
    {
        lock (_gate)
        {
            if (!_forward.TryGetValue(was, out var asked)) _forward[was] = asked = AskForwardAsync(memo.Version(was), now, cwd, ct);
            return asked;
        }
    }

    private static async Task<bool> AskForwardAsync(CountedRefs? was, CountedRefs now, string cwd, CancellationToken ct)
    {
        if (was is null || !string.Equals(was.CommonDir, now.CommonDir, StringComparison.OrdinalIgnoreCase)) return false;
        var moved = new List<(string From, string To)>();
        foreach (var (name, from) in was.Refs)
        {
            if (!now.Refs.TryGetValue(name, out var to)) return false;
            if (to == from) continue;
            if (!WorkingTree.IsCommitId(from) || !WorkingTree.IsCommitId(to)) return false;
            moved.Add((from, to));
        }

        foreach (var (from, to) in moved.Distinct())
        {
            var (code, _, _) = await WorkingTree.GitAsync(cwd, ["merge-base", "--is-ancestor", from, to], ct).ConfigureAwait(false);
            if (code != 0) return false;
        }

        return true;
    }

    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
