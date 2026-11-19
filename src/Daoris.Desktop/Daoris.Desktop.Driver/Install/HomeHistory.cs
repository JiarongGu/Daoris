namespace Daoris.Driver;

/// <summary>
/// Which of a home's files and store tables are its history, which <c>daoris home purge --history</c> moves out, and which are
/// its configuration, which stays. Every entry of <see cref="HomeEntry.Table"/> and every table this build's desktop store makes is named
/// on one side, so a new entry or table is classified where it is added (<c>HomePurgeTests</c>' census); where unclear, it is kept.
/// </summary>
/// <remarks>
/// History is what Daoris recorded of work done: sessions and their records, quests, asks, runs, landings, notices, proposals, opinions,
/// brainstorms, helpers and what each look told or read. Configuration is what the person set or signed in to, and what Daoris needs
/// to stay safe across a start: accounts, roles, casts, workflows' definitions, the registry, the trees and the act journal of a
/// checkout (an open act is how a start settles a repository), the knowledge and memory stores' kept facts, the home's id and build.
/// </remarks>
public static class HomeHistory
{
    /// <summary>The entries kept whole as history: every place of each moves out.</summary>
    public static IReadOnlyList<HomeEntry> Entries { get; } =
    [
        HomeEntry.Reads, HomeEntry.RuleProposals, HomeEntry.LinesMoved, HomeEntry.OverlapsTold,
        HomeEntry.Abandoned, HomeEntry.Sessions, HomeEntry.Helpers, HomeEntry.Partners, HomeEntry.Checks, HomeEntry.Handed,
        HomeEntry.OpinionGates, HomeEntry.OpinionDues, HomeEntry.Brainstorms, HomeEntry.Keeping, HomeEntry.Notices,
        HomeEntry.PipeWaiting, HomeEntry.PipeSent, HomeEntry.StewardDesk, HomeEntry.FilesFound, HomeEntry.Intake,
        HomeEntry.OpinionPackets, HomeEntry.QuestFiles, HomeEntry.AskFiles, HomeEntry.SessionFiles, HomeEntry.BrainstormRooms,
    ];

    /// <summary>
    /// The entries that stay. The two stores (<see cref="HomeEntry.DesktopDb"/>, <see cref="HomeEntry.Knowledge"/>) stay as files and
    /// lose their history tables; <see cref="HomeEntry.Workflows"/> stays and loses its <see cref="WorkflowRecords"/>.
    /// </summary>
    public static IReadOnlyList<HomeEntry> Kept { get; } =
    [
        HomeEntry.DriverConfig, HomeEntry.Harnesses, HomeEntry.Accounts, HomeEntry.Cooling, HomeEntry.Windows, HomeEntry.Permissions,
        HomeEntry.Files, HomeEntry.FilesAsks, HomeEntry.Tools, HomeEntry.PluginState, HomeEntry.Plugins, HomeEntry.PluginApprovals,
        HomeEntry.Usage, HomeEntry.RegistryFollowed, HomeEntry.Update, HomeEntry.Knowledge, HomeEntry.DesktopDb, HomeEntry.Memory,
        HomeEntry.Setup, HomeEntry.Workflows, HomeEntry.ConfiguredAccess, HomeEntry.BrowserSettings, HomeEntry.BrowserFavorites,
        HomeEntry.HomeId, HomeEntry.UsageHistory, HomeEntry.GitIdentities, HomeEntry.AgentOffers,
        HomeEntry.ActFiles, HomeEntry.LastBuild, HomeEntry.TerminalActs, HomeEntry.UxTrail,
        HomeEntry.Keys, HomeEntry.Remotes, HomeEntry.AgentHomes, HomeEntry.BrowserEngine, HomeEntry.BrowserEdge, HomeEntry.EdgeCookies,
        HomeEntry.GitKeys, HomeEntry.GitTokens, HomeEntry.PluginPrivate, HomeEntry.GitSshConfig, HomeEntry.GitKnownHosts, HomeEntry.GitConfig,
        HomeEntry.Window, HomeEntry.DepsSecret, HomeEntry.Landings, HomeEntry.SessionBranches, HomeEntry.PlaceReceipts,
        HomeEntry.Trees, HomeEntry.HelpRoom, HomeEntry.HelpRooms, HomeEntry.Proof, HomeEntry.UsageRead, HomeEntry.RoleRooms, HomeEntry.Deps,
        HomeEntry.TreeFiles, HomeEntry.Places, HomeEntry.Plans, HomeEntry.Evidence,
        HomeEntry.Toolchain, HomeEntry.ToolVersions, HomeEntry.Bin,
        HomeEntry.HomeLease, HomeEntry.Running, HomeEntry.Locks, HomeEntry.Spawn, HomeEntry.Starts, HomeEntry.Hooks,
        HomeEntry.Probing, HomeEntry.SessionPids, HomeEntry.Requests, HomeEntry.EdgeRecord, HomeEntry.Pipe,
        HomeEntry.GitHeld, HomeEntry.GitGrants, HomeEntry.GitUsed, HomeEntry.GitAsks, HomeEntry.GitCheckouts, HomeEntry.GitLibrary,
        HomeEntry.GitLibraryLeases, HomeEntry.GitHome, HomeEntry.GitSessions, HomeEntry.ClockMark, HomeEntry.Logs,
    ];

