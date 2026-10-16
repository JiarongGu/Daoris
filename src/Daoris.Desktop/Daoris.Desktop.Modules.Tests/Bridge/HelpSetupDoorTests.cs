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

        var applied = await AnswerAsync(module, "HELP_APPLY", new { id = "p1a2b3c4" });

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
