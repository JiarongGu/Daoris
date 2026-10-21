using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Clearing downloads (CACHECLEAR1): the agents' and tools' versions under <c>cache/</c> listed with what keeps each, and the press
/// removing only the listed ones nothing keeps, each judged again right before it goes (D88's shape). One case per kept version.
/// KEEP1b (D195 point 6; the cleanup design §3.5, §9) adds the rule's rows: the first-unused time of the current unused stretch, the
/// removal past its days, the judgement under the use files' locks held per version to the rename, the versions a running session was
/// handed, and Tools' delete through the same act.
/// </summary>
/// <remarks>Every case runs in a home under <c>_fixtures/</c>, as <see cref="KeepingTests"/>' do, and nothing here starts a program.</remarks>
public sealed class DownloadClearingTests : IDisposable
{
    private readonly string _root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "downloads", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    /// <summary>Long after every folder here was made, so nothing reads as a download a moment old.</summary>
    private static DateTimeOffset Later => DateTimeOffset.UtcNow + TimeSpan.FromHours(2);

    public DownloadClearingTests() => Directory.CreateDirectory(Home);

    public void Dispose() => GitSeed.Discard(_root);

    private string Agent(string agent, string version, string binary = "claude")
    {
        var folder = HarnessSettings.ManagedHome(Home, agent, version);
        Directory.CreateDirectory(Path.Combine(folder, "bin"));
        File.WriteAllText(Path.Combine(folder, "bin", OperatingSystem.IsWindows() ? binary + ".exe" : binary), binary);
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

    private string HarnessFile => DaorisHome.Entry(Home, HomeEntry.Harnesses);

    private DownloadUses Uses(HarnessSettings? harnesses = null, DriverConfig? config = null, bool configUnread = false) =>
        new(harnesses ?? new HarnessSettings(), Tools.Read(Home), configUnread ? null : config ?? DriverConfig.Empty);

    private static Dictionary<string, string> Pins(params (string Agent, string Version)[] pins) =>
        pins.ToDictionary(pin => pin.Agent, pin => pin.Version, StringComparer.OrdinalIgnoreCase);

    private DownloadedVersion Row(DownloadUses uses, string kind, string name, string version, DateTimeOffset? now = null) =>
        DownloadClearing.List(Home, uses, now ?? Later).Versions.Single(row => row.Kind == kind && row.Name == name && row.Version == version);

    private Task<IReadOnlyList<DownloadCleared>> PressAsync(DownloadUses uses, DateTimeOffset now, Action<string, string>? move = null) =>
        DownloadClearing.ClearAsync(
            Home, [.. DownloadClearing.List(Home, uses, now).Versions.Select(row => new DownloadAsked(row.Kind, row.Name, row.Version, row.Unfinished))],
            new DownloadJudge(() => uses, () => now), move: move);

    /// <summary>The judge the look's pass and the routes build: the three use files read as they stand, each locked per version.</summary>
    private DownloadJudge Judge(DateTimeOffset now, LiveSessions? sessions = null, string? harnesses = null) =>
        DownloadJudge.Of(Home, harnesses ?? HarnessFile, configPath: null, _ => Task.FromResult(sessions ?? LiveSessions.None), () => now);

    private static LiveSessions Running(params LiveSession[] sessions) => new(sessions, null);

    [Fact]
    public async Task A_version_nothing_uses_is_listed_with_its_size_and_goes_at_the_press()
    {
        var folder = Agent("claude-code", "2.0.1");
        var row = Row(Uses(), "agent", "claude-code", "2.0.1");

        Assert.Null(row.Keeps);
        Assert.Equal(6, row.Bytes);
        var cleared = Assert.Single(await PressAsync(Uses(), Later));
        Assert.Equal(("removed", 6L), (cleared.Outcome, cleared.Version!.Bytes));
        Assert.False(Directory.Exists(folder));
        Assert.False(Directory.Exists(Path.GetDirectoryName(folder)), "an agent's folder left empty goes with its last version");
    }

    [Fact]
    public async Task The_machines_pin_keeps_its_version_and_the_other_goes()
    {
        var pinned = Agent("claude-code", "2.1.0");
        var old = Agent("claude-code", "2.0.1");
        var uses = Uses(new HarnessSettings(Versions: Pins(("claude-code", "2.1.0"))));

        var row = Row(uses, "agent", "claude-code", "2.1.0");
        Assert.Equal("pinned", row.Keeps);
        Assert.Equal(["machine"], row.By);
        var cleared = await PressAsync(uses, Later);

        Assert.Equal("kept", cleared.Single(each => each.Asked.Version == "2.1.0").Outcome);
        Assert.True(Directory.Exists(pinned));
        Assert.False(Directory.Exists(old));
    }

    [Fact]
    public async Task A_workspaces_pin_keeps_its_version_naming_the_workspace()
    {
        var pinned = Agent("codex", "0.40.0");
        var uses = Uses(new HarnessSettings(WorkspaceVersions: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["games"] = Pins(("codex", "0.40.0")),
        }));

        var row = Row(uses, "agent", "codex", "0.40.0");
        Assert.Equal("pinned", row.Keeps);
        Assert.Equal(["workspace:games"], row.By);
        Assert.Equal("kept", Assert.Single(await PressAsync(uses, Later)).Outcome);
        Assert.True(Directory.Exists(pinned));
    }