    /// <summary>
    /// The run, unit, tenure and agent-step records under the workflows' folder, each a folder of records but <c>quests.json</c>: every
    /// other file there (a workflow's definition, <c>casts.json</c>) is configuration and stays.
    /// </summary>
    public static IReadOnlyList<string> WorkflowRecords { get; } =
    [
        "agent-steps", "candidates", "closures", "lines", "looks", "rebases", "resolves", "resources", "run-ends-owed", "runs", "tenures",
        "units", "quests.json",
    ];

    /// <summary>The desktop store's history tables, each emptied, in an order that empties a row's referrer before it.</summary>
    public static IReadOnlyList<string> DesktopTables { get; } =
    [
        "move_words", "moves", "integration_decides", "integration_dispositions", "integration_steps", "integration_members",
        "integration_parts", "integration_epochs", "integration_candidates", "integration_requests", "delivery_phases", "deliveries",
        "binding_roles", "binding_picks", "bindings", "role_moves", "role_starts", "continuation_admissions", "continuation_made",
        "continuation_epochs", "continuation_lines", "held_decisions", "notices", "news_said", "journal_cursors", "conversation_facts",
        "session_event_offsets", "session_event_heads",
    ];

    /// <summary>The desktop store's tables that stay: its own bookkeeping, the roles and their policies, the workspaces, the trees, the act journal.</summary>
    public static IReadOnlyList<string> DesktopKept { get; } =
    [
        "store_version", "store_revisions", "store_runs", "imports", "roles", "role_revisions", "role_staffing",
        "role_policy", "role_policy_revisions", "supervisor_rules", "supervisor_rule_revisions", "repository_workspaces",
        "kept_trees", "tree_claims", "tree_visits", "line_ends", "tree_hand_offs", "git_acts", "git_act_steps",
    ];

    /// <summary>
    /// The service store's history tables (<c>knowledge.db</c>), each emptied, the event journal last, since a trigger of another table
    /// writes it. Its revision marks, cursors and identity stay, so a revision never moves back and a sync never fetches again what it had.
    /// </summary>
    public static IReadOnlyList<string> ServiceTables { get; } =
    [
        "said_settled", "session_opens", "sessions", "sessions_relaxed", "opinions", "channel_member_reads", "channel_members",
        "channel_addressed_acks", "channel_addressed", "channel_acks", "channel_marks", "channel", "asks", "quest_log",
        "quest_closes", "quest_forwards", "quests", "work_record_sessions", "work_records", "work_records_fts",
        "memory_reads", "memory_ops", "team_parts", "team_plans", "team_members", "team_decisions", "team_scouted", "event_journal",
    ];

    /// <summary>
    /// The service store's tables that stay: the knowledge index (its <c>index_</c> tables, named by their version), the registry, the keys, and each
    /// family's revision mark, cursor and version.
    /// </summary>
    public static IReadOnlyList<string> ServiceKept { get; } =
    [
        "store_identity", "session_cursor", "session_revision", "words_revision", "ask_revision", "channel_revision", "channel_cursor",
        "quest_machine", "quest_cursor", "quest_passes", "quest_declared", "quest_store_version", "work_record_version",
        "work_record_fts_version", "registrations", "registry_meta", "feed_provenance", "fed_code_maps", "registration_provenance",
        "registry_retired", "api_keys", "quest_forgotten", "quest_retired",
    ];

    /// <summary>
    /// A history table that loses only the rows its condition names: the memory operations that finished. One still staged, awaiting its
    /// approver, ready, applying or held is a write the service settles at its next start, so it stays.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ServiceWhere { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["memory_ops"] = "state IN ('answered', 'applied', 'dropped', 'declined', 'stale')",
    };

    /// <summary>The prefix of the knowledge index's own tables, which stay.</summary>
    public const string IndexPrefix = "index_";
}
