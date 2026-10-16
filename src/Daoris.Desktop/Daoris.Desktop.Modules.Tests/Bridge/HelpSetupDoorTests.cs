using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// LAYOUT8 (D170 point 1): a set-up proposal's modules half. The judge is handed the press's own plan only when one is pending,
/// since a plan reads a line with git and asks the doctrine tool its version; the card carries the plan as the screen's
/// <c>SETUP_PLAN</c> answers it, and Apply is the press <c>SETUP_PRESS</c> and the terminal run. The press's world stands in.
/// </summary>
public sealed class HelpSetupDoorTests : DriverModuleBridge
{
    private readonly SetupStandIn _world = new();

    [Theory]
    [InlineData("setup", "setup", true)]
    [InlineData("setup", "register", true)]
    [InlineData("repository", "wire", false)]
    [InlineData("sync", "sync", false)]
    public void The_plan_is_read_for_a_set_up_proposal_only(string kind, string door, bool read)
    {
        var proposal = new HelpProposal("p1", kind, door, "atlas", null, null, null, "why", "h1", "proposed");

        Assert.Equal(read, HelpRoutes.JudgedAgainstSetups([proposal]));
        Assert.False(HelpRoutes.JudgedAgainstSetups([]));
    }

    [Fact]
    public async Task The_door_before_the_driver_is_up_is_the_cold_start_sentence()
    {
        var refused = await Assert.ThrowsAnyAsync<Exception>(
            () => Module().HelpDoors(null).SetUpAsync(new SetupPlan("atlas"), CancellationToken.None));

        Assert.Contains("still coming up", refused.Message);
    }

    /// <summary>The card shows the plan, the person applies it, and the press publishes the one ask and adds the rule.</summary>
    [Fact]
    public async Task A_set_up_is_drawn_with_its_plan_and_applied_as_the_press()
    {
        (DriverConfig.Empty with { Adapter = "claude-code-acp" }).WithDrivable("atlas", true).WithTrees("atlas", true).Save(DriverConfigPath);
        var module = await UpAsync();
        Proposal("""{"id":"p1a2b3c4","kind":"setup","door":"setup","target":"atlas","workspace":null,"value":null,"sentence":null,"why":"include it","state":"proposed","by":{"session":"h1"}}""");

        var drawn = await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" });

        var card = Assert.Single(drawn.GetProperty("proposals").EnumerateArray());
        Assert.Equal("setup", card.GetProperty("kind").GetString());
        Assert.Equal("daoris-driver setup atlas", card.GetProperty("terminal").GetString());
        var setup = card.GetProperty("setup");
        Assert.Equal("atlas", setup.GetProperty("repository").GetString());
        Assert.Equal("main", setup.GetProperty("line").GetString());
        Assert.Equal(SetupPressRules.Rules.Count, setup.GetProperty("rules").GetArrayLength());
        Assert.StartsWith("Set up this repository for every agent (", setup.GetProperty("title").GetString());
        Assert.Empty(_world.Published);

        var applied = await AnswerAsync(module, "HELP_APPLY", new { id = "p1a2b3c4", revision = card.GetProperty("revision").GetString() });

        Assert.True(applied.GetProperty("applied").GetBoolean());
        Assert.Contains("(`daoris-driver setup atlas`) — Asked as `#a1b2c3d4` in `work`.", applied.GetProperty("message").GetString());
        Assert.Equal(("work", "atlas"), (Assert.Single(_world.Published).Workspace, _world.Published[0].To));
        Assert.Equal(SetupPressRules.Rules, PermissionRules.Load(Home).Repositories["atlas"].Allow);
        Assert.Equal("applied", HelpProposals.Find(Home, "p1a2b3c4")!.State);
    }

    /// <summary>A set-up the press would refuse is never drawn: it is settled refused, in `--plan`'s words.</summary>
    [Fact]
    public async Task A_set_up_the_press_refuses_is_never_drawn()
    {
        (DriverConfig.Empty with { Adapter = "claude-code-acp" }).Save(DriverConfigPath);
        var module = await UpAsync();
        Proposal("""{"id":"p1a2b3c4","kind":"setup","door":"setup","target":"atlas","workspace":null,"value":null,"sentence":null,"why":"include it","state":"proposed","by":{"session":"h1"}}""");

        var drawn = await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" });

        Assert.Empty(drawn.GetProperty("proposals").EnumerateArray());
        var settled = HelpProposals.Find(Home, "p1a2b3c4")!;
        Assert.Equal("refused", settled.State);
    }

