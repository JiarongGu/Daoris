using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The shell forwards a tick to the page when the asks CHANGED (INT4d): the attention band reads them
/// beside the sessions, and an ask made by the other door — a terminal, a teammate's sync — moves
/// nothing else a tick reports, so without this the band missed it until the window was reloaded.
/// </summary>
public sealed class AskSignatureTests
{
    private static AskView Ask(string id, string state = "Proposed", string? intake = null, int quests = 0) =>
        new(id, "default", "a sentence", state, "declarations")
        {
            Intake = intake,
            Quests = [.. Enumerable.Range(0, quests).Select(n => $"q{n}")],
        };

    [Fact]
    public void The_same_asks_sign_the_same_whatever_their_order()
    {
        var a = new[] { Ask("a1"), Ask("b2", "Published", quests: 1) };

        Assert.Equal(Asks.Signature(a), Asks.Signature([a[1], a[0]]));
    }

    [Fact]
    public void A_new_ask_a_moved_state_an_intake_or_a_quest_each_sign_differently()
    {
        var before = new[] { Ask("a1") };

        Assert.NotEqual(Asks.Signature(before), Asks.Signature([Ask("a1"), Ask("b2")]));
        Assert.NotEqual(Asks.Signature(before), Asks.Signature([Ask("a1", "Closed")]));
        Assert.NotEqual(Asks.Signature(before), Asks.Signature([Ask("a1", intake: "s1a2b3c4")]));
        Assert.NotEqual(Asks.Signature(before), Asks.Signature([Ask("a1", quests: 1)]));
        Assert.Equal(string.Empty, Asks.Signature([]));
    }
}

