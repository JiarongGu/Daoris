using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSUX1g (D126 §7.1): <c>daoris-driver sessions</c>, the terminal's door to Sessions. It lists this machine's sessions by
/// what they need, from the one reader the screen reads; stops, finishes and declines a session another process runs
/// through the request its loop honours, and one nothing here runs through the ledger; and archives, unarchives and deletes
/// through the owners the screen's routes call. Exit codes are the family's: 0 done, 1 refused, 2 could not.
/// </summary>
/// <remarks>
/// In-process: the service is a stand-in reached through the real client, a live process here is this test's own (its
/// marker is what any driver on the home reads), and the loop that honours a request is the watch's seam. The suite's fast
/// half.
/// </remarks>
public sealed class SessionsCommandTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-sessions-" + Guid.NewGuid().ToString("N")[..8]);

    public SessionsCommandTests() => Directory.CreateDirectory(Path.Combine(_home, "sessions"));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static readonly DateTimeOffset T0 = DateTimeOffset.UtcNow.AddHours(-2);

    private static JsonObject Record(string id, string state, string? quest = null, string kind = "driven", string repository = "engine",
        string? ask = null, bool deletable = false, int minutes = 0) => new()
        {
            ["id"] = id, ["repository"] = repository, ["state"] = state, ["kind"] = kind, ["quest"] = quest, ["ask"] = ask,
            ["deletable"] = deletable, ["adapter"] = "claude-code",
            ["created"] = T0.AddMinutes(minutes).ToString("O"), ["updated"] = T0.AddMinutes(minutes + 5).ToString("O"),
        };

    private static Ledger Family() => new(
        [
            Record("p4rk3d00", "awaiting-person", "q1", minutes: 10),
            Record("w0rk1ng0", "working", "q2", minutes: 20),
            Record("0rphan00", "working", "q3", minutes: 25),
            Record("c0ffee11", "completed", kind: "chat", deletable: true, minutes: 30),
            Record("he1p0000", "stopped", kind: "chat", repository: "daoris:help", minutes: 40),
            Record("1ntake00", "awaiting-person", kind: "chat", repository: "ask #a1", ask: "a1", minutes: 45),
            Record("d0ne0000", "completed", "q4", repository: "game", minutes: 50),
            Record("laptop/s9", "working", "q2", minutes: 55),
        ],
        [
            Quest("q1", "Expose a streaming budget", "Taken"),
            Quest("q2", "Cap the hydration per frame", "Taken"),
            Quest("q3", "Trim the chunk cache", "Taken"),
            Quest("q4", "Draw the minimap", "Done", to: "game"),
        ]);

    private static JsonObject Quest(string id, string title, string status, string to = "engine") => new()
    {
        ["id"] = id, ["from"] = "ask #a1", ["to"] = to, ["title"] = title, ["body"] = "A body.", ["status"] = status,
    };

    private SessionsWorld World(Ledger ledger, ServiceClient service, MachineLog? log = null) =>
        new(service, _home, DriverConfig.Empty, SessionWire.Pipe, log)
        {
            Wait = TimeSpan.FromSeconds(5),
            Poll = TimeSpan.FromMilliseconds(20),
        };

    private async Task<(int Exit, string Said)> RunAsync(Ledger ledger, string[] args, MachineLog? log = null, SessionsWorld? world = null)
    {
        using var service = ledger.Client();
        var output = new StringWriter();
        var ask = SessionsCommand.Read(args, out var problem);
        Assert.True(problem is null, problem);
        var exit = await SessionsCommand.RunAsync(ask!, world ?? World(ledger, service, log), output);
        return (exit, output.ToString().ReplaceLineEndings("\n"));
    }

    /// <summary>A live process for <paramref name="id"/>, as any driver sharing the home marks one: this test's own.</summary>
    private void Alive(string id)
    {
        using var self = Process.GetCurrentProcess();
        File.WriteAllText(Path.Combine(_home, "sessions", $"{id}.pid"), $"{self.Id} {self.StartTime.ToUniversalTime().Ticks}");
    }

    public static TheoryData<string[]> Problems => new()
    {
        new[] { "frobnicate" },
        new[] { "stop" },
        new[] { "stop", "s1", "s2" },
        new[] { "decline", "s1" },
        new[] { "decline", "s1", "--reason" },
        new[] { "finish", "s1", "--note" },
        new[] { "--group", "nope" },
        new[] { "--repository" },
        new[] { "archive" },
        new[] { "archive", "s1", "--yes" },
        new[] { "archive", "--ended", "s1" },
        new[] { "unarchive" },
        new[] { "delete" },
        new[] { "delete", "s1", "s2" },
    };

    [Theory]
    [MemberData(nameof(Problems))]
    public void Words_it_does_not_take_are_a_problem_said_with_the_usage(string[] args)
    {
        Assert.Null(SessionsCommand.Read(args, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    /// <summary>The listing is the one reader's, group by group in the order a person acts on them, each heading counted.</summary>
    [Fact]
    public async Task The_listing_prints_each_group_in_order_with_a_row_per_session()
    {
        new SessionEvents(Path.Combine(_home, "sessions")).Append(
            "c0ffee11", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Why is the frame budget 4 ms?" });

        var (exit, said) = await RunAsync(Family(), []);

        Assert.Equal(0, exit);
        var headings = said.Split('\n').Where(line => line.Length > 0 && !line.StartsWith(' ')).ToList();
        Assert.Equal(["Waiting on you (2)", "Working (3)", "Ended (3)"], headings);
        Assert.Contains("p4rk3d00  waiting on you  engine — Expose a streaming budget", said);
        Assert.Contains("c0ffee11  completed  engine — Why is the frame budget 4 ms?", said);
        Assert.Contains("he1p0000  stopped  daoris:help — Ask Daoris", said);
        Assert.Contains("1ntake00  waiting on you  ask #a1 — Intake for ask #a1", said);
        Assert.Contains("laptop/s9  working  engine — Cap the hydration per frame", said);
        Assert.Contains("on laptop", said);
    }

    [Fact]
    public async Task The_listing_takes_a_group_and_a_repository()
    {
        var (_, ended) = await RunAsync(Family(), ["--group", "ended"]);
        Assert.Equal(["Ended (3)"], ended.Split('\n').Where(line => line.Length > 0 && !line.StartsWith(' ')));

        var (_, game) = await RunAsync(Family(), ["--repository", "game"]);
        Assert.Contains("d0ne0000", game);
        Assert.DoesNotContain("p4rk3d00", game);

        var (exit, none) = await RunAsync(Family(), ["--group", "review"]);
        Assert.Equal(0, exit);
        Assert.Contains("nothing to review", none);
    }

    /// <summary>With <c>--json</c>, the screen's answer: the same rows, the same fields, so the two doors cannot disagree.</summary>
    [Fact]
    public async Task With_json_it_prints_the_screens_answer()
    {
        var (exit, said) = await RunAsync(Family(), ["--json", "--group", "you"]);

        Assert.Equal(0, exit);
        using var answer = JsonDocument.Parse(said);
        var rows = answer.RootElement.GetProperty("sessions").EnumerateArray().ToList();
        Assert.Equal(["p4rk3d00", "1ntake00"], rows.Select(row => row.GetProperty("session").GetString()));
        Assert.Equal(SessionsCommand.JsonFields, rows[0].EnumerateObject().Select(field => field.Name));
    }

    /// <summary>
    /// §7.1 rule 1–3: a session another process on this machine runs is stopped through a request; the loop that runs it
    /// takes it, and the verb says what happened once the record moved.
    /// </summary>
    [Fact]
    public async Task A_session_another_process_runs_is_stopped_through_the_request_its_loop_honours()
    {
        var ledger = Family();
        Alive("w0rk1ng0");
        await using var loop = new SessionRequestWatch(
            _home, runsHere: id => id == "w0rk1ng0", stop: id => { ledger.Move(id, "stopped", "the person stopped it."); return true; },
            service: () => null);
        using var honouring = new CancellationTokenSource();
        var honour = Task.Run(async () =>
        {
            while (!honouring.IsCancellationRequested)
            {
                await loop.HonourAsync();
                await Task.Delay(20);
            }
        });

        var (exit, said) = await RunAsync(ledger, ["stop", "w0rk1ng0"]);
        honouring.Cancel();
        await honour;

        Assert.Equal(0, exit);
        Assert.Contains("stopped w0rk1ng0", said);
        Assert.Contains("daoris driver retry q2 --session w0rk1ng0", said);
        Assert.Equal("stopped", ledger.State("w0rk1ng0"));
        Assert.Empty(new SessionRequests(_home).Pending(DateTimeOffset.UtcNow));
    }

    /// <summary>Nothing that runs it took the request in time: it is withdrawn, and the verb could not, which is exit 2.</summary>
    [Fact]
    public async Task A_request_nothing_takes_in_time_is_withdrawn_and_said()
    {
        var ledger = Family();
        Alive("w0rk1ng0");
        using var service = ledger.Client();
        var world = World(ledger, service) with { Wait = TimeSpan.FromMilliseconds(200) };

        var (exit, said) = await RunAsync(ledger, ["stop", "w0rk1ng0"], world: world);

        Assert.Equal(2, exit);
        Assert.Contains("withdrawn", said);
        Assert.Equal("working", ledger.State("w0rk1ng0"));
        Assert.False(File.Exists(Path.Combine(_home, "sessions", "requests", "w0rk1ng0.json")));
    }

    /// <summary>§7.1 rule 4: nothing on this machine runs it, so it is ended as the screen's stop ends an orphan: the person's stop.</summary>
    [Fact]
    public async Task A_session_nothing_runs_is_ended_as_the_screens_stop_ends_an_orphan()
    {
        var ledger = Family();

        var (exit, said) = await RunAsync(ledger, ["stop", "0rphan00"]);

        Assert.Equal(0, exit);
        Assert.Contains("stopped 0rphan00", said);
        Assert.Equal(("stopped", false), (ledger.State("0rphan00"), ledger.Interrupted("0rphan00")));
    }

    /// <summary>§7.1 rule 4: a parked session with no process left is moved by the ledger directly, as its card's stop moved it.</summary>
    [Fact]
    public async Task A_parked_session_with_no_process_is_moved_by_the_ledger()
    {
        var ledger = Family();

        var (exit, said) = await RunAsync(ledger, ["stop", "p4rk3d00"]);

        Assert.Equal(0, exit);
        Assert.Equal(("stopped", "The person stopped this at a checkpoint."), (ledger.State("p4rk3d00"), ledger.Note("p4rk3d00")));
        Assert.Contains("daoris driver retry q1 --session p4rk3d00", said);
    }

    [Theory]
    [InlineData("d0ne0000", "already ended")]
    [InlineData("nobody00", "no session here")]
    [InlineData("laptop/s9", "on laptop")]
    public async Task A_stop_with_nothing_to_stop_is_refused(string id, string why)
    {
        var (exit, said) = await RunAsync(Family(), ["stop", id]);

        Assert.Equal(1, exit);
        Assert.Contains(why, said);
    }

    [Fact]
    public async Task A_parked_session_is_finished_or_declined_with_the_persons_words()
    {
        var ledger = Family();

        var (finished, _) = await RunAsync(ledger, ["finish", "p4rk3d00", "--note", "Merged by hand."]);
        Assert.Equal(0, finished);
        Assert.Equal(("completed", "Merged by hand."), (ledger.State("p4rk3d00"), ledger.Note("p4rk3d00")));

        var declining = Family();
        var (declined, _) = await RunAsync(declining, ["decline", "p4rk3d00", "--reason", "Not this way."]);
        Assert.Equal(0, declined);
        Assert.Equal(("declined", "Not this way."), (declining.State("p4rk3d00"), declining.Note("p4rk3d00")));
    }

    /// <summary>Finish and decline answer a session that waits on you; an intake is answered through its ask.</summary>
    [Theory]
    [InlineData("w0rk1ng0", "not waiting on you")]
    [InlineData("1ntake00", "ask #a1")]
    public async Task A_finish_of_a_session_that_does_not_wait_on_you_is_refused(string id, string why)
    {
        var ledger = Family();

        var (exit, said) = await RunAsync(ledger, ["finish", id]);

        Assert.Equal(1, exit);
        Assert.Contains(why, said);
    }

    /// <summary>
    /// A finish asked of a parked session another process runs goes through the request too, marked parked, so the loop
    /// that runs it lets its process go and has the ledger finish it with the person's words, as its card's finish does.
    /// </summary>
    [Fact]
    public async Task A_parked_session_another_process_runs_is_finished_through_its_request()
    {
        var ledger = Family();
        Alive("p4rk3d00");
        using var loopService = ledger.Client();
        var released = new List<string>();
        await using var loop = new SessionRequestWatch(
            _home, runsHere: id => id == "p4rk3d00", stop: id => { released.Add(id); return true; }, service: () => loopService);
        using var honouring = new CancellationTokenSource();
        var honour = Task.Run(async () =>
        {
            while (!honouring.IsCancellationRequested)
            {
                await loop.HonourAsync();
                await Task.Delay(20);
            }
        });

        var (exit, said) = await RunAsync(ledger, ["finish", "p4rk3d00", "--note", "Merged by hand."]);
        honouring.Cancel();
        await honour;

        Assert.Equal(0, exit);
        Assert.Contains("finished p4rk3d00", said);
        Assert.Equal(["p4rk3d00"], released);
        Assert.Equal(("completed", "Merged by hand."), (ledger.State("p4rk3d00"), ledger.Note("p4rk3d00")));
    }

    /// <summary>Archive through the screen's owner: what ended is marked, what needs you or runs is kept and said; the log counts.</summary>
    [Fact]
    public async Task Archive_marks_what_ended_and_keeps_what_needs_you_and_the_log_counts_it()
    {
        var log = new MachineLog(_home, "driver");

        var (archived, said) = await RunAsync(Family(), ["archive", "d0ne0000"], log);
        Assert.Equal(0, archived);
        Assert.Contains("archived d0ne0000", said);
        Assert.True(new SessionArchive(_home).Marks().ContainsKey("d0ne0000"));

        var (kept, keptSaid) = await RunAsync(Family(), ["archive", "w0rk1ng0", "p4rk3d00"], log);
        Assert.Equal(1, kept);
        Assert.Contains("kept w0rk1ng0: it is still running", keptSaid);
        Assert.Contains("kept p4rk3d00: it is waiting on you", keptSaid);

        log.Dispose();
        var lines = Directory.GetFiles(Path.Combine(_home, MachineLog.Folder)).SelectMany(StubFile.Lines)
            .Where(line => line.Contains("\"sessions.archived\"", StringComparison.Ordinal)).ToList();
        var line = Assert.Single(lines);
        Assert.Contains("\"count\":1", line);
        Assert.Contains("\"door\":\"terminal\"", line);
    }

    /// <summary>§5.3 at a terminal: <c>--ended</c> lists what it would take and what stays; <c>--yes</c> archives what it listed.</summary>
    [Fact]
    public async Task Archive_what_ended_lists_first_and_archives_on_yes()
    {
        var (listed, list) = await RunAsync(Family(), ["archive", "--ended"]);
        Assert.Equal(0, listed);
        Assert.Contains("would archive 3 sessions that ended", list);
        Assert.Contains("kept in the list: 2 waiting on you", list);
        Assert.Empty(new SessionArchive(_home).Marks());

        var (yes, done) = await RunAsync(Family(), ["archive", "--ended", "--yes"]);
        Assert.Equal(0, yes);
        Assert.Contains("archived 3 sessions that ended", done);
        Assert.Equal(["c0ffee11", "d0ne0000", "he1p0000"], new SessionArchive(_home).Marks().Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Unarchive_brings_a_session_back_and_says_one_that_was_not_archived()
    {
        await RunAsync(Family(), ["archive", "d0ne0000"]);

        var (exit, said) = await RunAsync(Family(), ["unarchive", "d0ne0000", "c0ffee11"]);

        Assert.Equal(0, exit);
        Assert.Contains("unarchived d0ne0000", said);
        Assert.Contains("c0ffee11 was not archived", said);
        Assert.Empty(new SessionArchive(_home).Marks());
    }

    /// <summary>Delete through the screen's owner (SESSUX1f): a conversation that served no quest goes, logged as the terminal's.</summary>
    [Fact]
    public async Task Delete_goes_through_the_screens_owner_and_says_its_refusal()
    {
        var ledger = Family();
        var log = new MachineLog(_home, "driver");

        var (deleted, said) = await RunAsync(ledger, ["delete", "c0ffee11"], log);
        Assert.Equal(0, deleted);
        Assert.Contains("Deleted session `c0ffee11`", said);

        var (refused, why) = await RunAsync(ledger, ["delete", "d0ne0000"], log);
        Assert.Equal(1, refused);
        Assert.Contains("served-quest", ledger.LastRefusal);
        Assert.Contains("worked on", why);

        log.Dispose();
        Assert.Contains(Directory.GetFiles(Path.Combine(_home, MachineLog.Folder)).SelectMany(StubFile.Lines),
            line => line.Contains("\"session.deleted\"", StringComparison.Ordinal) && line.Contains("\"door\":\"terminal\"", StringComparison.Ordinal));
    }

    /// <summary>
    /// The service's doors the verb crosses, standing in: the records, the quests, the registry, the state door, and a
    /// session's delete and its judgement, by each record's <c>deletable</c>.
    /// </summary>
    private sealed class Ledger(IEnumerable<JsonObject> records, IEnumerable<JsonObject> quests) : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<JsonObject> _records = [.. records];
        private readonly List<JsonObject> _quests = [.. quests];

        public string LastRefusal { get; private set; } = "";

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
                        ["repository"] = "engine", ["adopted"] = true, ["registered"] = true, ["workspace"] = "default",
                    }));
                }

                var rest = path["/api/sessions/".Length..];
                if (request.Method == HttpMethod.Post && rest.EndsWith("/state", StringComparison.Ordinal))
                {
                    var moved = Of(Uri.UnescapeDataString(rest[..^"/state".Length]));
                    moved["state"] = body!["state"]!.GetValue<string>();
                    if (body["note"] is { } note) moved["note"] = note.GetValue<string>();
                    moved["interrupted"] = body["interrupted"]?.GetValue<bool>() ?? false;
                    return Answer(HttpStatusCode.OK, new JsonObject { ["session"] = moved.DeepClone(), ["message"] = $"Session is now {moved["state"]}." });
                }

                var id = Uri.UnescapeDataString(rest.Replace("/deletable", "", StringComparison.Ordinal));
                var record = _records.FirstOrDefault(each => (string?)each["id"] == id);
                if (record is null) return Answer(HttpStatusCode.NotFound, new JsonObject { ["error"] = $"No session `{id}`.", ["refusal"] = "not-found" });
                if ((bool?)record["deletable"] != true)
                {
                    LastRefusal = "served-quest";
                    return Answer(request.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.Conflict, new JsonObject
                    {
                        ["deletable"] = false, ["error"] = $"Session `{id}` worked on `#{record["quest"]}`; archive it instead.",
                        ["refusal"] = "served-quest", ["quest"] = (string?)record["quest"],
                    });
                }

                if (request.Method == HttpMethod.Get) return Answer(HttpStatusCode.OK, new JsonObject { ["deletable"] = true });
                _records.Remove(record);
                return Answer(HttpStatusCode.OK, new JsonObject { ["id"] = id, ["message"] = $"Deleted session `{id}`." });
            }
        }

        private static HttpResponseMessage Answer(HttpStatusCode status, JsonNode body) => new(status)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
