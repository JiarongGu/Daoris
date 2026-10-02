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
                new HelpPlugin("example.packaged", Enabled: true, []) { Source = "package" },
                new HelpPlugin("example.broken", Enabled: true, []) { Problem = "`apiVersion` must be an integer." },
            ],
        });

        Assert.Contains("- Plugins: `example.lands` (on, speaks on `work/land`, added from a folder), "
            + "`example.off` (off, installed from this install's offer), "
            + "`example.packaged` (on, installed from a package), "
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
            // PLUGDIST1a: a plugin from a package is said by its kind, never by the folder that held the file.
            PluginSource.Write(folder, PluginSource.FromPackage(new PluginPackageOrigin(
                "Example.Lands", "1.0.0", Convert.ToBase64String(new byte[64]), Path.Combine(home, "feed"))));

            var machine = HelpRoom.Describe(
                DriverConfig.Empty, new Snapshot([], [], []), [], [], _ => null, asks: 0, plugins: PluginCatalog.Load(home));

            var plugin = Assert.Single(machine.Plugins);
            Assert.Equal(("example.lands", false, null), (plugin.Id, plugin.Enabled, plugin.Problem));
            Assert.Equal(["work/land"], plugin.Points);
            Assert.Equal("package", plugin.Source);
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

        Assert.Contains("- Landed branches: `feature/q2-second` in `engine` (session `s2a3b4c5`, not pushed), "
            + "`feature/q3-third` in `game` (session `s3`, pushed, pull request https://example.test/pr/3).", some);
        Assert.Contains("- Landed branches: none recorded.", HelpRoom.Render(HelpRoomFixture.Machine));
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

    /// <summary>
    /// HELP10: the quests the driver's last look parked by their failed sessions, by id and repository, so a retry
    /// names one the drawer would offer Retry on; and none said as none, so the helper does not guess one parked.
    /// </summary>
    [Fact]
    public void The_room_lists_the_quests_the_driver_parked_and_how_one_is_started_again()
    {
        var some = HelpRoom.Render(HelpRoomFixture.Machine with
        {
            Parked = [new ParkedQuest("q1a2b3c4", "engine"), new ParkedQuest("q5e6f7a8", "game")],
        });

        Assert.Contains("- Quests parked by their failed sessions, at the driver's last look: `#q1a2b3c4` (to `engine`), "
            + "`#q5e6f7a8` (to `game`).", some);
        Assert.Contains("- Quests parked by their failed sessions, at the driver's last look: none.", HelpRoom.Render(HelpRoomFixture.Machine));
        Assert.Contains("`retry` takes a quest parked by its failed sessions, or held by the person's stop, from this machine's lists below", some);
    }

    /// <summary>
    /// SESSUX1b: the quests the driver's last look held by the person's stop, each with the session stopped, so a retry
    /// names one *Try again* would release; and none said as none.
    /// </summary>
    [Fact]
    public void The_room_lists_the_quests_the_persons_stop_holds()
    {
        var some = HelpRoom.Render(HelpRoomFixture.Machine with
        {
            Held = [new HeldQuest("q2taken0", "engine", "s7a8b9c0")],
        });

        Assert.Contains("- Quests held by the person's stop, at the driver's last look: `#q2taken0` (to `engine`, session "
            + "`s7a8b9c0` stopped).", some);
        Assert.Contains("- Quests held by the person's stop, at the driver's last look: none.", HelpRoom.Render(HelpRoomFixture.Machine));
    }

    [Fact]
    public void The_held_quests_are_described_from_the_loops_last_look()
    {
        var machine = HelpRoom.Describe(
            DriverConfig.Empty, new Snapshot([], [], []), [], [], _ => null, asks: 0, held: [new HeldQuest("q2taken0", "engine", "s7")]);

        Assert.Equal(new HeldQuest("q2taken0", "engine", "s7"), Assert.Single(machine.Held));
    }

    /// <summary>
    /// HELP10: Daoris's browser as its two files hold it, so a browser proposal names a favorite kept; said only where
    /// the desktop read them, since the files are its own.
    /// </summary>
    [Fact]
    public void The_room_says_how_Daoris_browser_is_set_and_what_it_keeps()
    {
        var some = HelpRoom.Render(HelpRoomFixture.Machine with
        {
            Browser = new HelpBrowser("edge", "daoris", "refuse", ["https://site.example/board", "https://docs.example/"]),
        });

        Assert.Contains("- Daoris's browser: your Edge; links on the page open in Daoris's browser; other software's extensions "
            + "refused; favorites https://site.example/board, https://docs.example/.", some);
        Assert.Contains("favorites none.", HelpRoom.Render(HelpRoomFixture.Machine with { Browser = new HelpBrowser("daoris", "system", "offer", []) }));
        Assert.DoesNotContain("- Daoris's browser:", HelpRoom.Render(HelpRoomFixture.Machine));
    }

    [Fact]
    public void The_browser_is_described_from_its_files_as_the_desktop_read_them()
    {
        var files = new HelpBrowserFacts("daoris", "system", "offer", ["https://site.example/board"]) { Page = typed => typed };

        var machine = HelpRoom.Describe(DriverConfig.Empty, new Snapshot([], [], []), [], [], _ => null, asks: 0, browser: files);

        var browser = machine.Browser!;
        Assert.Equal(("daoris", "system", "offer"), (browser.Browser, browser.Links, browser.Extensions));
        Assert.Equal(["https://site.example/board"], browser.Favorites);
        Assert.Null(HelpRoom.Describe(DriverConfig.Empty, new Snapshot([], [], []), [], [], _ => null, asks: 0).Browser);
    }

    [Fact]
    public void The_parked_quests_are_described_from_the_loops_last_tick()
    {
        var machine = HelpRoom.Describe(
            DriverConfig.Empty, new Snapshot([], [], []), [], [], _ => null, asks: 0, parked: [new ParkedQuest("q1a2b3c4", "engine")]);

        Assert.Equal(new ParkedQuest("q1a2b3c4", "engine"), Assert.Single(machine.Parked));
    }
}
