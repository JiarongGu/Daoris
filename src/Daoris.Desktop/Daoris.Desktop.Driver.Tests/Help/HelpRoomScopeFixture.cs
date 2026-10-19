using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The machine the scoped room's tests describe (ASKSCOPE1b, D158's ASKSCOPE1 note point 4): workspaces <c>alpha</c> and
/// <c>beta</c> and the default one, each with repositories, sessions, asks, quests, finished work, work and landings of its own,
/// read through <see cref="HelpRoom.Describe"/> as the module reads the facts, so a room projected to one workspace is judged
/// from the same facts as the machine's. <c>beta</c> holds more ended sessions than the room lists, all newer than
/// <c>alpha</c>'s, so a projection made after the list's limit would fill <c>alpha</c>'s list with them.
/// </summary>
internal static class HelpRoomScopeFixture
{
    private static readonly DateTimeOffset At = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The registry: two of <c>alpha</c>'s (one with no checkout here), one of <c>beta</c>'s, one of the default's.</summary>
    internal static readonly IReadOnlyList<RepoView> Registry =
    [
        new("engine", Adopted: true, Root: "/alpha/engine", Workspace: "alpha"),
        new("site", Adopted: true, Root: null, Workspace: "alpha"),
        new("console", Adopted: true, Root: "/beta/console", Workspace: "beta"),
        new("tools", Adopted: true, Root: "/default/tools"),
    ];

    /// <summary>
    /// The machine whole, as the facts say it; <paramref name="unread"/> names the repositories whose line was not judged yet
    /// (<c>console</c>'s where none are named, so only <c>beta</c>'s room has anything not read yet; empty for none).
    /// </summary>
    internal static HelpMachine Machine(IReadOnlyList<string>? unread = null) => Describe(unread ?? ["console"]);

    private static HelpMachine Describe(IReadOnlyList<string> unread) => HelpRoom.Describe(
        Config, new Snapshot(Quests, Registry, Active), Lines(unread), Roster, _ => "Claude Code", asks: 2, Standing,
        landed: Landed, parked: [new ParkedQuest("qa1", "engine"), new ParkedQuest("qb1", "console")],
        held: [new HeldQuest("qb2", "console", "sb9")], sessions: Sessions, history: History, work: Work,
        workUnread:
        [
            "`site` has no checkout here, so its recovery refs were not read",
            "git could not list `console`'s recovery refs: fatal: not a git repository",
        ],
        workRecovery:
        [
            new InventoryEntry("", InventoryKind.MovedAside, "trees/alpha/engine/sa0-20261018", InventoryRole.Recovery, InventoryDisposition.Retained, "kept until 2026-10-25 09:00 UTC") { Repository = "engine" },
            new InventoryEntry("", InventoryKind.MovedAside, "trees/beta/console/sb0-20261018", InventoryRole.Recovery, InventoryDisposition.Retained, "kept until 2026-10-25 09:00 UTC") { Repository = "console" },
        ],
        notReadYet: new HelpNotReadYet(unread));

    private static IReadOnlyList<RepositoryLine> Lines(IReadOnlyList<string> unread) =>
    [
        .. new RepositoryLine[]
        {
            new("engine", "alpha", "main", LineSource.Checkout),
            new("console", "beta", "trunk", LineSource.Checkout),
            new("tools", "default", "develop", LineSource.Checkout),
        }.Where(line => !unread.Contains(line.Repository)),
    ];

    /// <summary>Each named workspace declares a kind of work; <c>alpha</c> pauses one of its quests and <c>beta</c> one of its asks.</summary>
    private static DriverConfig Config => DriverConfig.Empty with
    {
        Adapter = "claude-code",
        HelperAdapter = "claude-code-acp",
        Cap = 3,
        PausedAsks = new Dictionary<string, WorkPause> { ["ab1"] = new WorkPause(At, new Dictionary<string, string>()) },
        PausedQuests = new Dictionary<string, WorkPause> { ["qa1"] = new WorkPause(At, new Dictionary<string, string>()) },
        WorkspaceWorkflows = new Dictionary<string, WorkflowChoice>
        {
            ["alpha"] = new(null, [new("docs", new WorkflowKindChoice("Documentation", ["docs/**"], null))]),
            ["beta"] = new(null, [new("feature", new WorkflowKindChoice("Feature", [], null))]),
        },
    };

