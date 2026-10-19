using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// ASKSCOPE1c (D158's ASKSCOPE1 note point 5): a proposal is judged against the workspace its conversation is bound to before its
/// kind judges it. One naming another workspace, a repository of another, or every workspace at once is refused and says so; one
/// naming its own is judged as before. A conversation bound to none, <c>default</c>'s room's, proposes for the whole machine.
/// </summary>
public sealed class HelpProposalScopeTests : HelpProposalsFixture
{
    private static readonly DateTimeOffset At = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyDictionary<string, PausedBy> NonePaused = new Dictionary<string, PausedBy>();

    private static readonly AskView AskOfBeta = new("ab1beta0", "beta", "fix the stall", "Open", "full");

    private static QuestView Quest(string id, string workspace, string repository) =>
        new(id, "ask #a0", repository, "a quest", "", "Open") { Workspace = workspace };

    private static WorkPlan QuestWork(QuestView quest) => WorkPausing.Plan(
        new WorkPieces(WorkScope.Quest, quest.Id, [new WorkQuest(quest, WorkJoin.Named)], [], [], []), DriverConfig.Empty, NonePaused, null);

    private static HistoryPlan History(HistoryScope scope, string kind, string id, string workspace) =>
        new(scope, id, [new HistoryUnitPlan(kind, id, workspace) { Quests = [id], Bytes = new HistoryBytes(1024, 0, 0, 0, 0) }]);

    /// <summary>
    /// <c>engine</c> is <c>alpha</c>'s, <c>game</c> is <c>beta</c>'s; each landed a branch. ASKSCOPE1c2: each has a quest, an ask
    /// and a session, as each kind's facts read them: the delete's and a go's records, the accept's quests, the pause's works,
    /// the clear's plans, and the sessions where Sessions' list places them.
    /// </summary>
    private static readonly HelpMachineFacts Two = Facts with
    {
        Workspaces = ["default", "alpha", "beta"],
        Registered = [("engine", "alpha", "/alpha/engine"), ("game", "beta", "/beta/game")],
        Landed =
        [
            new("engine", "alpha", "feature/qa1-tiles", "main", "a1b2c3d", "sa0", "qa1", "Stream the tiles", At),
            new("game", "beta", "feature/qb1-stall", "main", "b2c3d4e", "sb0", "qb1", "Fix the stall", At),
        ],
        Quests =
        [
            new HelpQuestFacts("qa1alpha", "Stream the tiles", "engine", "Open", Deletable: true, Workspace: "alpha"),
            new HelpQuestFacts("qb1beta0", "Fix the stall", "game", "Open", Deletable: true, Workspace: "beta"),
        ],
        Asks =
        [
            new HelpAskFacts("aa1alpha", "stream the tiles", "alpha", "Open", [], Deletable: true),
            new HelpAskFacts("ab1beta0", "fix the stall", "beta", "Open", [], Deletable: true),
        ],
        QuestRecords = [Quest("qa1alpha", "alpha", "engine"), Quest("qb1beta0", "beta", "game")],
        Work =
        [
            QuestWork(Quest("qa1alpha", "alpha", "engine")),
            QuestWork(Quest("qb1beta0", "beta", "game")),
            // As WorkPausing.PlanAsync reads an ask's work: the ask as the service answered it, beside its pieces.
            WorkPausing.Plan(new WorkPieces(WorkScope.Ask, "ab1beta0", [], [], [], []), DriverConfig.Empty, NonePaused, AskOfBeta) with
            {
                Ask = AskOfBeta,
            },
        ],
        History =
        [
            History(HistoryScope.Quest, HistoryKinds.Quest, "qa1alpha", "alpha"),
            History(HistoryScope.Quest, HistoryKinds.Quest, "qb1beta0", "beta"),
            History(HistoryScope.Ask, HistoryKinds.Ask, "ab1beta0", "beta"),
            History(HistoryScope.Failed, HistoryKinds.Failed, "qb1beta0", "beta"),
        ],
        Sessions =
        [
            new SessionRecord("sa1alpha", "engine", "completed") { Workspace = "alpha", Deletable = true },
            new SessionRecord("sb1beta0", "game", "completed") { Workspace = "beta", Deletable = true },
            new SessionRecord("sa2alpha", "engine", "running") { Workspace = "alpha" },
            new SessionRecord("sb2beta0", "game", "running") { Workspace = "beta" },
        ],
        Placed =
        [
            new SessionGrouping("sa1alpha", SessionGroup.Ended, "completed"),
            new SessionGrouping("sb1beta0", SessionGroup.Ended, "completed"),
            new SessionGrouping("sa2alpha", SessionGroup.You, "waiting"),
            new SessionGrouping("sb2beta0", SessionGroup.Review, "review"),
            new SessionGrouping("sx9nowhere", SessionGroup.Ended, "completed"),
        ],
        // ASKSCOPE1c3: a retry's quest, parked for alpha's repository and held for beta's.
        Parked = [new ParkedQuest("qa1alpha", "engine")],
        Held = [new HeldQuest("qb1beta0", "game", "sb1beta0")],
    };

