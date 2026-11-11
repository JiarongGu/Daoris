using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>PLUGIN_SWITCH</c> writes the person's word on one interface or point of an installed plugin at one place, <c>PLUGINS</c> answers
/// each plugin's words, and <c>daoris plugin switch</c> presses the same route with the same payload.
/// </summary>
public sealed class DriverModulePluginSwitchTests : DriverModuleBridge
{
    private void Install(string id)
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
            $$"""{ "id": "{{id}}", "hooks": { "command": ["node", "plugin.mjs"], "points": ["session/ended"] } }""");
    }

    [Fact]
    public async Task A_switch_is_written_at_its_place_and_the_list_answers_it()
    {
        Install("acme-desk");
        using var loop = Loop();
        var module = new DriverModule(Bus, loop) { Offers = Path.Combine(Home, "no-offers") };

        var answered = await AnswerAsync(module, "PLUGIN_SWITCH", new { id = "acme-desk", what = "session/ended", place = "repository:engine", on = false });

        Assert.Equal("repository:engine", answered.GetProperty("place").GetString());
        Assert.False(PluginSwitches.IsOn(Home, "acme-desk", HookPoints.SessionEnded, new PluginPlace(Repository: "engine")));
        Assert.True(PluginSwitches.IsOn(Home, "acme-desk", HookPoints.SessionEnded, new PluginPlace(Repository: "other")));
        var word = (await AnswerAsync(module, "PLUGINS")).GetProperty("plugins").EnumerateArray().Single().GetProperty("switches").EnumerateArray().Single();
        Assert.Equal(("session/ended", "repository:engine", false),
            (word.GetProperty("what").GetString(), word.GetProperty("place").GetString(), word.GetProperty("on").GetBoolean()));

        Assert.Contains("machine alone", await RefusalAsync(module, "PLUGIN_SWITCH", new { id = "acme-desk", what = "storage", place = "workspace:home", on = false }), StringComparison.Ordinal);
        Assert.Contains("acme-gone", await RefusalAsync(module, "PLUGIN_SWITCH", new { id = "acme-gone", what = "tools", on = false }), StringComparison.Ordinal);
    }

    [Fact]
    public void The_terminal_s_switch_presses_the_route_and_refuses_words_that_name_nothing()
    {
        Install("acme-desk");
        var world = new PluginWorld
        {
            Instance = new InstanceWorld
            {
                Home = Home, Environment = _ => null, BaseDirectory = Home, Out = TextWriter.Null, Error = TextWriter.Null, Verb = "daoris",
            },
            Reserved = [],
        };

        var press = PluginCommand.Plan(["switch", "acme-desk", "tools", "off", "--place", "workspace:home"], world)!;

        Assert.Equal(PluginRoutes.Switch, press.Route);
        Assert.Equal("""{"id":"acme-desk","what":"tools","place":"workspace:home","on":false}""", press.Payload.ToJsonString());
        Assert.Throws<PluginWords>(() => PluginCommand.Plan(["switch", "acme-desk", "tools", "maybe"], world));
        Assert.Throws<PluginWords>(() => PluginCommand.Plan(["switch", "acme-desk", "chat", "off"], world));
        Assert.Equal(PluginPlace.MachineKey, PluginCommand.Plan(["switch", "acme-desk", "views", "on"], world)!.Payload["place"]!.GetValue<string>());
    }
}
