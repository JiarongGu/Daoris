using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// OWNSPACE1b-B (D175 point 5): where a sign-in's end says the account runs. A scope that names no account takes all of
/// Daoris's accounts in turn (OWNSPACE1a), so an account no list holds there is not one "no start runs on"; where this
/// machine does name others, the old sentence is still true.
/// </summary>
public sealed class AgentActionsPlacedTests
{
    [Fact]
    public void An_account_in_a_scope_that_names_nothing_is_said_to_be_taken_in_turn_not_to_run_nothing()
    {
        var said = AgentActions.PlacesSaid(new HarnessSettings(), "claude-code", "account-1", []);

        Assert.DoesNotContain("no start runs", said, StringComparison.Ordinal);
        Assert.Contains("in turn", said, StringComparison.Ordinal);
    }

    [Fact]
    public void An_account_the_machine_s_list_leaves_out_still_runs_no_start()
    {
        var settings = new HarnessSettings().WithRotation("claude-code", ["account-1", "account-2"]);

        var said = AgentActions.PlacesSaid(settings, "claude-code", "account-3", []);

        Assert.Contains("no start runs on it", said, StringComparison.Ordinal);
    }

    [Fact]
    public void An_account_a_list_holds_says_where_it_runs()
    {
        var settings = new HarnessSettings().WithRotation("claude-code", ["account-1"]);

        var said = AgentActions.PlacesSaid(settings, "claude-code", "account-1", settings.PlacesOf("claude-code", "account-1"));

        Assert.Equal("`account-1` runs work in this machine's list.", said);
    }
}
