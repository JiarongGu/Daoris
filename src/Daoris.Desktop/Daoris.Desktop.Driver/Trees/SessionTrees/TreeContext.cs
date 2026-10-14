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

    /// <summary>The test seam (AUTOTIDY1): true for a checkout whose working trees git could not list. Production sets none.</summary>
    internal Func<string, bool>? ListFails { get; set; }

    /// <summary>The branches this machine's landings made (WSR5) — the only ones the clean-up and the hand-off act on.</summary>
    internal LandedBranches Recorded => new(Home);

    /// <summary>Where this machine's session branches started (WSR6) — what bringing one up to date cuts at.</summary>
    internal SessionBranches Grown => new(Home);

    /// <summary>Every tree this machine's Daoris has opened lives under here, and only here.</summary>
    internal string TreesRoot => Path.Combine(Home, "trees");

    /// <summary>
    /// The person's choices, from the file the loop and the planner read (CONFIGSEAM1): this home's <c>driver.json</c> alone
    /// ignored an override's landing and review rules, so the work merged into the line while the planner sat a review for it.
    /// </summary>
    internal DriverConfig Config() => DriverConfig.Load(DriverConfig.ResolvePath(Home));

    /// <summary>
    /// The repository's line (WSR2), from the choices beside this home, read per call: the person may
    /// set it between one tree and the next, and a cached answer would grow the second from the old line.
    /// </summary>
    internal Task<Line> LineAsync(string root, string repository, string workspace, CancellationToken ct) =>
        CanonicalLine.ResolveAsync(root, repository, workspace, Config(), ct);

    /// <summary>A tree's workspace and repository, read from the layout Daoris itself chose (`trees/&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;`).</summary>
    internal (string Workspace, string Repository) OwnerOf(string full)
    {
        var parts = Path.GetRelativePath(TreesRoot, full).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length >= 3 ? (parts[0], parts[1]) : ("", "");
    }
}
