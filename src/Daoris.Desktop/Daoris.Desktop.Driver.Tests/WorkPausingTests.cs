using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Pause and resume an ask's work, or one quest's, on this machine (PAUSE1b, D132 points 2–4, design §2): the one
/// implementation the screen's routes and the terminal's verbs call. A pause writes itself first, stops what of the work runs
/// here as the person's own stop and records each stop as its own, and takes nothing Resume cannot give back: a parked
/// session stays parked, a running intake goes on, a teammate's session is named. Resume releases each stop it made and names
/// what still holds a quest, from the planner's own verdict.
/// </summary>
/// <remarks>
/// In-process: the service is a stand-in reached through the real client; the registry is the home's markers, as a terminal
/// holds it, so a session nothing here runs is ended as an orphan and one another process runs (a marker this test writes) is
/// reached through the request its loop takes, the watch's seam. The suite's fast half. A real process paused and resumed
/// across real ticks is <see cref="PauseTickTests"/>.
/// </remarks>
public sealed class WorkPausingTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-pause-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Now = new(2026, 10, 3, 14, 2, 11, TimeSpan.Zero);

    public WorkPausingTests() => Directory.CreateDirectory(Path.Combine(_home, "sessions"));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string ConfigPath => Path.Combine(_home, "driver.json");

    private static JsonObject Record(string id, string state, string? quest = null, string repository = "engine", string? ask = null, int minutes = 0) => new()
    {
        ["id"] = id, ["repository"] = repository, ["state"] = state, ["kind"] = "driven", ["quest"] = quest, ["ask"] = ask,
        ["adapter"] = "claude-code",
        ["created"] = Now.AddHours(-2).AddMinutes(minutes).ToString("O"), ["updated"] = Now.AddHours(-2).AddMinutes(minutes + 5).ToString("O"),
    };

    private static JsonObject Quest(string id, string status, string from = "ask #a1", string to = "engine", string? publishedBy = null) => new()
    {
        ["id"] = id, ["from"] = from, ["to"] = to, ["title"] = $"The work of #{id}", ["body"] = "A body.", ["status"] = status,
        ["publishedBy"] = publishedBy,
    };

    /// <summary>
    /// Ask <c>a1</c>'s work, every piece a pause meets: an open quest; a take a session works (nothing here runs it); a take
    /// whose session waits on you; a done quest; a take a teammate's session works; a question the working session asked; the
    /// ask's running intake; and beside it, ask <c>b2</c>'s quest and session, which no pause of <c>a1</c> touches.
    /// </summary>
    private static Ledger Family() => new(
        [
            Record("w0rk1ng0", "working", "q2", minutes: 10),
            Record("p4rk3d00", "awaiting-person", "q3", minutes: 20),
            Record("1ntake00", "working", repository: "ask #a1", ask: "a1", minutes: 25),
            Record("laptop/s9", "working", "q5", minutes: 30),
            Record("d0ne0000", "completed", "q4", minutes: 40),
            Record("s0ther00", "working", "q7", minutes: 50),
        ],
        [
            Quest("q1", "Open"),
            Quest("q2", "Taken"),
            Quest("q3", "Taken"),
            Quest("q4", "Done"),
            Quest("q5", "Taken"),
            Quest("q6", "Open", from: "engine", to: "backend", publishedBy: "w0rk1ng0"),
            Quest("q7", "Open", from: "ask #b2"),
        ],
        ["a1", "b2"]);

    private WorkWorld World(ServiceClient service, MachineLog? log = null) =>
        new(service, _home, ConfigPath, new SessionProcesses(Path.Combine(_home, "sessions")))
        {
            Log = log,
            Clock = () => Now,
            Wait = TimeSpan.FromSeconds(5),
            Poll = TimeSpan.FromMilliseconds(20),
        };

    /// <summary>A live process for <paramref name="id"/>, as any driver sharing the home marks one: this test's own.</summary>
    private void Alive(string id)
    {
        using var self = Process.GetCurrentProcess();
        File.WriteAllText(Path.Combine(_home, "sessions", $"{id}.pid"), $"{self.Id} {self.StartTime.ToUniversalTime().Ticks}");
    }

    private static readonly IReadOnlyDictionary<string, string> NoStops = new Dictionary<string, string>();

    /// <summary>The plan names what a pause does with each piece, in words from a fixed list, and whether there is anything to pause.</summary>
    [Fact]
    public async Task The_plan_names_what_a_pause_does_with_each_piece()
    {
        var ledger = Family();
        using var service = ledger.Client();

        var plan = (await WorkPausing.PlanAsync(World(service), WorkScope.Ask, "#a1"))!;

        Assert.Equal(
            ["q1:paused", "q2:paused", "q3:paused", "q4:closed", "q5:elsewhere", "q6:paused"],
            plan.Quests.Select(each => $"{each.Quest.Quest.Id}:{each.Pause}").Order(StringComparer.Ordinal));
        Assert.Equal(
            ["1ntake00:intake", "d0ne0000:ended", "laptop/s9:teammate", "p4rk3d00:parked", "w0rk1ng0:stopped"],
            plan.Sessions.Select(each => $"{each.Session.Record.Id}:{each.Pause}").Order(StringComparer.Ordinal));
        Assert.True(plan.Pausable);
        Assert.Null(plan.Paused);
    }

    /// <summary>
    /// 🔴 The pause is written before anything is stopped, so nothing of the work starts between the steps; then the session
    /// nothing here runs is ended as the person's stop, never interrupted, and the stop is recorded as the pause's own.
    /// </summary>
    [Fact]
    public async Task A_pause_writes_itself_first_stops_what_runs_and_records_each_stop_as_its_own()
    {
        var ledger = Family();
        var pausedWhenStopped = false;
        ledger.Moving = _ => pausedWhenStopped = DriverConfig.Load(ConfigPath).PausedAsk("a1") is not null;
        using var service = ledger.Client();

        var outcome = await WorkPausing.PauseAsync(World(service), WorkScope.Ask, "a1", PluginEvents.Terminal);

        Assert.Equal(PauseVerdict.Paused, outcome.Verdict);
        Assert.True(pausedWhenStopped, "the pause was written before the stop");
        Assert.Equal(new WorkStop("w0rk1ng0", "q2"), Assert.Single(outcome.Stopped));
        Assert.Equal(("stopped", false), (ledger.State("w0rk1ng0"), ledger.Interrupted("w0rk1ng0")));
        var pause = DriverConfig.Load(ConfigPath).PausedAsk("a1")!;
        Assert.Equal(Now, pause.At);
        Assert.Equal(new Dictionary<string, string> { ["q2"] = "w0rk1ng0" }, pause.Stopped);
        Assert.False(outcome.Already);
    }

    /// <summary>
    /// A pause takes nothing Resume cannot give back (design §2.1): the session waiting on you stays parked, the ask's intake
    /// goes on, and a teammate's session is named with its machine. Another ask's session is not the work's at all.
    /// </summary>
    [Fact]
    public async Task A_pause_leaves_the_parked_session_the_intake_and_a_teammates_session_and_names_them()
    {
        var ledger = Family();
        using var service = ledger.Client();

        var outcome = await WorkPausing.PauseAsync(World(service), WorkScope.Ask, "a1", PluginEvents.Terminal);

        Assert.Equal(
            ["1ntake00:intake", "laptop/s9:teammate", "p4rk3d00:parked"],
            outcome.Kept.Select(kept => $"{kept.Session}:{kept.Why}").Order(StringComparer.Ordinal));
        Assert.Equal("laptop", Assert.Single(outcome.Kept, kept => kept.Why == PauseAct.Teammate).Machine);
        Assert.All(outcome.Kept, kept => Assert.False(kept.Unreached));
        Assert.Equal(
            ("awaiting-person", "working", "working", "working"),
            (ledger.State("p4rk3d00"), ledger.State("1ntake00"), ledger.State("laptop/s9"), ledger.State("s0ther00")));
    }

    /// <summary>
    /// A session another Daoris process here runs is stopped through the request folder SESSUX1g made, `by: pause`, carrying
    /// the pause's words for its record; the loop that runs it takes it, and the stop is the pause's.
    /// </summary>
    [Fact]
    public async Task A_session_another_process_runs_is_stopped_through_a_request_by_the_pause_with_its_words()
    {
        var ledger = Family();
        Alive("w0rk1ng0");
        Noted? heard = null;
        await using var loop = new SessionRequestWatch(
            _home, runsHere: id => id == "w0rk1ng0",
            stop: (string id, Noted? note) => { heard = note; ledger.Move(id, "stopped", note?.Note ?? "the person stopped it."); return true; },
            service: () => null);
        var honoured = new List<SessionRequest>();
        using var honouring = new CancellationTokenSource();
        var honour = Task.Run(async () =>
        {
            while (!honouring.IsCancellationRequested)
            {
                honoured.AddRange(await loop.HonourAsync());
                await Task.Delay(20);
            }
        });
        using var service = ledger.Client();

        var outcome = await WorkPausing.PauseAsync(World(service), WorkScope.Ask, "a1", PluginEvents.Terminal);
        honouring.Cancel();
        await honour;

        Assert.Equal(new WorkStop("w0rk1ng0", "q2"), Assert.Single(outcome.Stopped));
        var request = Assert.Single(honoured);
        Assert.Equal((SessionMove.Stop, RequestDoor.Pause), (request.Move, request.By));
        Assert.Equal("paused with ask `#a1`.", heard?.Note);
        // The request carried the pause's line with its code, and the loop that took it stops with both (LANG1a).
        Assert.Equal(["stopped.paused-ask"], NoteAssert.Codes(heard?.Parts));
        Assert.Equal("a1", heard!.Parts[0].Value("ask"));
        Assert.Equal("paused with ask `#a1`.", ledger.Note("w0rk1ng0"));
        Assert.Equal("w0rk1ng0", DriverConfig.Load(ConfigPath).PausedAsk("a1")!.Stopped["q2"]);
    }

    /// <summary>
    /// Nothing took the request in time: it is withdrawn, so no loop acts on it after the person was told; the pause still
    /// holds, and the session is named as one it could not reach, which the terminal says with exit 2.
    /// </summary>
    [Fact]
    public async Task A_request_nothing_takes_in_time_is_withdrawn_and_the_pause_still_holds()
    {
        var ledger = Family();
        Alive("w0rk1ng0");
        using var service = ledger.Client();
        var world = World(service) with { Wait = TimeSpan.FromMilliseconds(200) };
        var output = new StringWriter();

        var exit = await WorkCommand.RunAsync(new WorkAsk(WorkScope.Ask, "pause", "a1"), world, output);

        Assert.Equal(2, exit);
        Assert.Contains("could not stop w0rk1ng0 on #q2", output.ToString());
        Assert.Equal("working", ledger.State("w0rk1ng0"));
        Assert.False(File.Exists(Path.Combine(_home, "sessions", "requests", "w0rk1ng0.json")));
        var pause = DriverConfig.Load(ConfigPath).PausedAsk("a1")!;
        Assert.Empty(pause.Stopped);
    }

    /// <summary>
    /// A move the service does not take leaves the pause written and holding, and names the session as one to ask again,
    /// rather than failing the press half-done.
    /// </summary>
    [Fact]
    public async Task A_stop_the_service_does_not_take_leaves_the_pause_holding_and_names_the_session()
    {
        var ledger = Family();
        ledger.Unreachable = true;
        using var service = ledger.Client();
        var output = new StringWriter();

        var exit = await WorkCommand.RunAsync(new WorkAsk(WorkScope.Ask, "pause", "a1"), World(service), output);

        Assert.Equal(2, exit);
        Assert.Contains("could not stop w0rk1ng0 on #q2: the service did not move its record", output.ToString());
        Assert.NotNull(DriverConfig.Load(ConfigPath).PausedAsk("a1"));
    }

    /// <summary>A quest's own pause stops its work's sessions as an ask's does, and is kept apart from any ask's.</summary>
    [Fact]
    public async Task A_quest_paused_on_its_own_stops_its_work_and_is_kept_as_the_quests()
    {
        var ledger = Family();
        using var service = ledger.Client();

        var outcome = await WorkPausing.PauseAsync(World(service), WorkScope.Quest, "q2", PluginEvents.Terminal);

        Assert.Equal(new WorkStop("w0rk1ng0", "q2"), Assert.Single(outcome.Stopped));
        var config = DriverConfig.Load(ConfigPath);
        Assert.Equal("w0rk1ng0", config.PausedQuest("q2")!.Stopped["q2"]);
        Assert.Null(config.PausedAsk("a1"));
    }

    /// <summary>Paused again, the pause keeps when it was made and the stops it made, and stops what started since.</summary>
    [Fact]
    public async Task Pausing_again_keeps_its_time_and_its_earlier_stops()
    {
        var ledger = Family();
        var earlier = Now.AddHours(-1);
        DriverConfig.Empty
            .WithPausedAsk("a1", new WorkPause(earlier, new Dictionary<string, string> { ["q9"] = "s9" }))
            .Save(ConfigPath);
        using var service = ledger.Client();

        var outcome = await WorkPausing.PauseAsync(World(service), WorkScope.Ask, "a1", PluginEvents.Terminal);

        Assert.True(outcome.Already);
        var pause = DriverConfig.Load(ConfigPath).PausedAsk("a1")!;
        Assert.Equal(earlier, pause.At);
        Assert.Equal(new Dictionary<string, string> { ["q9"] = "s9", ["q2"] = "w0rk1ng0" }, pause.Stopped);
    }

    /// <summary>A work with nothing open or taken here, and no intake to start, has nothing to pause: said, never written (D48 §6).</summary>
    [Fact]
    public async Task A_work_with_nothing_open_has_nothing_to_pause_and_nothing_is_written()
    {
        var ledger = new Ledger([Record("d0ne0000", "completed", "q4")], [Quest("q4", "Done", from: "ask #c3")], ["c3"]);
        using var service = ledger.Client();
        var output = new StringWriter();

        var exit = await WorkCommand.RunAsync(new WorkAsk(WorkScope.Ask, "pause", "c3"), World(service), output);

        Assert.Equal(0, exit);
        Assert.Contains("nothing of ask #c3 is open or taken on this machine", output.ToString());
        Assert.False(File.Exists(ConfigPath));
    }

    /// <summary>An ask this machine has no record of, or a quest not here, is refused in both doors' words, and nothing is written.</summary>
    [Theory]
    [InlineData(WorkScope.Ask, "zz", "no ask #zz on this machine.")]
    [InlineData(WorkScope.Quest, "q99", "no quest #q99 here.")]
    public async Task An_unknown_ask_or_quest_is_refused_and_nothing_is_written(WorkScope scope, string id, string said)
    {
        using var service = Family().Client();
        var output = new StringWriter();

        var paused = await WorkCommand.RunAsync(new WorkAsk(scope, "pause", id), World(service), output);
        var resumed = await WorkCommand.RunAsync(new WorkAsk(scope, "resume", id), World(service), output);

        Assert.Equal((1, 1), (paused, resumed));
        Assert.Contains($"daoris-driver: {said}", output.ToString());
        Assert.False(File.Exists(ConfigPath));
    }

    /// <summary>
    /// Resume removes the pause and releases each stop it made, the quest against the session; a stop the person made before
    /// the pause and a repository's hold are named from the planner's own verdict, and never released.
    /// </summary>
    [Fact]
    public async Task Resume_releases_each_stop_the_pause_made_and_names_what_still_holds()
    {
        var ledger = new Ledger(
            [
                Record("w0rk1ng0", "stopped", "q2", minutes: 10),
                Record("b3f0re00", "stopped", "q8", minutes: 5),
            ],
            [Quest("q1", "Open", to: "game"), Quest("q2", "Taken"), Quest("q8", "Taken")],
            ["a1"]);
        (DriverConfig.Empty with { Drivable = ["engine", "game"], Holds = ["game"] })
            .WithPausedAsk("a1", new WorkPause(Now, new Dictionary<string, string> { ["q2"] = "w0rk1ng0" }))
            .Save(ConfigPath);
        using var service = ledger.Client();

        var outcome = await WorkPausing.ResumeAsync(World(service), WorkScope.Ask, "a1", PluginEvents.Terminal);

        Assert.Equal(ResumeVerdict.Resumed, outcome.Verdict);
        Assert.Equal(new WorkStop("w0rk1ng0", "q2"), Assert.Single(outcome.Released));
        var config = DriverConfig.Load(ConfigPath);
        Assert.Null(config.PausedAsk("a1"));
        Assert.Equal("w0rk1ng0", config.ReleasedFor("q2"));
        Assert.Null(config.ReleasedFor("q8"));
        Assert.Equal(
            ["q1:Held", "q8:Stopped"],
            outcome.Holds!.Select(hold => $"{hold.Quest}:{hold.Verdict}").Order(StringComparer.Ordinal));
        Assert.Contains("session `b3f0re00`", Assert.Single(outcome.Holds!, hold => hold.Quest == "q8").Reason);
    }

    /// <summary>A resume of what is not paused changes nothing and says so: information, never a refusal (D48 §6).</summary>
    [Fact]
    public async Task A_resume_of_what_is_not_paused_says_so_and_changes_nothing()
    {
        using var service = Family().Client();
        var output = new StringWriter();

        var exit = await WorkCommand.RunAsync(new WorkAsk(WorkScope.Ask, "resume", "a1"), World(service), output);

        Assert.Equal(0, exit);
        Assert.Contains("ask #a1 is not paused on this machine, so nothing changed.", output.ToString());
        Assert.False(File.Exists(ConfigPath));
    }

    /// <summary>A pause of an ask since deleted still resumes: the entry is this machine's, and only Resume takes it away.</summary>
    [Fact]
    public async Task A_pause_of_an_ask_since_deleted_still_resumes()
    {
        DriverConfig.Empty.WithPausedAsk("gone", new WorkPause(Now, NoStops)).Save(ConfigPath);
        using var service = Family().Client();

        var outcome = await WorkPausing.ResumeAsync(World(service), WorkScope.Ask, "gone", PluginEvents.Terminal);

        Assert.Equal(ResumeVerdict.Resumed, outcome.Verdict);
        Assert.Empty(DriverConfig.Load(ConfigPath).PausedAsks);
    }

    /// <summary>The machine log keeps names and counts only (D94 §5): the scope, how many it stopped or released, and the door.</summary>
    [Fact]
    public async Task The_machine_log_counts_a_pause_and_a_resume_and_names_no_id()
    {
        using var service = Family().Client();
        var log = new MachineLog(_home, "driver");

        await WorkPausing.PauseAsync(World(service, log), WorkScope.Ask, "a1", PluginEvents.Terminal);
        await WorkPausing.ResumeAsync(World(service, log), WorkScope.Ask, "a1", PluginEvents.Screen);
        log.Dispose();

        var lines = Directory.GetFiles(Path.Combine(_home, MachineLog.Folder)).SelectMany(File.ReadAllLines).ToList();
        var paused = Assert.Single(lines, line => line.Contains("\"work.paused\"", StringComparison.Ordinal));
        var resumed = Assert.Single(lines, line => line.Contains("\"work.resumed\"", StringComparison.Ordinal));
        Assert.Contains("\"scope\":\"ask\"", paused);
        Assert.Contains("\"stopped\":1", paused);
        Assert.Contains("\"door\":\"terminal\"", paused);
        Assert.Contains("\"released\":1", resumed);
        Assert.Contains("\"door\":\"screen\"", resumed);
        Assert.DoesNotContain("a1", paused + resumed);
        Assert.DoesNotContain("w0rk1ng0", paused + resumed);
    }

    /// <summary>The terminal's lines: the pause, its door, and each piece in English, the first under the binary's name.</summary>
    [Fact]
    public async Task The_terminal_says_the_pause_its_door_and_each_piece()
    {
        using var service = Family().Client();
        var output = new StringWriter();

        var exit = await WorkCommand.RunAsync(new WorkAsk(WorkScope.Ask, "pause", "a1"), World(service), output);

        var said = output.ToString().ReplaceLineEndings("\n");
        Assert.Equal(0, exit);
        Assert.StartsWith(
            "daoris-driver: paused ask #a1: nothing of it starts on this machine until you resume it — daoris-driver ask --resume a1\n", said);
        Assert.Contains("  stopped w0rk1ng0 on #q2: its tree keeps what it wrote", said);
        Assert.Contains("  left p4rk3d00 on #q3 waiting on you", said);
        Assert.Contains("  its intake 1ntake00 keeps reading", said);
        Assert.Contains("  laptop/s9 on #q5 runs on laptop", said);
        // A pause's words say paused, never held (design §8.1): a hold is a repository's, and stops nothing that runs.
        Assert.DoesNotContain("held", said);
    }

    public static TheoryData<WorkScope, string[]> Problems => new()
    {
        { WorkScope.Ask, ["--pause"] },
        { WorkScope.Ask, ["--resume", "a1", "a2"] },
        { WorkScope.Ask, ["--pause", "--now"] },
        { WorkScope.Ask, ["--pause", "#"] },
        { WorkScope.Quest, ["pause"] },
        { WorkScope.Quest, ["resume", "q1", "--yes"] },
    };

    [Theory]
    [MemberData(nameof(Problems))]
    public void Words_it_does_not_take_are_a_problem(WorkScope scope, string[] args)
    {
        Assert.True(WorkCommand.Asks(scope, args));
        Assert.Null(WorkCommand.Read(scope, args, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Fact]
    public void The_words_name_a_pause_or_a_resume_of_an_ask_or_a_quest()
    {
        Assert.Equal(new WorkAsk(WorkScope.Ask, "pause", "a1"), WorkCommand.Read(WorkScope.Ask, ["--pause", "#a1"], out _));
        Assert.Equal(new WorkAsk(WorkScope.Quest, "resume", "q1"), WorkCommand.Read(WorkScope.Quest, ["resume", "q1"], out _));
        // The ask's other words are not a pause's, so `ask --publish` and a new ask's words reach the ask's console.
        Assert.False(WorkCommand.Asks(WorkScope.Ask, ["--publish", "a1", "--to", "engine"]));
        Assert.False(WorkCommand.Asks(WorkScope.Ask, ["pause", "the", "build"]));
        Assert.False(WorkCommand.Asks(WorkScope.Quest, ["delete", "q1"]));
    }

    /// <summary>
    /// The service's doors a pause reads: the quests, closed ones too; the session records, live or all; the registry; one
    /// ask by its id; and a record's move, which <see cref="Moving"/> hears before it lands.
    /// </summary>
    private sealed class Ledger(IEnumerable<JsonObject> records, IEnumerable<JsonObject> quests, IEnumerable<string> asks) : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<JsonObject> _records = [.. records];
        private readonly List<JsonObject> _quests = [.. quests];
        private readonly HashSet<string> _asks = [.. asks];

        /// <summary>Called with the session's id as its record is moved, before the move lands.</summary>
        public Action<string>? Moving { get; set; }

        /// <summary>A record's move does not reach the service: the connection fails, as a service that went away fails it.</summary>
        public bool Unreachable { get; set; }

        public ServiceClient Client() => new("http://ledger.test", null, new HttpClient(this, disposeHandler: false));

        private JsonObject Of(string id) => _records.Single(each => (string?)each["id"] == id);

        public string State(string id) { lock (_gate) return (string)Of(id)["state"]!; }

        public string? Note(string id) { lock (_gate) return (string?)Of(id)["note"]; }

        public bool Interrupted(string id) { lock (_gate) return (bool?)Of(id)["interrupted"] ?? false; }

        public void Move(string id, string state, string note)
        {
            lock (_gate)
            {
                Of(id)["state"] = state;
                Of(id)["note"] = note;
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var all = request.RequestUri.Query.Contains("includeClosed=true", StringComparison.Ordinal);
            var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(ct))?.AsObject();
            if (request.Method == HttpMethod.Post && path.EndsWith("/state", StringComparison.Ordinal))
            {
                if (Unreachable) throw new HttpRequestException("No connection could be made because the target machine actively refused it.");
                Moving?.Invoke(Uri.UnescapeDataString(path["/api/sessions/".Length..^"/state".Length]));
            }

            lock (_gate)
            {
                static bool Live(JsonObject record) => (string)record["state"]! is "queued" or "starting" or "working" or "awaiting-person";
                switch (request.Method.Method, path)
                {
                    case ("GET", "/api/sessions"):
                        return Answer(HttpStatusCode.OK, new JsonArray([.. _records.Where(each => all || Live(each)).Select(each => each.DeepClone())]));
                    case ("GET", "/api/quests"):
                        return Answer(HttpStatusCode.OK, new JsonArray([.. _quests
                            .Where(each => all || (string)each["status"]! is "Open" or "Taken").Select(each => each.DeepClone())]));
                    case ("GET", "/api/registry"):
                        return Answer(HttpStatusCode.OK, new JsonArray(
                            new JsonObject { ["repository"] = "engine", ["adopted"] = true, ["root"] = "X:/work/engine", ["workspace"] = "default" },
                            new JsonObject { ["repository"] = "game", ["adopted"] = true, ["root"] = "X:/work/game", ["workspace"] = "default" }));
                    case ("GET", _) when path.StartsWith("/api/asks/", StringComparison.Ordinal):
                        var ask = Uri.UnescapeDataString(path["/api/asks/".Length..]);
                        return _asks.Contains(ask)
                            ? Answer(HttpStatusCode.OK, new JsonObject
                            {
                                ["id"] = ask, ["workspace"] = "default", ["sentence"] = "Add the note field", ["state"] = "Published", ["tier"] = "named",
                            })
                            : Answer(HttpStatusCode.NotFound, new JsonObject { ["error"] = $"No ask `#{ask}`." });
                    case ("POST", _) when path.EndsWith("/state", StringComparison.Ordinal):
                        var moved = Of(Uri.UnescapeDataString(path["/api/sessions/".Length..^"/state".Length]));
                        moved["state"] = body!["state"]!.GetValue<string>();
                        if (body["note"] is { } note) moved["note"] = note.GetValue<string>();
                        moved["interrupted"] = body["interrupted"]?.GetValue<bool>() ?? false;
                        return Answer(HttpStatusCode.OK, new JsonObject { ["session"] = moved.DeepClone(), ["message"] = $"Session is now {moved["state"]}." });
                    default:
                        return Answer(HttpStatusCode.NotFound, new JsonObject { ["error"] = $"the stand-in has no {request.Method} {path}" });
                }
            }
        }

        private static HttpResponseMessage Answer(HttpStatusCode status, JsonNode body) => new(status)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
