using Daoris.Driver;
using Lyntai.Agents;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The accounts' translation (the agent-lyntai design §4.2): Daoris keeps its walk and its records, so the layer is handed only the account
/// the walk picked, under the registration's key, its agent where one is set and else the door's own id, so the layer never refuses
/// another provider's account; the tool's own sign-in is handed none. Proven on registrations the tests define.
/// </summary>
public sealed class LayerAccountsTests
{
    private static AgentProviderDescriptor Descriptor(string id, string? agent) => new() { Id = id, Doors = [], Agent = agent };

    [Fact]
    public void A_picked_account_is_handed_under_the_registration_s_agent_with_its_name_and_home()
    {
        var handed = LayerAccounts.Of(Descriptor("tests-acp", agent: "tests"), "account-1", Path.Combine("homes", "account-1"));

        Assert.Equal(("tests", "account-1", Path.Combine("homes", "account-1")), (handed!.Provider, handed.Name, handed.Home));
    }

    [Fact]
    public void A_registration_naming_no_agent_is_handed_its_own_id_and_the_tool_s_own_sign_in_none()
    {
        Assert.Equal("tests-acp", LayerAccounts.Of(Descriptor("tests-acp", agent: null), "account-1", "home")!.Provider);
        Assert.Null(LayerAccounts.Of(Descriptor("tests-acp", agent: "tests"), profile: null, "home"));
    }
}