/// <summary>
/// The intake session (D65 §1b, INT4b): an ask the declarations did not settle is answered by a
/// SESSION the driver opens in a room it owns — which reads the circle's declarations, publishes the
/// quests onto the ask itself, and asks the person where the declarations do not settle it.
/// </summary>
/// <remarks>
/// The loop tests run a REAL process — node, the stand-in harness every gate already needs — against a
/// stand-in service on a loopback port: the spawn, the environment, the room and the publish are
/// exactly what a fake would get wrong. No model anywhere, and no account.
/// </remarks>
public sealed class IntakeTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-intake-driver-" + Guid.NewGuid().ToString("N")[..8]);

    public IntakeTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly IReadOnlyList<DeclarationView> Circle =
    [
        new("media-api", Adopted: true, Registered: true, "The media service and its config.",
            ["media config", "video and image field names"], ["a media bug"], Root: "/work/media-api"),
        new("storefront", Adopted: true, Registered: true, "The storefront.", ["product pages"], ["a UI bug"], Root: null),
        new("importer", Adopted: true, Registered: false, null, [], [], Root: null),
        new("legacy", Adopted: false, Registered: false, null, [], [], Root: null),
    ];

    private static AskView Ask(string sentence = "use the media config instead of hard coding the video field name") =>
        new("a1b2c3", "work", sentence, "Proposed", "declarations")
        {
            Links = ["https://tickets.example/T-9"],
            Attachments = [new QuestFileView("shot.png", "abc", 12, "C:/somewhere/data/asks/a1b2c3/shot.png")],
            Proposed = ["media-api"],
        };

    // ——— The room.

    [Fact]
    public void The_room_is_the_circles_under_the_home_and_says_who_owns_what()
    {
        var room = IntakeRoom.Prepare(_home, "work", Circle);

        Assert.Equal(Path.Combine(_home, IntakeRoom.Folder, "work"), room);
        var agents = File.ReadAllText(Path.Combine(room, "AGENTS.md"));
        Assert.Contains("`media-api`", agents);
        Assert.Contains("video and image field names", agents);
        Assert.Contains("a media bug", agents);
        Assert.Contains("/work/media-api", agents);
        // Said, never hidden: who cannot be asked is part of "whose problem is this".
        Assert.Contains("`importer`", agents);
        Assert.Contains("declared nothing", agents);
        Assert.Contains("`legacy`", agents);
        Assert.Contains("not adopted", agents);
        // One harness reads AGENTS.md, another CLAUDE.md (D59) — the room carries both, the canon's shape.
        Assert.Equal("@AGENTS.md\n", File.ReadAllText(Path.Combine(room, "CLAUDE.md")));
        Assert.DoesNotContain("\r", agents);
    }

    /// <summary>
    /// The room's allow-list is the intake's whole job and nothing else: read the family, publish. A
    /// permission request over ACP is refused by construction (D52), so an unlisted tool is a stall.
    /// </summary>
    [Fact]
    public void The_room_allows_reading_the_family_and_publishing_and_nothing_outward_beyond_reading()
    {
        var room = IntakeRoom.Prepare(_home, "work", Circle);

        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(room, ".claude", "settings.json")));
        var allowed = settings.RootElement.GetProperty("permissions").GetProperty("allow")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("mcp__daoris-knowledge__quest_publish", allowed);
        Assert.Contains("mcp__daoris-knowledge__registry", allowed);
        Assert.DoesNotContain(allowed, rule => rule!.StartsWith("Bash", StringComparison.Ordinal));
        Assert.DoesNotContain(allowed, rule => rule!.Contains("quest_respond", StringComparison.Ordinal));
    }

    /// <summary>The room is re-rendered at every open: declarations change, and a stale room decides wrong.</summary>
    [Fact]
    public void The_room_follows_the_declarations_as_they_change()
    {
        IntakeRoom.Prepare(_home, "work", Circle);
        var room = IntakeRoom.Prepare(_home, "work", [Circle[1]]);

        var agents = File.ReadAllText(Path.Combine(room, "AGENTS.md"));
        Assert.DoesNotContain("`media-api`", agents);
        Assert.Contains("`storefront`", agents);
    }

    /// <summary>A circle's name is wiring a person typed; the room's folder must stay one folder under the home.</summary>
    [Fact]
    public void A_circle_name_that_is_not_a_folder_name_stays_one_folder_under_the_home()
    {
        var room = IntakeRoom.PathOf(_home, "../team/a:b");

        Assert.Equal(Path.Combine(_home, IntakeRoom.Folder), Path.GetDirectoryName(room));
        Assert.DoesNotContain("..", Path.GetFileName(room));
    }

    // ——— The instruction.

    [Fact]
    public void The_intakes_instruction_is_its_job_the_asks_words_and_what_it_carries()
    {
        var prompt = IntakePrompt.Compose(Ask());

        Assert.Contains("use the media config instead of hard coding the video field name", prompt);
        Assert.Contains("ask `#a1b2c3`", prompt);
        Assert.Contains("https://tickets.example/T-9", prompt);
        Assert.Contains("C:/somewhere/data/asks/a1b2c3/shot.png", prompt);
        Assert.Contains("quest_publish", prompt);
        Assert.Contains("AGENTS.md", prompt);
        // Where the declarations do not settle it, the person decides — never a guess.
        Assert.Contains("do not guess", prompt, StringComparison.OrdinalIgnoreCase);
        // It publishes; it never edits (D32).
        Assert.Contains("never edit", prompt, StringComparison.OrdinalIgnoreCase);
        // The no-model tier's proposal is offered as a proposal, not an answer.
        Assert.Contains("`media-api`", prompt);
    }

    // ——— The spawn.

    [Fact]
    public void An_intakes_spawn_names_its_ask_and_session_and_no_quest()
    {
        var target = IntakeTarget();

        var info = new StubAdapter().Prepare(target, ["node", "agent.mjs"]);

        Assert.Equal("a1b2c3", info.Environment[IntakeRoom.AskVariable]);
        Assert.Equal("s1", info.Environment[IntakeRoom.SessionVariable]);
        Assert.Equal(target.Prompt, info.Environment["DAORIS_TARGET"]);
        Assert.Equal(target.Root, info.WorkingDirectory);
        // Absent rather than blank: an intake serves no quest (the chat rule, D49 §3).
        Assert.False(info.Environment.ContainsKey("DAORIS_QUEST_ID"));
        Assert.False(info.Environment.ContainsKey("DAORIS_QUEST_ATTACHMENTS"));
    }

    [Fact]
    public void The_pipe_harness_is_handed_the_intakes_instruction_not_a_quests()
    {
        var target = IntakeTarget();

        var info = new ClaudeCodeAdapter().Prepare(target, ["claude"]);

        var prompt = info.ArgumentList[info.ArgumentList.IndexOf("-p") + 1];
        Assert.Equal(target.Prompt, prompt);
    }

    /// <summary>
    /// 🔴 The connector is how an intake publishes (§1b's first trap): a chat is handed none, so the
    /// intake's is handed with the ask and the session in its environment — the MCP host reads them.
    /// </summary>
    [Fact]
    public void The_connector_offered_to_an_intake_carries_its_ask_and_session()
    {
        var host = Path.Combine(_home, KnowledgeConnector.ExecutableName);
        File.WriteAllText(host, "");

        var offered = KnowledgeConnector.Offer(
            host, null, _home, new Dictionary<string, string?>(),
            IntakeRoom.Scope("a1b2c3", "s1"))!;

        Assert.Equal("a1b2c3", offered.Environment[IntakeRoom.AskVariable]);
        Assert.Equal("s1", offered.Environment[IntakeRoom.SessionVariable]);
    }

    private SessionTarget IntakeTarget() =>
        new("", "an ask", "the words", "ask #a1b2c3", "ask #a1b2c3", Path.Combine(_home, "intake", "work"), "http://localhost:0")
        {
            Ask = "a1b2c3",
            Session = "s1",
            Prompt = IntakePrompt.Compose(Ask()),
        };

    // ——— The conclusion, observed.

    [Fact]
    public void An_intake_that_published_onto_its_ask_completed()
    {
        var after = Ask() with { State = "Published", Tier = "intake", Quests = ["q1", "q2"] };

        var concluded = IntakeObservation.Conclude(0, before: 0, after);

        Assert.Equal("completed", concluded.State);
        Assert.Contains("`#q1`", concluded.Note);
        Assert.Contains("`#q2`", concluded.Note);
    }

    /// <summary>A clean exit that published nothing is the intake ASKING — parked for the person, never failed.</summary>
    [Fact]
    public void An_intake_that_published_nothing_and_exited_cleanly_is_asking_the_person()
    {
        var concluded = IntakeObservation.Conclude(0, before: 0, Ask());

        Assert.Equal("awaiting-person", concluded.State);
        Assert.Contains("--publish a1b2c3", concluded.Note);
        Assert.Contains("--close a1b2c3", concluded.Note);
    }

    [Fact]
    public void An_intake_that_died_before_publishing_failed()
    {
        Assert.Equal("failed", IntakeObservation.Conclude(1, before: 0, Ask()).State);
        Assert.Equal("failed", IntakeObservation.Conclude(0, before: 0, after: null).State);
    }

    /// <summary>Somebody else answered the ask while it ran — the person — so it stands down, like a lost take.</summary>
    [Fact]
    public void An_intake_whose_ask_was_answered_by_someone_else_stood_down()
    {
        var byPerson = Ask() with { State = "Published", Tier = "declarations", Quests = ["q1"] };
        var closed = Ask() with { State = "Closed", Note = "Not needed." };

        Assert.Equal("stood-down", IntakeObservation.Conclude(0, before: 0, byPerson).State);
        Assert.Equal("stood-down", IntakeObservation.Conclude(0, before: 0, closed).State);
    }

    [Fact]
    public void A_parked_intake_ends_when_the_person_answers_its_ask()
    {
        Assert.Null(IntakeObservation.Answered(Ask()));
        Assert.Equal("completed", IntakeObservation.Answered(Ask() with { State = "Published", Quests = ["q1"] })!.State);
        Assert.Equal("stopped", IntakeObservation.Answered(Ask() with { State = "Closed", Note = "No." })!.State);
    }

    // ——— The loop, over a real process.

    /// <summary>
    /// The whole intake, end to end with no model: the driver opens it for the ask in the circle's
    /// room, the stub agent reads the room's declarations and publishes onto the ask through the door
    /// its session can reach, and the record concludes from what the ask became.
    /// </summary>
    [Fact]
    public async Task An_ask_with_an_intake_harness_is_answered_by_a_session_that_publishes_onto_it()
    {
        await using var service = StandInService.Start(Circle, Ask());
        var driver = Driver(IntakeOn(), service);

        var report = await driver.TickAsync();

        var opened = Assert.Single(service.Intakes);
        Assert.Equal("a1b2c3", opened["ask"]!.GetValue<string>());
        Assert.Equal(Path.Combine(_home, IntakeRoom.Folder, "work"), opened["room"]!.GetValue<string>());
        Assert.Equal("stub", opened["adapter"]!.GetValue<string>());

        var published = Assert.Single(service.Published);
        Assert.Equal("media-api", published["to"]!.GetValue<string>());
        Assert.Equal("i1", published["session"]!.GetValue<string>());
        Assert.Equal("storefront", published["then"]![0]!["to"]!.GetValue<string>());

        Assert.Equal("completed", service.Session("i1")["state"]!.GetValue<string>());
        Assert.True(report.Progressed);
        Assert.Contains(report.Events, line => line.StartsWith("completed  intake i1 (ask #a1b2c3", StringComparison.Ordinal));
        var ended = Assert.Single(report.Concluded);
        Assert.Equal("ask #a1b2c3", ended.Repository);
        Assert.Null(ended.Quest);

        var transcript = File.ReadAllText(Path.Combine(_home, "sessions", "i1.log"));
        Assert.Contains("stub: intake for ask a1b2c3 as session i1", transcript);
        Assert.Contains("stub: quest (none)", transcript);
        Assert.Contains("stub: the room declares media-api", transcript);
    }

    /// <summary>
    /// Where the declarations do not settle it, the intake ASKS: it publishes nothing and ends, its
    /// record parks for the person — and ends when the person answers the ask, which is how a
    /// question from a session with no process left is ever closed.
    /// </summary>
    [Fact]
    public async Task An_intake_the_declarations_do_not_settle_parks_for_the_person_until_the_ask_is_answered()
    {
        await using var service = StandInService.Start(Circle, Ask("an unsettled widget is broken somewhere"));
        var driver = Driver(IntakeOn(), service);

        var first = await driver.TickAsync();

        Assert.Empty(service.Published);
        Assert.Equal("awaiting-person", service.Session("i1")["state"]!.GetValue<string>());
        Assert.Contains(first.Events, line => line.StartsWith("awaiting-person  intake i1", StringComparison.Ordinal));
        Assert.Contains("which repository owns the widget", File.ReadAllText(Path.Combine(_home, "sessions", "i1.log")));

        // The person answers — publishing it themselves — and the next tick ends the parked intake.
        service.Answer("a1b2c3", "q9");
        var second = await driver.TickAsync();

        Assert.Equal("completed", service.Session("i1")["state"]!.GetValue<string>());
        Assert.Contains(second.Concluded, ended => ended.Session == "i1" && ended.State == "completed");
        // And no second intake for an ask one already served.
        Assert.Single(service.Intakes);
    }

    /// <summary>
    /// The room is a circle's, so a tick takes one ask per circle — the OLDEST, which the service's
    /// newest-first list puts last — and the next ask in that circle waits for the next tick.
    /// </summary>
    [Fact]
    public async Task A_tick_answers_the_oldest_ask_in_each_circle_and_the_next_waits_its_turn()
    {
        var newer = Ask("the newer ask in the work circle") with { Id = "bbbbbb" };
        var older = Ask("the older ask in the work circle") with { Id = "aaaaaa" };
        var elsewhere = Ask("an ask in another circle") with { Id = "cccccc", Workspace = "other" };
        await using var service = StandInService.Start(Circle, newer, older, elsewhere);
        var driver = Driver(IntakeOn(), service);

        await driver.TickAsync();

        Assert.Equal(["aaaaaa", "cccccc"], service.Intakes.Select(i => i["ask"]!.GetValue<string>()).Order());
        Assert.True(Directory.Exists(Path.Combine(_home, IntakeRoom.Folder, "other")));

        await driver.TickAsync();

        Assert.Equal("bbbbbb", service.Intakes[^1]["ask"]!.GetValue<string>());
        Assert.Equal(3, service.Intakes.Count);
    }

    /// <summary>An intake is a session like any: it spends a slot of the machine's cap, and never more slots than there are.</summary>
    [Fact]
    public async Task Intakes_take_only_the_slots_the_cap_leaves()
    {
        await using var service = StandInService.Start(
            Circle, Ask("one ask") with { Id = "aaaaaa" }, Ask("another circle's") with { Id = "cccccc", Workspace = "other" });

        await Driver(IntakeOn() with { Cap = 1 }, service).TickAsync();

        Assert.Single(service.Intakes);
    }

    /// <summary>
    /// 🔴 An intake is one turn (§1b), so it takes no messages on either door: the pipe door gives it
    /// no stdin, and the protocol door's stdin carries the driver's own frames (INT4h). While it runs,
    /// the process registry refuses a person's line for it, in a sentence that says where the answer
    /// goes — and the stop still reaches it.
    /// </summary>
    [Fact]
    public async Task A_running_intake_takes_no_messages_and_says_where_the_answer_goes()
    {
        await using var service = StandInService.Start(Circle, Ask("a lingering ask the stub takes its time over"));
        var processes = new SessionProcesses();
        var driver = Driver(IntakeOn(), service, processes);

        var tick = driver.TickAsync();
        await Until(() => processes.Running.Contains("i1"));

        var why = processes.RefusesInput("i1");
        Assert.NotNull(why);
        Assert.Contains("ask #a1b2c3", why);
        Assert.Contains("parking", why);
        Assert.False(processes.Send("i1", "it is the storefront"));

        Assert.True(processes.Stop("i1"));
        await tick;
        Assert.Equal("stopped", service.Session("i1")["state"]!.GetValue<string>());
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var waited = 0; !condition(); waited += 50)
        {
            if (waited > 15_000) throw new TimeoutException("the intake never started");
            await Task.Delay(50);
        }
    }

    /// <summary>With no harness named for it, an ask is the declarations tier's alone — INT4a, unchanged.</summary>
    [Fact]
    public async Task With_no_intake_harness_named_no_intake_runs()
    {
        await using var service = StandInService.Start(Circle, Ask());

        var report = await Driver(Config(), service).TickAsync();

        Assert.Empty(service.Intakes);
        Assert.False(report.Progressed);
    }

    private DriverConfig Config() => DriverConfig.Empty with
    {
        Adapter = "stub",
        TimeoutMinutes = 1,
        Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent()] },
    };

    private DriverConfig IntakeOn() => Config().WithIntake("stub");

    private Daoris.Driver.Driver Driver(DriverConfig config, StandInService service, SessionProcesses? processes = null)
    {
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: processes,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>
    /// The stub intake: a harness with real mechanics and no model. It reads the room it was spawned
    /// in, and publishes onto its ask through the service door its environment names — or, when the
    /// words do not settle it, says what it would ask and publishes nothing.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "intake-agent.mjs");
        File.WriteAllText(script, """
            import { existsSync, readFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }

            const ask = process.env.DAORIS_ASK_ID;
            const session = process.env.DAORIS_SESSION_ID;
            const target = process.env.DAORIS_TARGET ?? '';
            console.log('stub: intake for ask ' + ask + ' as session ' + session);
            console.log('stub: quest ' + (process.env.DAORIS_QUEST_ID ?? '(none)'));

            // An intake still at work: it holds its turn until it is stopped.
            if (/lingering/.test(target)) await new Promise((resolve) => setTimeout(resolve, 60000));

            const agents = existsSync('AGENTS.md') ? readFileSync('AGENTS.md', 'utf8') : '';
            if (agents.includes('`media-api`')) console.log('stub: the room declares media-api');

            if (/unsettled/.test(target)) {
              console.log('stub: the declarations do not settle this — which repository owns the widget?');
              process.exit(0);
            }

            const answer = await fetch(process.env.DAORIS_SERVICE_URL + '/api/asks/' + ask + '/publish', {
              method: 'POST',
              headers: { 'content-type': 'application/json' },
              body: JSON.stringify({
                to: 'media-api',
                title: 'Read the video field name from the media config',
                body: 'The ticket names the field.',
                then: [{ to: 'storefront', title: 'Verify {parent}', body: 'Look.' }],
                session,
              }),
            });
            console.log('stub: publish answered ' + answer.status);
            if (!answer.ok) process.exit(1);
            """);
        return script;
    }

    /// <summary>
    /// A stand-in for the service's doors the intake crosses — a real loopback listener, because the
    /// stub agent is a real process that can only reach a real address. It keeps the records the way
    /// the ledger and the desk would, and remembers what it was asked.
    /// </summary>
    private sealed class StandInService : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly List<JsonObject> _asks;
        private readonly List<JsonObject> _sessions = [];
        private readonly IReadOnlyList<DeclarationView> _registry;

        public string Url { get; }

        public List<JsonObject> Intakes { get; } = [];

        public List<JsonObject> Published { get; } = [];

        /// <param name="asks">Newest first — the order the service answers its list in.</param>
        private StandInService(HttpListener listener, string url, IReadOnlyList<DeclarationView> registry, AskView[] asks)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _registry = registry;
            _asks =
            [
                .. asks.Select(ask => new JsonObject
                {
                    ["id"] = ask.Id, ["workspace"] = ask.Workspace, ["sentence"] = ask.Sentence,
                    ["state"] = ask.State, ["tier"] = ask.Tier, ["links"] = new JsonArray([.. ask.Links.Select(l => (JsonNode)l)]),
                    ["attachments"] = new JsonArray(), ["proposal"] = new JsonArray(), ["quests"] = new JsonArray(),
                    ["intake"] = null,
                }),
            ];
            _serving = ServeAsync();
        }

        public static StandInService Start(IReadOnlyList<DeclarationView> registry, params AskView[] asks)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new StandInService(listener, url, registry, asks);
        }

        public JsonObject Session(string id)
        {
            lock (_sessions) return _sessions.Single(s => s["id"]!.GetValue<string>() == id);
        }

        /// <summary>The person publishes the ask themselves.</summary>
        public void Answer(string ask, string quest)
        {
            lock (_asks)
            {
                var held = _asks.Single(a => a["id"]!.GetValue<string>() == ask);
                held["state"] = "Published";
                held["quests"]!.AsArray().Add(quest);
            }
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }

                var (status, body) = Answer(context.Request);
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        private (int, string) Answer(HttpListenerRequest request)
        {
            var path = request.Url!.AbsolutePath;
            var query = request.QueryString;
            JsonObject Body()
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                return JsonNode.Parse(reader.ReadToEnd())!.AsObject();
            }

            lock (_asks)
            lock (_sessions)
            {
                switch (request.HttpMethod, path)
                {
                    case ("GET", "/api/quests"):
                        return (200, "[]");

                    case ("GET", "/api/registry"):
                        return (200, new JsonArray([.. _registry
                            .Where(_ => query["workspace"] is null or "work")
                            .Select(r => (JsonNode)new JsonObject
                            {
                                ["repository"] = r.Repository, ["adopted"] = r.Adopted, ["registered"] = r.Registered,
                                ["summary"] = r.Summary, ["owns"] = new JsonArray([.. r.Owns.Select(o => (JsonNode)o)]),
                                ["accepts"] = new JsonArray([.. r.Accepts.Select(a => (JsonNode)a)]),
                                ["root"] = r.Root, ["workspace"] = "work",
                            })]).ToJsonString());

                    case ("GET", "/api/sessions"):
                        var all = query["includeClosed"] == "true";
                        return (200, new JsonArray([.. _sessions
                            .Where(s => all || s["state"]!.GetValue<string>() is "queued" or "starting" or "working" or "awaiting-person")
                            .Select(s => s.DeepClone())]).ToJsonString());

                    case ("GET", "/api/asks"):
                        return (200, new JsonArray([.. _asks
                            .Where(a => a["state"]!.GetValue<string>() != "Closed").Select(a => a.DeepClone())]).ToJsonString());

                    case ("GET", _) when path.StartsWith("/api/asks/", StringComparison.Ordinal):
                        var sought = path["/api/asks/".Length..];
                        return _asks.FirstOrDefault(a => a["id"]!.GetValue<string>() == sought) is { } found
                            ? (200, found.ToJsonString())
                            : (404, """{"error":"No such ask."}""");

                    case ("POST", "/api/sessions/intake"):
                    {
                        var body = Body();
                        Intakes.Add(body);
                        var ask = _asks.Single(a => a["id"]!.GetValue<string>() == body["ask"]!.GetValue<string>());
                        if (ask["intake"] is not null) return (409, """{"error":"already served"}""");
                        var session = new JsonObject
                        {
                            ["id"] = $"i{Intakes.Count}", ["repository"] = $"ask #{ask["id"]}", ["state"] = "queued",
                            ["kind"] = "chat", ["ask"] = ask["id"]!.GetValue<string>(),
                            ["workspace"] = ask["workspace"]!.GetValue<string>(),
                        };
                        _sessions.Add(session);
                        ask["intake"] = session["id"]!.GetValue<string>();
                        return (200, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "opened" }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal):
                    {
                        var id = path["/api/sessions/".Length..^"/state".Length];
                        var body = Body();
                        var session = _sessions.Single(s => s["id"]!.GetValue<string>() == id);
                        session["state"] = body["state"]!.GetValue<string>();
                        if (body["note"] is { } note) session["note"] = note.GetValue<string>();
                        return (200, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "moved" }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/asks/", StringComparison.Ordinal) && path.EndsWith("/publish", StringComparison.Ordinal):
                    {
                        var id = path["/api/asks/".Length..^"/publish".Length];
                        var body = Body();
                        Published.Add(body);
                        var ask = _asks.Single(a => a["id"]!.GetValue<string>() == id);
                        var quest = $"q{Published.Count}";
                        ask["quests"]!.AsArray().Add(quest);
                        ask["state"] = "Published";
                        if (body["session"]?.GetValue<string>() is { } by && by == ask["intake"]?.GetValue<string>()) ask["tier"] = "intake";
                        return (200, new JsonObject
                        {
                            ["ask"] = ask.DeepClone(), ["message"] = $"Published quest `#{quest}`.",
                            ["quest"] = new JsonObject { ["id"] = quest },
                        }.ToJsonString());
                    }

                    default:
                        return (404, $$"""{"error":"the stand-in has no {{request.HttpMethod}} {{path}}"}""");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            try { await _serving; } catch (ObjectDisposedException) { }
        }
    }
}
