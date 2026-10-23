using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ASKSCOPE1a (D158's ASKSCOPE1 note, points 1-3): Ask Daoris's conversation is placed in the workspace its window was on at the
/// press. The name reaches the ledger's open in its body and chooses the account as that workspace's starts do; none sent is
/// today's start, the default circle's. The real client over the stand-in ledger, and nothing spawned, so the fast half.
/// </summary>
public sealed class HelpPlaceWorkspaceTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-workspace-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpPlaceWorkspaceTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A file that is there, so the agent is present by a look and nothing runs (AccountRotationTests' way).</summary>
    private string Command => Path.Combine(_home, "door-here");

    /// <summary>
    /// A conversation's door with accounts, present by a look: nothing is probed or spawned. <paramref name="full"/> is its mode that
    /// asks nothing (HELPMODE1a1), none by default.
    /// </summary>
    private sealed class Door(HarnessToolchain toolchain, string? full = null) : ISessionAdapter
    {
        public string Name => "door";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public HarnessToolchain? Toolchain => toolchain;

        public string? FullPosture => full;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a placing test spawns nothing");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a placing test spawns nothing");
    }

    private ChatRunner Runner(ChatLedger ledger, string? full = null)
    {
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["door"] = new Door(
                new HarnessToolchain(Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "DOOR_HOME", ProbeByPresence: true),
                full),
        });
        var settings = DaorisHome.Entry(_home, HomeEntry.Harnesses);
        foreach (var account in new[] { "home", "office" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "door", account));
        new HarnessSettings().WithDefault("door", "home").WithWorkspaceDefault("alpha", "door", "office").Save(settings);
        return new ChatRunner(
            ledger.Client(), adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            harnesses: new HarnessRoster(adapters, settings));
    }

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "door", HelperAdapter = "door" };

    /// <summary>
    /// 🔴 The workspace named reaches the ledger's open; none named sends none. A second in the same workspace is the ledger's
    /// refusal, naming the first. Rewritten by POOL1a2 (D197 point 1): the workspace's default once chose its account; an older
    /// build's default is read as the set now, so both take the order's first, the machine's account.
    /// </summary>
    [Fact]
    public async Task The_workspace_reaches_the_ledger_s_open_and_its_account_is_the_set_s()
    {
        var ledger = new ChatLedger();
        var runner = Runner(ledger);

        var alpha = await runner.PlaceHelpAsync("door", Config, "alpha");
        var unnamed = await runner.PlaceHelpAsync("door", Config);
        var again = await runner.PlaceHelpAsync("door", Config, "alpha");

        Assert.Equal(["alpha", null, "alpha"], ledger.HelpAsked);
        Assert.Equal(("h1", "alpha"), (alpha.SessionId, alpha.Workspace));
        Assert.Equal("home", ledger.Record(alpha.SessionId!)["profile"]!.GetValue<string>());
        Assert.Equal(("h2", "default"), (unnamed.SessionId, unnamed.Workspace));
        Assert.Equal("home", ledger.Record(unnamed.SessionId!)["profile"]!.GetValue<string>());
        Assert.Null(again.SessionId);
        Assert.Contains("h1", again.Message);
    }

    /// <summary>
    /// ASKSCOPE1b (D158's ASKSCOPE1 note point 4): each conversation is opened in its workspace's own room folder, which its record
    /// keeps as its tree, so the harness keeps it there; none named opens in <c>help</c>, as every conversation before did.
    /// </summary>
    [Fact]
    public async Task Each_workspace_s_conversation_is_opened_in_its_own_room_folder()
    {
        var ledger = new ChatLedger();
        var runner = Runner(ledger);

        var alpha = await runner.PlaceHelpAsync("door", Config, "alpha");
        var unnamed = await runner.PlaceHelpAsync("door", Config);

        Assert.Equal(HelpRoom.PathOf(_home, "alpha"), ledger.Record(alpha.SessionId!)["tree"]!.GetValue<string>());
        Assert.Equal(HelpRoom.PathOf(_home), ledger.Record(unnamed.SessionId!)["tree"]!.GetValue<string>());
        Assert.NotEqual(HelpRoom.PathOf(_home), HelpRoom.PathOf(_home, "alpha"));
    }

    /// <summary>
    /// HELPMODE1a1 (D183 point 3): in full access, a start on an agent with no mode that asks nothing is refused before anything is
    /// opened, naming the agent; restricted places it as before.
    /// </summary>
    [Fact]
    public async Task Full_access_on_an_agent_with_no_such_mode_is_refused_before_anything_is_opened()
    {
        var ledger = new ChatLedger();
        var runner = Runner(ledger);

        var refused =await runner.PlaceHelpAsync("door", Config.WithHelpMode(HelpModes.Full), "alpha");
        var placed = await runner.PlaceHelpAsync("door", Config, "alpha");

        Assert.Null(refused.SessionId);
        Assert.Contains("`door`", refused.Message);
        Assert.Contains("help mode restricted", refused.Message);
        Assert.Equal(["alpha"], ledger.HelpAsked);
        Assert.NotNull(placed.SessionId);
    }

    /// <summary>
    /// HELPMODE1a1 (D183 point 3): a start is placed under the mode read when it runs, so one whose mode changed to full since it was
    /// placed, as one going on would, on an agent with no such mode ends saying why before anything is spawned; restricted reaches
    /// the spawn as before.
    /// </summary>
    [Fact]
    public async Task A_start_its_agent_cannot_run_in_full_access_ends_saying_why_before_any_spawn()
    {
        var ledger = new ChatLedger();
        var runner = Runner(ledger);
        static Task<HelpMachine?> Machine(CancellationToken _) => Task.FromResult<HelpMachine?>(new HelpMachine());

        var restricted = await runner.PlaceHelpAsync("door", Config, "alpha");
        var ran = await runner.RunHelpAsync(restricted, Config, Machine);
        var full = await runner.PlaceHelpAsync("door", Config, "beta");
        var refused = await runner.RunHelpAsync(full, Config.WithHelpMode(HelpModes.Full), Machine);

        // The door spawns nothing, so the restricted start ends at its spawn.
        Assert.Equal("a placing test spawns nothing", ran.Message);
        Assert.Null(refused.SessionId);
        Assert.Contains("`door`", refused.Message);
        Assert.Equal("failed", ledger.Record(full.SessionId!)["state"]!.GetValue<string>());
    }

    /// <summary>
    /// HELPMODE1a2 (D183 point 3): the ledger's open is told the mode the start got, the effective one, which its record keeps; a start
    /// in the default mode tells it restricted.
    /// </summary>
    [Fact]
    public async Task The_open_is_told_the_mode_its_start_got()
    {
        var ledger = new ChatLedger();
        var runner = Runner(ledger, full: "bypassPermissions");

        var full = await runner.PlaceHelpAsync("door", Config.WithHelpMode(HelpModes.Full), "alpha");
        var restricted = await runner.PlaceHelpAsync("door", Config, "beta");

        Assert.Equal([HelpModes.Full, HelpModes.Restricted], ledger.HelpModesAsked);
        Assert.Equal(HelpModes.Full, ledger.Record(full.SessionId!)["helpMode"]!.GetValue<string>());
        Assert.Equal(HelpModes.Restricted, (await ledger.Client().RecordAsync(restricted.SessionId!))!.HelpMode);
    }

    /// <summary>
    /// 🔴 HELPMODE1a2 (D183 point 3): a conversation going on after the person's mode changed runs under the new one, and its record is
    /// told so before anything is spawned; one going on under the mode it last ran under tells the record nothing.
    /// </summary>
    [Fact]
    public async Task A_conversation_going_on_after_the_mode_changed_records_the_new_one()
    {
        var ledger = new ChatLedger();
        var runner = Runner(ledger, full: "bypassPermissions");
        var placed = await runner.PlaceHelpAsync("door", Config, "alpha");
        var id = placed.SessionId!;
        var full = Config.WithHelpMode(HelpModes.Full);
        var ask = new ResumeAsk("conv-1", [], "— your words are the next turn of its own conversation.");
        async Task<ChatStart> GoOnAsync() => await runner.RunAsync(
            "ask-daoris", id, "goes on", placed.Adapter!, placed.Selection!, full, runner.HelpPlace(HelpRoom.PathOf(_home, "alpha"), [], "alpha"),
            null, CancellationToken.None, new GoingOn((await ledger.Client().RecordAsync(id))!, ask));

        var went = await GoOnAsync();

        Assert.Equal([(id, HelpModes.Full)], ledger.HelpModesKept);
        Assert.Equal(HelpModes.Full, (await ledger.Client().RecordAsync(id))!.HelpMode);
        // The door spawns nothing, so it ends at its spawn, under the mode its record now says.
        Assert.Equal("a placing test spawns nothing", went.Message);

        ledger.Edit(id, record => record["state"] = "working");
        await GoOnAsync();

        Assert.Single(ledger.HelpModesKept);
    }

    /// <summary>
    /// The active sessions the loop and the page read carry their workspace (ASKSCOPE1a): what the start hands the running one
    /// of its own workspace by. A record from before, which names none, is <c>default</c>, never guessed.
    /// </summary>
    [Fact]
    public void An_active_session_reads_its_workspace_and_one_naming_none_reads_default()
    {
        var active = ServiceClient.ReadSessions("""
            [{ "id": "h1", "repository": "daoris:help", "state": "working", "workspace": "alpha" },
             { "id": "h2", "repository": "daoris:help", "state": "working" }]
            """);

        Assert.Equal(["alpha", "default"], active.Select(session => session.Workspace));
    }

    /// <summary>
    /// What a new conversation, or one going on, sets aside (ASKHIST1, now per workspace): the running ones of its own workspace
    /// alone, never its own; another workspace's runs on beside it. A workspace unnamed is <c>default</c>'s.
    /// </summary>
    [Fact]
    public void Setting_aside_takes_its_own_workspace_s_running_conversations_alone()
    {
        var rooms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["h1"] = "alpha", ["h2"] = "beta", ["h3"] = "default" };

        Assert.Equal(["h1"], ChatHelp.Aside(rooms, goingOn: null, "ALPHA"));
        Assert.Empty(ChatHelp.Aside(rooms, goingOn: "h1", "alpha"));
        Assert.Equal(["h3"], ChatHelp.Aside(rooms, goingOn: null, null));
    }
}
