namespace Daoris.Driver;

/// <summary>Which of the two looks over the checkouts here <c>daoris-driver trees</c> was asked for.</summary>
public enum TreesLook
{
    /// <summary>The clean-up (WSR3, D88): the session branches whose work landed or that hold nothing, and the landed branches whose work reached the line.</summary>
    Clean,

    /// <summary>Bringing each repository up to date after a pull request merged (WSR6, D109).</summary>
    Sync,
}

/// <summary>What <c>daoris-driver trees clean|sync</c> was asked: which checkouts, and whether it is the press.</summary>
/// <param name="Look">The clean-up, or bringing up to date.</param>
public sealed record TreesAsk(TreesLook Look)
{
    /// <summary>The one repository named (<c>sync</c> alone), taken whatever it holds (D112); null for the scope's.</summary>
    public string? Repository { get; init; }

    /// <summary>The one workspace named (BRSCOPE1a), whose checkouts alone are taken; null for every checkout here.</summary>
    public string? Workspace { get; init; }

    /// <summary>Every repository with a checkout here, not only those holding Daoris's branches (<c>sync</c> alone, D112).</summary>
    public bool All { get; init; }

    /// <summary>The press, after the list.</summary>
    public bool Yes { get; init; }
}

/// <summary>The checkouts a look is handed, or why it is handed none.</summary>
/// <param name="Repositories">The registry's rows the look takes, each with its checkout here.</param>
/// <param name="Refusal">Why the words take nothing: a workspace the registry does not name. Null where they take what they name.</param>
public sealed record TreesTaken(IReadOnlyList<(string Repository, string? Workspace, string? Root)> Repositories, string? Refusal);

/// <summary>
/// The words of <c>daoris-driver trees clean|sync</c> (BRSCOPE1a, D150's BRSCOPE1 note, D50, WSP5): what they ask, which
/// checkouts each look is handed, and what its sentences call them. The looks and presses stay the host's
/// (<c>TreesConsole</c>), which hands them <see cref="Take"/>'s rows. In the library, so a test holds them.
/// </summary>
/// <remarks>
/// <para><b>One workspace's, as its Branches tab's.</b> With <c>--workspace</c>, a look and its press take that workspace's
/// checkouts alone, by the rule the screen's look and press take one by (<see cref="Checkouts"/>), so the terminal and a
/// workspace's tab look at the same repositories (D50). With none, every checkout here, as before. A workspace the registry
/// does not name is refused, naming it and the ones it does: a mistyped name would otherwise look at nothing and say all is
/// well.</para>
///
/// <para><b>Every word is read.</b> These looks took the words they knew and passed over the rest, so a mistyped
/// <c>--workspce aurora --yes</c> would have cleaned every workspace's branches. A word they do not take is the usage.</para>
///
/// <para><b>A twin</b> of the modules' <c>DriverModule.Checkouts</c>, which <c>TREES_SYNC_PLAN</c> and <c>TREES_SYNC</c> take a
/// workspace by: the driver cannot reference the modules. Both are held to one table,
/// <c>Daoris.Desktop.Driver.Tests/fixtures/checkout-scope.json</c>; <c>TreesCommandTests</c> reads it row for row.</para>
/// </remarks>
public static class TreesCommand
{
    /// <summary>What the words after <c>trees</c> ask of a look, or null with what is wrong with them.</summary>
    /// <remarks>
    /// <c>clean [--workspace &lt;name&gt;] [--yes]</c>, or <c>sync [--repository &lt;name&gt;] [--workspace &lt;name&gt;] [--all]
    /// [--yes]</c>, in any order; a name once.
    /// </remarks>
    public static TreesAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        TreesLook? look = args switch
        {
            ["clean", ..] => TreesLook.Clean,
            ["sync", ..] => TreesLook.Sync,
            _ => null,
        };
        if (look is not { } asked)
        {
            problem = "`trees` reads `clean` or `sync` here.";
            return null;
        }

        var sync = asked == TreesLook.Sync;
        var ask = new TreesAsk(asked);
        for (var at = 1; at < args.Count; at++)
        {
            switch (args[at])
            {
                case "--workspace" when ask.Workspace is null && Name(args, at + 1) is { } workspace:
                    ask = ask with { Workspace = workspace };
                    at++;
                    break;
                case "--workspace":
                    problem = "`--workspace` takes one workspace's name.";
                    return null;
                case "--repository" when sync && ask.Repository is null && Name(args, at + 1) is { } repository:
                    ask = ask with { Repository = repository };
                    at++;
                    break;
                case "--repository" when sync:
                    problem = "`--repository` takes one repository's name.";
                    return null;
                case "--all" when sync:
                    ask = ask with { All = true };
                    break;
                case "--yes":
                    ask = ask with { Yes = true };
                    break;
                default:
                    problem = $"`{args[at]}` is not a word `trees {args[0]}` takes.";
                    return null;
            }
        }

