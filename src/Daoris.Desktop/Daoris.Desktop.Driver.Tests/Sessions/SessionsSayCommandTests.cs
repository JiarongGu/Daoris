using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MSG1e (D137 §5.2, D50): <c>daoris-driver sessions say</c>, the terminal's door to what the screen's box says to a session.
/// The words reach the loop that runs the session through the request folder; a loop keeps those said to a session nothing
/// here runs on its record; where no loop drives the home, a driven record keeps them for the next and a conversation is
/// refused. One line says where they stand. Exit codes are the family's: 0 taken or held, 1 refused, 2 could not.
/// </summary>
/// <remarks>
/// In-process: the service is a stand-in reached through the real client, a live process here is this test's own marker, and
/// the loop is a real <see cref="SessionRequestWatch"/> over a registry that holds no process, its words handed by
/// <see cref="LoopWords"/>. The suite's fast half.
/// </remarks>
public sealed class SessionsSayCommandTests : IDisposable
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UtcNow.AddHours(-2);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-say-" + Guid.NewGuid().ToString("N")[..8]);

    public SessionsSayCommandTests() => Directory.CreateDirectory(Sessions);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string Sessions => DaorisHome.Entry(_home, HomeEntry.Sessions);

    private string Requests => DaorisHome.Entry(_home, HomeEntry.Requests);

    private static readonly DriverConfig Driving = DriverConfig.Empty with { Drivable = ["engine"] };

    private static JsonObject Record(
        string id, string state, string? quest = "q1", string repository = "engine", string kind = "driven", string? ask = null,
        int minutes = 0, string? from = null) => new()
        {
            ["id"] = id, ["repository"] = repository, ["state"] = state, ["kind"] = kind, ["quest"] = quest, ["ask"] = ask,
            ["adapter"] = "claude-code", ["created"] = T0.AddMinutes(minutes).ToString("O"),
            ["updated"] = T0.AddMinutes(minutes + 5).ToString("O"), ["said"] = new JsonArray(), ["from"] = from,
        };

    private static JsonObject Quest(string id, string status) => new()
    {
        ["id"] = id, ["from"] = "ask #a1", ["to"] = "engine", ["title"] = $"Quest {id}", ["body"] = "A body.", ["status"] = status,
    };

    private SessionsWorld World(ServiceClient service, bool loop = true, TimeSpan? wait = null, DriverConfig? config = null) =>
        new(service, _home, config ?? Driving, SessionWire.Pipe, null)
        {
            Wait = wait ?? TimeSpan.FromSeconds(5),
            Poll = TimeSpan.FromMilliseconds(20),
            LoopRuns = () => loop,
        };

    private async Task<(int Exit, string Said)> SayAsync(Room room, string[] words, SessionsWorld? world = null)
    {
        using var service = room.Client();
        var ask = SessionsCommand.Read(["say", .. words], out var problem);
        Assert.True(problem is null, problem);
        var output = new StringWriter();
        var exit = await SessionsCommand.RunAsync(ask!, world ?? World(service), output);
        return (exit, output.ToString().ReplaceLineEndings("\n"));
    }

    /// <summary>
    /// A loop on the home: a real watch over a registry that holds no process, looking every few milliseconds, or every
    /// <paramref name="every"/>.
    /// </summary>
    private SessionRequestWatch Loop(
        SessionProcesses processes, ServiceClient service, SessionEvents? events = null, Action? nudge = null, TimeSpan? every = null) =>
        new(_home, processes, () => service, every: every ?? TimeSpan.FromMilliseconds(20))
        {
            Say = new LoopWords(processes, () => service) { Events = events, Nudge = nudge is null ? null : _ => nudge() }.HoldAsync,
        };

    /// <summary>A live process for <paramref name="id"/>, as any driver sharing the home marks one: this test's own.</summary>
    private void Alive(string id)
    {
        using var self = Process.GetCurrentProcess();
        Directory.CreateDirectory(Pids);
        File.WriteAllText(Path.Combine(Pids, $"{id}.pid"), $"{self.Id} {self.StartTime.ToUniversalTime().Ticks}");
    }

    /// <summary>Where a session's process is marked: the home's pid folder (`run/sessions/` since HOMELAYOUT3), as every tracker reads it.</summary>
    private string Pids => DaorisHome.Entry(_home, HomeEntry.SessionPids);

    private bool AnyRequest() => Directory.Exists(Requests) && Directory.EnumerateFiles(Requests).Any();

    /// <summary>
    /// How often a loop under load looks, as these tests make it (FLAKE5): only after the verb's wait would have run out on the
    /// machine's clock, which is what a loaded machine did to a loop looking every 20 ms.
    /// </summary>
    private static TimeSpan Late(SessionsWorld world) => world.Wait * 2;

    /// <summary>
    /// The verb's clock in a test of a loop that took the words (FLAKE5): it stands still until the test's condition has held,
    /// then runs as the machine's, so the verb's wait counts from the take however late a loaded machine made it. It runs by
    /// itself after a minute, so a condition that never holds fails its test rather than holding it.
    /// </summary>
    private sealed class HeldClock : TimeProvider
    {
        private static readonly TimeSpan Bound = TimeSpan.FromMinutes(1);
        private readonly long _made = Stopwatch.GetTimestamp();
        private long _from;

        /// <summary>From now on it runs; once, whoever calls it again.</summary>
        public void Run() => Interlocked.CompareExchange(ref _from, Stopwatch.GetTimestamp(), 0);

        public override long TimestampFrequency => Stopwatch.Frequency;

        public override long GetTimestamp()
        {
            if (Volatile.Read(ref _from) == 0 && Stopwatch.GetElapsedTime(_made) > Bound) Run();
            var from = Volatile.Read(ref _from);
            return from == 0 ? 0 : Stopwatch.GetTimestamp() - from;
        }
    }

    public static TheoryData<string[]> Problems => new()
    {
        new[] { "say" },
        new[] { "say", "s1" },
        new[] { "say", "s1", "   " },
        new[] { "say", "s1", "hello", "--file" },
        new[] { "say", "--file", "notes.md", "hello" },
    };

    [Theory]
    [MemberData(nameof(Problems))]
    public void Words_it_does_not_take_are_a_problem_said_with_the_usage(string[] args)
    {
        Assert.Null(SessionsCommand.Read(args, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
        Assert.Contains("sessions say <id>", SessionsCommand.Usage);
    }

    [Fact]
    public void The_words_are_joined_and_each_file_is_named_after_its_flag()
    {
        var ask = SessionsCommand.Read(["say", "d0ne0000", "also", "the", "changelog", "--file", "notes.md", "--file", "b.txt"], out var problem);

        Assert.Null(problem);
        Assert.Equal(("say", "d0ne0000", "also the changelog"), (ask!.Verb, ask.Ids[0], ask.Text));
        Assert.Equal(["notes.md", "b.txt"], ask.Files);
    }

    /// <summary>STEER2c: <c>--now</c> is the terminal's twin of *Send now*, a flag anywhere after the id, and no part of the words.</summary>
    [Fact]
    public void Now_is_a_flag_beside_the_words_and_not_one_of_them()
    {
        var now = SessionsCommand.Read(["say", "d0ne0000", "also", "--now", "the", "changelog"], out var problem);
        var plain = SessionsCommand.Read(["say", "d0ne0000", "also the changelog"], out _);

        Assert.Null(problem);
        Assert.Equal(("also the changelog", true, "now"), (now!.Text, now.Now, now.Level));
        Assert.False(plain!.Now);
        Assert.Null(plain.Level);
        Assert.Null(SessionsCommand.Read(["say", "d0ne0000", "--now"], out var bare));
        Assert.False(string.IsNullOrWhiteSpace(bare));
        Assert.Contains("--now", SessionsCommand.Usage);
    }

    /// <summary>
    /// COWORK1a (the message-urgency design §8.6): <c>--level ordinary|urgent|now</c> says when the words should reach a working
    /// session, beside the words and no part of them; <c>--level now</c> is <c>--now</c>; any other word is a problem said.
    /// </summary>
    [Fact]
    public void A_level_is_a_flag_beside_the_words_and_now_is_the_level_now()
    {
        var ordinary = SessionsCommand.Read(["say", "d0ne0000", "tidy", "--level", "ordinary", "the readme"], out var problem);
        var now = SessionsCommand.Read(["say", "d0ne0000", "--level", "now", "stop: the tests are in /spec"], out _);

        Assert.Null(problem);
        Assert.Equal(("tidy the readme", "ordinary", false), (ordinary!.Text, ordinary.Level, ordinary.Now));
        Assert.Equal(("stop: the tests are in /spec", "now", true), (now!.Text, now.Level, now.Now));
        Assert.Null(SessionsCommand.Read(["say", "d0ne0000", "hi", "--level", "priority"], out var unknown));
        Assert.Contains("ordinary, urgent or now", unknown);
        Assert.Null(SessionsCommand.Read(["say", "d0ne0000", "hi", "--level"], out _));
        Assert.Contains("[--level ordinary|urgent|now]", SessionsCommand.Usage);
    }

    /// <summary>The answer a loop gives to a say with <c>--now</c> reads back whole, by the request's file, and its level beside it.</summary>
    [Fact]
    public void A_nows_request_and_answer_carry_it_through_the_request_folder()
    {
        var requests = new SessionRequests(_home);
        var request = new SessionRequest("w0rk1ng0", SessionMove.Say, DateTimeOffset.UtcNow)
        {
            Text = "hi", Key = SessionRequests.NewKey(), Now = true, Level = "now",
        };
        requests.Write(request);

        var pending = Assert.Single(requests.Pending(DateTimeOffset.UtcNow));
        Assert.Equal((true, "now", MessageLevel.Now), (pending.Now, pending.Level, pending.Asked));
        requests.Answer(request, new WordsHeld(true, "now", null) { Now = true });
        Assert.Equal(new WordsHeld(true, "now", null) { Now = true }, requests.AnswerOf(request));
        // A request from before COWORK1a carries no level: read by its `now`, else urgent.
        Assert.Equal(MessageLevel.Urgent, new SessionRequest("w0rk1ng0", SessionMove.Say, DateTimeOffset.UtcNow).Asked);
    }

    /// <summary>
    /// STEER2c, COWORK1a: the verb says what the door did, in its own words, from the answer { sent, reaches, why, now }: taken at
    /// once, or not now with the reason and where it reads the words instead. No door's turn is stopped (D188 point 3): a door that
    /// takes words only when its turn ends holds them for it, interrupt or none. Its clock is held until the loop took the words
    /// (FLAKE5).
    /// </summary>
    [Theory]
    [InlineData("steers", "sessions: sent now: it took this at once, cutting its step in flight.\n")]
    [InlineData("turn-end", "sessions: not sent now: its door takes words only when its turn ends; it reads this when its turn ends.\n")]
    [InlineData("no-interrupt", "sessions: not sent now: its door takes words only when its turn ends; it reads this when its turn ends.\n")]
    [InlineData("next-step", "sessions: not sent now: its agent cannot be steered mid-step; it reads this at its next step.\n")]
    [InlineData("opening", "sessions: not sent now: its first prompt has not left yet; it reads this at its next step.\n")]
    [InlineData("behind", "sessions: not sent now: words ahead of these are still on their way; it reads this at its next step.\n")]
    public async Task Now_says_what_the_door_did(string door, string line)
    {
        var room = Running();
        using var service = room.Client();
        var processes = new SessionProcesses(Pids);
        var inbox = processes.OpenInbox("w0rk1ng0");
        var stopped = 0;
        switch (door)
        {
            case "steers":
                inbox.Attach(interrupt: null, deliver: _ => Task.FromResult(""), steer: _ => Task.FromResult("injected"));
                inbox.Flow();
                break;
            case "turn-end":
                inbox.Attach(interrupt: () => { stopped++; return Task.CompletedTask; });
                break;
            case "no-interrupt":
                inbox.Attach(interrupt: null);
                break;
            case "next-step":
                inbox.Attach(interrupt: null, deliver: _ => new TaskCompletionSource<string>().Task);
                inbox.Flow();
                break;
            case "opening":
                inbox.Attach(interrupt: null, deliver: _ => Task.FromResult(""));
                break;
            default:
                inbox.Attach(interrupt: null, deliver: _ => new TaskCompletionSource<string>().Task);
                inbox.Flow();
                Assert.True(inbox.Hold(new ChatMessage("one before", [])));
                break;
        }

        Alive("w0rk1ng0");
        var clock = new HeldClock();
        var world = World(service) with { Clock = clock };
        var words = new LoopWords(processes, () => service);
        await using var loop = new SessionRequestWatch(_home, processes, () => service, every: TimeSpan.FromMilliseconds(20))
        {
            Say = (request, ct) =>
            {
                clock.Run();
                return words.HoldAsync(request, ct);
            },
        };

        var (exit, said) = await SayAsync(room, ["w0rk1ng0", "use the cache", "--now"], world);

        Assert.Equal(0, exit);
        Assert.Equal(line, said);
        Assert.Equal(0, stopped);
    }

    /// <summary>
    /// COWORK1a (the design §8.6): a say at a level prints where the words reach, and the level where the door could not honour it
    /// with the reason: an ordinary word waits for the turn's end on a door that takes words at its next step; an urgent one asked
    /// of a door that takes words only when its turn ends is held for it, said so.
    /// </summary>
    [Theory]
    [InlineData("next-step", "ordinary", "sessions: held: it reads this when its turn ends.\n")]
    [InlineData("next-step", "urgent", "sessions: held: it reads this at its next step.\n")]
    [InlineData("turn-end", "urgent", "sessions: held, not urgent: its door takes words only when its turn ends; it reads this then.\n")]
    [InlineData("turn-end", "ordinary", "sessions: held: it reads this when its turn ends.\n")]
    public async Task A_level_says_where_the_words_reach_and_why_where_the_door_cannot_honour_it(string door, string level, string line)
    {
        var room = Running();
        using var service = room.Client();
        var processes = new SessionProcesses(Pids);
        var inbox = processes.OpenInbox("w0rk1ng0");
        var sent = new List<string>();
        if (door == "next-step")
        {
            inbox.Attach(interrupt: null, deliver: message => { sent.Add(message.Text); return new TaskCompletionSource<string>().Task; });
            inbox.Flow();
        }
        else
        {
            inbox.Attach(interrupt: () => Task.CompletedTask);
        }

        Alive("w0rk1ng0");
        var clock = new HeldClock();
        var world = World(service) with { Clock = clock };
        var words = new LoopWords(processes, () => service);
        await using var loop = new SessionRequestWatch(_home, processes, () => service, every: TimeSpan.FromMilliseconds(20))
        {
            Say = (request, ct) =>
            {
                clock.Run();
                return words.HoldAsync(request, ct);
            },
        };

        var (exit, said) = await SayAsync(room, ["w0rk1ng0", "use the cache", "--level", level], world);

        Assert.Equal(0, exit);
        Assert.Equal(line, said);
        Assert.Equal(door == "next-step" && level == "urgent" ? ["use the cache"] : [], sent);
    }

    private static Room Nevers() => new(
        [
            Record("he1p0000", "stopped", quest: null, repository: HelpRoom.Repository, kind: "chat"),
            Record("1ntake00", "awaiting-person", quest: null, repository: "ask #a1", kind: "chat", ask: "a1"),
            Record("st00d000", "stood-down", quest: "q3"),
            Record("e4rl1er0", "failed", quest: "q2", minutes: 0),
            Record("l4t3r000", "completed", quest: "q2", minutes: 10),
            Record("laptop/s9", "working", quest: "q2", minutes: 20),
            // HANDWORDS1: a conversation handed over, then moved: its words go to the latest that continues it.
            Record("h4nd3d00", "completed", quest: null, kind: "chat", minutes: 30),
            Record("m0v3d000", "completed", quest: null, kind: "chat", minutes: 31, from: "h4nd3d00"),
            Record("n3w3st00", "working", quest: null, kind: "chat", minutes: 32, from: "m0v3d000"),
        ],
        [Quest("q2", "Done"), Quest("q3", "Taken")]);

    /// <summary>
    /// D137 §2.2's nevers and MSG1d's codes, judged from the record before anything is asked: no request is written and the
    /// say door is never posted to. A refusal is a policy answer, exit 1.
    /// </summary>
    [Theory]
    [InlineData("nobody00", "no session here is nobody00")]
    [InlineData("laptop/s9", "laptop/s9 ran on laptop, where its conversation is")]
    [InlineData("1ntake00", "is an intake; answer its ask #a1 instead")]
    [InlineData("st00d000", "it stood down: #q3 is someone else's, so it has nothing to go on with")]
    [InlineData("e4rl1er0", "#q2 went on in a later session here, l4t3r000; say it to that one: daoris-driver sessions say l4t3r000")]
    [InlineData("h4nd3d00", "h4nd3d00 went on in conversation n3w3st00; say it to that one: daoris-driver sessions say n3w3st00")]
    public async Task Each_never_is_refused_by_its_code_before_anything_is_asked(string id, string why)
    {
        var room = Nevers();

        var (exit, said) = await SayAsync(room, [id, "one more thing"]);

        Assert.Equal(1, exit);
        Assert.Contains(why, said);
        Assert.Equal(0, room.Says);
        Assert.False(AnyRequest());
    }

    /// <summary>
    /// ASKHIST1: Ask Daoris's own conversation is no never. With no driver on the home nothing could open it again, so the words
    /// are refused naming the window, where its panel holds it; the say door is never posted to.
    /// </summary>
    [Fact]
    public async Task Ask_daoris_s_conversation_goes_on_in_the_window_and_is_refused_where_none_runs()
    {
        var room = Nevers();
        using var service = room.Client();

        var (exit, said) = await SayAsync(room, ["he1p0000", "and the remote?"], World(service, loop: false));

        Assert.Equal(1, exit);
        Assert.Contains("he1p0000 is Ask Daoris's conversation, which goes on in the window: start Daoris, then say it again.", said);
        Assert.Equal(0, room.Says);
    }

    /// <summary>The never table is one: the record's facts in, the code out, in D137 §2.2's order, null where it can go on.</summary>
    [Theory]
    [InlineData("laptop/s1", "working", "q1", "engine", null, null, WordsNever.Teammate)]
    // ASKHIST1: Ask Daoris's own conversation goes on in itself, as a chat does.
    [InlineData("h1", "stopped", null, HelpRoom.Repository, null, null, null)]
    [InlineData("i1", "awaiting-person", null, "ask #a1", "a1", null, WordsNever.Intake)]
    [InlineData("s1", "stood-down", "q1", "engine", null, null, WordsNever.StoodDown)]
    [InlineData("s1", "completed", "q1", "engine", null, "s2", WordsNever.Superseded)]
    [InlineData("s1", "completed", "q1", "engine", null, "s1", null)]
    [InlineData("s1", "failed", "q1", "engine", null, null, null)]
    [InlineData("c1", "completed", null, "engine", null, null, null)]
    public void The_never_table_says_one_code_per_record(
        string id, string state, string? quest, string repository, string? ask, string? last, string? code)
    {
        var record = new SessionRecord(id, repository, state) { Quest = quest, Ask = ask };

        Assert.Equal(code, WordsNever.Judge(record, last));
    }

    /// <summary>
    /// HANDWORDS1 (D163 point 1, D165 point 3): a conversation handed over or moved went on in another, whose record's <c>from</c>
    /// names it; the one it is continued in is the latest down that chain, so a conversation handed over and then moved names where
    /// it is now. A teammate's record continues nothing here, a quest's session that went on is <c>superseded</c>'s, and a chain
    /// that comes back on itself ends where it would repeat.
    /// </summary>
    [Fact]
    public void A_conversation_handed_over_or_moved_is_continued_in_the_latest_that_continues_it()
    {
        static SessionRecord Chat(string id, string? from = null, int minutes = 0) =>
            new(id, "engine", "completed") { Kind = "chat", From = from, Created = T0.AddMinutes(minutes) };
        IReadOnlyList<SessionRecord> records =
        [
            Chat("c1"), Chat("c2", from: "c1", minutes: 1), Chat("c3", from: "c2", minutes: 2),
            new("laptop/c4", "engine", "working") { Kind = "chat", From = "c3" },
            new("s1", "engine", "failed") { Quest = "q1" }, new("s2", "engine", "working") { Quest = "q1", From = "s1" },
            Chat("c5"), Chat("c6", from: "c7"), Chat("c7", from: "c6"),
        ];

        Assert.Equal(("c3", "c3", (string?)null, (string?)null), (
            WordsNever.ContinuedIn(records, "c1"), WordsNever.ContinuedIn(records, "c2"), WordsNever.ContinuedIn(records, "c3"),
            WordsNever.ContinuedIn(records, "c5")));
        Assert.Equal(((string?)null, (string?)null, "c7"), (
            WordsNever.ContinuedIn(records, "s1"), WordsNever.ContinuedIn(records, "nobody"), WordsNever.ContinuedIn(records, "c6")));
        Assert.Equal(WordsNever.Continued, WordsNever.Judge(records[0], last: null, next: "c3"));
        Assert.Null(WordsNever.Judge(records[2], last: null, next: null));
        Assert.Equal(WordsNever.Superseded, WordsNever.Judge(records[4], last: "s2", next: "s9"));
        Assert.Equal("c1 went on in conversation c3; say it to that one.", WordsNever.WentOn("c1", "c3"));
    }

    [Fact]
    public void The_say_doors_word_for_a_teammates_record_is_the_teammate_code() =>
        Assert.Equal((WordsNever.Teammate, WordsNever.Intake), (WordsNever.Code("not-ours"), WordsNever.Code("intake")));

    private static Room Running() => new([Record("w0rk1ng0", "working")], [Quest("q1", "Taken")]);

    /// <summary>A running session on a door that takes words during its turn (D136): held at its door, read at its next step.</summary>
    [Fact]
    public async Task A_running_session_on_a_next_step_door_reads_it_at_its_next_step()
    {
        var room = Running();
        using var service = room.Client();
        var processes = new SessionProcesses(Pids);
        var inbox = processes.OpenInbox("w0rk1ng0");
        inbox.Attach(interrupt: null, deliver: _ => Task.FromResult(""));
        Alive("w0rk1ng0");
        await using var loop = Loop(processes, service);

        var (exit, said) = await SayAsync(room, ["w0rk1ng0", "use the cache"]);

        Assert.Equal(0, exit);
        Assert.Equal("sessions: held: it reads this at its next step.\n", said);
        Assert.Equal("use the cache", Assert.Single(inbox.State.Queued).Text);
        // COWORK1c: the record is asked to keep it for the run first; this one, as a service from before would, refuses a running
        // record, and the inbox holds it here alone, as it always did.
        Assert.Equal(1, room.Says);
        Assert.False(AnyRequest());
    }

    /// <summary>A running session on a door that does not (D90, MSG1b's native door): held for the turn's end.</summary>
    [Fact]
    public async Task A_running_session_on_a_turn_end_door_reads_it_when_its_turn_ends()
    {
        var room = Running();
        using var service = room.Client();
        var processes = new SessionProcesses(Pids);
        processes.OpenInbox("w0rk1ng0").Attach(interrupt: null);
        Alive("w0rk1ng0");
        await using var loop = Loop(processes, service);

        var (exit, said) = await SayAsync(room, ["w0rk1ng0", "use the cache"]);

        Assert.Equal(0, exit);
        Assert.Equal("sessions: held: it reads this when its turn ends.\n", said);
    }

    /// <summary>
    /// Words a running session took are the person's words on its ask (D133 §1; MSG1e4): once its door holds them they go to
    /// the service's door for them by the session's own id, as the screen's do (DRIFT1a2), and the service judges which ask.
    /// </summary>
    [Fact]
    public async Task Words_a_running_session_took_are_kept_on_its_ask()
    {
        var room = Running();
        using var service = room.Client();
        var processes = new SessionProcesses(Pids);
        processes.OpenInbox("w0rk1ng0").Attach(interrupt: null);
        Alive("w0rk1ng0");
        await using var loop = Loop(processes, service);

        var (exit, _) = await SayAsync(room, ["w0rk1ng0", "use the cache"]);

        Assert.Equal(0, exit);
        await Poll.Until(() => room.Added.Count > 0, () => "nothing was posted to the ask's door");
        Assert.Equal(("w0rk1ng0", "use the cache"), Assert.Single(room.Added));
    }

    private static Room Ended() => new([Record("d0ne0000", "completed")], [Quest("q1", "Done")]);

    /// <summary>
    /// An ended session (D137 §2.2): the loop keeps the words on its record, shows them in its conversation as the terminal's,
    /// and nudges its look; the look goes on with them in the same record, and the verb says so.
    /// </summary>
    [Fact]
    public async Task An_ended_session_goes_on_with_it_and_the_same_session_takes_it()
    {
        var room = Ended();
        using var service = room.Client();
        var processes = new SessionProcesses(Pids);
        var events = new SessionEvents(Sessions);
        await using var loop = Loop(processes, service, events, nudge: () => room.GoOn("d0ne0000"));

        var (exit, said) = await SayAsync(room, ["d0ne0000", "also", "the", "changelog"]);

        Assert.Equal(0, exit);
        Assert.Equal("sessions: going on: the same session took it.\n", said);
        Assert.Equal(1, room.Says);
        var shown = Assert.Single(events.Page("d0ne0000").Events);
        Assert.Equal((SessionEventKind.User, "person", "w1", "also the changelog", "resume", RequestDoor.Terminal),
            (shown.Kind, shown.Origin, shown.Id, shown.Text, shown.Reaches, shown.Door));

        // The service keeps words said after a record ended on its ask once taken, as `reopened` (MSG1a): never added twice.
        Assert.Empty(room.Added);
    }

    /// <summary>Where it could not go on in its own conversation, its words went to a new session, and the verb names it (D137 §3.1).</summary>
    [Fact]
    public async Task Words_that_went_to_a_new_session_say_which_and_why()
    {
        var room = Ended();
        using var service = room.Client();
        var events = new SessionEvents(Sessions);
        await using var loop = Loop(new SessionProcesses(Pids), service, events, nudge: () =>
        {
            room.Hand("d0ne0000");
            events.Append("d0ne0000", Continuations.Went(["w1"], "n3w00000", ContinueWhy.Of(ContinueWhy.Tree)));
        });

        var (exit, said) = await SayAsync(room, ["d0ne0000", "also the changelog"]);

        Assert.Equal(0, exit);
        Assert.Equal("sessions: went to session `n3w00000`, because its tree is gone.\n", said);
    }

    /// <summary>
    /// A closed quest's session that cannot go on (D137 §2.2): the words stay on its record, marked, and nothing carries them on
    /// by itself, so they will not reach it: a refusal, with the conversation the person can start instead.
    /// </summary>
    [Fact]
    public async Task Words_it_cannot_go_on_with_are_kept_and_said_with_why()
    {
        var room = Ended();
        using var service = room.Client();
        var events = new SessionEvents(Sessions);
        await using var loop = Loop(new SessionProcesses(Pids), service, events, nudge: () =>
            events.Append("d0ne0000", Continuations.Cannot(["w1"], ContinueWhy.Of(ContinueWhy.Gone))));

        var (exit, said) = await SayAsync(room, ["d0ne0000", "also the changelog"]);

        Assert.Equal(1, exit);
        Assert.Contains("sessions: kept, but it cannot go on in this session, because the agent no longer has its conversation.", said);
        Assert.Contains("start a conversation with them instead: daoris-driver sessions start-from d0ne0000", said);
        Assert.Equal(["w1"], room.Said("d0ne0000"));
    }

    /// <summary>Kept, and nothing took them within the wait: held for the driver's next look, which nothing holds.</summary>
    [Fact]
    public async Task Held_words_say_the_drivers_next_look_where_nothing_holds_them()
    {
        var room = Ended();
        using var service = room.Client();
        await using var loop = Loop(new SessionProcesses(Pids), service);

        var (exit, said) = await SayAsync(room, ["d0ne0000", "also the changelog"], World(service, wait: TimeSpan.FromMilliseconds(600)));

        Assert.Equal(0, exit);
        Assert.Equal("sessions: held: the same session goes on with this at the driver's next look.\n", said);
        Assert.Equal(["w1"], room.Said("d0ne0000"));
    }

    /// <summary>
    /// 🔴 FLAKE3: a loop under load takes the words in time and keeps them, and nudges its look, only after the verb's wait is over.
    /// A loop that took a say always answers it, so the verb waits on that answer rather than its clock, and follows the words:
    /// it said the loop had taken them and not said where they stood, exit 2, while the loop was writing its answer.
    /// </summary>
    /// <remarks>
    /// 🔴 FLAKE5: "in time" was the machine's 300 ms, which a loaded machine's loop missed, and the verb withdrew the words. The
    /// loop here looks late, as one under load does, and the verb's wait is read off <see cref="HeldClock"/>, which runs from
    /// the take.
    /// </remarks>
    [Fact]
    public async Task A_loop_that_took_the_words_and_answers_after_the_wait_is_waited_for()
    {
        var room = Ended();
        using var service = room.Client();
        var processes = new SessionProcesses(Pids);
        var clock = new HeldClock();
        var world = World(service, wait: TimeSpan.FromMilliseconds(300)) with { Clock = clock };
        var words = new LoopWords(processes, () => service) { Nudge = _ => room.GoOn("d0ne0000") };
        await using var loop = new SessionRequestWatch(_home, processes, () => service, every: Late(world))
        {
            // Its pass is late: the words are kept, and its look nudged, only once the verb's wait is over.
            Say = async (request, ct) =>
            {
                clock.Run();
                await Task.Delay(world.Wait * 2, ct);
                return await words.HoldAsync(request, ct);
            },
        };

        var (exit, said) = await SayAsync(room, ["d0ne0000", "also the changelog"], world);

        Assert.Equal(0, exit);
        Assert.Equal("sessions: going on: the same session took it.\n", said);
        Assert.Equal(1, room.Says);
    }

    /// <summary>
    /// The answer is waited for only while a loop holds the home (FLAKE3): one that took the words and went away without
    /// answering is said at once, never waited out to the loop's bound on a say. TESTCLOCK2: the bound is an hour, so a verb that
    /// waited it out never ends inside the test's patience, where the machine's clock timed it against twenty seconds.
    /// </summary>
    [Fact]
    public async Task A_loop_that_took_the_words_and_went_away_unanswered_is_said_at_once()
    {
        var room = Ended();
        using var service = room.Client();
        var holds = 1;
        var clock = new HeldClock();
        var world = World(service, wait: TimeSpan.FromMilliseconds(300)) with
        {
            LoopRuns = () => Volatile.Read(ref holds) == 1,
            Answer = TimeSpan.FromHours(1),
            Clock = clock,
        };
        await using var loop = new SessionRequestWatch(_home, new SessionProcesses(Pids), () => service, every: Late(world))
        {
            Say = async (_, ct) =>
            {
                clock.Run();
                await Task.Delay(world.Wait * 2, ct);
                Volatile.Write(ref holds, 0);
                await Task.Delay(Timeout.Infinite, ct);
                return WordsHeld.Failed("never");
            },
        };

        var (exit, said) = await SayAsync(room, ["d0ne0000", "also the changelog"], world).WaitAsync(Poll.Patience);

        Assert.Equal(2, exit);
        Assert.Contains("took your words, and gave no answer of where they stand", said);
    }

    /// <summary>And no longer than the loop's own bound on a say (FLAKE3), where a loop that holds the home never answers.</summary>
    [Fact]
    public async Task A_loop_that_took_the_words_and_never_answers_is_said_after_its_bound()
    {
        var room = Ended();
        using var service = room.Client();
        var clock = new HeldClock();
        var world = World(service, wait: TimeSpan.FromMilliseconds(300)) with { Answer = TimeSpan.FromMilliseconds(300), Clock = clock };
        await using var loop = new SessionRequestWatch(_home, new SessionProcesses(Pids), () => service, every: Late(world))
        {
            Say = async (_, ct) =>
            {
                clock.Run();
                await Task.Delay(Timeout.Infinite, ct);
                return WordsHeld.Failed("never");
            },
        };

        var (exit, said) = await SayAsync(room, ["d0ne0000", "also the changelog"], world);

        Assert.Equal(2, exit);
        Assert.Contains("took your words, and gave no answer of where they stand", said);
    }

    /// <summary>What holds a start holds a reopen, and the line names it (D137 §2.2), by the planner's own verdict.</summary>
    [Fact]
    public async Task Held_words_name_what_holds_them()
    {
        // TESTWAIT5: the line falls back to "at the driver's next look" when the planner's look failed or gave no verdict, which the
        // world reports (Fell). A try that fell back is asked again, up to three times, over a fresh room; the last try judges, and
        // names the cause it was told.
        var causes = new List<string>();
        for (var attempt = 1; ; attempt++)
        {
            var room = Ended();
            using var service = room.Client();
            await using var loop = Loop(new SessionProcesses(Pids), service);
            var fell = new List<string>();
            var world = World(service, wait: TimeSpan.FromMilliseconds(600), config: Driving with { Holds = ["engine"] }) with { Fell = fell.Add };

            var (exit, said) = await SayAsync(room, ["d0ne0000", "also the changelog"], world);

            if (fell.Count > 0 && attempt < 3)
            {
                causes.AddRange(fell);
                continue;
            }

            Assert.Equal(0, exit);
            Assert.True(fell.Count == 0, $"fell back after {string.Join("; ", causes.Concat(fell))}");
            Assert.Equal("sessions: held: it goes on with this once engine is no longer held: daoris driver resume engine\n", said);
            return;
        }
    }

    /// <summary>
    /// Words said as a session winds up wait for its record to end (D137 §2.1): the say door answers it is still running, so
    /// the verb asks again until it has ended, and then the record keeps them.
    /// </summary>
    [Fact]
    public async Task A_session_winding_up_keeps_them_once_it_has_ended()
    {
        var room = new Room([Record("w1nd1ng0", "working")], [Quest("q1", "Done")]) { Winding = 3 };
        using var service = room.Client();
        await using var loop = Loop(new SessionProcesses(Pids), service, nudge: () => room.GoOn("w1nd1ng0"));

        var (exit, said) = await SayAsync(room, ["w1nd1ng0", "and the tests"]);

        Assert.Equal(0, exit);
        Assert.Equal("sessions: going on: the same session took it.\n", said);
        Assert.Equal(4, room.Says);
    }

    /// <summary>
    /// Still winding up when the wait ends: nothing here holds the words. 🔴 FLAKE5: the wait is read off a clock that runs
    /// once the loop has taken the verb's second ask, so the verb has heard it running before it gives up; on the machine's
    /// 400 ms a late loop had not answered once, and the verb kept the words as if no loop had taken them.
    /// </summary>
    [Fact]
    public async Task A_session_still_winding_up_after_the_wait_could_not_take_them()
    {
        var clock = new HeldClock();
        var room = new Room([Record("w1nd1ng0", "working")], [Quest("q1", "Taken")])
        {
            Winding = int.MaxValue,
            Heard = says =>
            {
                if (says == 2) clock.Run();
            },
        };
        using var service = room.Client();
        var world = World(service, wait: TimeSpan.FromMilliseconds(400)) with { Clock = clock };
        await using var loop = Loop(new SessionProcesses(Pids), service, every: Late(world));

        var (exit, said) = await SayAsync(room, ["w1nd1ng0", "and the tests"], world);

        Assert.Equal(2, exit);
        Assert.Contains("was still winding up after", said);
        Assert.Empty(room.Said("w1nd1ng0"));
    }

    /// <summary>
    /// Where no loop drives the home (§5.2), a driven session's words are kept on its record for the next loop to take up, and
    /// shown in its conversation as the terminal's: nothing else writes that record now.
    /// </summary>
    [Fact]
    public async Task Where_no_loop_drives_the_home_a_driven_record_keeps_them_for_the_next()
    {
        var room = Ended();
        using var service = room.Client();

        var (exit, said) = await SayAsync(room, ["d0ne0000", "also the changelog"], World(service, loop: false));

        Assert.Equal(0, exit);
        Assert.Equal("sessions: held: the same session goes on with this when a driver next runs on this machine.\n", said);
        Assert.Equal(["w1"], room.Said("d0ne0000"));
        var shown = Assert.Single(new SessionEvents(Sessions).Page("d0ne0000").Events);
        Assert.Equal(("w1", "resume", RequestDoor.Terminal), (shown.Id, shown.Reaches, shown.Door));
        Assert.False(AnyRequest());
    }

    /// <summary>A conversation goes on only through a loop's chat runner, so with none on this machine nothing can open it.</summary>
    [Fact]
    public async Task Where_no_loop_drives_the_home_a_conversation_is_refused()
    {
        var room = new Room([Record("c0ffee11", "completed", quest: null, kind: "chat")], []);
        using var service = room.Client();

        var (exit, said) = await SayAsync(room, ["c0ffee11", "one more question"], World(service, loop: false));

        Assert.Equal(1, exit);
        Assert.Contains("no driver runs on this machine to open it again", said);
        Assert.Equal(0, room.Says);
    }

    /// <summary>
    /// A loop that took nothing in time (one still coming up, or a build before this door): the request is withdrawn, and a
    /// driven record keeps the words here as where no loop runs, shown by nobody, since the loop's record of it may be open.
    /// </summary>
    [Fact]
    public async Task A_loop_that_takes_nothing_leaves_a_driven_record_to_keep_them_here()
    {
        var room = Ended();
        using var service = room.Client();

        var (exit, said) = await SayAsync(room, ["d0ne0000", "also the changelog"], World(service, wait: TimeSpan.FromMilliseconds(300)));

        Assert.Equal(0, exit);
        Assert.Equal("sessions: held: the same session goes on with this at the driver's next look.\n", said);
        Assert.Equal(["w1"], room.Said("d0ne0000"));
        Assert.Empty(new SessionEvents(Sessions).Page("d0ne0000").Events);
        Assert.False(AnyRequest());
    }

    /// <summary>A session another process runs that takes no requests (a terminal's chat): withdrawn, and the verb could not.</summary>
    [Fact]
    public async Task A_session_a_process_without_a_loop_runs_is_withdrawn_and_said()
    {
        var room = Running();
        using var service = room.Client();
        Alive("w0rk1ng0");

        var (exit, said) = await SayAsync(room, ["w0rk1ng0", "use the cache"], World(service, wait: TimeSpan.FromMilliseconds(300)));

        Assert.Equal(2, exit);
        Assert.Contains("withdrawn", said);
        Assert.False(AnyRequest());
        Assert.Equal(0, room.Says);
    }

    /// <summary>A conversation the window runs takes the words typed in its box; a loop that cannot hand it a terminal's says so.</summary>
    [Fact]
    public async Task A_conversation_a_loop_cannot_reach_could_not_take_them()
    {
        var room = new Room([Record("ch4t0000", "working", quest: null, kind: "chat")], []);
        using var service = room.Client();
        Alive("ch4t0000");
        await using var loop = new SessionRequestWatch(_home, runsHere: id => id == "ch4t0000", stop: _ => true, service: () => service)
        {
            Say = (_, _) => Task.FromResult(WordsHeld.Refused(WordsHeld.Unreached)),
        };
        using var honouring = new CancellationTokenSource();
        var honour = Task.Run(async () =>
        {
            while (!honouring.IsCancellationRequested)
            {
                await loop.HonourAsync();
                await Task.Delay(20);
            }
        });

        var (exit, said) = await SayAsync(room, ["ch4t0000", "one more question"]);
        honouring.Cancel();
        await honour;

        Assert.Equal(2, exit);
        Assert.Contains("write in its box there", said);
    }

    /// <summary>Files given with the words are read here and kept for the session, as the box keeps what is attached; the record names them.</summary>
    [Fact]
    public async Task Files_are_kept_for_the_session_and_named_on_its_record()
    {
        var room = Ended();
        using var service = room.Client();
        var notes = Path.Combine(_home, "notes.md");
        File.WriteAllText(notes, "# notes");

        var (exit, _) = await SayAsync(room, ["d0ne0000", "see the notes", "--file", notes], World(service, loop: false));

        Assert.Equal(0, exit);
        Assert.Equal(["notes.md"], room.Files("d0ne0000"));
        Assert.Single(Directory.GetFiles(ChatFiles.Folder(_home, "d0ne0000")));
    }

    [Fact]
    public async Task A_file_that_is_not_there_could_not_be_given()
    {
        var room = Ended();

        var (exit, said) = await SayAsync(room, ["d0ne0000", "see the notes", "--file", Path.Combine(_home, "missing.md")]);

        Assert.Equal(2, exit);
        Assert.Contains("is not a file on this machine", said);
        Assert.Equal(0, room.Says);
    }

    // ——— SAIDBACK1a (D137 §2.4): `sessions take-back` and `sessions reword`, the person's words taken back before a session took them.

    private async Task<(int Exit, string Said)> RunAsync(Room room, string[] args, SessionsWorld? world = null)
    {
        using var service = room.Client();
        var ask = SessionsCommand.Read(args, out var problem);
        Assert.True(problem is null, problem);
        var output = new StringWriter();
        var exit = await SessionsCommand.RunAsync(ask!, world ?? World(service, loop: false), output);
        return (exit, output.ToString().ReplaceLineEndings("\n"));
    }

    public static TheoryData<string[]> TakeBackProblems => new()
    {
        new[] { "take-back" },
        new[] { "take-back", "--word" },
        new[] { "take-back", "d0ne0000", "w1", "more" },
        new[] { "reword", "d0ne0000", "w1" },
        new[] { "reword", "d0ne0000", "w1", "   " },
        new[] { "reword", "d0ne0000" },
    };

    [Theory]
    [MemberData(nameof(TakeBackProblems))]
    public void A_take_back_or_reword_it_does_not_take_is_a_problem(string[] args)
    {
        Assert.Null(SessionsCommand.Read(args, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
        Assert.Contains("sessions take-back <id> [<word>]  ·  sessions reword <id> <word> \"…\"", SessionsCommand.Usage);
    }

    [Fact]
    public void A_take_back_names_a_word_and_a_reword_joins_its_words()
    {
        var back = SessionsCommand.Read(["take-back", "d0ne0000", "w1"], out var problem);
        var list = SessionsCommand.Read(["take-back", "d0ne0000"], out var listed);
        var reword = SessionsCommand.Read(["reword", "d0ne0000", "w1", "use", "the", "green", "one"], out var reworded);

        Assert.Equal((null, null, null), (problem, listed, reworded));
        Assert.Equal(("take-back", "d0ne0000", "w1", (string?)null), (back!.Verb, back.Ids[0], back.Word, back.Text));
        Assert.Equal(("take-back", (string?)null), (list!.Verb, list.Word));
        Assert.Equal(("reword", "w1", "use the green one"), (reword!.Verb, reword.Word, reword.Text));
    }

    /// <summary>With no word named, the words waiting on the record are listed, each under the id a take-back names it by.</summary>
    [Fact]
    public async Task A_take_back_naming_no_word_lists_the_words_waiting_with_their_ids()
    {
        var room = Waiting("Use the blue one.", "And another,\nover two lines.");

        var (exit, said) = await RunAsync(room, ["take-back", "d0ne0000"]);
        var (none, nothing) = await RunAsync(Ended(), ["take-back", "d0ne0000"]);

        Assert.Equal(0, exit);
        Assert.Equal(
            "sessions: 2 words wait on d0ne0000; take one back with `daoris-driver sessions take-back d0ne0000 <word>`:\n"
            + "  w1  Use the blue one.\n  w2  And another, over two lines.\n",
            said);
        Assert.Equal(0, room.TakeBacks);
        Assert.Equal((0, "sessions: no words wait on d0ne0000.\n"), (none, nothing));
    }

    /// <summary>
    /// Taken back where no loop drives the home: off the record by its id, and its conversation settles the word it showed waiting
    /// with the withdrawn line, as the composer's <c>Remove</c> does (QUEUEEDIT1a).
    /// </summary>
    [Fact]
    public async Task A_word_taken_back_leaves_the_record_and_its_conversation_says_so()
    {
        var room = Waiting("Use the blue one.", "And another.");

        var (exit, said) = await RunAsync(room, ["take-back", "d0ne0000", "w1"]);

        Assert.Equal(0, exit);
        Assert.Equal("sessions: Took word `w1` back from session `d0ne0000`: 1 still wait.\n", said);
        Assert.Equal(["w2"], room.Said("d0ne0000"));
        var withdrawn = Assert.Single(new SessionEvents(Sessions).Page("d0ne0000").Events);
        Assert.Equal((SessionEventKind.Note, SessionEventCodes.Withdrawn), (withdrawn.Kind, withdrawn.Code));
        Assert.Equal(["w1"], withdrawn.Words);
    }

    /// <summary>
    /// Reworded: the old word settled as withdrawn and the new words shown waiting under their own id, as the terminal's; where a
    /// loop drives the home its record of the conversation may be open, so nothing is written there.
    /// </summary>
    [Fact]
    public async Task A_reworded_word_is_shown_under_its_new_id_and_a_loops_record_is_left_alone()
    {
        var room = Waiting("Use the blue one.");

        var (exit, said) = await RunAsync(room, ["reword", "d0ne0000", "w1", "Use", "the", "green", "one."]);
        using var service = room.Client();
        // A loop is said to hold the home and none answers: the verb waits out its short time and goes to the service itself.
        var (looped, _) = await RunAsync(
            room, ["reword", "d0ne0000", "w2", "Use", "the", "red", "one."], World(service, loop: true, wait: TimeSpan.FromMilliseconds(200)));

        Assert.Equal((0, 0), (exit, looped));
        Assert.Equal("sessions: Reworded word `w1` on session `d0ne0000`: the new words wait last, as `w2`.\n", said);
        Assert.Equal(["w3"], room.Said("d0ne0000"));
        var events = new SessionEvents(Sessions).Page("d0ne0000").Events;
        Assert.Equal(2, events.Count);
        Assert.Equal((SessionEventCodes.Withdrawn, "w1"), (events[0].Code, Assert.Single(events[0].Words!)));
        Assert.Equal((SessionEventKind.User, "w2", "Use the green one.", "resume", RequestDoor.Terminal),
            (events[1].Kind, events[1].Id, events[1].Text, events[1].Reaches, events[1].Door));
    }

    /// <summary>
    /// SAIDBACK1c: with a loop on the home, a take-back reaches it through the request folder, as a say does, and the loop that
    /// takes it writes the withdrawn note into its own record of the conversation, so the page settles the word it showed waiting.
    /// A reword's new words are shown by that loop too, and no request is left behind.
    /// </summary>
    [Fact]
    public async Task A_take_back_with_a_loop_on_the_home_reaches_the_loop_which_writes_the_withdrawn_note()
    {
        var room = Waiting("Use the blue one.", "And another.");
        using var service = room.Client();
        var events = new SessionEvents(Sessions);
        var processes = new SessionProcesses(Pids);
        await using var loop = new SessionRequestWatch(_home, processes, () => service, every: TimeSpan.FromMilliseconds(20))
        {
            TakeBack = new LoopWords(processes, () => service) { Events = events }.TakeBackAsync,
        };

        var (exit, said) = await RunAsync(room, ["take-back", "d0ne0000", "w1"], World(service, loop: true));
        var (reworded, rewording) = await RunAsync(room, ["reword", "d0ne0000", "w2", "Use", "the", "red", "one."], World(service, loop: true));

        Assert.Equal((0, 0), (exit, reworded));
        Assert.Equal("sessions: Took word `w1` back from session `d0ne0000`: 1 still wait.\n", said);
        Assert.Equal("sessions: Reworded word `w2` on session `d0ne0000`: the new words wait last, as `w3`.\n", rewording);
        Assert.Equal(["w3"], room.Said("d0ne0000"));
        var shown = events.Page("d0ne0000").Events;
        Assert.Equal(
            [(SessionEventCodes.Withdrawn, "w1"), (SessionEventCodes.Withdrawn, "w2")],
            shown.Where(each => each.Kind == SessionEventKind.Note).Select(each => (each.Code!, Assert.Single(each.Words!))));
        Assert.Equal("w3", Assert.Single(shown, each => each.Kind == SessionEventKind.User).Id);
        Assert.False(AnyRequest());
    }

    /// <summary>A loop that refuses the take-back, as the service does, is a policy answer, exit 1, and writes no note.</summary>
    [Fact]
    public async Task A_take_back_the_loop_refuses_is_a_policy_answer_and_writes_no_note()
    {
        var room = Waiting("Use the blue one.");
        using var service = room.Client();
        var events = new SessionEvents(Sessions);
        var processes = new SessionProcesses(Pids);
        await using var loop = new SessionRequestWatch(_home, processes, () => service, every: TimeSpan.FromMilliseconds(20))
        {
            TakeBack = new LoopWords(processes, () => service) { Events = events }.TakeBackAsync,
        };

        var (exit, said) = await RunAsync(room, ["take-back", "d0ne0000", "w9"], World(service, loop: true));

        Assert.Equal(1, exit);
        Assert.Contains("Word `w9` no longer waits on session `d0ne0000`", said);
        Assert.Empty(events.Page("d0ne0000").Events);
        Assert.Equal(["w1"], room.Said("d0ne0000"));
    }

    /// <summary>A word a session took already, and what never goes on, are refusals: exit 1, the record and its conversation untouched.</summary>
    [Theory]
    [InlineData("d0ne0000", "w9", "Word `w9` no longer waits on session `d0ne0000`")]
    [InlineData("st00d000", "w1", "It stood down.")]
    public async Task A_take_back_refused_is_a_policy_answer(string id, string word, string why)
    {
        var room = new Room([Record("d0ne0000", "completed"), Record("st00d000", "stood-down", quest: "q3")], [Quest("q1", "Done")]);

        var (exit, said) = await RunAsync(room, ["take-back", id, word]);

        Assert.Equal(1, exit);
        Assert.Contains(why, said);
        Assert.Empty(new SessionEvents(Sessions).Page(id).Events);
    }

    /// <summary>An ended record with the words waiting on it, <c>w1</c> onwards.</summary>
    private static Room Waiting(params string[] words)
    {
        var record = Record("d0ne0000", "completed");
        var at = 0;
        foreach (var text in words)
        {
            record["said"]!.AsArray().Add(new JsonObject
            {
                ["id"] = $"w{++at}", ["text"] = text, ["at"] = T0.AddMinutes(at).ToString("O"), ["files"] = new JsonArray(), ["reopens"] = true,
            });
        }

        return new Room([record], [Quest("q1", "Done")]) { Next = words.Length };
    }

    /// <summary>
    /// The service's doors the verb crosses, standing in: the records with the words waiting on each, the quests, the registry,
    /// and the say door (MSG1a), which keeps words on a parked or ended record and refuses the rest by its word.
    /// </summary>
    private sealed class Room(IEnumerable<JsonObject> records, IEnumerable<JsonObject> quests) : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<JsonObject> _records = [.. records];
        private readonly List<JsonObject> _quests = [.. quests];
        private readonly List<(string Session, string Text)> _added = [];
        private int _words;

        /// <summary>How many more times the say door answers a working record still running; at none it has ended.</summary>
        public int Winding { get; set; }

        /// <summary>How many times the say door was posted to.</summary>
        public int Says { get; private set; }

        /// <summary>Told each post to the say door, with how many there have been: a test's condition (FLAKE5).</summary>
        public Action<int>? Heard { get; init; }

        /// <summary>How many times the take-back door was posted to (SAIDBACK1a).</summary>
        public int TakeBacks { get; private set; }

        /// <summary>The words already given ids, so a word kept next is <c>w{Next + 1}</c>.</summary>
        public int Next
        {
            get => _words;
            init => _words = value;
        }

        public ServiceClient Client() => new("http://room.test", null, new HttpClient(this, disposeHandler: false));

        private JsonObject Of(string id) => _records.Single(each => (string?)each["id"] == id);

        public IReadOnlyList<string> Said(string id)
        {
            lock (_gate) return [.. Of(id)["said"]!.AsArray().Select(word => (string)word!["id"]!)];
        }

        /// <summary>What the door for what the person adds to a running session heard (DRIFT1a), in order: the session, then the words.</summary>
        public IReadOnlyList<(string Session, string Text)> Added
        {
            get
            {
                lock (_gate) return [.. _added];
            }
        }

        public IReadOnlyList<string> Files(string id)
        {
            lock (_gate) return [.. Of(id)["said"]!.AsArray().SelectMany(word => word!["files"]!.AsArray().Select(name => (string)name!))];
        }

        /// <summary>The same session goes on with every word waiting: the look took them (MSG1b), and its record is working.</summary>
        public void GoOn(string id)
        {
            lock (_gate)
            {
                Of(id)["said"] = new JsonArray();
                Of(id)["state"] = "working";
            }
        }

        /// <summary>The words went to a new session: taken off the record, which stays as it ended.</summary>
        public void Hand(string id)
        {
            lock (_gate) Of(id)["said"] = new JsonArray();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var all = request.RequestUri.Query.Contains("includeClosed=true", StringComparison.Ordinal);
            var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(ct))?.AsObject();
            lock (_gate)
            {
                static bool Live(JsonObject record) => (string)record["state"]! is "queued" or "starting" or "working" or "awaiting-person";
                if (request.Method == HttpMethod.Get && path == "/api/sessions")
                {
                    return Answer(HttpStatusCode.OK, new JsonArray([.. _records.Where(each => all || Live(each)).Select(each => each.DeepClone())]));
                }

                if (request.Method == HttpMethod.Get && path == "/api/quests")
                {
                    return Answer(HttpStatusCode.OK, new JsonArray([.. _quests
                        .Where(each => all || (string)each["status"]! is "Open" or "Taken").Select(each => each.DeepClone())]));
                }

                if (request.Method == HttpMethod.Get && path == "/api/registry")
                {
                    return Answer(HttpStatusCode.OK, new JsonArray(new JsonObject
                    {
                        ["repository"] = "engine", ["adopted"] = true, ["registered"] = true, ["workspace"] = "default", ["root"] = "/work/engine",
                    }));
                }

                if (request.Method == HttpMethod.Post && path.EndsWith("/say", StringComparison.Ordinal))
                {
                    Says++;
                    Heard?.Invoke(Says);
                    var id = Uri.UnescapeDataString(path["/api/sessions/".Length..^"/say".Length]);
                    var record = _records.FirstOrDefault(each => (string?)each["id"] == id);
                    if (record is null) return Refuse(HttpStatusCode.NotFound, "not-found", $"No session `{id}` of this machine's.");
                    if (id.Contains('/')) return Refuse(HttpStatusCode.Conflict, "not-ours", $"Session `{id}` is a teammate's.");
                    if (record["ask"] is not null) return Refuse(HttpStatusCode.Conflict, "intake", "An intake is answered through its ask.");
                    if ((string)record["state"]! == "stood-down") return Refuse(HttpStatusCode.Conflict, "stood-down", "It stood down.");
                    if ((string)record["state"]! is "queued" or "starting" or "working")
                    {
                        if (Winding > 0 && --Winding == 0) record["state"] = "completed";
                        return Refuse(HttpStatusCode.Conflict, "running", $"Session `{id}` is working; it hears words through its driver.");
                    }

                    var word = new JsonObject
                    {
                        ["id"] = $"w{++_words}", ["text"] = body!["text"]!.GetValue<string>(), ["at"] = DateTimeOffset.UtcNow.ToString("O"),
                        ["files"] = body["files"]?.DeepClone() ?? new JsonArray(), ["reopens"] = (string)record["state"]! != "awaiting-person",
                    };
                    record["said"]!.AsArray().Add(word.DeepClone());
                    return Answer(HttpStatusCode.OK, new JsonObject
                    {
                        ["session"] = record.DeepClone(), ["message"] = $"Kept for session `{id}` to go on with.", ["said"] = word,
                    });
                }

                // The take-back door (SAIDBACK1a): one waiting word off a parked or ended record by its id, or reworded, the new
                // words last under a new id; a word not waiting, or a running record's, answers `taken`.
                if (request.Method == HttpMethod.Post && path.EndsWith("/take-back", StringComparison.Ordinal))
                {
                    TakeBacks++;
                    var id = Uri.UnescapeDataString(path["/api/sessions/".Length..^"/take-back".Length]);
                    var record = _records.FirstOrDefault(each => (string?)each["id"] == id);
                    if (record is null) return Refuse(HttpStatusCode.NotFound, "not-found", $"No session `{id}`.");
                    if ((string)record["state"]! == "stood-down") return Refuse(HttpStatusCode.Conflict, "stood-down", "It stood down.");
                    var named = body!["word"]!.GetValue<string>();
                    var said = record["said"]!.AsArray();
                    var old = said.FirstOrDefault(word => (string?)word!["id"] == named);
                    if ((string)record["state"]! is "queued" or "starting" or "working" || old is null)
                    {
                        return Answer(HttpStatusCode.OK, new JsonObject
                        {
                            ["done"] = false, ["why"] = "taken", ["message"] = $"Word `{named}` no longer waits on session `{id}`.",
                        });
                    }

                    said.Remove(old);
                    if (body["text"]?.GetValue<string>() is not { } text)
                    {
                        return Answer(HttpStatusCode.OK, new JsonObject
                        {
                            ["done"] = true, ["session"] = record.DeepClone(),
                            ["message"] = $"Took word `{named}` back from session `{id}`: {said.Count} still wait.",
                        });
                    }

                    var reworded = new JsonObject
                    {
                        ["id"] = $"w{++_words}", ["text"] = text.Trim(), ["at"] = DateTimeOffset.UtcNow.ToString("O"),
                        ["files"] = old["files"]?.DeepClone() ?? new JsonArray(), ["reopens"] = old["reopens"]?.GetValue<bool>() ?? true,
                    };
                    said.Add(reworded.DeepClone());
                    return Answer(HttpStatusCode.OK, new JsonObject
                    {
                        ["done"] = true, ["session"] = record.DeepClone(), ["said"] = reworded,
                        ["message"] = $"Reworded word `{named}` on session `{id}`: the new words wait last, as `{reworded["id"]}`.",
                    });
                }

                if (request.Method == HttpMethod.Post && path.EndsWith("/added", StringComparison.Ordinal))
                {
                    var id = Uri.UnescapeDataString(path["/api/sessions/".Length..^"/added".Length]);
                    _added.Add((id, body!["text"]!.GetValue<string>()));
                    return Answer(HttpStatusCode.OK, new JsonObject { ["kept"] = true, ["message"] = "Kept on ask #a1." });
                }

                return Answer(HttpStatusCode.NotFound, new JsonObject { ["error"] = $"No door {path}." });
            }
        }

        private static HttpResponseMessage Refuse(HttpStatusCode status, string refusal, string error) =>
            Answer(status, new JsonObject { ["error"] = error, ["refusal"] = refusal });

        private static HttpResponseMessage Answer(HttpStatusCode status, JsonNode body) => new(status)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
