using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomOwnPlugins"/>: the install's own plugins, by id, with what each needs.</summary>
public sealed class HelpRoomOwnPluginsTests
{
    /// <summary>
    /// PLUG9 (d): the install's own plugins not installed here are named by id, with what each speaks on and
    /// what it needs, so Ask Daoris can propose installing one — by its id, never a path — before making one.
    /// </summary>
    [Fact]
    public void The_room_names_the_installs_own_plugins_by_id_with_what_each_needs()
    {
        var offers = HelpRoom.Render(HelpRoomFixture.Machine with
        {
            Offers =
            [
                new HelpOffer("github-pull-request", "GitHub pull request", "1.0.0", ["work/land"], ["gh, signed in: `gh auth login`."]),
                new HelpOffer("in-app-browser", "In-app browser", "1.0.0", [], ["node on the PATH."]) { Servers = ["browser"] },
            ],
        });

        Assert.Contains("## Daoris's own plugins", offers);
        Assert.Contains("`offer` its id, never a path", offers);
        Assert.Contains("- `github-pull-request` (GitHub pull request 1.0.0): speaks on `work/land`, to push a branch Daoris made "
            + "and open its pull request; needs: gh, signed in: `gh auth login`.", offers);
        Assert.Contains("- `in-app-browser` (In-app browser 1.0.0): hands every session `browser`; needs: node on the PATH.", offers);
        // No landing plugin installed, and one offered: the helper may propose installing it first.
        Assert.Contains("this install offers `github-pull-request`, which you may propose installing first.", offers);
        Assert.DoesNotContain("## Daoris's own plugins", HelpRoom.Render(HelpRoomFixture.Machine));
    }

    [Fact]
    public void The_offers_the_room_lists_are_the_sound_ones_not_installed()
    {
        var root = Path.Combine(Path.GetTempPath(), "daoris-help-offers-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var home = Path.Combine(root, "data");
            var offersFolder = Path.Combine(root, "app", "plugin-offers");
            void Offer(string id, string manifest)
            {
                Directory.CreateDirectory(Path.Combine(offersFolder, id));
                File.WriteAllText(Path.Combine(offersFolder, id, PluginCatalog.ManifestName), manifest);
            }

            Offer("github-pull-request", """{ "id": "github-pull-request", "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] } }""");
            Offer("installed.one", """{ "id": "installed.one" }""");
            Offer("future", """{ "id": "future", "apiVersion": 99 }""");
            Directory.CreateDirectory(Path.Combine(home, PluginCatalog.Folder, "installed.one"));
            File.WriteAllText(Path.Combine(home, PluginCatalog.Folder, "installed.one", PluginCatalog.ManifestName), """{ "id": "installed.one" }""");
            File.WriteAllText(Path.Combine(home, PluginCatalog.Folder, "installed.one", PluginSource.FileName), """{ "offer": "installed.one" }""");

            var machine = HelpRoom.Describe(
                DriverConfig.Empty, new Snapshot([], [], []), [], [], _ => null, asks: 0,
                plugins: PluginCatalog.Load(home), offers: PluginOffers.Load(offersFolder, home, []));

            Assert.Equal(["github-pull-request"], machine.Offers.Select(offer => offer.Id));
            Assert.Equal(["work/land"], machine.Offers[0].Points);
            Assert.Equal("offer", Assert.Single(machine.Plugins).Source);
            Assert.DoesNotContain(root, HelpRoom.Render(machine));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
