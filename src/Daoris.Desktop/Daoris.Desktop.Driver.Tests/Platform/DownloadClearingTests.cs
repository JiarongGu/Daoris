using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Clearing downloads (CACHECLEAR1): the agents' and tools' versions under <c>cache/</c> listed with what keeps each, and the press
/// removing only the listed ones nothing keeps, each judged again right before it goes (D88's shape). One case per kept version.
/// </summary>
/// <remarks>Every case runs in a temporary directory, as the tools' tests do, and nothing here starts a program.</remarks>
public sealed class DownloadClearingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-downloads-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    /// <summary>Long after every folder here was made, so nothing reads as a download a moment old.</summary>
    private static DateTimeOffset Later => DateTimeOffset.UtcNow + TimeSpan.FromHours(2);

    public DownloadClearingTests() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Agent(string agent, string version)
    {
        var folder = HarnessSettings.ManagedHome(Home, agent, version);
        Directory.CreateDirectory(Path.Combine(folder, "bin"));
        File.WriteAllText(Path.Combine(folder, "bin", "claude.exe"), "claude");
        return folder;
    }

    private string Tool(string tool, string version)
    {
        var folder = ToolInstall.VersionFolder(Home, tool, version);
        Directory.CreateDirectory(Path.Combine(folder, Tools.Package, "bin"));
        File.WriteAllText(Path.Combine(folder, Tools.Package, "bin", $"{tool}.exe"), tool);
        File.WriteAllText(Path.Combine(folder, Tools.Record), $$"""{"exe":"bin/{{tool}}.exe"}""");
        return folder;
    }

    private void ToolsFile(string text) => File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Tools), text);

    private DownloadUses Uses(HarnessSettings? harnesses = null, DriverConfig? config = null, bool configUnread = false) =>
        new(harnesses ?? new HarnessSettings(), Tools.Read(Home), configUnread ? null : config ?? DriverConfig.Empty);

    private static Dictionary<string, string> Pins(params (string Agent, string Version)[] pins) =>
        pins.ToDictionary(pin => pin.Agent, pin => pin.Version, StringComparer.OrdinalIgnoreCase);

    private DownloadedVersion Row(DownloadUses uses, string kind, string name, string version, DateTimeOffset? now = null) =>
        DownloadClearing.List(Home, uses, now ?? Later).Versions.Single(row => row.Kind == kind && row.Name == name && row.Version == version);

    private IReadOnlyList<DownloadCleared> Press(DownloadUses uses, DateTimeOffset now, Action<string, string>? move = null) =>
        DownloadClearing.Clear(
            Home, [.. DownloadClearing.List(Home, uses, now).Versions.Select(row => new DownloadAsked(row.Kind, row.Name, row.Version, row.Unfinished))],
            () => uses, () => now, move: move);

    [Fact]
    public void A_version_nothing_uses_is_listed_with_its_size_and_goes_at_the_press()
    {
        var folder = Agent("claude-code", "2.0.1");
        var row = Row(Uses(), "agent", "claude-code", "2.0.1");

        Assert.Null(row.Keeps);
        Assert.Equal(6, row.Bytes);
        Assert.Equal("removed", Assert.Single(Press(Uses(), Later)).Outcome);
        Assert.False(Directory.Exists(folder));
        Assert.False(Directory.Exists(Path.GetDirectoryName(folder)), "an agent's folder left empty goes with its last version");
    }

    [Fact]
    public void The_machines_pin_keeps_its_version_and_the_other_goes()
    {
        var pinned = Agent("claude-code", "2.1.0");
        var old = Agent("claude-code", "2.0.1");
        var uses = Uses(new HarnessSettings(Versions: Pins(("claude-code", "2.1.0"))));

        var row = Row(uses, "agent", "claude-code", "2.1.0");
        Assert.Equal("pinned", row.Keeps);
        Assert.Equal(["machine"], row.By);
        var cleared = Press(uses, Later);

        Assert.Equal("kept", cleared.Single(each => each.Asked.Version == "2.1.0").Outcome);
        Assert.True(Directory.Exists(pinned));
        Assert.False(Directory.Exists(old));
    }

    [Fact]
    public void A_workspaces_pin_keeps_its_version_naming_the_workspace()
    {
        var pinned = Agent("codex", "0.40.0");
        var uses = Uses(new HarnessSettings(WorkspaceVersions: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["games"] = Pins(("codex", "0.40.0")),
        }));

        var row = Row(uses, "agent", "codex", "0.40.0");
        Assert.Equal("pinned", row.Keeps);
        Assert.Equal(["workspace:games"], row.By);
        Assert.Equal("kept", Assert.Single(Press(uses, Later)).Outcome);
        Assert.True(Directory.Exists(pinned));
    }

    [Fact]
    public void A_managed_tools_version_is_kept_and_its_other_versions_go()
    {
        var used = Tool("gh", "2.62.0");
        var old = Tool("gh", "2.61.0");
        ToolsFile("""{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""");

        Assert.Equal("used", Row(Uses(), "tool", "gh", "2.62.0").Keeps);
        Press(Uses(), Later);

        Assert.True(Directory.Exists(used));
        Assert.False(Directory.Exists(old));
    }

    [Fact]
    public void A_tools_json_that_does_not_read_keeps_every_version()
    {
        var tool = Tool("node", "22.20.0");
        var agent = Agent("claude-code", "2.0.1");
        ToolsFile("not json");

        Assert.Equal(("unread", "tools.json"), (Row(Uses(), "tool", "node", "22.20.0").Keeps, Row(Uses(), "tool", "node", "22.20.0").By.Single()));
        Assert.Equal("unread", Row(Uses(), "agent", "claude-code", "2.0.1").Keeps);
        Assert.All(Press(Uses(), Later), each => Assert.Equal("kept", each.Outcome));
        Assert.True(Directory.Exists(tool) && Directory.Exists(agent));
    }

    [Fact]
    public void A_harnesses_json_that_does_not_read_keeps_every_agents_version()
    {
        var agent = Agent("claude-code", "2.0.1");
        var uses = Uses(new HarnessSettings { Problem = "harnesses.json could not be read" });

        Assert.Equal("unread", Row(uses, "agent", "claude-code", "2.0.1").Keeps);
        Assert.Equal("kept", Assert.Single(Press(uses, Later)).Outcome);
        Assert.True(Directory.Exists(agent));
    }

    [Fact]
    public void A_driver_json_that_does_not_read_keeps_the_versions_its_commands_may_name()
    {
        var agent = Agent("claude-code", "2.0.1");
        var uses = Uses(configUnread: true);

        Assert.Equal(("unread", "driver.json"), (Row(uses, "agent", "claude-code", "2.0.1").Keeps, Row(uses, "agent", "claude-code", "2.0.1").By.Single()));
        Assert.True(Directory.Exists(agent));
    }

    [Fact]
    public void A_command_that_names_a_file_inside_a_version_keeps_it()
    {
        var agent = Agent("claude-code", "2.0.1");
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = [Path.Combine(agent, "bin", "claude.exe"), "-p"] },
        };

        Assert.Equal(("named", "driver.json"), (Row(Uses(config: config), "agent", "claude-code", "2.0.1").Keeps, Row(Uses(config: config), "agent", "claude-code", "2.0.1").By.Single()));
        Assert.Equal("kept", Assert.Single(Press(Uses(config: config), Later)).Outcome);
        Assert.True(Directory.Exists(agent));
    }

    [Fact]
    public void A_tool_run_as_a_file_inside_a_version_keeps_it()
    {
        var folder = Tool("git", "2.51.0");
        var file = Path.Combine(folder, Tools.Package, "bin", "git.exe").Replace("\\", "\\\\");
        ToolsFile("""{"tools":{"git":{"use":"file","file":"FILE"}}}""".Replace("FILE", file));

        Assert.Equal("named", Row(Uses(), "tool", "git", "2.51.0").Keeps);
        Press(Uses(), Later);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void A_download_a_moment_old_stays_since_its_pin_may_be_on_its_way()
    {
        var folder = Agent("claude-code", "2.2.0");
        var now = DateTimeOffset.UtcNow;

        Assert.Equal("recent", Row(Uses(), "agent", "claude-code", "2.2.0", now).Keeps);
        Assert.Equal("kept", Assert.Single(Press(Uses(), now)).Outcome);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void A_version_a_program_holds_is_kept_in_use_and_left_whole()
    {
        var folder = Agent("claude-code", "2.0.1");

        var cleared = Assert.Single(Press(Uses(), Later, move: (_, _) => throw new IOException("in use")));

        Assert.Equal(("kept", "in-use"), (cleared.Outcome, cleared.Version!.Keeps));
        Assert.True(File.Exists(Path.Combine(folder, "bin", "claude.exe")));
    }

    [Fact]
    public void Each_version_is_judged_again_right_before_it_goes()
    {
        var folder = Agent("claude-code", "2.0.1");
        var listed = DownloadClearing.List(Home, Uses(), Later).Versions;
        Assert.Null(Assert.Single(listed).Keeps);

        // Pinned between the list and the press: the press reads the pins again and keeps it.
        var pinned = Uses(new HarnessSettings(Versions: Pins(("claude-code", "2.0.1"))));
        var cleared = DownloadClearing.Clear(Home, [new DownloadAsked("agent", "claude-code", "2.0.1")], () => pinned, () => Later);

        Assert.Equal(("kept", "pinned"), (Assert.Single(cleared).Outcome, cleared[0].Version!.Keeps));
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void Only_what_was_asked_goes_and_a_name_never_reaches_past_a_listed_folder()
    {
        var asked = Agent("claude-code", "2.0.1");
        var other = Tool("gh", "2.61.0");

        var cleared = DownloadClearing.Clear(
            Home, [new DownloadAsked("agent", "claude-code", "2.0.1"), new DownloadAsked("tool", "gh", "../../data"), new DownloadAsked("agent", "claude-code", "9.9.9")],
            () => Uses(), () => Later);

        Assert.Equal(["removed", "gone", "gone"], cleared.Select(each => each.Outcome));
        Assert.False(Directory.Exists(asked));
        Assert.True(Directory.Exists(other));
        Assert.True(Directory.Exists(Home));
    }

    [Fact]
    public void A_staged_download_is_never_listed_and_an_unfinished_clearing_goes_at_the_next_press()
    {
        var staged = ToolInstall.VersionFolder(Home, "gh", "2.63.0") + ".part";
        Directory.CreateDirectory(staged);
        var unfinished = HarnessSettings.ManagedHome(Home, "claude-code", "2.0.1") + ".0123abcd" + DownloadClearing.Aside;
        Directory.CreateDirectory(Path.Combine(unfinished, "bin"));

        var row = Assert.Single(DownloadClearing.List(Home, Uses(), Later).Versions);
        Assert.Equal(("claude-code", "2.0.1", true, (string?)null), (row.Name, row.Version, row.Unfinished, row.Keeps));

        Assert.Equal("removed", Assert.Single(Press(Uses(), Later)).Outcome);
        Assert.False(Directory.Exists(unfinished));
        Assert.True(Directory.Exists(staged));
    }

    [Fact]
    public void A_read_only_file_does_not_stop_a_removal()
    {
        var folder = Tool("node", "22.19.0");
        var file = Path.Combine(folder, Tools.Package, "bin", "node.exe");
        File.SetAttributes(file, FileAttributes.ReadOnly);

        Assert.Equal("removed", Assert.Single(Press(Uses(), Later)).Outcome);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void No_home_lists_nothing_and_says_why()
    {
        Assert.Equal(("no-home", 0), (DownloadClearing.List(null, Uses(), Later).Why, DownloadClearing.List(null, Uses(), Later).Versions.Count));
    }
}
