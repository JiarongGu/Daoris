namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What a sign-in's end says of a new account: every account of Daoris's serves every workspace,
/// so it says the account joined Daoris's accounts, and names no list.
/// </summary>
public sealed class AgentActionsPlacedTests
{
    [Fact]
    public void A_new_account_is_said_to_have_joined_Daoris_s_accounts_naming_no_list()
    {
        var said = AgentActions.JoinedSaid("seat");

        Assert.Equal("`seat` joined Daoris's accounts: every workspace's work may run on it.", said);
        Assert.DoesNotContain("list", said, StringComparison.Ordinal);
        Assert.DoesNotContain("this machine", said, StringComparison.Ordinal);
    }
}