    [Fact]
    public async Task A_managed_tools_version_is_kept_and_its_other_versions_go()
    {
        var used = Tool("gh", "2.62.0");
        var old = Tool("gh", "2.61.0");
        ToolsFile("""{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""");

        Assert.Equal("used", Row(Uses(), "tool", "gh", "2.62.0").Keeps);
        await PressAsync(Uses(), Later);

        Assert.True(Directory.Exists(used));
        Assert.False(Directory.Exists(old));
    }

    [Fact]
    public async Task A_tools_json_that_does_not_read_keeps_every_version()
    {
        var tool = Tool("node", "22.20.0");
        var agent = Agent("claude-code", "2.0.1");
        ToolsFile("not json");

        Assert.Equal(("unread", "tools.json"), (Row(Uses(), "tool", "node", "22.20.0").Keeps, Row(Uses(), "tool", "node", "22.20.0").By.Single()));
        Assert.Equal("unread", Row(Uses(), "agent", "claude-code", "2.0.1").Keeps);
        Assert.All(await PressAsync(Uses(), Later), each => Assert.Equal("kept", each.Outcome));
        Assert.True(Directory.Exists(tool) && Directory.Exists(agent));
    }

    [Fact]
    public async Task A_harnesses_json_that_does_not_read_keeps_every_agents_version()
    {
        var agent = Agent("claude-code", "2.0.1");
        var uses = Uses(new HarnessSettings { Problem = "harnesses.json could not be read" });

        Assert.Equal("unread", Row(uses, "agent", "claude-code", "2.0.1").Keeps);
        Assert.Equal("kept", Assert.Single(await PressAsync(uses, Later)).Outcome);
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
    public async Task A_command_that_names_a_file_inside_a_version_keeps_it()
    {
        var agent = Agent("claude-code", "2.0.1");
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = [Path.Combine(agent, "bin", "claude.exe"), "-p"] },
        };

