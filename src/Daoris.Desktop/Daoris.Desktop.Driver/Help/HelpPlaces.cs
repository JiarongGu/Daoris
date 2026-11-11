namespace Daoris.Driver;

/// <summary>
/// The places on the window a go link may name: the views, Settings' domains, and the parts of them a
/// door already opens — the groups of Sessions' and Quests' lists that wait on the person, the setup guide's steps, a
/// domain's cards, Repositories' drawers, a repository's Setup, Branches and History and a workspace's page's tabs and sections, Knowledge's two
/// modes, an agent's page's sections. The room lists them, and the window opens the link that names one.
/// </summary>
/// <remarks>
/// A twin (`.claude/knowledge/twins.md`) of the page's <c>help/places.ts</c>, which navigates to them:
/// they share no code, each side's test holds the same table, and they change together. Every name is
/// the one the window's own label shows.
/// </remarks>
public static class HelpPlaces
{
    // The bar's eight places and Settings, in the bar's order.
    public static readonly IReadOnlyList<(string Id, string Name)> Views =
    [
        ("overview", "Overview"), ("sessions", "Sessions"), ("quests", "Quests"), ("projects", "Repositories"),
        ("map", "Map"), ("knowledge", "Knowledge"), ("agents", "Agents"), ("plugins", "Plugins"), ("settings", "Settings"),
    ];

    public static readonly IReadOnlyList<(string Id, string Name)> Domains =
    [
        ("start", "Get started"), ("appearance", "Appearance"), ("driver", "Driver"), ("ai", "AI features"),
        ("git", "Git"), ("browser", "Browser"), ("space", "Disk space"), ("updates", "Updates"), ("wiring", "This Daoris"),
        ("logs", "Machine log"),
    ];

    /// <summary>The parts, each within a view (Sessions, Quests, Repositories, Knowledge, Agents) or a Settings domain.</summary>
    public static readonly IReadOnlyList<(string Within, string Id, string Name)> Parts =
    [
        // What waits on the person below Sessions and Quests, a group of the view's list the
        // page brings into view, named by its heading: Sessions' parked and to-review groups (the reader's `you` and
        // `review`), Quests' asks and the quests held for the person's yes or review. A part names no session or quest in it
        // (a link's item names one, on Quests and Sessions); Overview has no part, since what
        // waits on the person leads it.
        // New conversation, the composer; a link to it may carry a repository and words.
        ("sessions", "waiting", "Waiting on you"), ("sessions", "review", "To review"), ("sessions", "new", "New conversation"),
        ("quests", "asks", "Asks"), ("quests", "held", "Waiting on you"),
        // A repository's Setup, where its own values are set; a link names no repository.
        ("projects", "add", "Add repository"), ("projects", "import", "Import a folder"), ("projects", "setup", "a repository's Setup"),
        // A repository's Branches and History tabs, named apart from a workspace's Branches; likewise no repository.
        ("projects", "branches", "a repository's Branches"), ("projects", "history", "a repository's History"),
        // A workspace's page's tabs and its two sections, prefixed since a repository's page has
        // three of the tabs' names; a link to one names no workspace, so the one in view opens.
        ("projects", "workspace-details", "a workspace's Details"), ("projects", "workspace-branches", "a workspace's Branches"),
        ("projects", "workspace-workflow", "a workspace's Workflow"),
        ("projects", "workspace-defaults", "a workspace's Defaults"), ("projects", "workspace-remote", "a workspace's Remote and reach"),
        // Knowledge's two modes, by the names its list's choice shows.
        ("knowledge", "search", "Search"), ("knowledge", "convergence", "Convergence"),
        ("start", "agent", "step 1, an agent"), ("start", "helper", "step 2, Ask Daoris's agent"),
        ("start", "repositories", "step 3, a workspace and its repositories"), ("start", "setUp", "step 4, setting a place up"),
        ("start", "rules", "step 5, what agents may do"),
        // The agent's page's sections a door opens; Permissions' Proposals are its What it may do.
        ("agents", "accounts", "Accounts"), ("agents", "rules", "What it may do"), ("agents", "usage", "Usage"),
        // The Agents place's Roles mode, where the working roles are set.
        ("agents", "roles", "Roles"),
    ];

    /// <summary>How an offer's item is told from an installed plugin's on Plugins, the page's <c>offerItem</c> (<c>plugins/catalog.ts</c>).</summary>
    public const string OfferItem = "offer:";

    /// <summary>
    /// How an ask's item is told from a quest's on Quests, the page's <c>askItem</c> (<c>opener.ts</c>): duplicated
    /// deliberately, as a twin's constant is, never imported across.
    /// </summary>
    public const string AskItem = "ask:";
}
