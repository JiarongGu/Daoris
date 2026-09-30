using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomLanding"/>: what a landing pattern may say, and the plugins a branch rule may name.</summary>
public sealed class HelpRoomLandingTests
{
    /// <summary>
    /// HELP8: the plugins that can land work here are named, so a landing rule the helper proposes names
    /// one the route takes (D100); with none, it says so and that installing one is the person's.
    /// </summary>
    [Fact]
    public void The_room_names_the_plugins_that_can_land_work_here()
    {
        var some = HelpRoom.Render(HelpRoomFixture.Machine with { LandingPlugins = ["example.github-pull-request", "example.lands"] });
        var none = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("`--plugin <id>`", some);
        Assert.Contains("Plugins that can land work here: `example.github-pull-request`, `example.lands`.", some);
        Assert.Contains("No plugin that lands work is installed here", none);
        Assert.Contains("`daoris plugin add <folder>`", none);
    }

    [Fact]
    public void The_room_is_told_which_installed_plugins_land_work()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-help-plugins-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            void Install(string id, string points)
            {
                var folder = Path.Combine(home, PluginCatalog.Folder, id);
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
                    $$"""{ "id": "{{id}}", "hooks": { "command": ["node", "${plugin}/h.mjs"], "points": [{{points}}] } }""");
            }

            Install("example.lands", "\"work/land\"");
            Install("example.off", "\"work/land\"");
            Install("example.quiet", "\"session/ended\"");
            PluginState.Disable(home, "example.off");

            Assert.Equal(["example.lands"], HelpRoomLanding.LandingPluginsOf(PluginCatalog.Load(home)));
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
