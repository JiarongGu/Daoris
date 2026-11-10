using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// A proposal is never refused for the workspace it names: its conversation's workspace is its focus, and one naming another
/// workspace, a repository of another, a record of another or every workspace at once is judged by its kind alone, as the
/// whole machine's conversation's is. A list of the machine's sessions leads with the conversation's workspace's.
/// </summary>
public sealed class HelpProposalScopeTests : HelpProposalsFixture
{
    private static readonly DateTimeOffset At = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private static HistoryPlan History(HistoryScope scope, string kind, string id, string workspace) =>
        new(scope, id, [new HistoryUnitPlan(kind, id, workspace) { Quests = [id], Bytes = new HistoryBytes(1024, 0, 0, 0, 0) }]);

    /// <summary>
    /// <c>engine</c> is <c>alpha</c>'s, <c>game</c> is <c>beta</c>'s. Each has a quest, an ask and a session, as each
    /// kind's facts read them: the delete's and a go's records, the pause's works, the clear's plans, and the sessions where
    /// Sessions' list places them.
    /// </summary>
    private static readonly HelpMachineFacts Two = Facts with
    {
        Workspaces = ["default", "alpha", "beta"],
        Registered = [("engine", "alpha", "/alpha/engine"), ("game", "beta", "/beta/game")],
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
        Parked = [new ParkedQuest("qa1alpha", "engine")],
        Held = [new HeldQuest("qb1beta0", "game", "sb1beta0")],
    };

    private static HelpMachineFacts Bound(string? circle) => Two with { Circle = circle };

    private static string? Refusal(HelpProposal proposal, string? circle = "alpha") =>
        HelpProposals.Plan(proposal, DriverConfig.Empty, Bound(circle)).Refusal;

    /// <summary>The conversation of <c>alpha</c> proposes it as the whole machine's does: no refusal names a workspace it is bound to.</summary>
    private static void Reaches(HelpProposal proposal)
    {
        Assert.DoesNotContain("this conversation is workspace", Refusal(proposal) ?? "");
        Assert.Equal(Refusal(proposal, circle: null), Refusal(proposal));
    }

    private static HelpProposal Ask(string workspace) =>
        new("p1", "ask", "ask", null, workspace, null, "fix the stall", "why", "h1", "proposed");

    [Fact]
    public void An_ask_at_another_workspace_is_judged_as_one_at_its_own_is()
    {
        Reaches(Ask("beta"));
        Reaches(Ask("default"));
        Reaches(Ask("alpha"));
        Assert.Null(Refusal(Ask("beta")));
    }

    [Fact]
    public void A_setting_naming_another_workspace_s_repository_is_judged_by_its_kind()
    {
        Reaches(Setting("drive", target: "game"));
        Reaches(Setting("line", workspace: "beta", value: "trunk"));
        Reaches(Setting("across", target: "engine", value: "write-to game"));
        Reaches(Setting("cap", value: "3"));
        Assert.Contains("not registered", Refusal(Setting("drive", target: "nowhere")));
    }

    [Fact]
    public void A_setup_a_move_and_a_workflow_naming_another_workspace_are_judged_by_their_kinds()
    {
        Reaches(Of("setup", "setup", "game"));
        Reaches(Of("repository", "wire", "engine", "beta"));
        Reaches(Of("repository", "wire", "game", "alpha"));
        Reaches(Of("workflow", "new") with { Scope = "game", Workflow = "docs-flow" });
        Reaches(Of("workflow", "new") with { Scope = "beta", Workflow = "docs-flow" });
    }

    [Fact]
    public void A_go_a_plugin_and_a_clear_of_another_workspace_are_judged_by_their_kinds()
    {
        Reaches(Of("go", "go", "repositories") with { Repository = "game" });
        Reaches(Of("go", "go", "repositories") with { Workspace = "beta" });
        Reaches(Of("plugin", "add", "x") with { Repository = "game", Folder = "plugins/x" });
        Reaches(Of("clear", "workspace", "beta"));
    }

    /// <summary>A look and a press over every workspace at once, or another workspace's, are judged by the sync's own kind.</summary>
    [Fact]
    public void A_sync_over_every_workspace_or_another_is_judged_by_its_kind()
    {
        Reaches(Of("sync", "sync"));
        Reaches(Of("sync", "sync") with { Workspace = "beta" });
        Reaches(Of("sync", "sync", "game"));
    }

    /// <summary>
    /// A proposal naming a record of another workspace by its id is judged by its kind, which finds the record and plans it:
    /// a delete, an accept, a pause, a clear, an archive and a go to <c>beta</c>'s quest, ask or session.
    /// </summary>
    [Fact]
    public void A_proposal_naming_another_workspace_s_record_by_id_is_judged_by_its_kind()
    {
        Reaches(Of("clear", "quest", "qb1beta0"));
        Reaches(Of("clear", "ask", "ab1beta0"));
        Reaches(Of("clear", "failed", "qb1beta0"));
        Reaches(Of("go", "go", "quests") with { Item = "qb1beta0" });
        Reaches(Of("go", "go", "quests") with { Item = "ask:ab1beta0" });
        Reaches(Of("go", "go", "sessions") with { Item = "#sb1beta0" });

        Assert.Null(Refusal(Of("go", "go", "sessions") with { Item = "sb1beta0" }));
    }

    /// <summary>A retry of a quest of another workspace is judged as its own is; a quest in neither list is its kind's own refusal.</summary>
    [Fact]
    public void A_retry_of_another_workspace_s_quest_is_judged_as_its_own_is()
    {
        Reaches(Setting("retry", target: "qb1beta0"));
        Reaches(Setting("retry", target: "#QB1BETA0"));
        Reaches(Setting("retry", target: "qa1alpha"));
        Assert.Contains("neither parked nor held", Refusal(Setting("retry", target: "zz000000")));
    }

    [Fact]
    public void A_record_not_found_is_its_kind_s_own_refusal()
    {
        Assert.Equal("there is no ask `#zz000000` on this machine.", Refusal(Of("go", "go", "quests") with { Item = "ask:zz000000" }));
        Assert.Equal("there is no quest `#zz000000` on this machine.", Refusal(Of("clear", "quest", "zz000000")));
    }

    /// <summary>
    /// The circle is read from the conversation's record: Ask Daoris's, its room's workspace, so one going on in <c>help</c>, where
    /// it started before each workspace had a room, and <c>default</c>'s, have none; any other record has none.
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
