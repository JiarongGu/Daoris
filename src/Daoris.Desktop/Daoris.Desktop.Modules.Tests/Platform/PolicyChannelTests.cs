using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Who may change the policy is read from the pipe's proof, never from the payload: Ask Daoris's conversation by its proved session, a working
/// session's own press as an agent's, the owner's window or card as the owner, and a terminal tier or any press proving nothing as none.
/// </summary>
public sealed class PolicyChannelTests
{
    [Fact]
    public void Each_door_reads_as_the_channel_it_proves()
    {
        Assert.Equal(PolicyChannel.Of("s-ask"), PolicyRoutes.ChannelOf(TerminalTierBy.AskDaoris, "s-ask", "s-named"));
        Assert.Equal(PolicyChannel.Agent("s-work"), PolicyRoutes.ChannelOf(TerminalTierBy.Session, "s-work", "s-ask"));
        Assert.Equal(PolicyChannel.OwnerPress("s-named"), PolicyRoutes.ChannelOf(TerminalTierBy.Card, null, "s-named"));
        Assert.Equal(PolicyChannel.OwnerPress(null), PolicyRoutes.ChannelOf(TerminalTierBy.Window, null, null));
        Assert.Equal(PolicyChannel.Keyless, PolicyRoutes.ChannelOf(TerminalTierBy.Terminal, null, "s-ask"));
        Assert.Equal(PolicyChannel.Keyless, PolicyRoutes.ChannelOf(TerminalTierBy.AskDaoris, null, "s-ask"));
    }

    [Fact]
    public void A_session_named_in_the_payload_makes_no_session_of_a_process_that_proves_none()
    {
        Assert.Null(PolicyRoutes.ChannelOf(TerminalTierBy.Terminal, null, "s-ask").Session);
        Assert.Null(PolicyRoutes.ChannelOf(TerminalTierBy.Session, null, "s-ask").Session);
    }

    [Theory]
    [InlineData("POLICY_PROPOSE")]
    [InlineData("POLICY_APPLY")]
    [InlineData("POLICY_PROPOSALS")]
    public void The_policys_doors_are_answered_by_the_driver_module(string route) => Assert.Contains(route, DriverModule.Routes);
}
