namespace Daoris.Driver;

/// <summary>
/// What every part of the session trees reads of the machine it works on: the home, the plugin wire, the two test seams
/// and the reads derived from the home. Built once by <see cref="SessionTrees"/>, which hands it to the classes that moved out of it.
/// </summary>
internal sealed class TreeContext
{
    internal TreeContext(string home, IWorkplacePluginWire plugins)
    {
        Home = home;
        Plugins = plugins;
        Worktrees = new TreeWorktrees(this);
        Leftovers = new TreeLeftovers(this);
    }

    /// <summary>The working trees git lists, over this context's seam.</summary>
    internal TreeWorktrees Worktrees { get; }

    /// <summary>The empty folders trees left behind, over this context's trees home.</summary>
    internal TreeLeftovers Leftovers { get; }

    /// <summary>The Daoris home the trees, the records and the choices live under.</summary>
    internal string Home { get; }

    /// <summary>The plugin wire a branch rule's landing speaks.</summary>
    internal IWorkplacePluginWire Plugins { get; }

    /// <summary>The test seam: run with the branch's name between a judgement and the delete it cleared. Production sets none.</summary>
    internal Func<string, Task>? BeforeDeleting { get; set; }

    /// <summary>A test's seam: run once the trees are read, right before the branch's ref goes.</summary>
    internal Func<string, Task>? BeforeRefDeleted { get; set; }

    /// <summary>The test seam: true for a checkout whose working trees git could not list. Production sets none.</summary>
    internal Func<string, bool>? ListFails { get; set; }

    /// <summary>What time it is (the look's fallback judges a kept branch again at a bound); the system's unless a test sets it.</summary>
    internal Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

    /// <summary>The branches this machine's landings made — the only ones the clean-up and the hand-off act on.</summary>
    internal LandedBranches Recorded => new(Home);

    /// <summary>Where this machine's session branches started — what bringing one up to date cuts at.</summary>
    internal SessionBranches Grown => new(Home);

    /// <summary>
    /// Where this machine's Daoris opens a tree: the home's <see cref="HomeEntry.Trees"/>, asked short of an item, the folder
    /// holding them. A tree opened before the home's layout moved may sit at the entry's other place (<see cref="TreesRoots"/>).
    /// </summary>
    internal string TreesRoot => DaorisHome.Entry(Home, HomeEntry.Trees);

    /// <summary>
    /// Both places of <see cref="HomeEntry.Trees"/>, the new first: a layout move never moves a tree, so the trees
    /// opened before the layout moved stay at the old place while new ones open at the new, and each is one this home opened.
    /// </summary>
    internal IReadOnlyList<string> TreesRoots => DaorisHome.Places(Home, HomeEntry.Trees);

    /// <summary>Whether <paramref name="path"/> is a tree this home opened — the only kind a landing rule reaches.</summary>
    internal bool Holds(string path) => RootOf(path) is not null;

