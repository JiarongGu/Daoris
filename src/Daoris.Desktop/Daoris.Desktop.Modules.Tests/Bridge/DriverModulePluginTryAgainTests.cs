using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>PLUGIN_ACTION</c>'s <c>try-again</c>, the page's and <c>daoris plugin try-again</c>'s press: an unsound plugin is started again at
/// the loop's next look (<see cref="HookSet.TryAgain"/>), and <c>PLUGINS</c> says of each installed plugin whether the loop holds it
/// unsound, which the page offers <i>Try again</i> on.
/// </summary>
public sealed class DriverModulePluginTryAgainTests : DriverModuleBridge
{
    private void Install(string id)
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
            $$"""{ "id": "{{id}}", "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/state"] } }""");
    }

    [Fact]
    public async Task Try_again_is_a_plugin_action_and_the_list_says_whether_the_loop_holds_a_plugin_unsound()
    {
        Install("acme-tickets");
        using var loop = Loop();
        var module = new DriverModule(Bus, loop) { Offers = Path.Combine(Home, "no-offers") };

        var row = (await AnswerAsync(module, "PLUGINS")).GetProperty("plugins").EnumerateArray().Single();
        Assert.False(row.GetProperty("unsound").GetBoolean());

        var answered = await AnswerAsync(module, "PLUGIN_ACTION", new { action = "try-again", id = "acme-tickets" });
        Assert.Equal("acme-tickets", answered.GetProperty("id").GetString());
        Assert.Equal("try-again", answered.GetProperty("action").GetString());
        Assert.Contains("try-again", await RefusalAsync(module, "PLUGIN_ACTION", new { action = "again", id = "acme-tickets" }), StringComparison.Ordinal);
    }
}
