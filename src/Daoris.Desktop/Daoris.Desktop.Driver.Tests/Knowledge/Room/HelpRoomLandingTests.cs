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

        Assert.DoesNotContain("`--plugin <id>`", some);
        Assert.Contains("A pull request is a step of a workflow, performed by a plugin", some);
        Assert.Contains("Plugins that perform pull-request steps here: `example.github-pull-request`, `example.lands`.", some);
        Assert.Contains("No plugin that performs pull-request steps is installed here", none);
        Assert.Contains("`daoris plugin add <folder>`", none);
        // UX6i2a (D150 §2): Settings → Plugins retired into the Plugins place with UX6j.
        Assert.Contains("or the Plugins place), so never propose a workflow naming one", none);
        Assert.DoesNotContain("Settings → Plugins", none);
    }

    [Fact]
    public void The_room_is_told_which_installed_plugins_land_work()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-help-plugins-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            void Install(string id, string points)
            {
                var folder = DaorisHome.Entry(home, HomeEntry.Plugins,id);
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
                    $$"""{ "id": "{{id}}", "hooks": { "command": ["node", "${plugin}/h.mjs"], "points": [{{points}}] } }""");
            }

            Install("example.lands", "\"work/perform\"");
            Install("example.off", "\"work/perform\"");
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
