namespace Daoris.Driver;

/// <summary>What the machine holds that a press's view is judged against: names only.</summary>
/// <remarks>A fact only one view reads is read by that view in its file; the facts stay in this one declaration, under a comment per family.</remarks>
public sealed record HelpMachineFacts(
    IReadOnlyCollection<string> Repositories, IReadOnlyCollection<string> Workspaces, IReadOnlyCollection<string> Agents)
{
    // Every view's.

    /// <summary>
    /// The workspace the conversation that pressed belongs to, read from its record (<see cref="HelpCircle.OfRecord"/>): the
    /// focus of what a list shows first, no limit on what a press names. Null for <c>default</c>'s room, the whole machine.
    /// </summary>
    public string? Circle { get; init; }

    /// <summary>The git identities on this machine (labels and hosts, never a key or a token), for a <c>git use</c> view's judge.</summary>
    public IReadOnlyList<HelpGitIdentity> GitIdentities { get; init; } = [];

    /// <summary>This machine's plugins, for a landing rule that names one and a plugin's press.</summary>
    public PluginCatalog Plugins { get; init; } = PluginCatalog.None;

    // The plugin views'.

    /// <summary>The Daoris home, which a plugin is never added from; null where none is named.</summary>
    public string? Home { get; init; }

    /// <summary>The harness names this build carries, which a plugin may not declare.</summary>
    public IReadOnlyCollection<string> Reserved { get; init; } = [];

    /// <summary>The install's offers folder, where an offer is found and an offer's update reads; null is none.</summary>
    public string? OffersFolder { get; init; }

    /// <summary>The install's offers, as the Plugins place lists them under Daoris's own plugins.</summary>
    public IReadOnlyList<PluginOffer> Offers { get; init; } = [];

    // The rule views'.

    /// <summary>
    /// The quests the loop's last tick parked by their failed sessions, which a retry must name: the verdict
    /// the quest drawer shows its Retry by. Empty before any tick, and while another loop holds the home.
    /// </summary>
    public IReadOnlyList<ParkedQuest> Parked { get; init; } = [];

    /// <summary>
    /// The quests the loop's last look held by the person's stop, each with the session they stopped: a retry of
    /// one releases that stop. Empty before any look, and while another loop holds the home.
    /// </summary>
    public IReadOnlyList<HeldQuest> Held { get; init; } = [];

    /// <summary>
    /// Each registered repository with its workspace and its checkout here, or none: what a <c>review</c>'s
    /// procedure is looked for in, the one repository it names or each of the workspace's, and where a workflow finds a repository's workspace. Empty where not asked.
    /// </summary>
    public IReadOnlyList<(string Repository, string? Workspace, string? Root)> Registered { get; init; } = [];
}

/// <summary>A git identity as a rule view's judge knows it: its id, label, host and way. Never a key, a token or a user name.</summary>
public sealed record HelpGitIdentity(string Id, string Label, string Host, string Way);
