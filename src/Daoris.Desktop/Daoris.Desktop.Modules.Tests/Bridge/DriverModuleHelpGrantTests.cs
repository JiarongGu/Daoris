using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Restricted mode's configured access over the bridge (HELPMODE1c2b, D183's note): <c>SET_HELP_GRANT</c>, the person's press of one
/// housekeeping operation on Ask Daoris's screen, written to the home's <c>configured-access.json</c>, which each start and each tool
/// call read; the state answering it as <c>helpGrants</c>, pushed with the choices. Edits to the home only, so no service and no process.
/// </summary>
public sealed class DriverModuleHelpGrantTests : DriverModuleBridge
{
    private static IReadOnlyList<string> Chosen(JsonElement state)
    {
        var housekeeping = state.GetProperty("helpGrants").EnumerateArray().Single(grant => grant.GetProperty("id").GetString() == "housekeeping");
        return [.. housekeeping.GetProperty("chosen").EnumerateArray().Select(each => each.GetString()!)];
    }

    /// <summary>🔴 The route chooses one operation at a time, and the state answers what the record holds: none until the person chooses.</summary>
    [Fact]
    public async Task The_route_chooses_an_operation_the_state_answers()
    {
        var module = Module();
        Assert.Empty(Chosen(await AnswerAsync(module, "STATE")));

        await AnswerAsync(module, "SET_HELP_GRANT", new { grant = "housekeeping", operation = "inspect", chosen = true });
        var both = await AnswerAsync(module, "SET_HELP_GRANT", new { grant = "housekeeping", operation = "clean-up", chosen = true });

        Assert.Equal(["inspect", "clean-up"], Chosen(both));
        Assert.Equal([ConfiguredAccess.Inspect, ConfiguredAccess.CleanUp], ConfiguredAccess.Read(Home).Housekeeping);

        var one = await AnswerAsync(module, "SET_HELP_GRANT", new { grant = "housekeeping", operation = "inspect", chosen = false });
        Assert.Equal(["clean-up"], Chosen(one));
    }

    /// <summary>A grant or an operation this build does not act on, or a press naming none, is refused in the driver's words, and nothing is written.</summary>
    [Fact]
    public async Task What_it_does_not_act_on_is_refused_and_nothing_is_written()
    {
        var module = Module();

        Assert.Contains("not built", await RefusalAsync(module, "SET_HELP_GRANT", new { grant = "housekeeping", operation = "drop-recovery", chosen = true }));
        Assert.Contains("plugin", await RefusalAsync(module, "SET_HELP_GRANT", new { grant = "browser", operation = "reads", chosen = true }));
        Assert.Contains("operation", await RefusalAsync(module, "SET_HELP_GRANT", new { grant = "housekeeping", chosen = true }));
        Assert.False(File.Exists(ConfiguredAccess.PathOf(Home)));
    }

    /// <summary>The grant is a standing choice every window holds: pushed in the driver slice's choices, as the mode is.</summary>
    [Fact]
    public void The_grant_is_pushed_with_the_choices()
    {
        Assert.Contains("helpGrants", DriverStateSlice.Partitions["choices"]);
    }

    /// <summary>
    /// 🔴 ACCESSSWAP1 (D183 point 2): the state says what has not reached a running conversation yet: each grant's <c>pending</c>, while
    /// an Ask Daoris conversation running here was handed another choice of it, and <c>helpSwapping</c>, the conversations running under
    /// another access than the one chosen, pushed with what runs. With none running, nothing is pending, before and after a choice.
    /// </summary>
    [Fact]
    public async Task With_no_conversation_running_nothing_is_pending()
    {
        var module = Module();

        var before = await AnswerAsync(module, "STATE");
        var after = await AnswerAsync(module, "SET_HELP_GRANT", new { grant = "housekeeping", operation = "inspect", chosen = true });

        foreach (var state in new[] { before, after })
        {
            var housekeeping = state.GetProperty("helpGrants").EnumerateArray().Single(grant => grant.GetProperty("id").GetString() == "housekeeping");
            Assert.False(housekeeping.GetProperty("pending").GetBoolean());
            Assert.Equal(0, state.GetProperty("helpSwapping").GetArrayLength());
        }

        Assert.Contains("helpSwapping", DriverStateSlice.Partitions["running"]);
    }
}
