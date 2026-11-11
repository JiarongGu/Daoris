using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// Ask Daoris's proposals: a file under the driver's home, shaped here and judged by the driver; nothing here applies anything.
/// </summary>
public sealed class HelpProposalBoxTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_proposal_needs_its_reason_and_a_home()
    {
        Assert.Null(Box().ProposeGo("quests", null, null, null, " ", "h1", Now).Id);
        Assert.Null(new HelpProposalBox(null).ProposeGo("quests", null, null, null, "a reason", "h1", Now).Id);
    }
}