    private static HelpMachineFacts Bound(string? circle) => Two with { Circle = circle };

    private static string? Refusal(HelpProposal proposal, string? circle = "alpha") =>
        HelpProposals.Plan(proposal, DriverConfig.Empty, Bound(circle)).Refusal;

    private static void Outside(HelpProposal proposal, string names)
    {
        var refused = Refusal(proposal);
        Assert.NotNull(refused);
        Assert.StartsWith("this conversation is workspace `alpha`'s", refused);
        Assert.Contains(names, refused);
        // The whole machine's conversation is never refused for where it reaches.
        Assert.DoesNotContain("this conversation is workspace", Refusal(proposal, circle: null) ?? "");
    }

    private static void Inside(HelpProposal proposal) =>
        Assert.DoesNotContain("this conversation is workspace", Refusal(proposal) ?? "");

    private static HelpProposal Ask(string workspace) =>
        new("p1", "ask", "ask", null, workspace, null, "fix the stall", "why", "h1", "proposed");

    [Fact]
    public void An_ask_at_another_workspace_is_refused_and_one_at_its_own_is_judged_as_before()
    {
        Outside(Ask("beta"), "workspace `beta`");
        Outside(Ask("default"), "workspace `default`");
        Assert.Null(Refusal(Ask("alpha")));
        Assert.Null(Refusal(Ask(" ALPHA ")));
        Assert.Null(Refusal(Ask("beta"), circle: null));
    }

    /// <summary>🔴 A setting naming another workspace's repository, or another workspace, is refused; the machine's own are not.</summary>
    [Fact]
    public void A_setting_naming_another_workspace_s_repository_is_refused()
    {
        Outside(Setting("drive", target: "game"), "`game`");
        Outside(Setting("line", workspace: "beta", value: "trunk"), "workspace `beta`");
        Outside(Setting("across", target: "engine", value: "write-to game"), "`game`");
        Inside(Setting("drive", target: "engine"));
        Inside(Setting("cap", value: "3"));
        // A name registered nowhere is the kind's own refusal, as before.
        Assert.Contains("not registered", Refusal(Setting("drive", target: "nowhere")));
    }

    [Fact]
    public void A_setup_a_move_and_a_workflow_naming_another_workspace_are_refused()
    {
        Outside(Of("setup", "setup", "game"), "`game`");
        Inside(Of("setup", "setup", "engine"));

        Outside(Of("repository", "wire", "engine", "beta"), "workspace `beta`");
        Outside(Of("repository", "wire", "game", "alpha"), "`game`");

        Outside(Of("workflow", "new") with { Scope = "game", Workflow = "docs-flow" }, "`game`");
        Outside(Of("workflow", "new") with { Scope = "beta", Workflow = "docs-flow" }, "`beta`");
        Inside(Of("workflow", "new") with { Scope = "engine", Workflow = "docs-flow" });
        Inside(Of("workflow", "new") with { Scope = "alpha", Workflow = "docs-flow" });
    }

    [Fact]
    public void A_hand_off_a_go_and_a_plugin_from_another_workspace_s_repository_are_refused()
    {
        Outside(Of("hand", "hand", "feature/qb1-stall"), "`game`");
        Outside(Of("hand", "hand", "sa0") with { Repository = "game" }, "`game`");
        Inside(Of("hand", "hand", "feature/qa1-tiles"));

        Outside(Of("go", "go", "repositories") with { Repository = "game" }, "`game`");
        Outside(Of("go", "go", "repositories") with { Workspace = "beta" }, "workspace `beta`");
        Outside(Of("plugin", "add", "x") with { Repository = "game", Folder = "plugins/x" }, "`game`");
        Outside(Of("clear", "workspace", "beta"), "workspace `beta`");
        Inside(Of("clear", "workspace", "alpha"));
    }

