using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// Ask Daoris's proposals: a change it wants is a file under the driver's home, shaped
/// here and judged by the driver with the route's own code before the person sees it. Nothing here
/// applies anything: every change is the person's press.
/// </summary>
/// <remarks>What holds for every kind. Each kind's writer is held beside this under <c>Help/</c>.</remarks>
public sealed class HelpProposalBoxTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_proposal_needs_its_reason_and_a_home()
    {
        Assert.Null(Box().ProposeSetting(new SettingChange("helpers", "engine", null, "--mode on"), " ", "h1", Now).Id);
        Assert.Null(Box().ProposeAsk(" ", "work", "a reason", "h1", Now).Id);
        Assert.Null(new HelpProposalBox(null).ProposeSetting(new SettingChange("helpers", "engine", null, "--mode on"), "a reason", "h1", Now).Id);
        Assert.Null(Box().ProposeAgent("update", "claude-code", null, " ", "h1", Now).Id);
        Assert.Null(new HelpProposalBox(null).ProposeGo("quests", null, null, null, "a reason", "h1", Now).Id);
    }
}