    private static readonly IReadOnlyList<QuestView> Quests =
    [
        new("qa1", "site", "engine", "Stream the tiles", "A body.", "Published") { Workspace = "alpha" },
        new("qb1", "console", "console", "Fix the console", "A body.", "Published") { Workspace = "beta" },
        new("qb2", "console", "console", "Tidy the console", "A body.", "Published") { Workspace = "beta" },
    ];

    private static readonly IReadOnlyList<SessionView> Active =
    [
        new("sa1", "engine", "awaiting-person") { Workspace = "alpha" },
        new("sb1", "console", "awaiting-person") { Workspace = "beta" },
        new("sb2", "console", "awaiting-person") { Workspace = "beta" },
    ];

    private static readonly IReadOnlyList<AskView> Standing =
    [
        new("aa1", "alpha", "stream the tiles", "Proposed", "declarations"),
        new("ab1", "beta", "fix the console", "Proposed", "declarations"),
        new("ab2", "beta", "tidy the console", "Published", "declarations") { Quests = ["qb2"] },
    ];

    /// <summary>
    /// <c>alpha</c>: one waiting on the person, two ended. <c>beta</c>: twelve ended, every one newer than <c>alpha</c>'s. The
    /// default: one ended.
    /// </summary>
    private static SessionLook Sessions
    {
        get
        {
            var records = new System.Text.StringBuilder("[");
            records.Append("""{ "id": "sa1", "repository": "engine", "state": "awaiting-person", "workspace": "alpha" },""");
            records.Append("""{ "id": "ea1", "repository": "engine", "state": "completed", "updated": "2026-09-01T00:00:00Z", "workspace": "alpha" },""");
            records.Append("""{ "id": "ea2", "repository": "site", "state": "completed", "updated": "2026-09-02T00:00:00Z", "workspace": "alpha" },""");
            for (var n = 1; n <= 12; n++)
            {
                records.Append($$"""{ "id": "eb{{n:00}}", "repository": "console", "state": "completed", "updated": "2026-10-{{n:00}}T00:00:00Z", "workspace": "beta" },""");
            }

            records.Append("""{ "id": "ed1", "repository": "tools", "state": "completed", "updated": "2026-08-01T00:00:00Z" }]""");
            return new SessionLook(SessionRecords.Parse(records.ToString()), [], []);
        }
    }

    private static readonly IReadOnlyList<HistoryReading> History =
    [
        new HistoryReading("alpha") { Quests = 1, Sessions = 2, Bytes = new HistoryBytes(2048, 0, 0, 0, 0), Takes = new HistoryTakes(1, 0, 2, 0, 2048) },
        new HistoryReading("beta") { Quests = 4, Sessions = 12, Bytes = new HistoryBytes(8192, 0, 0, 0, 0), Takes = new HistoryTakes(4, 0, 12, 0, 8192) },
        new HistoryReading("default"),
    ];

    private static readonly IReadOnlyList<WorkMilestone> Work =
    [
        Unit("ua1", "engine", "daoris/sa0"),
        Unit("ub1", "console", "daoris/sb0"),
    ];

    private static WorkMilestone Unit(string unit, string repository, string branch) => new(
        unit, repository, branch, branch["daoris/".Length..],
        new MilestoneEnd("ended", EndBy.Merged, "main", "a1b2c3d4e5f6", At, "every commit of it is on `main`"),
        [new(unit, InventoryKind.Line, "main", InventoryRole.Result, InventoryDisposition.Kept, "the line holds it")],
        new MilestoneCleanup("pending", [new(unit, InventoryKind.Tree, branch["daoris/".Length..], InventoryRole.Temporary, InventoryDisposition.Blocked, "a session still holds this tree")], []),
        [], []);

    private static readonly IReadOnlyList<LandedBranch> Landed =
    [
        new("engine", "alpha", "feature/qa1-tiles", "main", "a1b2c3d", "sa0", "qa1", "Stream the tiles", At),
        new("console", "beta", "feature/qb1-console", "trunk", "b2c3d4e", "sb0", "qb1", "Fix the console", At),
    ];

    /// <summary>One agent with an account: the machine's, said in every workspace's room.</summary>
    private static readonly IReadOnlyList<HarnessReport> Roster =
    [
        new("claude-code", true, "2.1.0", null, null, null, [new ProfileReport("account-1", "/profiles/account-1", LoginState.In)]),
    ];
}
