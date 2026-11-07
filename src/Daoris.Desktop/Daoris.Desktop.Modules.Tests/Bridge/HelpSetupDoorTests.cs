using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Ask Daoris's <c>setup</c> kind has one door, <c>place</c>. The repository-alone <c>setup</c> door is refused naming it, and the
/// claim a proposal's Apply takes is held by one until it is let go.
/// </summary>
public sealed class HelpSetupDoorTests : DriverModuleBridge
{
    private readonly SetupStandIn _world = new();

    private const string Setup = """{"id":"p1a2b3c4","kind":"setup","door":"setup","target":"atlas","workspace":null,"value":null,"sentence":null,"why":"include it","state":"proposed","by":{"session":"h1"}}""";

    /// <summary>The old door is never drawn: its card is settled refused, in words that name the place.</summary>
    [Fact]
    public async Task The_setup_door_is_refused_naming_the_place_door()
    {
        (StaffedRoles.Config("claude-code-acp")).WithDrivable("atlas", true).WithTrees("atlas", true).Save(DriverConfigPath);
        var module = await UpAsync();
        Proposal(Setup);

        var drawn = await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" });

        Assert.Empty(drawn.GetProperty("proposals").EnumerateArray());
        Assert.Equal("refused", HelpProposals.Find(Home, "p1a2b3c4")!.State);
        Assert.Empty(_world.Published);
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
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(new SetupStandIn.EmptyHost()))));
        return module;
    }
}
