using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>The claim a proposal's Apply takes is held by one until it is let go, and one left by a run long gone is taken over.</summary>
public sealed class HelpProposalClaimTests : DriverModuleBridge
{
    private const string Go = """{"id":"p1a2b3c4","kind":"go","door":"go","target":"quests","workspace":null,"value":null,"sentence":null,"why":"look","state":"proposed","by":{"session":"h1"}}""";

    [Fact]
    public void A_claim_is_held_by_one_until_it_is_let_go()
    {
        Proposal(Go);
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
        Proposal(Go);
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
}