    private const string Setup = """{"id":"p1a2b3c4","kind":"setup","door":"setup","target":"atlas","workspace":null,"value":null,"sentence":null,"why":"include it","state":"proposed","by":{"session":"h1"}}""";

    private async Task<DriverModule> DrawnAsync()
    {
        (DriverConfig.Empty with { Adapter = "claude-code-acp" }).WithDrivable("atlas", true).WithTrees("atlas", true).Save(DriverConfigPath);
        var module = await UpAsync();
        Proposal(Setup);
        return module;
    }

    private static async Task<string> RevisionAsync(DriverModule module) =>
        (await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" })).GetProperty("proposals")[0].GetProperty("revision").GetString()!;

    /// <summary>HELPAPPLY1: a card is the plan it showed. A material change since asks again and applies nothing.</summary>
    [Fact]
    public async Task A_plan_changed_since_the_card_asks_again_and_applies_nothing()
    {
        var module = await DrawnAsync();
        var shown = await RevisionAsync(module);
        _world.Facts = _world.Facts with { Line = "develop" };

        var answer = await AnswerAsync(module, "HELP_APPLY", new { id = "p1a2b3c4", revision = shown });

        Assert.False(answer.GetProperty("applied").GetBoolean());
        Assert.True(answer.GetProperty("stands").GetBoolean());
        Assert.True(answer.GetProperty("changed").GetBoolean());
        Assert.Contains("changed since", answer.GetProperty("message").GetString());
        Assert.Empty(_world.Published);
        Assert.Equal("proposed", HelpProposals.Find(Home, "p1a2b3c4")!.State);

        // The card drawn again carries the new plan's revision, and that one is applied.
        var again = await RevisionAsync(module);
        Assert.NotEqual(shown, again);
        var applied = await AnswerAsync(module, "HELP_APPLY", new { id = "p1a2b3c4", revision = again });
        Assert.True(applied.GetProperty("applied").GetBoolean());
        Assert.Single(_world.Published);
    }

    [Fact]
    public async Task An_unchanged_plan_has_the_same_revision_each_time_it_is_drawn()
    {
        var module = await DrawnAsync();

        Assert.Equal(await RevisionAsync(module), await RevisionAsync(module));
    }

    [Fact]
    public async Task An_apply_naming_no_revision_is_refused_for_the_card_to_be_drawn_again()
    {
        var module = await DrawnAsync();

        var refused = await RefusalAsync(module, "HELP_APPLY", new { id = "p1a2b3c4" });

        Assert.Contains("revision", refused);
        Assert.Empty(_world.Published);
        Assert.Equal("proposed", HelpProposals.Find(Home, "p1a2b3c4")!.State);
    }

    /// <summary>HELPAPPLY1: two windows pressing one card at once. One runs it; the other is told, and the ask is published once.</summary>
    [Fact]
    public async Task Two_applies_at_once_run_the_proposal_once()
    {
        var module = await DrawnAsync();
        var shown = await RevisionAsync(module);

        var both = await Task.WhenAll(
            AskAsync(module, "HELP_APPLY", new { id = "p1a2b3c4", revision = shown }),
            AskAsync(module, "HELP_APPLY", new { id = "p1a2b3c4", revision = shown }));

        Assert.Equal(1, both.Count(each => each.Success));
        Assert.Single(_world.Published);
        Assert.Equal("applied", HelpProposals.Find(Home, "p1a2b3c4")!.State);
    }

    [Fact]
    public void A_claim_is_held_by_one_until_it_is_let_go()
    {
        Proposal(Setup);

        using (var first = HelpProposals.Claim(Home, "p1a2b3c4"))
        {
            Assert.NotNull(first);
            Assert.Null(HelpProposals.Claim(Home, "p1a2b3c4"));
        }

        using var after = HelpProposals.Claim(Home, "p1a2b3c4");
        Assert.NotNull(after);
    }

    [Fact]
    public void A_claim_left_by_a_run_long_gone_is_not_held_for_ever()
    {
        Proposal(Setup);
        var claim = Path.Combine(HelpProposals.FolderOf(Home), "p1a2b3c4.claim");
        File.WriteAllText(claim, "");
        File.SetLastWriteTimeUtc(claim, DateTime.UtcNow - TimeSpan.FromHours(3));

        using var taken = HelpProposals.Claim(Home, "p1a2b3c4");

        Assert.NotNull(taken);
    }

    private void Proposal(string json)
    {
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "p1a2b3c4.json"), json);
    }

    private async Task<DriverModule> UpAsync()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop) { SetupWorlds = _ => _world };
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SetupStandIn.EmptyHost())));
        return module;
    }
}