        Assert.Equal(("named", "driver.json"), (Row(Uses(config: config), "agent", "claude-code", "2.0.1").Keeps, Row(Uses(config: config), "agent", "claude-code", "2.0.1").By.Single()));
        Assert.Equal("kept", Assert.Single(await PressAsync(Uses(config: config), Later)).Outcome);
        Assert.True(Directory.Exists(agent));
    }

    [Fact]
    public async Task A_tool_run_as_a_file_inside_a_version_keeps_it()
    {
        var folder = Tool("git", "2.51.0");
        var file = Path.Combine(folder, Tools.Package, "bin", "git.exe").Replace("\\", "\\\\");
        ToolsFile("""{"tools":{"git":{"use":"file","file":"FILE"}}}""".Replace("FILE", file));

        Assert.Equal("named", Row(Uses(), "tool", "git", "2.51.0").Keeps);
        await PressAsync(Uses(), Later);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public async Task A_download_a_moment_old_stays_since_its_pin_may_be_on_its_way()
    {
        var folder = Agent("claude-code", "2.2.0");
        var now = DateTimeOffset.UtcNow;

        Assert.Equal("recent", Row(Uses(), "agent", "claude-code", "2.2.0", now).Keeps);
        Assert.Equal("kept", Assert.Single(await PressAsync(Uses(), now)).Outcome);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public async Task A_version_a_program_holds_is_kept_in_use_and_left_whole()
    {
        var folder = Agent("claude-code", "2.0.1");

        var cleared = Assert.Single(await PressAsync(Uses(), Later, move: (_, _) => throw new IOException("in use")));

        Assert.Equal(("kept", "in-use"), (cleared.Outcome, cleared.Version!.Keeps));
        Assert.True(File.Exists(Path.Combine(folder, "bin", "claude.exe")));
    }

    [Fact]
    public async Task Each_version_is_judged_again_right_before_it_goes()
    {
        var folder = Agent("claude-code", "2.0.1");
        var listed = DownloadClearing.List(Home, Uses(), Later).Versions;
        Assert.Null(Assert.Single(listed).Keeps);

        // Pinned between the list and the press: the press reads the pins again and keeps it.
        var pinned = Uses(new HarnessSettings(Versions: Pins(("claude-code", "2.0.1"))));
        var cleared = await DownloadClearing.ClearAsync(Home, [new DownloadAsked("agent", "claude-code", "2.0.1")], new DownloadJudge(() => pinned, () => Later));

        Assert.Equal(("kept", "pinned"), (Assert.Single(cleared).Outcome, cleared[0].Version!.Keeps));
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public async Task Only_what_was_asked_goes_and_a_name_never_reaches_past_a_listed_folder()
    {
        var asked = Agent("claude-code", "2.0.1");
        var other = Tool("gh", "2.61.0");

        var cleared = await DownloadClearing.ClearAsync(
            Home, [new DownloadAsked("agent", "claude-code", "2.0.1"), new DownloadAsked("tool", "gh", "../../data"), new DownloadAsked("agent", "claude-code", "9.9.9")],
            new DownloadJudge(() => Uses(), () => Later));

        Assert.Equal(["removed", "gone", "gone"], cleared.Select(each => each.Outcome));
        Assert.False(Directory.Exists(asked));
        Assert.True(Directory.Exists(other));
        Assert.True(Directory.Exists(Home));
    }

    [Fact]
    public async Task A_staged_download_is_never_listed_and_an_unfinished_clearing_goes_at_the_next_press()
    {
        var staged = ToolInstall.VersionFolder(Home, "gh", "2.63.0") + ".part";
        Directory.CreateDirectory(staged);
        // A run's own staging (PUSH1f2c), named by its process: never a version either.
        var running = ToolInstall.StagingOf(ToolInstall.VersionFolder(Home, "gh", "2.64.0"), Environment.ProcessId);
        Directory.CreateDirectory(running);
        var unfinished = HarnessSettings.ManagedHome(Home, "claude-code", "2.0.1") + ".0123abcd" + DownloadClearing.Aside;
        Directory.CreateDirectory(Path.Combine(unfinished, "bin"));

        var row = Assert.Single(DownloadClearing.List(Home, Uses(), Later).Versions);
        Assert.Equal(("claude-code", "2.0.1", true, (string?)null), (row.Name, row.Version, row.Unfinished, row.Keeps));

        Assert.Equal("removed", Assert.Single(await PressAsync(Uses(), Later)).Outcome);
        Assert.False(Directory.Exists(unfinished));
        Assert.True(Directory.Exists(staged));
        Assert.True(Directory.Exists(running));
    }

    [Fact]
    public async Task A_read_only_file_does_not_stop_a_removal()
    {
        var folder = Tool("node", "22.19.0");
        var file = Path.Combine(folder, Tools.Package, "bin", "node.exe");
        File.SetAttributes(file, FileAttributes.ReadOnly);

        Assert.Equal("removed", Assert.Single(await PressAsync(Uses(), Later)).Outcome);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void No_home_lists_nothing_and_says_why()
    {
        Assert.Equal(("no-home", 0), (DownloadClearing.List(null, Uses(), Later).Why, DownloadClearing.List(null, Uses(), Later).Versions.Count));
    }

    [Fact]
    public void A_list_that_does_not_measure_walks_no_version()
    {
        Agent("claude-code", "2.0.1");

        var row = Assert.Single(DownloadClearing.List(Home, Uses(), Later, measure: false).Versions);

        Assert.Equal((0L, false), (row.Bytes, row.Partial));
    }

    // ── KEEP1b: what a running session was handed (Fable F2.5) ─────────────────────────────────────────────────────

    /// <summary>
    /// 🔴 §3.5 (F2.5): a version holding a file a running session was handed is kept as <c>running</c>, both of an ACP session's: its
    /// adapter and the <c>claude</c> it is told to use. The rename alone proves nothing (Codex 2.6).
    /// </summary>
    [Fact]
    public async Task A_version_a_running_session_was_handed_is_kept_running_both_of_an_acp_session_s()
    {
        var adapter = Agent("codex-acp", "0.5.0", "codex-acp");
        var claude = Agent("claude-code", "2.0.1");
        var other = Agent("claude-code", "1.9.0");
        var sessions = Running(new LiveSession("s-acp", Later.AddDays(-1), ["agent/codex-acp/0.5.0", "agent/claude-code/2.0.1"]));

        var cleared = await DownloadClearing.ClearAsync(
            Home, [new("agent", "codex-acp", "0.5.0"), new("agent", "claude-code", "2.0.1"), new("agent", "claude-code", "1.9.0")], Judge(Later, sessions));

        Assert.Equal(["kept", "kept", "removed"], cleared.Select(each => each.Outcome));
        Assert.All(cleared.Take(2), each => Assert.Equal(("running", "session:s-acp"), (each.Version!.Keeps, Assert.Single(each.Version.By))));
        Assert.True(Directory.Exists(adapter) && Directory.Exists(claude));
        Assert.False(Directory.Exists(other));
    }

    /// <summary>
    /// §3.5 (F2.5): what a start was handed, read off the start itself: the managed binary it runs, a shim's path in its arguments, the
    /// <c>claude</c> an ACP adapter is told to use, and each managed tool's folder on its PATH; a path outside the downloads is none.
    /// </summary>
    [Fact]
    public void The_versions_a_start_was_handed_are_read_off_its_start()
    {
        var acp = Agent("codex-acp", "0.5.0", "codex-acp");
        var claude = Agent("claude-code", "2.0.1");
        var shim = Agent("gemini", "0.3.0", "gemini");
        var gh = Tool("gh", "2.61.0");
        var info = new ProcessStartInfo(Path.Combine(acp, "bin", "codex-acp.exe"));
        info.ArgumentList.Add("--flag");
        info.Environment[ClaudeAcp.ExecutableVariable] = Path.Combine(claude, "bin", "claude.exe");
        info.Environment[Tools.PathVariable] = string.Join(Path.PathSeparator, Path.Combine(gh, Tools.Package, "bin"), Path.Combine(_root, "elsewhere"));
        var wrapped = new ProcessStartInfo("cmd.exe");
        wrapped.ArgumentList.Add("/c");
        wrapped.ArgumentList.Add(Path.Combine(shim, "node_modules", ".bin", "gemini.cmd"));

        Assert.Equal(["agent/claude-code/2.0.1", "agent/codex-acp/0.5.0", "tool/gh/2.61.0"], DownloadClearing.Handed(Home, info));
        Assert.Equal(["agent/gemini/0.3.0"], DownloadClearing.Handed(Home, wrapped));
        Assert.Empty(DownloadClearing.Handed(Home, new ProcessStartInfo("claude")));
    }

    /// <summary>
    /// §3.5 (F2.5): a running session whose record holds no versions, a start from before KEEP1b, keeps the start-time rule: kept while it
    /// started before the version was first found unused, and not where it started after.
    /// </summary>
    [Fact]
    public async Task A_record_with_no_versions_is_kept_by_its_start_before_the_first_unused_moment()
    {
        Agent("claude-code", "2.0.1");
        var firstUnused = Later.AddDays(-8);
        DownloadJudge By(DateTimeOffset started) => Judge(Later, Running(new LiveSession("s-old", started, null))) with
        {
            FirstUnused = key => key == "agent/claude-code/2.0.1" ? firstUnused : null,
        };

        var before = Assert.Single(await DownloadClearing.ClearAsync(Home, [new("agent", "claude-code", "2.0.1")], By(firstUnused.AddHours(-1))));
        Assert.Equal(("kept", "running"), (before.Outcome, before.Version!.Keeps));

        var after = Assert.Single(await DownloadClearing.ClearAsync(Home, [new("agent", "claude-code", "2.0.1")], By(firstUnused.AddHours(1))));
        Assert.Equal("removed", after.Outcome);
    }

    /// <summary>
    /// §3.5 (F2.5): what a start was handed is kept on its record through the record's own door before it is spawned, a go-on's added to the
    /// first's, and read back as the running session's; a start handed nothing asks nothing, and a record the service does not know is no
    /// failure of the start.
    /// </summary>
    [Fact]
    public async Task What_a_start_was_handed_is_kept_on_its_record_and_read_back()
    {
        using var ledger = new StandInLedger();
        ledger.Working("s-1", "Engine", Path.Combine(_root, "tree"));
        var claude = Agent("claude-code", "2.0.1");
        var gh = Tool("gh", "2.61.0");
        var first = new ProcessStartInfo(Path.Combine(claude, "bin", "claude.exe"));
        var goOn = new ProcessStartInfo("claude");
        goOn.Environment[Tools.PathVariable] = Path.Combine(gh, Tools.Package, "bin");
        using var service = ledger.Client();

        await DownloadKeeping.KeepHandedAsync(service, Home, "s-1", first, CancellationToken.None);
        await DownloadKeeping.KeepHandedAsync(service, Home, "s-1", goOn, CancellationToken.None);
        await DownloadKeeping.KeepHandedAsync(service, Home, "s-1", new ProcessStartInfo("claude"), CancellationToken.None);
        await DownloadKeeping.KeepHandedAsync(service, Home, "nothere", first, CancellationToken.None);

        var running = Assert.Single(await service.LiveSessionsAsync(CancellationToken.None));
        Assert.Equal("s-1", running.Id);
        Assert.Equal(["agent/claude-code/2.0.1", "tool/gh/2.61.0"], running.Resolved);
    }

    /// <summary>
    /// §3.5: the running sessions as the service answers them, each with when it opened and what its starts were handed; a record with none
    /// reads none, the start-time rule's, and a teammate's runs nothing here.
    /// </summary>
    [Fact]
    public void The_running_sessions_are_read_with_what_their_starts_were_handed()
    {
        var running = ServiceClient.ReadRunning("""
            [ { "id": "s1", "created": "2026-10-20T09:00:00+00:00", "resolved": ["agent/claude-code/2.0.1", "tool/gh/2.61.0"] },
              { "id": "s2", "created": "2026-10-21T09:00:00+00:00" },
              { "id": "alice-laptop/s3", "created": "2026-10-21T09:00:00+00:00", "resolved": ["agent/codex/0.40.0"] } ]
            """);

        Assert.Equal(["s1", "s2"], running.Select(session => session.Id));
        Assert.Equal(["agent/claude-code/2.0.1", "tool/gh/2.61.0"], running[0].Resolved);
        Assert.Equal((new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero), (IReadOnlyList<string>?)null), (running[1].Started, running[1].Resolved));
    }

    /// <summary>
    /// §6.7: with no service to ask, the home's process markers say whether a session runs here at all: none is none; a live one keeps
    /// every version, since what it was handed cannot be read.
    /// </summary>
    [Fact]
    public async Task With_no_service_a_live_process_marker_keeps_every_version_and_none_keeps_none()
    {
        var markers = Path.Combine(_root, "markers");
        Assert.Same(LiveSessions.None, await LiveSessions.ReadAsync(null, markers, CancellationToken.None));

        Directory.CreateDirectory(markers);
        using var self = Process.GetCurrentProcess();
        File.WriteAllText(Path.Combine(markers, "s-alone.pid"), $"{self.Id} {self.StartTime.ToUniversalTime().Ticks}");
        var read = await LiveSessions.ReadAsync(null, markers, CancellationToken.None);

        Assert.NotNull(read.Problem);
        Assert.Empty(read.Sessions);
    }

    /// <summary>§6.7: sessions that cannot be read keep every version, named by what could not be read.</summary>
    [Fact]
    public async Task Sessions_that_cannot_be_read_keep_every_version()
    {
        var folder = Agent("claude-code", "2.0.1");

        var cleared = Assert.Single(await DownloadClearing.ClearAsync(
            Home, [new("agent", "claude-code", "2.0.1")], Judge(Later, new LiveSessions([], "the service did not answer"))));

        Assert.Equal(("kept", "unread", "sessions"), (cleared.Outcome, cleared.Version!.Keeps, cleared.Version.By.Single()));
        Assert.True(Directory.Exists(folder));
    }

    // ── KEEP1b: the use files' locks, held per version from the judgement to the rename (Fable F2.2 to F2.4) ──────────────

    /// <summary>
    /// 🔴 §3.5 (F2.2, F2.3): a pin written between the judgement and the rename waits on the harness file's lock, and its writer, looking
    /// again under its lock, finds the version gone and refuses it: no pin is left naming a version that is not there.
    /// </summary>
    [Fact]
    public async Task A_pin_written_between_the_judgement_and_the_rename_waits_and_is_refused_once_the_version_is_gone()
    {
        var folder = Agent("claude-code", "2.0.1");
        Task<HarnessSettings>? pin = null;
        var judge = Judge(Later) with
        {
            Judged = _ =>
            {
                pin = Task.Run(() => HarnessSettings.Pin(HarnessFile, Home, "claude-code", "2.0.1"));
                Assert.False(pin.Wait(TimeSpan.FromMilliseconds(300)), "the pin waits on the lock the judgement holds");
            },
        };

        var cleared = Assert.Single(await DownloadClearing.ClearAsync(Home, [new("agent", "claude-code", "2.0.1")], judge));

        Assert.Equal("removed", cleared.Outcome);
        Assert.False(Directory.Exists(folder));
        var refused = await Assert.ThrowsAsync<DriverException>(() => pin!);
        Assert.Contains("2.0.1", refused.Message, StringComparison.Ordinal);
        Assert.False(HarnessSettings.Load(HarnessFile).Versions.ContainsKey("claude-code"));
    }

    /// <summary>
    /// 🔴 §3.5 (F2.3): the locks are let go at the rename, never held across the removal: a pin of another version, written while the
    /// removed one is still being deleted, is taken at once.
    /// </summary>
    [Fact]
    public async Task A_pin_while_another_version_is_removed_is_taken_at_once()
    {
        Agent("claude-code", "2.0.1");
        var kept = Agent("claude-code", "2.1.0");
        TimeSpan? took = null;
        var judge = Judge(Later) with
        {
            Renamed = _ =>
            {
                var clock = Stopwatch.StartNew();
                HarnessSettings.Pin(HarnessFile, Home, "claude-code", "2.1.0");
                took = clock.Elapsed;
            },
        };

        Assert.Equal("removed", Assert.Single(await DownloadClearing.ClearAsync(Home, [new("agent", "claude-code", "2.0.1")], judge)).Outcome);

        Assert.True(took < TimeSpan.FromSeconds(2), $"the pin waited {took}");
        Assert.Equal("2.1.0", HarnessSettings.Load(HarnessFile).Versions["claude-code"]);
        Assert.True(Directory.Exists(kept));
    }

    /// <summary>
    /// §3.5 (F2.4): the harness file locked is the one the roster reads, which <c>DAORIS_HARNESS_CONFIG</c> may name elsewhere, never
    /// <c>harnesses.json</c> by name; with <c>tools.json</c> and <c>driver.json</c> at the loop's path, each held at the judgement.
    /// </summary>
    [Fact]
    public async Task The_harness_file_the_roster_reads_is_the_one_locked_with_tools_and_driver_json()
    {
        Agent("claude-code", "2.0.1");
        var named = Path.Combine(_root, "elsewhere", "wiring.json");
        Directory.CreateDirectory(Path.GetDirectoryName(named)!);
        var config = Path.Combine(_root, "elsewhere", "driver.json");
        var held = new List<bool>();
        var judge = DownloadJudge.Of(Home, named, config, _ => Task.FromResult(LiveSessions.None), () => Later) with
        {
            Judged = _ => held.AddRange([
                File.Exists(KeepLock.PathOf(named)), File.Exists(KeepLock.PathOf(DaorisHome.Entry(Home, HomeEntry.Tools))),
                File.Exists(KeepLock.PathOf(config)), File.Exists(KeepLock.PathOf(HarnessFile)),
            ]),
        };

        Assert.Equal("removed", Assert.Single(await DownloadClearing.ClearAsync(Home, [new("agent", "claude-code", "2.0.1")], judge)).Outcome);

        Assert.Equal([true, true, true, false], held);
        Assert.False(File.Exists(KeepLock.PathOf(named)), "let go at the rename");
    }

    /// <summary>§3.5: a pin made before the judgement, under the file's lock, is read by the judgement and keeps its version.</summary>
    [Fact]
    public async Task A_pin_written_before_the_judgement_keeps_its_version()
    {
        var folder = Agent("claude-code", "2.0.1");
        HarnessSettings.Pin(HarnessFile, Home, "claude-code", "2.0.1");

        var cleared = Assert.Single(await DownloadClearing.ClearAsync(Home, [new("agent", "claude-code", "2.0.1")], Judge(Later)));

        Assert.Equal(("kept", "pinned"), (cleared.Outcome, cleared.Version!.Keeps));
        Assert.True(Directory.Exists(folder));
    }

    // ── KEEP1b: the rule, the current unused stretch and the days (Fable F2.1) ─────────────────────────────────────────

    /// <summary>A pass's moment, whole seconds as the record writes them, two days past every folder made here, so none reads as recent.</summary>
    private static readonly DateTimeOffset T0 = new(DateTime.UtcNow.Date.AddDays(2).AddHours(9), TimeSpan.Zero);

    private Task<DownloadsPass> PassAsync(DateTimeOffset now, bool daily = false, LiveSessions? sessions = null, DownloadsPolicy? policy = null) =>
        DownloadKeeping.PassAsync(Home, policy ?? KeepPolicy.Default.Downloads, Judge(now, sessions), daily, KeepingRun.Look, "run-1");

    private DateTimeOffset? FirstUnused(string key) => KeepingRecord.Read(Home).Unused.TryGetValue(key, out var at) ? at : null;

    /// <summary>§2.3: a version found unused gets its first-unused time once in a stretch; a later pass that finds it unused keeps it.</summary>
    [Fact]
    public async Task First_found_unused_is_recorded_once_in_an_unused_stretch()
    {
        Agent("claude-code", "2.0.1");

        await PassAsync(T0);
        await PassAsync(T0.AddHours(5));

        Assert.Equal(T0, FirstUnused("agent/claude-code/2.0.1"));
    }

    /// <summary>
    /// 🔴 §2.3 (F2.1): found unused at T0, pinned at T1 with a pass seeing it pinned, started on at T2 by a session whose record holds no
    /// versions, unpinned at T3: at T4 its first-unused time is T4's pass, and past its days the session still running keeps it.
    /// </summary>
    [Fact]
    public async Task A_stretch_a_pin_broke_starts_again_and_a_session_started_on_the_pin_keeps_it()
    {
        var folder = Agent("claude-code", "2.0.1");
        await PassAsync(T0);
        Assert.Equal(T0, FirstUnused("agent/claude-code/2.0.1"));

        HarnessSettings.Pin(HarnessFile, Home, "claude-code", "2.0.1");
        await PassAsync(T0.AddDays(1));
        Assert.Null(FirstUnused("agent/claude-code/2.0.1"));

        var session = Running(new LiveSession("s-pinned", T0.AddDays(2), null));
        HarnessSettings.Change(HarnessFile, read => read.WithVersion("claude-code", null));
        var t4 = T0.AddDays(4);
        await PassAsync(t4);
        Assert.Equal(t4, FirstUnused("agent/claude-code/2.0.1"));

        var pass = await PassAsync(t4.AddDays(8), daily: true, sessions: session);
        var kept = Assert.Single(pass.Cleared);
        Assert.Equal(("kept", "running"), (kept.Outcome, kept.Version!.Keeps));
        Assert.True(Directory.Exists(folder));
    }

    /// <summary>§2.1: an unused version goes once its days have passed in its current stretch, through the act, and its last run is written.</summary>
    [Fact]
    public async Task An_unused_version_goes_after_its_days_and_the_runs_last_is_written()
    {
        var folder = Agent("claude-code", "2.0.1");
        var pinned = Agent("claude-code", "2.1.0");
        HarnessSettings.Pin(HarnessFile, Home, "claude-code", "2.1.0");
        await PassAsync(T0);

        Assert.Empty((await PassAsync(T0.AddDays(6), daily: true)).Cleared);
        Assert.True(Directory.Exists(folder));

        var pass = await PassAsync(T0.AddDays(7), daily: true);
        Assert.Equal("removed", Assert.Single(pass.Cleared).Outcome);
        Assert.False(Directory.Exists(folder));
        Assert.True(Directory.Exists(pinned));
        var last = KeepingRecord.Read(Home).Last["downloads"];
        Assert.Equal((KeepingRun.Look, "run-1", 1), (last.Door, last.Run, last.Removed));

        await PassAsync(T0.AddDays(7).AddMinutes(5));
        Assert.Null(FirstUnused("agent/claude-code/2.0.1"));
    }

    /// <summary>
    /// §3.1: the look's Keeping pass runs the rule: its first pass records the stretch, the day's removal seven days on takes the version,
    /// said in the report and in the machine log by the rule's door, and its last run written; a pass later that day removes nothing more.
    /// </summary>
    [Fact]
    public async Task The_look_s_keeping_pass_removes_a_download_past_its_days_and_says_so()
    {
        var folder = Agent("claude-code", "2.0.1");
        using var ledger = new StandInLedger();
        var now = T0;
        var tidying = new BranchTidying(Home) { Pace = TimeSpan.Zero, Clock = () => now };
        using var log = new MachineLog(Home, "driver", () => now);

        Assert.Empty(await tidying.LookAsync(ledger.Client(), log, CancellationToken.None));
        Assert.Equal(T0, FirstUnused("agent/claude-code/2.0.1"));

        now = T0.AddDays(7);
        var said = await tidying.LookAsync(ledger.Client(), log, CancellationToken.None);
        now = T0.AddDays(7).AddHours(1);
        var again = await tidying.LookAsync(ledger.Client(), log, CancellationToken.None);

        Assert.StartsWith("tidy  removed the download claude-code 2.0.1, which nothing had used for 7 days (", Assert.Single(said), StringComparison.Ordinal);
        Assert.Empty(again);
        Assert.False(Directory.Exists(folder));
        Assert.Equal((KeepingRun.Look, 1), (KeepingRecord.Read(Home).Last["downloads"].Door, KeepingRecord.Read(Home).Last["downloads"].Removed));
        var cleared = Directory.EnumerateFiles(DaorisHome.Entry(Home, HomeEntry.Logs), "*.jsonl")
            .SelectMany(StubFile.Lines).Select(line => System.Text.Json.JsonDocument.Parse(line).RootElement)
            .Single(line => line.GetProperty("event").GetString() == "download.cleared").GetProperty("data");
        Assert.Equal(("claude-code", "2.0.1", "look"),
            (cleared.GetProperty("name").GetString(), cleared.GetProperty("version").GetString(), cleared.GetProperty("door").GetString()));
    }

    /// <summary>
    /// §3.1: the rule removes within the pass's budget, heard between versions: one spent before any is reached removes nothing and says
    /// the day's removal was cut, for the next pass to take up.
    /// </summary>
    [Fact]
    public async Task A_spent_budget_removes_nothing_and_leaves_the_day_s_removal_to_the_next_pass()
    {
        var folder = Agent("claude-code", "2.0.1");
        await PassAsync(T0);

        var cut = await DownloadKeeping.PassAsync(Home, KeepPolicy.Default.Downloads, Judge(T0.AddDays(8)), true, KeepingRun.Look, "run-1", budget: TimeSpan.Zero);

        Assert.Equal((0, true), (cut.Cleared.Count, cut.Cut));
        Assert.True(Directory.Exists(folder));
        Assert.False((await PassAsync(T0.AddDays(8), daily: true)).Cut);
        Assert.False(Directory.Exists(folder));
    }

    /// <summary>A version another remover took while this one waited on the locks is gone, never said as one a program holds.</summary>
    [Fact]
    public async Task A_version_taken_meanwhile_is_gone_not_in_use()
    {
        var folder = Agent("claude-code", "2.0.1");
        var judge = Judge(Later) with
        {
            Sessions = _ =>
            {
                GitSeed.Remove(folder);
                return Task.FromResult(LiveSessions.None);
            },
        };

        Assert.Equal("gone", Assert.Single(await DownloadClearing.ClearAsync(Home, [new("agent", "claude-code", "2.0.1")], judge)).Outcome);
    }

    /// <summary>§2.2, §6.7: under <c>never</c> nothing goes by its days, and a <c>keeping.json</c> that does not read lets nothing go.</summary>
    [Fact]
    public async Task Never_and_an_unreadable_keeping_json_keep_it()
    {
        var folder = Agent("claude-code", "2.0.1");
        await PassAsync(T0);

        Assert.Empty((await PassAsync(T0.AddDays(30), daily: true, policy: new DownloadsPolicy(null))).Cleared);
        Assert.True(Directory.Exists(folder));

        File.WriteAllText(KeepingRecord.PathOf(Home), "{ not json\n");
        var unread = await PassAsync(T0.AddDays(30), daily: true);
        Assert.Empty(unread.Cleared);
        Assert.NotNull(unread.Unwritten);
        Assert.True(Directory.Exists(folder));
        Assert.Equal("{ not json\n", File.ReadAllText(KeepingRecord.PathOf(Home)));
    }

    /// <summary>The Downloads row's date: an unused version goes by the rule at its first-unused time and the days; one kept, or under <c>never</c>, has none.</summary>
    [Fact]
    public async Task The_rule_says_when_each_unused_version_goes()
    {
        Agent("claude-code", "2.0.1");
        Agent("claude-code", "2.1.0");
        HarnessSettings.Pin(HarnessFile, Home, "claude-code", "2.1.0");
        await PassAsync(T0);
        var uses = DownloadUses.Read(Home, HarnessSettings.Load(HarnessFile), null);
        var rows = DownloadClearing.List(Home, uses, T0.AddHours(1)).Versions;
        var record = KeepingRecord.Read(Home);

        var unused = rows.Single(row => row.Version == "2.0.1");
        var pinned = rows.Single(row => row.Version == "2.1.0");
        Assert.Equal((T0, T0.AddDays(7)), (DownloadKeeping.UnusedSince(unused, record), DownloadKeeping.Goes(unused, record, KeepPolicy.Default.Downloads)));
        Assert.Null(DownloadKeeping.Goes(pinned, record, KeepPolicy.Default.Downloads));
        Assert.Null(DownloadKeeping.Goes(unused, record, new DownloadsPolicy(null)));
    }

    // ── KEEP1b: Tools' delete takes the same act (§3.5, §6.6) ───────────────────────────────────────────────────────

    /// <summary>
    /// Tools' delete of a version a running session was handed is refused the same way, the version left whole: held, its reason the
    /// act's (the page's <c>TOOL_HELD</c> says it), since Tools runs another version.
    /// </summary>
    [Fact]
    public async Task Tools_delete_of_a_version_a_session_runs_is_refused_the_same_way()
    {
        var folder = Tool("gh", "2.61.0");

        var refused = await Assert.ThrowsAsync<ToolRefusal>(() => ToolInstall.DeleteAsync(
            Home, "gh", "2.61.0", _ => Task.FromResult(Running(new LiveSession("s-gh", Later.AddDays(-1), ["tool/gh/2.61.0"])))));

        Assert.Equal(ToolRefusal.Held, refused.Check);
        Assert.Contains("s-gh", refused.Message, StringComparison.Ordinal);
        Assert.Contains(("reason", refused.Message), refused.Facts);
        Assert.True(Directory.Exists(folder));
        Assert.Equal(folder, await ToolInstall.DeleteAsync(Home, "gh", "2.61.0", _ => Task.FromResult(LiveSessions.None)));
        Assert.False(Directory.Exists(folder));
    }

    /// <summary>Tools' delete is moved aside first: a version a program holds is refused as held, and nothing of it goes.</summary>
    [Fact]
    public async Task Tools_delete_of_a_held_version_is_refused_and_leaves_it_whole()
    {
        var folder = Tool("gh", "2.61.0");

        var refused = await Assert.ThrowsAsync<ToolRefusal>(() => ToolInstall.DeleteAsync(
            Home, "gh", "2.61.0", _ => Task.FromResult(LiveSessions.None), move: (_, _) => throw new IOException("in use")));

        Assert.Equal(ToolRefusal.Held, refused.Check);
        Assert.True(File.Exists(Path.Combine(folder, Tools.Package, "bin", "gh.exe")));
    }
}
