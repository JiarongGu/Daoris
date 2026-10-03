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

    private string Sessions => Path.Combine(_home, "sessions");

    private string Requests => Path.Combine(Sessions, "requests");

    private static readonly DriverConfig Driving = DriverConfig.Empty with { Drivable = ["engine"] };

    private static JsonObject Record(
        string id, string state, string? quest = "q1", string repository = "engine", string kind = "driven", string? ask = null,
        int minutes = 0) => new()
        {
            ["id"] = id, ["repository"] = repository, ["state"] = state, ["kind"] = kind, ["quest"] = quest, ["ask"] = ask,
            ["adapter"] = "claude-code", ["created"] = T0.AddMinutes(minutes).ToString("O"),
            ["updated"] = T0.AddMinutes(minutes + 5).ToString("O"), ["said"] = new JsonArray(),
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

    /// <summary>A loop on the home: a real watch over a registry that holds no process, looking every few milliseconds.</summary>
    private SessionRequestWatch Loop(SessionProcesses processes, ServiceClient service, SessionEvents? events = null, Action? nudge = null) =>
        new(_home, processes, () => service, every: TimeSpan.FromMilliseconds(20))
        {
            Say = new LoopWords(processes, () => service) { Events = events, Nudge = nudge }.HoldAsync,
        };

    /// <summary>A live process for <paramref name="id"/>, as any driver sharing the home marks one: this test's own.</summary>
    private void Alive(string id)
    {
        using var self = Process.GetCurrentProcess();
        File.WriteAllText(Path.Combine(Sessions, $"{id}.pid"), $"{self.Id} {self.StartTime.ToUniversalTime().Ticks}");
    }

    private bool AnyRequest() => Directory.Exists(Requests) && Directory.EnumerateFiles(Requests).Any();

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

    private static Room Nevers() => new(
        [
            Record("he1p0000", "stopped", quest: null, repository: HelpRoom.Repository, kind: "chat"),
            Record("1ntake00", "awaiting-person", quest: null, repository: "ask #a1", kind: "chat", ask: "a1"),
            Record("st00d000", "stood-down", quest: "q3"),
            Record("e4rl1er0", "failed", quest: "q2", minutes: 0),
            Record("l4t3r000", "completed", quest: "q2", minutes: 10),
            Record("laptop/s9", "working", quest: "q2", minutes: 20),
        ],
        [Quest("q2", "Done"), Quest("q3", "Taken")]);

    /// <summary>
    /// D137 §2.2's nevers and MSG1d's codes, judged from the record before anything is asked: no request is written and the
    /// say door is never posted to. A refusal is a policy answer, exit 1.
    /// </summary>
    [Theory]
    [InlineData("nobody00", "no session here is nobody00")]
    [InlineData("laptop/s9", "laptop/s9 ran on laptop, where its conversation is")]
    [InlineData("he1p0000", "Ask Daoris's own conversation")]
    [InlineData("1ntake00", "is an intake; answer its ask #a1 instead")]
    [InlineData("st00d000", "it stood down: #q3 is someone else's, so it has nothing to go on with")]
    [InlineData("e4rl1er0", "#q2 went on in a later session here, l4t3r000; say it to that one: daoris-driver sessions say l4t3r000")]
    public async Task Each_never_is_refused_by_its_code_before_anything_is_asked(string id, string why)
    {
        var room = Nevers();

        var (exit, said) = await SayAsync(room, [id, "one more thing"]);

        Assert.Equal(1, exit);
        Assert.Contains(why, said);
        Assert.Equal(0, room.Says);
        Assert.False(AnyRequest());
    }

    /// <summary>The never table is one: the record's facts in, the code out, in D137 §2.2's order, null where it can go on.</summary>
    [Theory]
    [InlineData("laptop/s1", "working", "q1", "engine", null, null, WordsNever.Teammate)]
    [InlineData("h1", "stopped", null, HelpRoom.Repository, null, null, WordsNever.Help)]
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
        var processes = new SessionProcesses(Sessions);
        var inbox = processes.OpenInbox("w0rk1ng0");
        inbox.Attach(interrupt: null, deliver: _ => Task.FromResult(""));
        Alive("w0rk1ng0");
        await using var loop = Loop(processes, service);

        var (exit, said) = await SayAsync(room, ["w0rk1ng0", "use the cache"]);

        Assert.Equal(0, exit);
        Assert.Equal("sessions: held: it reads this at its next step.\n", said);
        Assert.Equal("use the cache", Assert.Single(inbox.State.Queued).Text);
        Assert.Equal(0, room.Says);
        Assert.False(AnyRequest());
    }

    /// <summary>A running session on a door that does not (D90, MSG1b's native door): held for the turn's end.</summary>
    [Fact]
    public async Task A_running_session_on_a_turn_end_door_reads_it_when_its_turn_ends()
    {
        var room = Running();
        using var service = room.Client();
        var processes = new SessionProcesses(Sessions);
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
        var processes = new SessionProcesses(Sessions);
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
        var processes = new SessionProcesses(Sessions);
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
        await using var loop = Loop(new SessionProcesses(Sessions), service, events, nudge: () =>
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
        await using var loop = Loop(new SessionProcesses(Sessions), service, events, nudge: () =>
            events.Append("d0ne0000", Continuations.Cannot(["w1"], ContinueWhy.Of(ContinueWhy.Gone))));

        var (exit, said) = await SayAsync(room, ["d0ne0000", "also the changelog"]);

        Assert.Equal(1, exit);
        Assert.Contains("sessions: kept, but it cannot go on in this session, because the agent no longer has its conversation.", said);
        Assert.Contains("daoris-driver chat --repository engine", said);
        Assert.Equal(["w1"], room.Said("d0ne0000"));
    }

    /// <summary>Kept, and nothing took them within the wait: held for the driver's next look, which nothing holds.</summary>
    [Fact]
    public async Task Held_words_say_the_drivers_next_look_where_nothing_holds_them()
    {
        var room = Ended();
        using var service = room.Client();
        await using var loop = Loop(new SessionProcesses(Sessions), service);

        var (exit, said) = await SayAsync(room, ["d0ne0000", "also the changelog"], World(service, wait: TimeSpan.FromMilliseconds(600)));

        Assert.Equal(0, exit);
        Assert.Equal("sessions: held: the same session goes on with this at the driver's next look.\n", said);
        Assert.Equal(["w1"], room.Said("d0ne0000"));
    }

    /// <summary>What holds a start holds a reopen, and the line names it (D137 §2.2), by the planner's own verdict.</summary>
    [Fact]
    public async Task Held_words_name_what_holds_them()
    {
        var room = Ended();
        using var service = room.Client();
        await using var loop = Loop(new SessionProcesses(Sessions), service);
        var world = World(service, wait: TimeSpan.FromMilliseconds(600), config: Driving with { Holds = ["engine"] });

        var (exit, said) = await SayAsync(room, ["d0ne0000", "also the changelog"], world);

        Assert.Equal(0, exit);
        Assert.Equal("sessions: held: it goes on with this once engine is no longer held: daoris driver resume engine\n", said);
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
        await using var loop = Loop(new SessionProcesses(Sessions), service, nudge: () => room.GoOn("w1nd1ng0"));

        var (exit, said) = await SayAsync(room, ["w1nd1ng0", "and the tests"]);

        Assert.Equal(0, exit);
        Assert.Equal("sessions: going on: the same session took it.\n", said);
        Assert.Equal(4, room.Says);
    }

    [Fact]
    public async Task A_session_still_winding_up_after_the_wait_could_not_take_them()
    {
        var room = new Room([Record("w1nd1ng0", "working")], [Quest("q1", "Taken")]) { Winding = int.MaxValue };
        using var service = room.Client();
        await using var loop = Loop(new SessionProcesses(Sessions), service);

        var (exit, said) = await SayAsync(room, ["w1nd1ng0", "and the tests"], World(service, wait: TimeSpan.FromMilliseconds(400)));

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
