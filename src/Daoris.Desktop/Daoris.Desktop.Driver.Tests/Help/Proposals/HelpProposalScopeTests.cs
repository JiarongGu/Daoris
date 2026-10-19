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

    /// <summary><c>engine</c> is <c>alpha</c>'s, <c>game</c> is <c>beta</c>'s; each landed a branch.</summary>
    private static readonly HelpMachineFacts Two = Facts with
    {
        Workspaces = ["default", "alpha", "beta"],
        Registered = [("engine", "alpha", "/alpha/engine"), ("game", "beta", "/beta/game")],
        Landed =
        [
            new("engine", "alpha", "feature/qa1-tiles", "main", "a1b2c3d", "sa0", "qa1", "Stream the tiles", At),
            new("game", "beta", "feature/qb1-stall", "main", "b2c3d4e", "sb0", "qb1", "Fix the stall", At),
        ],
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
