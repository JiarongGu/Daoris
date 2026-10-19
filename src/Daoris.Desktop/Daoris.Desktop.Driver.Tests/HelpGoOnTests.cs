using System.Diagnostics;
using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ASKHIST1 (D137 §2.2's chat, for Ask Daoris): an ended Ask Daoris conversation the person writes to goes on in its own harness
/// conversation, by the id kept for it, in its room, with the room's place: its rules, its asking mode and its tools up front.
/// The real client over an in-process ledger, and an adapter that hands back what it was asked and spawns nothing, so the
/// fast half: the run fails at the spawn, which is where the judgement and the place are read.
/// </summary>
public sealed class HelpGoOnTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-goon-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpGoOnTests() => Directory.CreateDirectory(Room);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Room => HelpRoom.PathOf(_home);

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    /// <summary>The native door's shape: a pipe that resumes, which reads the conversation it goes on in from its target.</summary>
    private sealed class Native : ISessionAdapter
    {
        public List<ChatTarget> Asked { get; } = [];

        public string Name => "native-talk";

        public SessionWire Wire => SessionWire.Pipe;

        public bool Interactive => true;

        public bool Resumes => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a go-on test spawns nothing");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command)
        {
            lock (Asked) Asked.Add(target);
            throw new InvalidOperationException("a go-on test spawns nothing");
        }
    }

    private ChatRunner Runner(ChatLedger ledger, Native native, SessionEvents events)
    {
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            [native.Name] = native,
        });
        return new ChatRunner(
            ledger.Client(), adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>The person's words as the say door shows them (MSG1d): theirs, under the record's id, reaching it on resume.</summary>
    private static SessionEvent Shown(string id, string text) => new()
    {
        Kind = SessionEventKind.User, Origin = "person", Id = id, Text = text, Reaches = "resume", Door = "screen",
    };

    /// <summary>
    /// 🔴 The heart of it: words written to an ended Ask Daoris conversation reopen its record and resume its own harness
    /// conversation, the id kept for it, in its room, as the room's conversation; never a new conversation under the record.
    /// </summary>
    [Fact]
    public async Task A_resumed_ask_daoris_conversation_resumes_its_own_conversation_id_in_its_room()
    {
        var ledger = new ChatLedger();
        ledger.Chat("h1", "completed", "native-talk", Room, repository: HelpRoom.Repository).Say("w1", "and the remote?");
        new HarnessConversations(_home).Keep("h1", "native-talk", "conv-h1");
        var native = new Native();
        var events = Events;
        using var runner = Runner(ledger, native, events);
        var ended = new List<(string Session, string State)>();
        runner.TakenUpEnded += (session, state) => { lock (ended) ended.Add((session, state)); };

        events.Append("h1", Shown("w1", "and the remote?"));

        await Poll.Until(() => { lock (ended) return ended.Count > 0; }, () => $"moves [{string.Join(",", ledger.Moves("h1"))}]");
        ChatTarget asked;
        lock (native.Asked) asked = Assert.Single(native.Asked);
        Assert.Equal("conv-h1", asked.Resume);
        Assert.Equal(HelpRoom.Repository, asked.Repository);
        Assert.Equal(Path.GetFullPath(Room), Path.GetFullPath(asked.Root));
        Assert.Equal("h1", asked.Session);
        // The same record, out of its ended state to working, then ended by the spawn that failed: never a new one.
        Assert.Equal(["working", "failed"], ledger.Moves("h1"));
        Assert.Equal(1, ledger.Count);
    }

    /// <summary>
    /// One from before its conversation's id was kept cannot go on in it: its words wait marked by the reason <c>unkept</c>, and
    /// its conversation says so, as a chat's do. Nothing moves and nothing is spawned.
    /// </summary>
    [Fact]
    public async Task One_whose_conversation_was_not_kept_cannot_go_on_and_says_why()
    {
        var ledger = new ChatLedger();
        ledger.Chat("h1", "completed", "native-talk", Room, repository: HelpRoom.Repository).Say("w1", "and the remote?");
        var native = new Native();
        var events = Events;
        using var runner = Runner(ledger, native, events);

        events.Append("h1", Shown("w1", "and the remote?"));

        await Poll.Until(
            () => events.After("h1", 0).Events.Any(e => e.Kind == SessionEventKind.Note && e.Why is not null),
            () => string.Join(" | ", events.After("h1", 0).Events.Select(e => $"{e.Kind}:{e.Text}")));
        Assert.Equal(ContinueWhy.Unkept, new GoOnMarks(_home).Read("h1")!.Why);
        Assert.Empty(ledger.Moves("h1"));
        lock (native.Asked) Assert.Empty(native.Asked);
    }

    /// <summary>
    /// MSG1g, CONVNOTE2g: its own account cooling holds its words for the reset, said in its conversation with Ask Daoris's door
    /// out of the wait (no repository holds it, so a new conversation from its history), each line by code and the English as
    /// it was written. Nothing moves and nothing is spawned.
    /// </summary>
    [Fact]
    public async Task One_whose_account_is_cooling_waits_and_says_its_door_by_code()
    {
        var ledger = new ChatLedger();
        ledger.Chat("h1", "completed", "acp-stub", Room, repository: HelpRoom.Repository, profile: "account-1").Say("w1", "and the remote?");
        new HarnessConversations(_home).Keep("h1", "acp-stub", "conv-h1");
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", "account-1"));
        AccountCooling.Cool(
            _home, new CoolingEntry("stub", "account-1", DateTimeOffset.UtcNow.AddHours(2), true, "session", DateTimeOffset.UtcNow, "s0"),
            DateTimeOffset.UtcNow);
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["acp-stub"] = new AcpStubAdapter(),
        });
        var events = Events;
        using var runner = new ChatRunner(
            ledger.Client(), adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));

        var answer = await runner.GoOnAsync("h1", DriverConfig.Empty);

        Assert.Null(answer.SessionId);
        var held = Assert.Single(events.After("h1", 0).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Equal($"{ResumeWords.NotYet}{answer.Message} {ResumeWords.HelpDoor}", held.Text);
        Assert.Equal(["waits.account", "waits.door-help"], NoteAssert.Codes(held.Parts));
        NoteAssert.Holds(held.Text, held.Parts);
        Assert.Empty(ledger.Moves("h1"));
    }

    /// <summary>The room is written again from the shell's reading where it hands one, as an open writes it (HELP1a).</summary>
    [Fact]
    public async Task The_room_is_written_again_from_the_machine_as_it_stands()
    {
        var ledger = new ChatLedger();
        ledger.Chat("h1", "stopped", "native-talk", Room, repository: HelpRoom.Repository).Say("w1", "and the remote?");
        new HarnessConversations(_home).Keep("h1", "native-talk", "conv-h1");
        var native = new Native();
        var events = Events;
        using var runner = Runner(ledger, native, events);
        var described = 0;
        runner.DescribeHelp = _ =>
        {
            Interlocked.Increment(ref described);
            return Task.FromResult<HelpMachine?>(HelpRoomFixture.Machine);
        };

        var answer = await runner.GoOnAsync("h1", DriverConfig.Empty);

        Assert.Null(answer.SessionId);
        Assert.Equal(1, described);
        Assert.Equal(HelpRoom.Render(HelpRoomFixture.Machine), File.ReadAllText(Path.Combine(Room, "AGENTS.md")));
        lock (native.Asked) Assert.Equal("conv-h1", Assert.Single(native.Asked).Resume);
    }

    /// <summary>
    /// 🔴 ASKSCOPE1b (D158's ASKSCOPE1 note point 4): a named workspace's conversation goes on in its own room folder, the one its
    /// harness keeps it by, written from its workspace's facts; the machine's room is not written for it.
    /// </summary>
    [Fact]
    public async Task A_named_workspace_s_conversation_goes_on_in_its_own_room_written_from_its_workspace()
    {
        // Its room stands, as its start wrote it: a conversation goes on only in a tree that stands.
        var alpha = Directory.CreateDirectory(HelpRoom.PathOf(_home, "alpha")).FullName;
        var ledger = new ChatLedger();
        ledger.Chat("h1", "completed", "native-talk", alpha, repository: HelpRoom.Repository).Say("w1", "and the remote?");
        ledger.Edit("h1", record => record["workspace"] = "alpha");
        new HarnessConversations(_home).Keep("h1", "native-talk", "conv-h1");
        var native = new Native();
        using var runner = Runner(ledger, native, Events);
        var machine = HelpRoomScopeFixture.Machine();
        runner.DescribeHelp = _ => Task.FromResult<HelpMachine?>(machine);

        await runner.GoOnAsync("h1", DriverConfig.Empty);

        ChatTarget asked;
        lock (native.Asked) asked = Assert.Single(native.Asked);
        Assert.Equal("conv-h1", asked.Resume);
        Assert.Equal(Path.GetFullPath(alpha), Path.GetFullPath(asked.Root));
        Assert.Equal(HelpRoom.Render(machine.Within("alpha")), File.ReadAllText(Path.Combine(alpha, "AGENTS.md")));
        Assert.False(File.Exists(Path.Combine(Room, "AGENTS.md")));
    }

    /// <summary>
    /// ASKSCOPE1b: one started in the machine's room before each workspace had its own goes on there, since its harness keeps it
    /// by that folder, and the room written there is the machine's, as when it started; its workspace's folder is never made.
    /// </summary>
    [Fact]
    public async Task One_started_in_the_machine_s_room_before_each_workspace_had_its_own_goes_on_there()
    {
        var ledger = new ChatLedger();
        ledger.Chat("h1", "completed", "native-talk", Room, repository: HelpRoom.Repository).Say("w1", "and the remote?");
        ledger.Edit("h1", record => record["workspace"] = "alpha");
        new HarnessConversations(_home).Keep("h1", "native-talk", "conv-h1");
        var native = new Native();
        using var runner = Runner(ledger, native, Events);
        var machine = HelpRoomScopeFixture.Machine();
        runner.DescribeHelp = _ => Task.FromResult<HelpMachine?>(machine);

        await runner.GoOnAsync("h1", DriverConfig.Empty);

        ChatTarget asked;
        lock (native.Asked) asked = Assert.Single(native.Asked);
        Assert.Equal(Path.GetFullPath(Room), Path.GetFullPath(asked.Root));
        Assert.Equal(HelpRoom.Render(machine), File.ReadAllText(Path.Combine(Room, "AGENTS.md")));
        Assert.False(Directory.Exists(HelpRoom.PathOf(_home, "alpha")));
    }

    /// <summary>A reading that fails leaves the room as last written: the conversation still goes on in it.</summary>
    [Fact]
    public async Task A_reading_that_fails_leaves_the_room_as_last_written()
    {
        var ledger = new ChatLedger();
        ledger.Chat("h1", "completed", "native-talk", Room, repository: HelpRoom.Repository).Say("w1", "and the remote?");
        new HarnessConversations(_home).Keep("h1", "native-talk", "conv-h1");
        File.WriteAllText(Path.Combine(Room, "AGENTS.md"), "the room as last written\n");
        var native = new Native();
        using var runner = Runner(ledger, native, Events);
        runner.DescribeHelp = _ => throw new HttpRequestException("the service did not answer");

        await runner.GoOnAsync("h1", DriverConfig.Empty);

        Assert.Equal("the room as last written\n", File.ReadAllText(Path.Combine(Room, "AGENTS.md")));
        lock (native.Asked) Assert.Equal("conv-h1", Assert.Single(native.Asked).Resume);
    }

    /// <summary>
    /// Starting a new one from an earlier conversation that nobody spoke in hands nothing on: it is said, and nothing opens.
    /// </summary>
    [Fact]
    public async Task Starting_from_a_conversation_nobody_spoke_in_opens_nothing()
    {
        var ledger = new ChatLedger();
        var native = new Native();
        using var runner = Runner(ledger, native, Events);

        var started = await runner.StartHelpFromAsync("h9", "native-talk", DriverConfig.Empty, HelpRoomFixture.Machine);

        Assert.Null(started.SessionId);
        Assert.Contains("nothing was said in conversation `h9`", started.Message);
        Assert.Equal(0, ledger.Count);
    }
}