    /// <summary>
    /// The place of <see cref="HomeEntry.Trees"/> that holds <paramref name="path"/> below it, or null for a path under neither:
    /// the one test of a tree this home opened, which every check asks.
    /// </summary>
    internal string? RootOf(string path)
    {
        var full = Path.GetFullPath(path);
        return TreesRoots.FirstOrDefault(root =>
            full.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The folder of the tree the layout names <c>&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;</c>: where a tree
    /// is at either place, else where a new one opens.
    /// </summary>
    internal string TreeAt(string workspace, string repository, string name) =>
        DaorisHome.Places(Home, HomeEntry.Trees, workspace, repository, name).FirstOrDefault(Directory.Exists)
        ?? Path.Combine(TreesRoot, workspace, repository, name);

    /// <summary>The person's choices, from the file the loop and the planner read: this home's <c>driver.json</c>.</summary>
    internal DriverConfig Config() => DriverConfig.Load(DriverConfig.ResolvePath(Home));

    /// <summary>
    /// The repository's line, from the choices beside this home, read per call: the person may
    /// set it between one tree and the next, and a cached answer would grow the second from the old line.
    /// Within one look (<see cref="OneLook"/>), git's guess at a checkout's line is asked once.
    /// </summary>
    internal Task<Line> LineAsync(string root, string repository, string workspace, CancellationToken ct) =>
        CanonicalLine.ResolveAsync(root, repository, workspace, Config(), GuessAsync, ct);

    /// <summary>
    /// The output a repository declares, which an unforced removal of its tree takes with it (<see cref="TreeScratch.DeclaredAsync"/>):
    /// its accepted <c>integration.scratch</c> and its manifest's generated index, at its line's tip. None where the choices could not
    /// be read or the line is not known, so the tree keeps what it names.
    /// </summary>
    /// <param name="line">The repository's line where the caller read it already; null reads it here.</param>
    internal async Task<IReadOnlyList<System.Text.RegularExpressions.Regex>> ScratchAsync(
        string root, string repository, string workspace, CancellationToken ct, string? line = null)
    {
        try
        {
            var config = Config();
            line ??= (await LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
            return await TreeScratch.DeclaredAsync(config, root, repository, line, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return [];
        }
    }

    /// <summary>
    /// <see cref="ScratchAsync"/> for a reader with no context: the same declaration, read from the home's choices and git's own guess at the line.
    /// </summary>
    internal static async Task<IReadOnlyList<System.Text.RegularExpressions.Regex>> ScratchOfHomeAsync(
        string home, string root, string repository, string workspace, CancellationToken ct)
    {
        try
        {
            var config = DriverConfig.Load(DriverConfig.ResolvePath(home));
            var line = (await CanonicalLine.ResolveAsync(root, repository, workspace, config, s_gitGuess, ct).ConfigureAwait(false)).Branch;
            return await TreeScratch.DeclaredAsync(config, root, repository, line, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return [];
        }
    }

    /// <summary>The test seam: git's guess at a checkout's line, counted by a test. Production asks git.</summary>
    internal Func<string, CancellationToken, Task<string?>> CheckoutLine { get; set; } = s_gitGuess;

    private static readonly Func<string, CancellationToken, Task<string?>> s_gitGuess = WorkingTree.DefaultBranchAsync;

    /// <summary>
    /// Whether Review's graph may ask git's guess as its three questions together: no look is open, which shares one
    /// guess across its flow, and no test's seam stands in for git. Otherwise the graph asks through <see cref="LineAsync"/>, as today.
    /// </summary>
    internal bool GuessesApart => _look.Value is null && ReferenceEquals(CheckoutLine, s_gitGuess);

    // The look this flow is in, or null: an async local, so a look running beside it on the same context shares nothing.
    private readonly AsyncLocal<Dictionary<string, Task<string?>>?> _look = new();

    /// <summary>
    /// One look over the repositories: until it is disposed, each checkout's guess at its line is asked of git once
    /// in this flow, and every caller in the look shares the answer. The sweep's list asked it three times a repository, up to
    /// three git processes each, and a frozen window's stack held seventeen such asks on the process-start lock.
    /// </summary>
    /// <remarks>
    /// Only git's guess is kept, never the person's choice: a line set in the choices is still read at each call. A look
    /// inside a look is the outer one's. A look ends with the flow that opened it, so a later look asks git again.
    /// </remarks>
    internal TreeLook OneLook()
    {
        if (_look.Value is not null) return TreeLook.Nested;
        var asked = new Dictionary<string, Task<string?>>(StringComparer.OrdinalIgnoreCase);
        _look.Value = asked;
        return new TreeLook(_look, asked);
    }

    private Task<string?> GuessAsync(string root, CancellationToken ct)
    {
        if (_look.Value is not { } look) return CheckoutLine(root, ct);
        var key = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        lock (look)
        {
            if (look.TryGetValue(key, out var asked) && !asked.IsFaulted && !asked.IsCanceled) return asked;
            return look[key] = CheckoutLine(root, ct);
        }
    }

    /// <summary>A tree's workspace and repository, read from the layout Daoris itself chose (`trees/&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;`).</summary>
    internal (string Workspace, string Repository) OwnerOf(string full)
    {
        var parts = Path.GetRelativePath(RootOf(full) ?? TreesRoot, full).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length >= 3 ? (parts[0], parts[1]) : ("", "");
    }
}
