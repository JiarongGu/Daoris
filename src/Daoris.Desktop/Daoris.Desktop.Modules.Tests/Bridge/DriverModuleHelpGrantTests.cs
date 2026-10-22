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

    /// <summary>
    /// HELPMODE1c3: the chosen settings are a grant of their own, chosen one setting at a time beside housekeeping, and the state answers
    /// what it holds and the five it acts on.
    /// </summary>
    [Fact]
    public async Task The_route_chooses_a_setting_beside_housekeeping()
    {
        var module = Module();
        await AnswerAsync(module, "SET_HELP_GRANT", new { grant = "housekeeping", operation = "inspect", chosen = true });

        var state = await AnswerAsync(module, "SET_HELP_GRANT", new { grant = "settings", operation = "cap", chosen = true });

        var settings = state.GetProperty("helpGrants").EnumerateArray().Single(grant => grant.GetProperty("id").GetString() == "settings");
        Assert.Equal(["cap"], settings.GetProperty("chosen").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(
            ["notify", "language", "cap", "cooloff"], settings.GetProperty("built").EnumerateArray().Select(each => each.GetString()));
        Assert.False(settings.GetProperty("pending").GetBoolean());
        Assert.Equal(["inspect"], Chosen(state));
        Assert.Contains("`mode`", await RefusalAsync(module, "SET_HELP_GRANT", new { grant = "settings", operation = "mode", chosen = true }));
    }

    /// <summary>
    /// HELPMODE1c3 (the modes design §4.1's decided note): the browser's row is drawn from its plugin's own record, read only: its state
    /// and the plugin whose tab changes it, never a choice made here. With no plugin attaching to Daoris's browser, it names none.
    /// </summary>
    [Fact]
    public async Task The_state_answers_the_browsers_grant_from_its_plugins_record()
    {
        var module = Module();
        JsonElement Browser(JsonElement state) =>
            state.GetProperty("helpGrants").EnumerateArray().Single(grant => grant.GetProperty("id").GetString() == "browser");
        // The bridge leaves a null out.
        static string? Text(JsonElement grant, string name) =>
            grant.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        var none = Browser(await AnswerAsync(module, "STATE"));
        Assert.Null(Text(none, "plugin"));
        Assert.Empty(none.GetProperty("chosen").EnumerateArray());

        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins, "in-app-browser");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), """
            { "id": "in-app-browser", "servers": [
                { "name": "browser", "command": ["node", "server.js", "--cdp", "${browser}"], "to": ["sessions", "help"],
                  "allow": ["browser_snapshot"], "writes": ["browser_click", "browser_type"] } ] }
            """);
        var waiting = Browser(await AnswerAsync(module, "STATE"));
        Assert.Equal("in-app-browser", Text(waiting, "plugin"));
        Assert.Empty(waiting.GetProperty("chosen").EnumerateArray());
        Assert.False(waiting.GetProperty("changed").GetBoolean());

        HelpGrants.Approve(Home, PluginCatalog.Load(Home), "in-app-browser", "browser", ["browser_click"]);
        var approved = Browser(await AnswerAsync(module, "STATE"));
        Assert.Equal(["reads", "browser_click"], approved.GetProperty("chosen").EnumerateArray().Select(each => each.GetString()));
        Assert.False(approved.TryGetProperty("built", out var built) && built.ValueKind == JsonValueKind.Array);
    }

    /// <summary>
    /// 🔴 HELPMODE1c4: the checkouts' row is on by default. It answers each checkout here that D107 lets agents outside read as built,
    /// from the machine's facts as last judged (none before they are), and each the person has not turned off as chosen; the route turns
    /// one off and on again by its repository's name, the record keeping only those turned off.
    /// </summary>
    [Fact]
    public async Task The_checkouts_row_answers_the_readable_checkouts_and_the_route_turns_one_off()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        static JsonElement Checkouts(JsonElement state) =>
            state.GetProperty("helpGrants").EnumerateArray().Single(grant => grant.GetProperty("id").GetString() == "checkouts");
        static IEnumerable<string?> Names(JsonElement grant, string field) => grant.GetProperty(field).EnumerateArray().Select(each => each.GetString());

        Assert.Empty(Names(Checkouts(await AnswerAsync(module, "STATE")), "built"));

        DriverConfig.Load(loop.ConfigPath).WithReadAcross("secret", false).Save(loop.ConfigPath);
        var whole = new HelpMachineWhole(
            new Snapshot(
                [],
                [
                    new RepoView("engine", true, "X:/daoris/engine", "aurora"), new RepoView("game", true, "X:/daoris/game", "borealis"),
                    new RepoView("secret", true, "X:/daoris/secret", "aurora"), new RepoView("elsewhere", true, null, "aurora"),
                ],
                []),
            [], []);
        loop.Facts.Topic(HelpFacts.Machine, HelpFacts.MachineRules, (_, _) => Task.FromResult<object?>(whole));
        loop.Facts.Start();
        try
        {
            await loop.Facts.FirstAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Poll.Patience);

            var on = Checkouts(await AnswerAsync(module, "STATE"));
            Assert.Equal(["engine", "game"], Names(on, "built"));
            Assert.Equal(["engine", "game"], Names(on, "chosen"));
            Assert.False(on.GetProperty("pending").GetBoolean());

            var off = Checkouts(await AnswerAsync(module, "SET_HELP_GRANT", new { grant = "checkouts", operation = "engine", chosen = false }));
            Assert.Equal(["game"], Names(off, "chosen"));
            Assert.Equal(["engine"], ConfiguredAccess.Read(Home).CheckoutsOff);

            var again = Checkouts(await AnswerAsync(module, "SET_HELP_GRANT", new { grant = "checkouts", operation = "engine", chosen = true }));
            Assert.Equal(["engine", "game"], Names(again, "chosen"));
            Assert.Empty(ConfiguredAccess.Read(Home).CheckoutsOff);
        }
        finally
        {
            loop.Facts.Dispose();
        }
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
