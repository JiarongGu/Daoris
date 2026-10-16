namespace Daoris.Driver;

/// <summary>
/// What every part of the session trees reads of the machine it works on (PART10i): the home, the plugin wire, the two test seams
/// and the reads derived from the home. Built once by <see cref="SessionTrees"/>, which hands it to the classes that moved out of it.
/// </summary>
internal sealed class TreeContext
{
    internal TreeContext(string home, LandingPlugins plugins)
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

    /// <summary>The plugin wire a branch rule's landing speaks (WSR4, D100).</summary>
    internal LandingPlugins Plugins { get; }

    /// <summary>The test seam (SQUASHTIDY1c): run with the branch's name between a judgement and the delete it cleared. Production sets none.</summary>
    internal Func<string, Task>? BeforeDeleting { get; set; }

    /// <summary>A test's seam (FABRIC-END4a review): run once the trees were read, right before the branch's ref goes.</summary>
    internal Func<string, Task>? BeforeRefDeleted { get; set; }

    /// <summary>The test seam (AUTOTIDY1): true for a checkout whose working trees git could not list. Production sets none.</summary>
    internal Func<string, bool>? ListFails { get; set; }

    /// <summary>The branches this machine's landings made (WSR5) — the only ones the clean-up and the hand-off act on.</summary>
    internal LandedBranches Recorded => new(Home);

    /// <summary>Where this machine's session branches started (WSR6) — what bringing one up to date cuts at.</summary>
    internal SessionBranches Grown => new(Home);

    /// <summary>
    /// Every tree this machine's Daoris has opened lives under here, and only here: the home's <see cref="HomeEntry.Trees"/>,
    /// asked short of an item, the folder holding them.
    /// </summary>
    internal string TreesRoot => DaorisHome.Entry(Home, HomeEntry.Trees);

    /// <summary>Whether <paramref name="path"/> is a tree this home opened — the only kind a landing rule reaches.</summary>
    internal bool Holds(string path) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(TreesRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The person's choices, from the file the loop and the planner read (CONFIGSEAM1): this home's <c>driver.json</c> alone
    /// ignored an override's landing and review rules, so the work merged into the line while the planner sat a review for it.
    /// </summary>
    internal DriverConfig Config() => DriverConfig.Load(DriverConfig.ResolvePath(Home));

    /// <summary>
    /// The repository's line (WSR2), from the choices beside this home, read per call: the person may
    /// set it between one tree and the next, and a cached answer would grow the second from the old line.
    /// Within one look (<see cref="OneLook"/>), git's guess at a checkout's line is asked once (UIFREEZE1).
    /// </summary>
    internal Task<Line> LineAsync(string root, string repository, string workspace, CancellationToken ct) =>
        CanonicalLine.ResolveAsync(root, repository, workspace, Config(), GuessAsync, ct);

    /// <summary>The test seam (UIFREEZE1): git's guess at a checkout's line, counted by a test. Production asks git.</summary>
    internal Func<string, CancellationToken, Task<string?>> CheckoutLine { get; set; } = WorkingTree.DefaultBranchAsync;

    // The look this flow is in, or null: an async local, so a look running beside it on the same context shares nothing.
    private readonly AsyncLocal<Dictionary<string, Task<string?>>?> _look = new();

    /// <summary>
    /// One look over the repositories (UIFREEZE1): until it is disposed, each checkout's guess at its line is asked of git once
    /// in this flow, and every caller in the look shares the answer. The sweep's list asked it three times a repository, up to
    /// three git processes each, and a frozen window's stack held seventeen such asks on the process-start lock.
    /// </summary>
    /// <remarks>
    /// Only git's guess is kept, never the person's choice: a line set in the choices is still read at each call. A look
    /// inside a look is the outer one's. A look ends with the flow that opened it, so a later look asks git again.
    /// </remarks>
    internal IDisposable OneLook()
    {
        if (_look.Value is not null) return Nothing.Instance;
        _look.Value = new Dictionary<string, Task<string?>>(StringComparer.OrdinalIgnoreCase);
        return new Ending(_look);
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

    private sealed class Ending(AsyncLocal<Dictionary<string, Task<string?>>?> look) : IDisposable
    {
        public void Dispose() => look.Value = null;
    }

    private sealed class Nothing : IDisposable
    {
        public static readonly Nothing Instance = new();

        public void Dispose()
        {
        }
    }

    /// <summary>A tree's workspace and repository, read from the layout Daoris itself chose (`trees/&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;`).</summary>
    internal (string Workspace, string Repository) OwnerOf(string full)
    {
        var parts = Path.GetRelativePath(TreesRoot, full).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length >= 3 ? (parts[0], parts[1]) : ("", "");
    }
}
