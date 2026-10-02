using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Registration follows the line (WSSETUP5, D124 §3): each repository with a checkout here registered from what its line
/// declares, sent only where the row holds something else, and every refusal said, against a world held in memory. The
/// git half is <see cref="LineDeclarationReader"/>'s, held in the Process half.
/// </summary>
public sealed class RegistrationFollowTests
{
    private const string Declared = """{"source":"daoris@0.0.1","packs":["web"],"domain":{"summary":"The game","owns":["play"],"accepts":["bugs"],"uses":["engine"]}}""";

    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private sealed class World : RegistrationStandIn;

    private static RegistrationRow Row(string repository, string? root = "/checkouts/x", bool adopted = false) =>
        new(repository, "default", root, adopted, null, [], [], [], [], false, false, []);

    private static LineFiles OnLine(string? manifest, string? lanes = null) =>
        new("main", Commit, null) { Manifest = manifest, Lanes = lanes };

    [Fact]
    public async Task A_line_that_declares_registers_what_connect_would_send_and_the_index_is_refreshed_once()
    {
        var world = new World();
        world.Rows.Add(Row("game", "/checkouts/game"));
        world.Lines["game"] = OnLine(Declared, """{"lanes":[{"id":"core","title":"Core","paths":["src/**"]}]}""");

        var report = await RegistrationFollow.FollowAsync(world, only: null);

        var sent = Assert.Single(world.Sent);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{"repository":"game","packs":["web"],"domain":{"summary":"The game","owns":["play"],"accepts":["bugs"],"uses":["engine"]},"join":false,"shareKnowledge":false,"lanes":[{"id":"core","title":"Core","summary":"","steward":false}],"root":"/checkouts/game"}"""),
            sent), sent.ToJsonString());
        Assert.Equal(1, world.Refreshes);
        var followed = Assert.Single(report.Followed);
        Assert.Equal(RegistryOutcome.Registered, followed.Outcome);
        Assert.True(followed.Sent);
        Assert.Equal("main", followed.Line);
        Assert.Equal(Commit, followed.Commit);
        Assert.Contains("`main` at `0123456`", followed.Said);
        Assert.Equal([followed], world.Said);
    }

    [Fact]
    public async Task A_row_that_already_holds_the_lines_declaration_is_sent_nothing()
    {
        var world = new World();
        world.Rows.Add(new RegistrationRow("game", "default", "/checkouts/game", true, "The game", ["play"], ["bugs"], ["engine"], ["web"], false, false, []));
        world.Lines["game"] = OnLine(Declared);

        var report = await RegistrationFollow.FollowAsync(world, only: null);

        Assert.Empty(world.Sent);
        Assert.Equal(0, world.Refreshes);
        Assert.Equal(RegistryOutcome.Unchanged, Assert.Single(report.Followed).Outcome);
    }

    [Theory]
    [InlineData("packs")]
    [InlineData("summary")]
    [InlineData("owns")]
    [InlineData("uses")]
    [InlineData("join")]
    [InlineData("lanes")]
    [InlineData("adopted")]
    public async Task A_row_that_differs_in_any_field_the_line_declares_is_registered(string differs)
    {
        var world = new World();
        world.Rows.Add(new RegistrationRow(
            "game", "default", "/checkouts/game", differs != "adopted", differs == "summary" ? "An older game" : "The game",
            differs == "owns" ? ["play", "render"] : ["play"], ["bugs"], differs == "uses" ? [] : ["engine"],
            differs == "packs" ? [] : ["web"], differs == "join", false,
            differs == "lanes" ? [new LaneView("core", "Core", "", false)] : []));
        world.Lines["game"] = OnLine(Declared);

        var report = await RegistrationFollow.FollowAsync(world, only: null);

        Assert.Single(world.Sent);
        Assert.Equal(RegistryOutcome.Registered, Assert.Single(report.Followed).Outcome);
    }

    [Fact]
    public async Task A_blank_summary_is_the_same_as_none()
    {
        var world = new World();
        world.Rows.Add(new RegistrationRow("game", "default", "/checkouts/game", true, null, ["play"], [], [], [], false, false, []));
        world.Lines["game"] = OnLine("""{"source":"s","domain":{"summary":"  ","owns":["play"],"accepts":[]}}""");

        var report = await RegistrationFollow.FollowAsync(world, only: null);

        Assert.Empty(world.Sent);
        Assert.Equal(RegistryOutcome.Unchanged, Assert.Single(report.Followed).Outcome);
    }

    [Fact]
    public async Task A_line_with_no_manifest_is_not_set_up_and_says_which_door_sets_it_up()
    {
        var world = new World();
        world.Rows.Add(Row("game"));
        world.Lines["game"] = OnLine(null);

        var followed = Assert.Single((await RegistrationFollow.FollowAsync(world, only: null)).Followed);

        Assert.Empty(world.Sent);
        Assert.Equal(RegistryOutcome.NotSetUp, followed.Outcome);
        Assert.Contains("not set up on its line `main`", followed.Said);
        Assert.Contains("`daoris-driver setup game`", followed.Said);
    }

    [Fact]
    public async Task A_set_up_waiting_for_review_is_named_with_what_registers_it()
    {
        var world = new World();
        world.Rows.Add(Row("game"));
        world.Lines["game"] = OnLine(null);
        world.Waiting["game"] = "feature/12-set-up";

        var followed = Assert.Single((await RegistrationFollow.FollowAsync(world, only: null)).Followed);

        Assert.Equal(RegistryOutcome.NotSetUp, followed.Outcome);
        Assert.Contains("`feature/12-set-up`", followed.Said);
        Assert.Contains("*Bring up to date*", followed.Said);
    }

    [Fact]
    public async Task A_repository_with_no_line_is_said_with_the_doors_that_set_one()
    {
        var world = new World();
        world.Rows.Add(Row("game"));

        var followed = Assert.Single((await RegistrationFollow.FollowAsync(world, only: null)).Followed);

        Assert.Equal(RegistryOutcome.NoLine, followed.Outcome);
        Assert.Contains("none is set, and git names none", followed.Said);
        Assert.Contains("`daoris driver line game <branch>`", followed.Said);
        Assert.Null(followed.Line);
    }

    [Fact]
    public async Task A_manifest_that_does_not_read_keeps_the_row_and_says_why()
    {
        var world = new World();
        world.Rows.Add(Row("game"));
        world.Lines["game"] = OnLine("""{"packs":[]}""");

        var followed = Assert.Single((await RegistrationFollow.FollowAsync(world, only: null)).Followed);

        Assert.Empty(world.Sent);
        Assert.Equal(RegistryOutcome.Unreadable, followed.Outcome);
        Assert.Contains("has no 'source'", followed.Said);
        Assert.Contains("keeps what it held", followed.Said);
    }

    [Fact]
    public async Task A_lanes_file_that_does_not_read_is_refused_whole_naming_each_problem()
    {
        var world = new World();
        world.Rows.Add(Row("game"));
        world.Lines["game"] = OnLine(Declared, """{"lanes":[{"id":"Core"},{"id":"a","paths":["a"],"steward":true},{"id":"b","paths":["b"],"steward":true}]}""");

        var followed = Assert.Single((await RegistrationFollow.FollowAsync(world, only: null)).Followed);

        Assert.Empty(world.Sent);
        Assert.Equal(RegistryOutcome.LanesUnreadable, followed.Outcome);
        Assert.Contains("lane 'Core': its id must be", followed.Said);
        Assert.Contains("lane 'Core': it has no paths", followed.Said);
        Assert.Contains("two stewards (a, b)", followed.Said);
    }

    [Fact]
    public async Task A_manifest_that_declares_nothing_records_adoption_and_keeps_the_declaration_the_row_held()
    {
        var world = new World();
        world.Rows.Add(new RegistrationRow("game", "default", "/checkouts/game", false, "Said by hand", ["play"], [], ["engine"], [], false, false, []));
        world.Lines["game"] = OnLine("""{"source":"daoris@0.0.1","packs":["web"],"domain":{"summary":"","owns":[],"accepts":[]}}""");

        var followed = Assert.Single((await RegistrationFollow.FollowAsync(world, only: null)).Followed);

        var sent = Assert.Single(world.Sent);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{"repository":"game","packs":["web"],"domain":{"summary":"Said by hand","owns":["play"],"accepts":[],"uses":["engine"]},"join":false,"shareKnowledge":false,"lanes":[],"root":"/checkouts/game"}"""),
            sent), sent.ToJsonString());
        Assert.Equal(RegistryOutcome.DeclaresNothing, followed.Outcome);
        Assert.True(followed.Sent);
        Assert.Contains("adopted, declares nothing", followed.Said);
    }

    [Fact]
    public async Task A_manifest_that_declares_nothing_over_a_row_that_held_none_sends_no_declaration()
    {
        var world = new World();
        world.Rows.Add(Row("game", "/checkouts/game"));
        world.Lines["game"] = OnLine("""{"source":"daoris@0.0.1"}""");

        await RegistrationFollow.FollowAsync(world, only: null);

        var sent = Assert.Single(world.Sent);
        Assert.Null(sent["domain"]);
        Assert.True(sent.ContainsKey("domain"));
    }

    [Fact]
    public async Task Adoption_already_recorded_with_nothing_declared_is_sent_nothing_again()
    {
        var world = new World();
        world.Rows.Add(Row("game", "/checkouts/game", adopted: true));
        world.Lines["game"] = OnLine("""{"source":"daoris@0.0.1"}""");

        var followed = Assert.Single((await RegistrationFollow.FollowAsync(world, only: null)).Followed);

        Assert.Empty(world.Sent);
        Assert.Equal(RegistryOutcome.DeclaresNothing, followed.Outcome);
        Assert.False(followed.Sent);
    }

    [Fact]
    public async Task A_root_that_is_a_linked_worktree_is_refused()
    {
        var world = new World();
        world.Rows.Add(Row("game", "/trees/game-1"));
        world.Worktrees.Add("/trees/game-1");
        world.Lines["game"] = OnLine(Declared);

        var followed = Assert.Single((await RegistrationFollow.FollowAsync(world, only: null)).Followed);

        Assert.Empty(world.Sent);
        Assert.Equal(RegistryOutcome.Worktree, followed.Outcome);
        Assert.Contains("linked worktree", followed.Said);
    }

    [Fact]
    public async Task Every_row_with_a_checkout_here_is_followed_and_a_teammates_row_is_left_to_its_machine()
    {
        var world = new World();
        world.Rows.Add(Row("game", "/checkouts/game"));
        world.Rows.Add(Row("engine", root: null));
        world.Lines["game"] = OnLine(Declared);

        var report = await RegistrationFollow.FollowAsync(world, only: null);

        Assert.Equal(["game"], report.Followed.Select(followed => followed.Repository));
    }

    [Fact]
    public async Task A_repository_named_is_followed_alone_and_one_with_no_checkout_or_no_row_is_said()
    {
        var world = new World();
        world.Rows.Add(Row("game", "/checkouts/game"));
        world.Rows.Add(Row("other", "/checkouts/other"));
        world.Rows.Add(Row("engine", root: null));
        world.Lines["game"] = OnLine(Declared);

        var named = await RegistrationFollow.FollowAsync(world, only: ["GAME"]);
        var teammate = await RegistrationFollow.FollowAsync(world, only: ["engine"]);
        var nobody = await RegistrationFollow.FollowAsync(world, only: ["ghost"]);

        Assert.Equal(["game"], named.Followed.Select(followed => followed.Repository));
        Assert.Equal(RegistryOutcome.NoCheckout, Assert.Single(teammate.Followed).Outcome);
        var missing = Assert.Single(nobody.Followed);
        Assert.Equal(RegistryOutcome.NotOnRegistry, missing.Outcome);
        Assert.Equal("ghost", missing.Repository);
    }

    [Fact]
    public async Task A_registration_the_service_refuses_is_said_in_its_words_and_nothing_is_refreshed()
    {
        var world = new World { RefuseWith = "the workspace `aurora` is another deployment's" };
        world.Rows.Add(Row("game"));
        world.Lines["game"] = OnLine(Declared);

        var followed = Assert.Single((await RegistrationFollow.FollowAsync(world, only: null)).Followed);

        Assert.Equal(RegistryOutcome.Refused, followed.Outcome);
        Assert.Contains("the workspace `aurora` is another deployment's", followed.Said);
        Assert.Equal(0, world.Refreshes);
    }

    [Fact]
    public async Task To_a_service_elsewhere_the_root_is_not_sent()
    {
        var world = new World { LocalService = false };
        world.Rows.Add(Row("game", "/checkouts/game"));
        world.Lines["game"] = OnLine(Declared);

        await RegistrationFollow.FollowAsync(world, only: null);

        Assert.False(Assert.Single(world.Sent).ContainsKey("root"));
    }

    [Fact]
    public async Task Two_registrations_ask_for_one_refresh_and_a_refresh_that_fails_is_said()
    {
        var world = new World { RefreshFails = "the index could not be refreshed: busy" };
        world.Rows.Add(Row("game", "/checkouts/game"));
        world.Rows.Add(Row("engine", "/checkouts/engine"));
        world.Lines["game"] = OnLine(Declared);
        world.Lines["engine"] = OnLine(Declared.Replace("The game", "The engine", StringComparison.Ordinal));

        var report = await RegistrationFollow.FollowAsync(world, only: null);

        Assert.Equal(2, world.Sent.Count);
        Assert.Equal(1, world.Refreshes);
        Assert.Equal("the index could not be refreshed: busy", report.Refresh);
    }

    /// <summary>
    /// What a tick or a terminal says of each outcome: what changed, and what the person must fix, and never a standing
    /// state, which the row and the machine log keep (a repository nobody set up would otherwise be said at every start).
    /// </summary>
    [Theory]
    [InlineData(RegistryOutcome.Registered, true, true)]
    [InlineData(RegistryOutcome.DeclaresNothing, true, true)]
    [InlineData(RegistryOutcome.DeclaresNothing, false, false)]
    [InlineData(RegistryOutcome.Unchanged, false, false)]
    [InlineData(RegistryOutcome.NotSetUp, false, false)]
    [InlineData(RegistryOutcome.NoLine, false, false)]
    [InlineData(RegistryOutcome.NoCheckout, false, false)]
    [InlineData(RegistryOutcome.Unreadable, false, true)]
    [InlineData(RegistryOutcome.LanesUnreadable, false, true)]
    [InlineData(RegistryOutcome.Worktree, false, true)]
    [InlineData(RegistryOutcome.Refused, false, true)]
    [InlineData(RegistryOutcome.NotOnRegistry, false, true)]
    public void A_tick_says_what_changed_and_what_must_be_fixed(string outcome, bool sent, bool said)
    {
        var line = RegistrationFollow.EventLine(new RegistrationFollowed("game", outcome, "words", Sent: sent));

        Assert.Equal(said, line is not null);
        if (said) Assert.Equal("registry  game: words", line);
    }

    [Fact]
    public void Every_outcome_is_a_word_from_one_list_and_refusals_are_told_from_the_rest()
    {
        Assert.Equal(11, RegistryOutcome.All.Count);
        Assert.All(RegistryOutcome.All, word => Assert.Matches("^[a-z]+(-[a-z]+)*$", word));
        Assert.Equal(
            [RegistryOutcome.Registered, RegistryOutcome.Unchanged, RegistryOutcome.DeclaresNothing],
            RegistryOutcome.All.Where(word => !RegistryOutcome.IsRefusal(word)));
    }
}
