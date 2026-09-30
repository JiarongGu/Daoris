using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomMachineNow"/>: what runs where, what waits, the plugins installed and the branches landed.</summary>
public sealed class HelpRoomMachineNowTests
{
    /// <summary>
    /// PLUG9: the plugins installed here, by id and state, so a switch names one the catalogue holds — and
    /// since PLUG9 (c) where each came from, by kind and never by path, so an update is proposed only where
    /// there is something to read.
    /// </summary>
    [Fact]
    public void The_room_lists_the_plugins_installed_here()
    {
        var some = HelpRoom.Render(HelpRoomFixture.Machine with
        {
            Plugins =
            [
                new HelpPlugin("example.lands", Enabled: true, ["work/land"]) { Source = "folder" },
                new HelpPlugin("example.off", Enabled: false, []) { Source = "offer" },
                new HelpPlugin("example.broken", Enabled: true, []) { Problem = "`apiVersion` must be an integer." },
            ],
        });

        Assert.Contains("- Plugins: `example.lands` (on, speaks on `work/land`, added from a folder), "
            + "`example.off` (off, installed from this install's offer), "
            + "`example.broken` (on, contributes nothing: `apiVersion` must be an integer., no record of where it came from)", some);
        Assert.Contains("- Plugins: none installed.", HelpRoom.Render(HelpRoomFixture.Machine));
        Assert.Contains("an `update` (the plugin's `id`) takes a", some);
        Assert.Contains("`daoris plugin add --offer <id>`, `daoris plugin update <id>`", some);
    }

    [Fact]
    public void The_plugins_are_described_from_the_catalogue()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-help-installed-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var folder = Path.Combine(home, PluginCatalog.Folder, "example.lands");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
                """{ "id": "example.lands", "hooks": { "command": ["node", "${plugin}/h.mjs"], "points": ["work/land"] } }""");
            PluginState.Disable(home, "example.lands");

            var machine = HelpRoom.Describe(
                DriverConfig.Empty, new Snapshot([], [], []), [], [], _ => null, asks: 0, plugins: PluginCatalog.Load(home));

            var plugin = Assert.Single(machine.Plugins);
            Assert.Equal(("example.lands", false, null), (plugin.Id, plugin.Enabled, plugin.Problem));
            Assert.Equal(["work/land"], plugin.Points);
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// WSR5b: the room lists the branches landings made here, so a hand-off names one the record holds, and
    /// says what `hand_propose` does and its doors.
    /// </summary>
    [Fact]
    public void The_room_lists_the_branches_landings_made_and_how_one_is_handed_on()
    {
        var some = HelpRoom.Render(HelpRoomFixture.Machine with
        {
            Landed =
            [
                new HelpLanded("engine", "feature/q2-second", "s2a3b4c5", Pushed: false, PullRequest: null),
                new HelpLanded("game", "feature/q3-third", "s3", Pushed: true, PullRequest: "https://example.test/pr/3"),
            ],
        });

        Assert.Contains("- Branches landings made: `feature/q2-second` in `engine` (session `s2a3b4c5`, not pushed), "
            + "`feature/q3-third` in `game` (session `s3`, pushed, pull request https://example.test/pr/3).", some);
        Assert.Contains("- Branches landings made: none recorded.", HelpRoom.Render(HelpRoomFixture.Machine));
        Assert.Contains("`hand_propose`", some);
        Assert.Contains("`daoris-driver trees hand <session|branch> [--plugin <id>]`", some);
    }

    [Fact]
    public void The_branches_landings_made_are_described_from_the_record()
    {
        var landed = new LandedBranch("engine", "work", "feature/q2-second", "main", "abc1234", "s2a3b4c5", "q2", "Second", DateTimeOffset.UnixEpoch)
        {
            Pushed = true, PullRequest = "https://example.test/pr/9",
        };

        var machine = HelpRoom.Describe(DriverConfig.Empty, new Snapshot([], [], []), [], [], _ => null, asks: 0, landed: [landed]);

        Assert.Equal(new HelpLanded("engine", "feature/q2-second", "s2a3b4c5", true, "https://example.test/pr/9"), Assert.Single(machine.Landed));
    }
}
