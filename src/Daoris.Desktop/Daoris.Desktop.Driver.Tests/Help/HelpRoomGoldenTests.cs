using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's room rendered whole (MOD6), against the text it rendered before its sections were split
/// into files of their own: three machines between them take every branch a section has. A section moved,
/// reordered or reworded shows here as the whole room's difference, byte for byte.
/// </summary>
/// <remarks>
/// The golden files under <c>Help/golden/help-room/</c> are the room as the helper reads it. A change to what the
/// room says changes its golden file in the same commit, so review reads the room as the helper will; the
/// section tests beside this one say why each sentence is there.
/// </remarks>
public sealed class HelpRoomGoldenTests
{
    /// <summary>Every section speaking: offers with no landing plugin installed, each plugin state, each line and landing source.</summary>
    internal static readonly HelpMachine Full = new()
    {
        Adapter = "claude-code",
        Intake = "claude-code-acp",
        Helper = "claude-code-acp",
        Cap = 4,
        Waiting = 1,
        Asks = 2,
        Repositories =
        [
            new("console-ui", "work")
            {
                Checkout = true, Drivable = true, OwnTree = true,
                Line = new Line("feature/app", LineSource.Repository),
                Landing = new Landing(new LandingRule("branch", "feature/{slug}-{quest}", Tidy: true, Plugin: "example.lands"), LandingSource.Workspace),
            },
            new("reports-db", "work")
            {
                Checkout = true, Drivable = true, Held = true, Line = new Line("main", LineSource.Checkout),
            },
            new("engine", "default")
            {
                Checkout = false, Landing = new Landing(new LandingRule("merge", null), LandingSource.Repository),
            },
            new("tools", "default")
            {
                Checkout = true, Drivable = true, Line = new Line("develop", LineSource.Workspace),
            },
        ],
        // Reading across (D107): `reports-db` switched off, the rest on, one path that git needs quoted.
        Reads = [new("console-ui", "work", "/work/console-ui"), new("tools", "default", "/src/my tools")],
        Agents =
        [
            new("claude-code")
            {
                Product = "Claude Code", Present = true, Version = "2.1.0", Login = "in",
                Accounts = [new("work", "out"), new("play", "unknown")],
            },
            new("codex") { Present = false, Login = "unknown" },
            new("dsh") { Present = true, Login = "out" },
        ],
        OpenAsks =
        [
            new HelpAsk("a1b2c3d4", "fix the chunk streamer's stall", "work", "Published", ["q1a2b3c4", "q5e6f7a8"]),
            new HelpAsk("a2none00", "a test ask", "default", "Proposed", []),
            new HelpAsk("a3long00", "stream   the tiles\nfrom the cold cache, and keep the budget under the ceiling the engine sets for "
                + "every chunk it streams, whatever the weather", "work", "Open", []),
        ],
        Plugins =
        [
            new HelpPlugin("example.lands", Enabled: true, ["work/land"]) { Source = "folder" },
            new HelpPlugin("example.off", Enabled: false, []) { Source = "offer" },
            new HelpPlugin("example.broken", Enabled: true, []) { Problem = "`apiVersion` must be an integer." },
            new HelpPlugin("example.quiet", Enabled: true, []) { Source = "folder" },
        ],
        Parked = [new ParkedQuest("q5e6f7a8", "engine"), new ParkedQuest("q6b7c8d9", "tools")],
        Browser = new HelpBrowser("edge", "daoris", "refuse", ["https://site.example/board", "https://docs.example/"]),
        Landed =
        [
            new HelpLanded("engine", "feature/q2-second", "s2a3b4c5", Pushed: false, PullRequest: null),
            new HelpLanded("game", "feature/q3-third", "s3", Pushed: true, PullRequest: "https://example.test/pr/3"),
            new HelpLanded("game", "feature/q4-fourth", "s4", Pushed: true, PullRequest: null),
        ],
        Offers =
        [
            new HelpOffer("github-pull-request", "GitHub pull request", "1.0.0", ["work/land"], ["gh, signed in: `gh auth login`."]),
            new HelpOffer("in-app-browser", "In-app browser", "1.0.0", [], ["node on the PATH."]) { Servers = ["browser"] },
            new HelpOffer("example.agent", "example.agent", "", ["custom/point", "session/ended"], []) { Harnesses = ["acme-agent"] },
        ],
    };

    /// <summary>Plugins that land work installed, and nothing else: no agent, no repository, no offer.</summary>
    internal static readonly HelpMachine Sparse = new()
    {
        Asks = 1,
        LandingPlugins = ["example.github-pull-request", "example.lands"],
    };

    /// <summary>A machine with nothing at all.</summary>
    internal static readonly HelpMachine Empty = new();

    internal static HelpMachine Named(string name) => name switch
    {
        "full" => Full,
        "sparse" => Sparse,
        _ => Empty,
    };

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    internal static string GoldenPath(string file) =>
        Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "Help", "golden", "help-room", file);

    [Theory]
    [InlineData("full")]
    [InlineData("sparse")]
    [InlineData("empty")]
    public void The_room_renders_the_text_it_rendered_before_its_sections_were_split(string machine)
    {
        Assert.Equal(File.ReadAllText(GoldenPath($"{machine}.md")), HelpRoom.Render(Named(machine)));
    }

    /// <summary>The allow-list the room writes is the one it wrote before its kinds each named their own tool.</summary>
    [Fact]
    public void The_rooms_settings_are_the_ones_it_wrote_before()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-help-golden-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var room = HelpRoom.Prepare(home, Empty);

            Assert.Equal(File.ReadAllText(GoldenPath("settings.json")), File.ReadAllText(Path.Combine(room, ".claude", "settings.json")));
            using var settings = JsonDocument.Parse(File.ReadAllText(GoldenPath("settings.json")));
            Assert.Equal(
                settings.RootElement.GetProperty("permissions").GetProperty("allow").EnumerateArray().Select(rule => rule.GetString()!),
                HelpRoom.Allowed);
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