    /// <summary>A look and a press over every workspace at once is refused from a conversation bound to one; its own is judged.</summary>
    [Fact]
    public void A_sync_over_every_workspace_is_refused_and_over_its_own_is_judged()
    {
        Outside(Of("sync", "sync"), "every workspace");
        Outside(Of("sync", "sync") with { Workspace = "beta" }, "workspace `beta`");
        Outside(Of("sync", "sync", "game"), "`game`");
        Inside(Of("sync", "sync") with { Workspace = "alpha" });
        Inside(Of("sync", "sync", "engine"));
    }

    /// <summary>
    /// 🔴 ASKSCOPE1c2: a proposal naming a record by its id is judged against the workspace that record is filed under, as the
    /// facts its kind reads hold it. A delete, an accept, a pause, a clear and an archive of <c>beta</c>'s quest, ask or session,
    /// and a go to one of them, are refused from <c>alpha</c>'s conversation, naming the record and its workspace.
    /// </summary>
    [Fact]
    public void A_proposal_naming_another_workspace_s_record_by_id_is_refused()
    {
        const string BetaQuest = "quest `#qb1beta0`, of workspace `beta`";
        const string BetaAsk = "ask `#ab1beta0`, of workspace `beta`";
        const string BetaSession = "session `sb1beta0`, of workspace `beta`";

        Outside(Of("delete", "quest", "qb1beta0"), BetaQuest);
        Outside(Of("delete", "ask", "#AB1BETA0"), BetaAsk);
        Outside(Of("delete", "session", "sb1beta0"), BetaSession);
        Outside(Of("accept", "accept", "#qb1beta0"), BetaQuest);
        Outside(Of("pause", "pause", "qb1beta0"), BetaQuest);
        Outside(Of("pause", "pause", "ab1beta0", value: "ask"), BetaAsk);
        Outside(Of("clear", "quest", "qb1beta0"), BetaQuest);
        Outside(Of("clear", "ask", "ab1beta0"), BetaAsk);
        Outside(Of("clear", "failed", "qb1beta0"), BetaQuest);
        Outside(Of("session", "archive", "sa1alpha, sb1beta0"), BetaSession);
        Outside(Of("session", "unarchive", "sb1beta0"), BetaSession);
        Outside(Of("go", "go", "quests") with { Item = "qb1beta0" }, BetaQuest);
        Outside(Of("go", "go", "quests") with { Item = "ask:ab1beta0" }, BetaAsk);
        Outside(Of("go", "go", "sessions") with { Item = "#sb1beta0" }, BetaSession);
    }

    /// <summary>
    /// 🔴 ASKSCOPE1c3: <c>ended</c> archives the conversation's workspace's ended sessions alone, and the list's kept counts are its
    /// own; a placed row no record files is excluded under a circle, and nothing is filtered under none.
    /// </summary>
    [Fact]
    public void Archive_what_ended_takes_only_the_conversation_s_workspace_s_sessions()
    {
        var own = HelpProposals.Plan(Of("session", "archive", value: "ended"), DriverConfig.Empty, Bound("alpha"));
        Assert.Equal(["sa1alpha"], own.Sessions);
        Assert.Contains("Archives 1 session that ended.", own.Describe);
        Assert.Contains("Kept in the list: 1 waiting on you.", own.Describe);
        Assert.DoesNotContain("to review", own.Describe);

        var whole = HelpProposals.Plan(Of("session", "archive", value: "ended"), DriverConfig.Empty, Bound(null));
        Assert.Equal(["sa1alpha", "sb1beta0", "sx9nowhere"], whole.Sessions);
        Assert.Contains("Kept in the list: 1 waiting on you, 1 to review.", whole.Describe);
    }