        return ask;
    }

    /// <summary>The repositories bringing up to date takes (D112): every one with <c>--all</c>, the one named, else those holding Daoris's branches.</summary>
    public static SyncScope Scope(TreesAsk ask) =>
        ask.All ? SyncScope.Everything : ask.Repository is { } named ? SyncScope.Named([named]) : SyncScope.Held;

    /// <summary>
    /// The checkouts the words take: the registry's rows with a checkout here, of the one workspace named, or a refusal where
    /// the registry names no such workspace.
    /// </summary>
    public static TreesTaken Take(IReadOnlyCollection<RepoView> registry, TreesAsk ask)
    {
        if (ask.Workspace is { } workspace && !registry.Any(row => SameWorkspace(row.Workspace, workspace)))
        {
            var named = registry.Select(row => RemoteTarget.Workspace(row.Workspace)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.Ordinal).Select(name => $"`{name}`").ToList();
            return new([], named.Count == 0
                ? $"there is no workspace `{workspace}` on this machine: no repository is registered here."
                : $"there is no workspace `{workspace}` on this machine — one of {string.Join(", ", named)}.");
        }

        return new(Checkouts(registry, ask.Repository, ask.Workspace), null);
    }

    /// <summary>
    /// The registry's rows with a checkout here, by name: the one <paramref name="repository"/> names where it names one, and
    /// <paramref name="workspace"/>'s alone where it names one (BRSCOPE1, WSP5), matched without case, a row with no
    /// workspace read as the default's, as <see cref="RemoteTarget.Workspace"/> reads it for every door.
    /// </summary>
    /// <remarks>The modules' <c>DriverModule.Checkouts</c> is its twin; one table holds both (see the class's remarks).</remarks>
    public static List<(string Repository, string? Workspace, string? Root)> Checkouts(
        IEnumerable<RepoView> registry, string? repository, string? workspace) =>
        registry
            .Where(row => !string.IsNullOrWhiteSpace(row.Root))
            .Where(row => repository is null || string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Where(row => workspace is null || SameWorkspace(row.Workspace, workspace))
            .OrderBy(row => row.Repository, StringComparer.Ordinal)
            .Select(row => (row.Repository, (string?)row.Workspace, row.Root))
            .ToList();

    /// <summary>The clean-up's list found nothing in the checkouts it took.</summary>
    public static string NothingToClean(TreesAsk ask) =>
        $"trees: no session branches, and no branch a landing made, in any repository{Of(ask)} with a checkout here.";

    /// <summary>Bringing up to date took no checkout: none here, none of the workspace named, or not the repository named.</summary>
    public static string NothingToSync(TreesAsk ask) =>
        (ask.Repository is { } named
            ? $"trees: `{named}` has no checkout here{(ask.Workspace is { } workspace ? $" in workspace `{workspace}`" : "")}"
            : $"trees: no repository{Of(ask)} has a checkout here")
        + ", so there is nothing to bring up to date.";

    /// <summary>Bringing up to date's list looked at none, since none of the checkouts it took holds a branch of Daoris's.</summary>
    public static string NoneHeld(TreesAsk ask) => $"trees: no repository{Of(ask)} with a checkout here holds a branch of Daoris's.";

    /// <summary>The press a list suggests, carrying the list's own scope (D112) and its workspace, so <c>--yes</c> takes what was listed.</summary>
    public static string Press(TreesAsk ask) =>
        $"daoris-driver trees {(ask.Look == TreesLook.Clean ? "clean" : "sync")}"
        + (ask.Repository is { } named ? $" --repository {named}" : "")
        // A workspace's name goes through the one spelling every shell keeps whole (ACCTQUOTE1b).
        + (ask.Workspace is { } workspace ? $" --workspace {ShellWord.Of(workspace, ShellWord.Workspace)}" : "")
        + (ask.All ? " --all" : "")
        + " --yes";

    private static string Of(TreesAsk ask) => ask.Workspace is { } workspace ? $" of workspace `{workspace}`" : "";

    private static bool SameWorkspace(string? row, string asked) =>
        string.Equals(RemoteTarget.Workspace(row), RemoteTarget.Workspace(asked), StringComparison.OrdinalIgnoreCase);

    /// <summary>The name after a flag: a word that is not a flag and not blank, trimmed; null where there is none.</summary>
    private static string? Name(IReadOnlyList<string> args, int at) =>
        at < args.Count && !args[at].StartsWith('-') && args[at].Trim() is { Length: > 0 } name ? name : null;
}
