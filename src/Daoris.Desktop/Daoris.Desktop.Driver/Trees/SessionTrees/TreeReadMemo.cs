using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Each kept tree's last read, by what it was read from (TREELOOK1): the trees fact walks a tree again only where something its
/// work is judged from moved since, and asks one <c>git status</c> of every other.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> A tree's walk (<see cref="TreeWorkReads.WorkAsync"/>) is five git starts or more, one after another: on the
/// install, one repository's part of the trees fact read its 143-145 kept trees in 45-110 s, past its bound, every few minutes,
/// where nearly every tree held what it held the last time.</para>
///
/// <para><b>What a read rests on, read from git's files, no git started.</b> The tree's own: its <c>.git</c> link, its
/// <c>HEAD</c> and the commit of the branch it names. Its repository's: every branch and remote-tracking ref the walk counts as
/// holding work, which is every one but a session's (<c>daoris/</c>), loose and packed. And the home's: the landings record
/// the squash and content proofs read, and the choices the line is read from. A repository whose refs are not files
/// (<c>reftable</c>), a tree that is no linked tree, and a file that cannot be read are walked as before, and nothing is kept.</para>
///
/// <para>🔴 <b>Never unchanged where it changed.</b> What the working tree holds is in no file of git's, so <c>git status</c> is
/// asked of every tree every time, and a tree whose status git cannot give is walked. A read is kept only where git counted its
/// commits and nothing holds them by content (no <see cref="TreeWork.Held"/>), since that judgement reads the working tree's
/// ignored paths too. A tree's stamp is taken before its walk, so a change during it is a different stamp the next time.</para>
///
/// <para><b>The line moving on.</b> A landing moves the line, which would walk every tree of the repository again. A tree whose
/// walk counted no commit stays at none where its own stamp is the same and its repository's refs only moved forward since:
/// none went, and each that moved is a descendant of where it stood (one <c>merge-base --is-ancestor</c> a moved ref, a pass),
/// since a commit a ref held is still held then. A tree that counted commits is walked again.</para>
/// </remarks>
internal sealed class TreeReadMemo
{
    private static readonly ConcurrentDictionary<string, TreeReadMemo> Homes = new(StringComparer.OrdinalIgnoreCase);

    // Each tree's last kept read, by its full path.
    private readonly ConcurrentDictionary<string, Kept> _trees = new(StringComparer.OrdinalIgnoreCase);

    // The refs each kept read was taken at, by their version, so a later pass can tell whether they only moved forward.
    private readonly ConcurrentDictionary<string, CountedRefs> _versions = new(StringComparer.Ordinal);

    /// <summary>The memo of <paramref name="home"/>'s trees, one for the process.</summary>
    internal static TreeReadMemo Of(string home) => Homes.GetOrAdd(Path.GetFullPath(home), _ => new TreeReadMemo());

    /// <summary>One read of some trees through <paramref name="trees"/>: each repository's refs and the home's records read once.</summary>
    internal TreeReadPass Begin(SessionTrees trees)
    {
        // What no kept read rests on any more is let go: a gone tree's read, then a version no read was taken at.
        foreach (var tree in _trees.Keys)
        {
            if (!Directory.Exists(tree)) _trees.TryRemove(tree, out _);
        }

        var used = _trees.Values.Select(kept => kept.Refs).ToHashSet(StringComparer.Ordinal);
        foreach (var version in _versions.Keys)
        {
            if (!used.Contains(version)) _versions.TryRemove(version, out _);
        }

        return new TreeReadPass(this, trees);
    }

    internal bool TryLast(string tree, out Kept kept) => _trees.TryGetValue(tree, out kept!);

    internal void Keep(string tree, Kept kept, CountedRefs refs)
    {
        _versions.TryAdd(refs.Version, refs);
        _trees[tree] = kept;
    }

    internal void Forget(string tree) => _trees.TryRemove(tree, out _);

    internal CountedRefs? Version(string version) => _versions.GetValueOrDefault(version);

    /// <summary>A tree's kept read: its own stamp, its repository's refs version and the home's stamp it was taken at, and its work.</summary>
    internal sealed record Kept(string Tree, string Refs, string Home, TreeWork Work);
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

/// <summary>
/// One read of some trees (TREELOOK1, <see cref="TreeReadMemo"/>): each tree's kept read where nothing it rests on moved, its
/// <c>git status</c> asked again; a walk where anything did. Safe to use from several trees at once.
/// </summary>
internal sealed class TreeReadPass(TreeReadMemo memo, SessionTrees trees)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CountedRefs?> _repositories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<bool>> _forward = new(StringComparer.Ordinal);
    private string? _home;

    /// <summary>
    /// The tree's work (<see cref="SessionTrees.WorkAsync"/>'s answer): the kept read with its status asked again where its stamp
    /// stands, else the walk, kept where it may be.
    /// </summary>
    internal async Task<TreeWork?> WorkAsync(string tree, CancellationToken ct)
    {
        string full;
        try
        {
            full = Path.GetFullPath(tree);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return await trees.WorkAsync(tree, ct).ConfigureAwait(false);
        }

        if (!trees.Holds(full) || TreeFolders.TreeGone(full) || Stamp(full) is not { } stamp)
        {
            memo.Forget(full);
            return await trees.WorkAsync(tree, ct).ConfigureAwait(false);
        }

        var home = Home();
        if (memo.TryLast(full, out var kept) && kept.Tree == stamp.Tree && kept.Home == home
            && (kept.Refs == stamp.Refs.Version || kept.Work.Commits == 0 && await ForwardAsync(kept.Refs, stamp.Refs, full, ct).ConfigureAwait(false))
            // The same link to a linked tree still there is the repository's top, as the walk that kept it proved: one start.
            && await trees.StatusCountAsync(full, ct).ConfigureAwait(false) is { } uncommitted)
        {
            var work = kept.Work with { Uncommitted = uncommitted };
            memo.Keep(full, kept with { Refs = stamp.Refs.Version, Work = work }, stamp.Refs);
            return work;
        }

        var walked = await trees.WorkAsync(tree, ct).ConfigureAwait(false);
        if (walked is { Commits: not null, Held: null }) memo.Keep(full, new TreeReadMemo.Kept(stamp.Tree, stamp.Refs.Version, home, walked), stamp.Refs);
        else memo.Forget(full);
        return walked;
    }

    /// <summary>
    /// What the tree's read rests on, from git's files: its link, its <c>HEAD</c> and the commit of the branch it names, with its
    /// repository's counted refs. Null where it is no linked tree, its refs are no files, or a file cannot be read.
    /// </summary>
    private (string Tree, CountedRefs Refs)? Stamp(string tree)
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
            if (head.StartsWith("ref:", StringComparison.Ordinal))
            {
                var name = head["ref:".Length..].Trim();
                var loose = Path.Combine(common, name.Replace('/', Path.DirectorySeparatorChar));
                at = File.Exists(loose) ? File.ReadAllText(loose).Trim() : refs.Packed.GetValueOrDefault(name, "none");
                if (at.Length == 0) return null;
            }

            return (string.Join('\n', linkText, head, at), refs);
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
    /// Whether the refs at <paramref name="was"/> only moved forward to <paramref name="now"/>: none went, and each that moved stands
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
