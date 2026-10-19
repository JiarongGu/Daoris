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

    /// <summary>A conversation's door with accounts, present by a look: nothing is probed or spawned.</summary>
    private sealed class Door(HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => "door";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a placing test spawns nothing");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a placing test spawns nothing");
    }

    private ChatRunner Runner(ChatLedger ledger)
    {
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["door"] = new Door(new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "DOOR_HOME", ProbeByPresence: true)),
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
    /// 🔴 The workspace named reaches the ledger's open and chooses the account by that workspace's default; none named sends
    /// none and takes the machine's, as before. A second in the same workspace is the ledger's refusal, naming the first.
    /// </summary>
    [Fact]
    public async Task The_workspace_reaches_the_ledger_s_open_and_chooses_its_account()
    {
        var ledger = new ChatLedger();
        var runner = Runner(ledger);

        var alpha = await runner.PlaceHelpAsync("door", Config, "alpha");
        var unnamed = await runner.PlaceHelpAsync("door", Config);
        var again = await runner.PlaceHelpAsync("door", Config, "alpha");

        Assert.Equal(["alpha", null, "alpha"], ledger.HelpAsked);
        Assert.Equal(("h1", "alpha"), (alpha.SessionId, alpha.Workspace));
        Assert.Equal("office", ledger.Record(alpha.SessionId!)["profile"]!.GetValue<string>());
        Assert.Equal(("h2", "default"), (unnamed.SessionId, unnamed.Workspace));
        Assert.Equal("home", ledger.Record(unnamed.SessionId!)["profile"]!.GetValue<string>());
        Assert.Null(again.SessionId);
        Assert.Contains("h1", again.Message);
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
