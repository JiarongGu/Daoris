using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>A proposal naming another workspace, or every one at once, is judged by its kind alone, never refused for the workspace.</summary>
public sealed class HelpProposalScopeTests : HelpProposalsFixture
{
    private static readonly DateTimeOffset At = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    /// <summary><c>engine</c> is <c>alpha</c>'s, <c>game</c> is <c>beta</c>'s, each with a quest, an ask and a session.</summary>
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

    private static void Reaches(HelpProposal proposal)
    {
        Assert.DoesNotContain("this conversation is workspace", Refusal(proposal) ?? "");
        Assert.Equal(Refusal(proposal, circle: null), Refusal(proposal));
    }

    [Fact]
    public void A_move_and_a_workflow_naming_another_workspace_are_judged_by_their_kinds()
    {
        Reaches(Of("repository", "wire", "engine", "beta"));
        Reaches(Of("repository", "wire", "game", "alpha"));
        Reaches(Of("workflow", "new") with { Scope = "game", Workflow = "docs-flow" });
        Reaches(Of("workflow", "new") with { Scope = "beta", Workflow = "docs-flow" });
    }

    [Fact]
    public void A_go_to_another_workspace_is_judged_by_its_kind()
    {
        Reaches(Of("go", "go", "repositories") with { Repository = "game" });
        Reaches(Of("go", "go", "repositories") with { Workspace = "beta" });
    }

    [Fact]
    public void A_sync_over_every_workspace_or_another_is_judged_by_its_kind()
    {
        Reaches(Of("sync", "sync"));
        Reaches(Of("sync", "sync") with { Workspace = "beta" });
        Reaches(Of("sync", "sync", "game"));
    }

    /// <summary>A go to another workspace's quest, ask or session by its id is judged by its kind, which finds the record.</summary>
    [Fact]
    public void A_proposal_naming_another_workspace_s_record_by_id_is_judged_by_its_kind()
    {
        Reaches(Of("go", "go", "quests") with { Item = "qb1beta0" });
        Reaches(Of("go", "go", "quests") with { Item = "ask:ab1beta0" });
        Reaches(Of("go", "go", "sessions") with { Item = "#sb1beta0" });

        Assert.Null(Refusal(Of("go", "go", "sessions") with { Item = "sb1beta0" }));
    }

    [Fact]
    public void A_record_not_found_is_its_kind_s_own_refusal()
    {
        Assert.Equal("there is no ask `#zz000000` on this machine.", Refusal(Of("go", "go", "quests") with { Item = "ask:zz000000" }));
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