    /// <summary>
    /// 🔴 ASKSCOPE1c3: a retry names a quest by its repository's workspace, so another workspace's parked or held quest is refused,
    /// and its own is judged as before. A quest in neither list names nothing, and its kind refuses it.
    /// </summary>
    [Fact]
    public void A_retry_of_another_workspace_s_quest_is_refused_and_its_own_is_judged()
    {
        Outside(Setting("retry", target: "qb1beta0"), "quest `#qb1beta0`, of workspace `beta`");
        Outside(Setting("retry", target: "#QB1BETA0"), "quest `#qb1beta0`, of workspace `beta`");
        Inside(Setting("retry", target: "qa1alpha"));
        Assert.Null(Refusal(Setting("retry", target: "qa1alpha")));
        Assert.Contains("neither parked nor held", Refusal(Setting("retry", target: "zz000000")));
        Assert.Null(Refusal(Setting("retry", target: "qb1beta0"), circle: null));

        // A repository registered in several workspaces is the circle's where any registration is.
        var shared = Bound("alpha") with
        {
            Registered = [.. Two.Registered, ("game", "alpha", "/alpha/game")],
        };
        Assert.DoesNotContain("this conversation is workspace", HelpProposals.Plan(Setting("retry", target: "qb1beta0"), DriverConfig.Empty, shared).Refusal ?? "");
    }

    /// <summary>
    /// ASKSCOPE1c2: a record of the conversation's own workspace is judged by its kind as before, and accepted where its kind
    /// accepts it. A record not found names no workspace, so its kind's own refusal is said, as before: the scope fails open there.
    /// </summary>
    [Fact]
    public void A_proposal_naming_its_own_workspace_s_record_is_judged_by_its_kind()
    {
        Assert.Null(Refusal(Of("delete", "quest", "qa1alpha")));
        Assert.Null(Refusal(Of("delete", "ask", "aa1alpha")));
        Assert.Null(Refusal(Of("delete", "session", "sa1alpha")));
        Assert.Null(Refusal(Of("clear", "quest", "qa1alpha")));
        Assert.Null(Refusal(Of("session", "archive", "sa1alpha")));
        Assert.Null(Refusal(Of("go", "go", "quests") with { Item = "qa1alpha" }));
        Assert.Null(Refusal(Of("go", "go", "quests") with { Item = "ask:aa1alpha" }));
        Assert.Null(Refusal(Of("go", "go", "sessions") with { Item = "sa1alpha" }));
        Inside(Of("accept", "accept", "qa1alpha"));
        Inside(Of("pause", "pause", "qa1alpha"));

        Assert.Equal("there is no quest `#zz000000` on this machine.", Refusal(Of("delete", "quest", "zz000000")));
        Assert.Equal("there is no ask `#zz000000` on this machine.", Refusal(Of("go", "go", "quests") with { Item = "ask:zz000000" }));
        Assert.Equal("there is no session `zz000000` on this machine.", Refusal(Of("session", "archive", "zz000000")));
        Assert.Equal("there is no quest `#zz000000` on this machine.", Refusal(Of("accept", "accept", "zz000000")));
        Assert.Equal("there is no quest `#zz000000` on this machine.", Refusal(Of("pause", "pause", "zz000000", value: "quest")));
        Assert.Equal("there is no quest `#zz000000` on this machine.", Refusal(Of("clear", "quest", "zz000000")));
    }

    /// <summary>
    /// The circle is read from the conversation's record: Ask Daoris's, its room's workspace, so one going on in <c>help</c>, where
    /// it started before each workspace had a room, and <c>default</c>'s, are bound to none; any other record binds nothing.
    /// </summary>
    [Fact]
    public void The_circle_is_the_record_s_room_s_workspace()
    {
        SessionRecord Help(string? workspace, string? tree) =>
            new("h1", HelpRoom.Repository, "completed") { Kind = "chat", Workspace = RemoteTarget.Workspace(workspace), Tree = tree };

        Assert.Equal("alpha", HelpCircle.OfRecord(_home, Help("alpha", HelpRoom.PathOf(_home, "alpha"))));
        Assert.Equal("alpha", HelpCircle.OfRecord(_home, Help("alpha", tree: null)));
        Assert.Null(HelpCircle.OfRecord(_home, Help("alpha", HelpRoom.PathOf(_home))));
        Assert.Null(HelpCircle.OfRecord(_home, Help(null, HelpRoom.PathOf(_home))));
        Assert.Null(HelpCircle.OfRecord(_home, new SessionRecord("c1", "engine", "completed") { Kind = "chat", Workspace = "alpha" }));
        Assert.Null(HelpCircle.OfRecord(_home, null));
    }
}
