using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Ask Daoris's conversation is placed in the workspace its window is on at the
/// press. The name reaches the ledger's open in its body and chooses the account as that workspace's starts do; none sent is
/// today's start, the default circle's. The real client over the stand-in ledger, and nothing spawned, so the fast half.
/// </summary>
public sealed class HelpPlaceWorkspaceTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-help-workspace-" + Guid.NewGuid().ToString("N")[..8]);

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
    private static Provider Door(TestAgent toolchain, string? full = null, string name = "door", bool interactive = true) =>
        TestDoors.Door(
            name, toolchain, transport: Transport.Protocol, interactive: interactive,
            postures: new PosturesPart(null, null, full, null), spawn: new TestDoors.RefusingSpawn("a placing test spawns nothing"));

    private ProviderRegistry _adapters = null!;

    private ChatRunner Runner(ChatLedger ledger, string? full = null)
    {
        TestAgent Toolchain(string variable) =>
            new(Binary: [Command], VersionArguments: ["--version"], ProfileVariable: variable, ProbeByPresence: true);
        var adapters = _adapters = TestDoors.Registry(
            Door(Toolchain("DOOR_HOME"), full),
            // A second agent with a mode that asks nothing, and one that holds no conversation, for the actors' walks.
            Door(Toolchain("FULL_DOOR_HOME"), "bypassPermissions", "full-door"),
            Door(Toolchain("MUTE_HOME"), name: "mute", interactive: false));
        var settings = DaorisHome.Entry(_home, HomeEntry.Harnesses);
        foreach (var account in new[] { "home", "office" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "door", account));
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "full-door", "home"));
        new HarnessSettings
        {
            Rotation = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["door"] = ["home", "office"], ["full-door"] = ["home"],
            },
        }.Save(settings);
        return new ChatRunner(
            ledger.Client(), adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            harnesses: new HarnessRoster(adapters, settings));
    }

    private static DriverConfig Config => ActorChoices.With(DriverConfig.Empty, "ask-daoris", [new ActorChoice("door")]);

    /// <summary>
    /// The workspace named reaches the ledger's open; none named sends none. A second in the same workspace is the ledger's
    /// refusal, naming the first. The accounts are one set: a workspace chooses none of its own.
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
        // The first holds the order's first account, so the second takes the next one the set has free.
        Assert.Equal("office", ledger.Record(unnamed.SessionId!)["profile"]!.GetValue<string>());
        Assert.Null(again.SessionId);
        Assert.Contains("h1", again.Message);
    }

    /// <summary>
    /// Each conversation is opened in its workspace's own room folder, which its record
    /// keeps as its tree, so the harness keeps it there; none named opens in <c>help</c>.
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
    /// In full access, a start on an agent with no mode that asks nothing is refused before anything is
    /// opened, naming the agent; restricted places it.
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
        Assert.Contains("ask-daoris mode restricted", refused.Message);
        Assert.Equal(["alpha"], ledger.HelpAsked);
        Assert.NotNull(placed.SessionId);
    }

    /// <summary>
    /// A start is placed under the mode read when it runs, so one whose mode changed to full after it is
    /// placed, as one going on would, on an agent with no such mode ends saying why before anything is spawned; restricted reaches
    /// the spawn.
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
    /// The ledger's open is told the mode the start got, the effective one, which its record keeps; a start
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
    /// A conversation going on after the person's mode changed runs under the new one, and its record is
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
            "ask-daoris", id, "goes on", new ChatAgent(_adapters, placed.Adapter!), placed.Selection!, full, runner.HelpPlace(HelpRoom.PathOf(_home, "alpha"), [], "alpha"),
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
    /// The active sessions the loop and the page read carry their workspace: what the start hands the running one
    /// of its own workspace by. A record that names none is <c>default</c>, never guessed.
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
    /// What a new conversation, or one going on, sets aside: the running ones of its own workspace
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

    /// <summary>
    /// Ask Daoris's order is walked choice by choice, each entry judged first by
    /// <c>HelpAccess.For</c> under the mode standing now, so full access passes an agent with no mode that asks nothing
    /// (<c>no-posture</c>) and opens on the next, carrying its model; its record names that agent.
    /// </summary>
    [Fact]
    public async Task Ask_Daoris_s_order_passes_an_entry_its_access_refuses_and_opens_on_the_next()
    {
        var ledger = new ChatLedger();
        var runner = Runner(ledger);
        var config = ActorChoices.With(Config.WithHelpMode(HelpModes.Full), "ask-daoris", [new ActorChoice("door"), new ActorChoice("full-door", "opus")]);

        var placed = await runner.PlaceHelpAsync("door", config, "alpha", order: ActorChoices.Effective("ask-daoris", config));

        Assert.NotNull(placed.SessionId);
        Assert.Equal("full-door", placed.Adapter!.Id.Value);
        Assert.Equal(new ActorEffective("full-door", "opus", null, "2"), placed.Chosen);
        Assert.Equal("full-door", ledger.Record(placed.SessionId!)["door"]!.GetValue<string>());
    }

    /// <summary>
    /// An order of the plain kind is a start on its agent,
    /// restricted on an agent with no mode that asks nothing included; full access on it alone is refused.
    /// </summary>
    [Fact]
    public async Task Ask_Daoris_as_before_starts_as_today_restricted_and_full()
    {
        var ledger = new ChatLedger();
        var runner = Runner(ledger);

        var restricted = await runner.PlaceHelpAsync("door", Config, "alpha", order: ActorChoices.Effective("ask-daoris", Config));
        var full = await runner.PlaceHelpAsync("door", Config.WithHelpMode(HelpModes.Full), "beta", order: ActorChoices.Effective("ask-daoris", Config));

        Assert.Equal(new ActorEffective("door", null, null, "1"), restricted.Chosen);
        Assert.Equal("door", restricted.Adapter!.Id.Value);
        Assert.Null(full.SessionId);
        Assert.Contains("ask-daoris mode restricted", full.Message);
        Assert.Equal(["alpha"], ledger.HelpAsked);
    }

    /// <summary>
    /// With no choice of Ask Daoris's that can run, the press is refused in the wait's sentence, each choice
    /// named with its reason, and nothing is opened.
    /// </summary>
    [Fact]
    public async Task No_choice_that_can_run_refuses_the_press_in_the_wait_s_sentence()
    {
        var ledger = new ChatLedger();
        var runner = Runner(ledger);
        var config = ActorChoices.With(Config, "ask-daoris", [new ActorChoice("gone"), new ActorChoice("mute")]);

        var refused = await runner.PlaceHelpAsync("door", config, "alpha", order: ActorChoices.Effective("ask-daoris", config));

        Assert.Null(refused.SessionId);
        Assert.StartsWith("Ask Daoris waits: none of its choices can run now. choice 1, gone · account default: `gone` is not an agent", refused.Message);
        Assert.Contains("choice 2, mute · account default: `mute` does not hold conversations", refused.Message);
        Assert.Empty(ledger.HelpAsked);
    }

    /// <summary>
    /// Chat's walk takes the first entry that can hold a conversation, each passed one said with its code, its
    /// accounts judged for its model; a pick is one entry, its agent's and its model Chat's first entry for that agent.
    /// </summary>
    [Fact]
    public async Task Chat_s_walk_passes_what_cannot_hold_a_conversation_and_a_pick_takes_its_agent_s_model()
    {
        var runner = Runner(new ChatLedger());
        var config = ActorChoices.With(Config, "chat", [new ActorChoice("mute"), new ActorChoice("door", "sonnet", "high")]);

        var walk = await runner.WalkAsync("chat", ActorChoices.Effective("chat", config), config, null, null, CancellationToken.None);

        Assert.Equal(new ActorEffective("door", "sonnet", "high", "2"), walk.Chosen);
        Assert.Equal("door", walk.Adapter!.Id.Value);
        Assert.Equal("unavailable", Assert.Single(walk.Passes).Code);
        Assert.Equal([new ActorEffective("door", "sonnet", "high", "pick")], ActorChoices.Effective("chat", config, pick: "door"));
        Assert.Equal(["--model", "sonnet"], new ActorEffective("door", "sonnet", null, "pick").Asked.NativeArguments([]));
    }

    /// <summary>
    /// A conversation's start holds one reservation, the taken entry's, however many it passed; a probe (the limit
    /// move's judging, the re-ask) takes none, and the next start's count follows the last one's.
    /// </summary>
    [Fact]
    public async Task A_conversation_s_walk_takes_one_reservation_and_a_probe_none()
    {
        var runner = Runner(new ChatLedger());
        var config = ActorChoices.With(Config, "ask-daoris", [new ActorChoice("mute"), new ActorChoice("door", "sonnet")]);
        var order = ActorChoices.Effective("ask-daoris", config);

        var first = await runner.WalkAsync("ask-daoris", order, config, null, null, CancellationToken.None);
        var probe = await runner.WalkAsync("ask-daoris", order, config, null, null, CancellationToken.None, counts: false);
        var second = await runner.WalkAsync("ask-daoris", order, config, null, null, CancellationToken.None);

        Assert.Equal(0, probe.Selection!.Number);
        Assert.True(first.Selection!.Number > 0);
        Assert.Equal(first.Selection.Number + 1, second.Selection!.Number);
    }

    /// <summary>
    /// Chat's walk says a pass the first time and not at the looks that pass the same choice for the same reason, and
    /// its `actor.chose` carries the passes as names in an array, never the walk's sentence.
    /// </summary>
    [Fact]
    public async Task Chat_s_walk_logs_a_pass_once_while_it_stands()
    {
        var runner = Runner(new ChatLedger());
        var config = ActorChoices.With(Config, "chat", [new ActorChoice("mute"), new ActorChoice("door", "sonnet", "high")]);
        var order = ActorChoices.Effective("chat", config);

        for (var look = 0; look < 3; look++) await runner.WalkAsync("chat", order, config, null, null, CancellationToken.None);
        await runner.WalkAsync("chat", order, config, "alpha", null, CancellationToken.None);

        var lines = Directory.GetFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl").SelectMany(StubFile.Lines)
            .Select(line => System.Text.Json.JsonDocument.Parse(line).RootElement).ToList();
        // Once for the place asked first, once for the workspace's: a pass is said per place asked.
        Assert.Equal(2, lines.Count(line => line.GetProperty("event").GetString() == "actor.passed"));
        var chose = lines.Where(line => line.GetProperty("event").GetString() == "actor.chose").ToList();
        Assert.Equal(4, chose.Count);
        Assert.All(chose, line => Assert.Equal(["1:mute:unavailable"],
            line.GetProperty("data").GetProperty("passed").EnumerateArray().Select(word => word.GetString())));
    }
}
