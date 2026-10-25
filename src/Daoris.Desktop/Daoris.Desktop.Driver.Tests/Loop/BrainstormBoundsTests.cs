using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRAINSTORM1b (D176 points 6, 7 and 9): a brainstorm never spends past its bounds or hangs. The generators run side by side
/// within one deadline that keeps the synthesis's and the challenge's minutes reserved; a pass past its minutes, or the request past
/// its deadline, is stopped and what answered is kept; a crash fails its pass alone; a limit starts the pass once more on the next
/// account; a full cap waits within the deadline; the person's stop ends every pass; a restart ends what ran as interrupted and runs
/// nothing again; and the policy's agents bound the request's. Every turn is a scripted stand-in on a minute shortened to a tenth of
/// a second, so nothing starts a process (MOD8); the deadline's stop on a real agent is <c>BrainstormProcessTests</c>'.
/// </summary>
public sealed class BrainstormBoundsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-brainstorm-bounds-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-19T10:00:00Z");

    /// <summary>A brainstorm's minute here: a tenth of a second.</summary>
    private static readonly TimeSpan Minute = TimeSpan.FromMilliseconds(100);

    public BrainstormBoundsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private BrainstormFiles Files => new(_home);

    private sealed class Refuses : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"none here\"}") });
    }

    /// <summary>The turns, scripted: each answers from <see cref="Answers"/> unless a behaviour is set for it.</summary>
    private sealed class Script
    {
        private int _now;

        public List<string> Started { get; } = [];

        public Dictionary<string, BrainstormTurn> Seen { get; } = [];

        public List<string> Cancelled { get; } = [];

        public int Most { get; private set; }

        public Dictionary<string, string> Answers { get; } = [];

        public Dictionary<string, Func<BrainstormTurn, CancellationToken, Task<BrainstormTurnRun>>> Behaviours { get; } = [];

        public async Task<BrainstormTurnRun> RunAsync(BrainstormTurn turn, CancellationToken ct)
        {
            lock (Started)
            {
                Started.Add(turn.Turn);
                Seen[turn.Turn] = turn;
                Most = Math.Max(Most, ++_now);
            }

            try
            {
                return Behaviours.TryGetValue(turn.Turn, out var behave) ? await behave(turn, ct) : Answer(turn);
            }
            catch (OperationCanceledException)
            {
                lock (Started) Cancelled.Add(turn.Turn);
                throw;
            }
            finally
            {
                lock (Started) _now--;
            }
        }

        public BrainstormTurnRun Answer(BrainstormTurn turn, string account = "work")
        {
            if (Answers.TryGetValue(turn.Turn, out var answer))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(turn.Answer)!);
                File.WriteAllText(turn.Answer, answer);
            }

            return new BrainstormTurnRun(true, account, null, $"completed  brainstorm turn {turn.Turn}");
        }
    }

    /// <summary>A turn that never answers: it waits until it is stopped, as an agent past its minutes would.</summary>
    private static async Task<BrainstormTurnRun> Hang(BrainstormTurn turn, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), ct);
        return new BrainstormTurnRun(true, "work", null, $"completed  brainstorm turn {turn.Turn}");
    }

    private static string Give(params string[] ideas) =>
        new JsonObject
        {
            ["brainstorm"] = 1,
            ["proposals"] = new JsonArray([.. ideas.Select(idea => (JsonNode)new JsonObject
            {
                ["idea"] = idea, ["rationale"] = "Because the report reads the window.", ["effort"] = "small", ["risk"] = "low",
                ["reversible"] = "yes", ["undo"] = null, ["confidence"] = "medium", ["assumptions"] = new JsonArray(), ["sources"] = new JsonArray(),
            })]),
            ["needs"] = new JsonArray(),
            ["none"] = null,
        }.ToJsonString();

    private static string Synthesize(params string[] members) =>
        new JsonObject
        {
            ["brainstorm"] = 1,
            ["groups"] = new JsonArray([.. members.Select((member, at) => (JsonNode)new JsonObject
            {
                ["title"] = $"Idea {member}", ["idea"] = $"Idea {member}, merged.", ["members"] = new JsonArray(member),
                ["rank"] = at + 1, ["why"] = "It answers the question as asked.", ["flags"] = new JsonArray(),
            })]),
            ["leftOut"] = new JsonArray(),
        }.ToJsonString();

    private static BrainstormRule Rule(int pass = 10, int synthesis = 10, int challenge = 10, int request = 30, params string[] agents) =>
        new(BrainstormRules.Ask, agents, new BrainstormMinutes(pass, synthesis, challenge, request));

    private (Daoris.Driver.Driver Driver, Script Script) Start(int cap = 3, BrainstormRule? rule = null)
    {
        var script = new Script();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient("http://127.0.0.1:9", null, new HttpClient(new Refuses())),
            DriverConfig.Empty with { Adapter = "stub", Cap = cap, Brainstorms = rule }, AdapterSet.Built(), _home)
        {
            BrainstormTurns = script.RunAsync,
            BrainstormMinute = Minute,
        };
        return (driver, script);
    }

    private static async Task LookAsync(Daoris.Driver.Driver driver, params SessionView[] active)
    {
        await driver.BrainstormsAsync(new Snapshot([], [], active), 0, [], CancellationToken.None);
        await driver.Running.SettledAsync().WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>A request frozen at a look and sent by the person, ready to run at the next.</summary>
    private async Task<string> SentAsync(Daoris.Driver.Driver driver, params string[] agents)
    {
        var id = Files.WriteRequest("How should the daily report stop dropping its last day?", null, agents, At);
        await LookAsync(driver);
        Files.WriteSent(id, Files.ReadState(id)!.Packet!, At);
        return id;
    }

    private JsonNode Pass(string id, string pass) => JsonNode.Parse(File.ReadAllText(Files.PathOf(id, "passes", $"{pass}.json")))!;

    /// <summary>A pass's record: its state, its codes joined by commas, and its turns.</summary>
    private static (string State, string Codes, int Turns) Of(JsonNode pass) =>
        (pass["state"]!.GetValue<string>(), string.Join(",", pass["codes"]!.AsArray().Select(code => code!.GetValue<string>())), pass["turns"]!.GetValue<int>());

    private JsonNode Document(string id) => JsonNode.Parse(File.ReadAllText(Files.PathOf(id, "document.json")))!;

    /// <summary>🔴 The generators run side by side: each waits for the other, so in sequence the first would wait out its minutes.</summary>
    /// <remarks>
    /// The generators' window is wide here, as the cap case's is, since the claim is that they run side by side, not the minutes': with the
    /// default rule's 10 minutes, a second at this test's minute, a loaded run's second generator started after the first's pass was spent,
    /// and the first read <c>failed</c> (FLAKE1, three times on 2026-10-25).
    /// </remarks>
    [Fact]
    public async Task The_generators_run_side_by_side_within_one_deadline()
    {
        var (driver, script) = Start(rule: Rule(pass: 100, request: 300));
        var id = await SentAsync(driver, "stub", "acp-stub");
        var arrived = 0;
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<BrainstormTurnRun> Meet(BrainstormTurn turn, CancellationToken ct)
        {
            if (Interlocked.Increment(ref arrived) == 2) both.TrySetResult();
            await both.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            return script.Answer(turn);
        }

        script.Behaviours["g1"] = Meet;
        script.Behaviours["g2"] = Meet;
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Answers["g2"] = Give("Cache the report");
        script.Answers["s1"] = Synthesize("P1", "P2");

        await LookAsync(driver);

        Assert.Equal(2, script.Most);
        Assert.Equal(("answered", 1), (Of(Pass(id, "g1")).State, Of(Pass(id, "g1")).Turns));
        Assert.Equal("answered", Of(Pass(id, "g2")).State);
        Assert.True(Document(id)["ranked"]!.GetValue<bool>());
    }

    /// <summary>
    /// 🔴 A pass past its own minutes is stopped, with no repair after them; what answered is ranked; and the document says which
    /// makers did not answer, and why, by code.
    /// </summary>
    [Fact]
    public async Task A_pass_late_past_its_minutes_is_stopped_and_the_document_says_who_did_not_answer()
    {
        // A test minute is 100 ms: the answering pass gets a second, and the request's 6 seconds leave the generators 4 more
        // than the pass's own minutes, so the hang is cut by its minutes, never by the deadline (a merge's load, 2026-10-19).
        var (driver, script) = Start(rule: Rule(pass: 10, request: 60));
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Behaviours["g2"] = Hang;
        script.Answers["s1"] = Synthesize("P1");

        await LookAsync(driver);

        Assert.Equal(("failed", "timed-out", 1), Of(Pass(id, "g2")));
        Assert.Contains("g2", script.Cancelled);
        Assert.DoesNotContain("g2r", script.Started);
        var document = Document(id);
        Assert.True(document["ranked"]!.GetValue<bool>());
        var unanswered = Assert.Single(document["unanswered"]!.AsArray())!;
        Assert.Equal(("acp-stub", "timed-out"), (unanswered["adapter"]!.GetValue<string>(), unanswered["codes"]![0]!.GetValue<string>()));
        Assert.Contains("Did not answer: `acp-stub` (maker stub): timed-out", Files.DocumentText(id)!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The request's deadline stops a generator before its own minutes, since the synthesis's and the challenge's are reserved
    /// from the start: the synthesis still runs, in its reserve, on what answered.
    /// </summary>
    [Fact]
    public async Task The_request_s_deadline_stops_a_generator_with_the_synthesis_s_and_the_challenge_s_minutes_reserved()
    {
        // The generators' second (32 less the 20 and 2 reserved) ends before the pass's own five; the synthesis's reserve is two
        // seconds, not the 200 ms that a loaded merge's driver gate outran (2026-10-19).
        var (driver, script) = Start(rule: Rule(pass: 50, synthesis: 20, challenge: 2, request: 32));
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Behaviours["g2"] = Hang;
        script.Answers["s1"] = Synthesize("P1");

        await LookAsync(driver);

        Assert.Equal(("failed", "deadline", 1), Of(Pass(id, "g2")));
        Assert.Equal("answered", Of(Pass(id, "s1")).State);
        Assert.True(Document(id)["ranked"]!.GetValue<bool>());
    }

    /// <summary>🔴 A synthesis past its minutes leaves the validated proposals unranked, by maker, saying why.</summary>
    [Fact]
    public async Task A_synthesis_past_its_minutes_leaves_the_proposals_unranked_saying_why()
    {
        var (driver, script) = Start(rule: Rule(synthesis: 2));
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Behaviours["s1"] = Hang;

        await LookAsync(driver);

        Assert.Equal(("failed", "timed-out", 1), Of(Pass(id, "s1")));
        Assert.Equal((BrainstormStates.Ended, BrainstormStates.Unranked), (Files.ReadState(id)!.State, Files.ReadState(id)!.Code));
        var document = Document(id);
        Assert.False(document["ranked"]!.GetValue<bool>());
        Assert.Equal("Make the window inclusive", Assert.Single(document["rows"]!.AsArray())!["idea"]!.GetValue<string>());
        Assert.Contains("Unranked: the synthesis did not finish: timed-out", Files.DocumentText(id)!, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Files.Rooms(id)));
    }

    /// <summary>🔴 A turn that crashes fails its pass alone, after its one repair; the other maker's answer is still ranked.</summary>
    [Fact]
    public async Task A_crash_fails_its_pass_alone_and_the_rest_is_ranked()
    {
        var (driver, script) = Start();
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Behaviours["g2"] = (_, _) => throw new InvalidOperationException("the agent crashed");
        script.Behaviours["g2r"] = (_, _) => throw new InvalidOperationException("the agent crashed again");
        script.Answers["s1"] = Synthesize("P1");

        await LookAsync(driver);

        var (state, codes, turns) = Of(Pass(id, "g2"));
        Assert.Equal(("failed", 2), (state, turns));
        Assert.Contains("crashed", codes);
        Assert.Contains("g2r", script.Started);
        Assert.True(Document(id)["ranked"]!.GetValue<bool>());
        Assert.Equal(BrainstormStates.Ended, Files.ReadState(id)!.State);
    }

    /// <summary>
    /// 🔴 A limit met mid-pass starts the pass once more, on the next ready account, as its one repair: handed its first instruction,
    /// not a repair's, and its record names the account it answered on.
    /// </summary>
    [Fact]
    public async Task A_limit_starts_the_pass_once_more_on_the_next_account()
    {
        var (driver, script) = Start();
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Behaviours["g1"] = (turn, _) => Task.FromResult(new BrainstormTurnRun(true, "work", "limit", $"limit  brainstorm turn {turn.Turn}"));
        script.Behaviours["g1r"] = (turn, _) => Task.FromResult(script.Answer(turn, account: "home"));
        script.Answers["g1r"] = Give("Make the window inclusive");
        script.Answers["g2"] = Give("Cache the report");
        script.Answers["s1"] = Synthesize("P1", "P2");

        await LookAsync(driver);

        Assert.Equal(script.Seen["g1"].Instruction, script.Seen["g1r"].Instruction);
        var pass = Pass(id, "g1");
        Assert.Equal(("answered", 2), (Of(pass).State, Of(pass).Turns));
        Assert.Equal("home", pass["account"]!.GetValue<string>());
    }

    /// <summary>🔴 A full cap waits: on a cap of one, the second generator starts when the first lets its slot go.</summary>
    /// <remarks>
    /// The generators' window is wide here (the request's 300 minutes less the synthesis's and the challenge's), since the claim is the
    /// cap's, not the deadline's: with the default rule's 10 minutes, a second at this test's minute, a loaded run's first pass used it up
    /// and the second read <c>cap-full</c> (FLAKE1, three times by 2026-10-25); the deadline's own case is the cap-full case below.
    /// </remarks>
    [Fact]
    public async Task A_full_cap_waits_and_the_next_pass_runs_when_a_slot_is_free()
    {
        var (driver, script) = Start(cap: 1, rule: Rule(request: 300));
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Behaviours["g1"] = async (turn, ct) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), ct);
            return script.Answer(turn);
        };
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Answers["g2"] = Give("Cache the report");
        script.Answers["s1"] = Synthesize("P1", "P2");

        await LookAsync(driver);

        Assert.Equal(1, script.Most);
        Assert.Equal("answered", Of(Pass(id, "g2")).State);
    }

    /// <summary>
    /// CAPCOUNT1 (D130's note): the cap counts what the driver starts. On a cap of one a driven session in the look holds the only slot
    /// and no brainstorm begins; a conversation the person holds (Ask Daoris's, here) holds none, and its passes run in turn.
    /// </summary>
    [Fact]
    public async Task A_conversation_the_person_holds_takes_no_slot_from_a_pass_but_a_driven_session_does()
    {
        var (driver, script) = Start(cap: 1);
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Answers["g2"] = Give("Cache the report");
        script.Answers["s1"] = Synthesize("P1", "P2");

        await LookAsync(driver, new SessionView("s1", "engine", "working") { Quest = "q1" });

        Assert.Empty(script.Started);
        Assert.Equal(BrainstormStates.Frozen, Files.ReadState(id)!.State);

        await LookAsync(driver, new SessionView("h1", HelpRoom.Repository, "working") { Kind = "chat" });

        Assert.Equal(["answered", "answered"], [Of(Pass(id, "g1")).State, Of(Pass(id, "g2")).State]);
        Assert.Equal(1, script.Most);
    }

    /// <summary>🔴 A pass that finds no slot by the generators' deadline never starts, and is said as <c>cap-full</c>.</summary>
    [Fact]
    public async Task A_pass_that_finds_no_slot_by_the_deadline_is_said_as_cap_full()
    {
        var (driver, script) = Start(cap: 1, rule: Rule(pass: 10, synthesis: 2, challenge: 2, request: 6));
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Behaviours["g1"] = Hang;

        await LookAsync(driver);

        Assert.Equal(("failed", "deadline", 1), Of(Pass(id, "g1")));
        Assert.Equal(("unavailable", "cap-full"), (Of(Pass(id, "g2")).State, Of(Pass(id, "g2")).Codes));
        Assert.DoesNotContain("g2", script.Started);
        Assert.Equal((BrainstormStates.Ended, BrainstormStates.NoAgent), (Files.ReadState(id)!.State, Files.ReadState(id)!.Code));
    }

    /// <summary>
    /// 🔴 The person's stop ends every pass that runs and keeps what answered: no synthesis, and the document is the validated
    /// proposals, unranked, saying it was stopped.
    /// </summary>
    [Fact]
    public async Task The_person_s_stop_ends_every_pass_and_keeps_what_answered()
    {
        var (driver, script) = Start();
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Behaviours["g2"] = async (turn, ct) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            Assert.Equal(0, BrainstormCommand.Run(["stop", id], _home, TextWriter.Null, At));
            return await Hang(turn, ct);
        };

        await LookAsync(driver);

        Assert.Equal(("stopped", "stopped"), (Of(Pass(id, "g2")).State, Of(Pass(id, "g2")).Codes));
        Assert.DoesNotContain("s1", script.Started);
        Assert.Equal((BrainstormStates.Ended, BrainstormStates.Stopped), (Files.ReadState(id)!.State, Files.ReadState(id)!.Code));
        Assert.Equal("Make the window inclusive", Assert.Single(Document(id)["rows"]!.AsArray())!["idea"]!.GetValue<string>());
        Assert.Contains("Unranked: stopped by the person", Files.DocumentText(id)!, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Files.Rooms(id)));
    }

    /// <summary>🔴 A request stopped before it runs ends at the next look, and nothing runs.</summary>
    [Fact]
    public async Task A_request_stopped_before_it_runs_ends_and_nothing_runs()
    {
        var (driver, script) = Start();
        var id = await SentAsync(driver, "stub");
        Assert.Equal(0, BrainstormCommand.Run(["stop", id], _home, TextWriter.Null, At));

        await LookAsync(driver);

        Assert.Empty(script.Started);
        Assert.Equal((BrainstormStates.Ended, BrainstormStates.Stopped), (Files.ReadState(id)!.State, Files.ReadState(id)!.Code));
    }

    /// <summary>
    /// 🔴 A Daoris that stopped mid-brainstorm, coming up, ends the passes that ran as interrupted, keeps what answered as the
    /// unranked document, and runs nothing again, at this look or the next.
    /// </summary>
    [Fact]
    public async Task A_restart_ends_the_passes_that_ran_as_interrupted_and_runs_none_again()
    {
        var (first, _) = Start();
        var id = await SentAsync(first, "stub", "acp-stub");
        var state = Files.ReadState(id)!;
        Files.WriteState(state with { State = BrainstormStates.Running });
        Files.WritePass(id, new BrainstormPassRecord(new BrainstormBy("g1", "stub", "stub", "work"), BrainstormPassRecord.Generate, BrainstormPassRecord.Answered, [], 1));
        Directory.CreateDirectory(Path.GetDirectoryName(Files.AnswerPath(id, "g1"))!);
        File.WriteAllText(Files.AnswerPath(id, "g1"), Give("Make the window inclusive"));
        Files.WritePass(id, new BrainstormPassRecord(new BrainstormBy("g2", "stub", "acp-stub", "work"), BrainstormPassRecord.Generate, "running", [], 1));

        var (again, script) = Start();
        await LookAsync(again);
        await LookAsync(again);

        Assert.Empty(script.Started);
        Assert.Equal((BrainstormStates.Ended, BrainstormStates.Interrupted), (Files.ReadState(id)!.State, Files.ReadState(id)!.Code));
        Assert.Equal(("interrupted", "interrupted"), (Of(Pass(id, "g2")).State, Of(Pass(id, "g2")).Codes));
        Assert.Equal("answered", Of(Pass(id, "g1")).State);
        Assert.Equal("Make the window inclusive", Assert.Single(Document(id)["rows"]!.AsArray())!["idea"]!.GetValue<string>());
        Assert.Contains("Unranked: interrupted", Files.DocumentText(id)!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The policy's agents bound the request's: one it does not list is said as <c>not-in-policy</c> and never runs, and a request
    /// that names none runs the policy's, in its order.
    /// </summary>
    [Fact]
    public async Task The_policy_s_agents_bound_the_request_s()
    {
        var (driver, script) = Start(rule: Rule(agents: ["acp-stub", "stub"]));
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Answers["s1"] = Synthesize("P1");
        var named = await SentAsync(driver, "stub", "dsh");

        await LookAsync(driver);

        Assert.Equal(["g1", "s1"], script.Started);
        Assert.Equal("stub", script.Seen["g1"].Agent);
        Assert.Equal(("unavailable", "not-in-policy"), (Of(Pass(named, "g2")).State, Of(Pass(named, "g2")).Codes));

        script.Started.Clear();
        script.Answers["g2"] = Give("Cache the report");
        script.Answers["s1"] = Synthesize("P1", "P2");
        await SentAsync(driver);
        await LookAsync(driver);

        Assert.Equal(["acp-stub", "stub"], script.Started.Where(turn => turn.StartsWith('g')).Order().Select(turn => script.Seen[turn].Agent));
    }

    /// <summary>
    /// 🔴 A request is idempotent by its id: asked again with the same words it is the same brainstorm, never a second; asked with
    /// other words under that id it is refused; and an id that is no plain name is a line the verb does not read.
    /// </summary>
    [Fact]
    public void A_request_is_idempotent_by_its_id()
    {
        var said = new StringWriter();
        Assert.Equal(0, BrainstormCommand.Run(["Why does the report drop its last day?", "--id", "daily-report"], _home, said, At));
        Assert.Equal(0, BrainstormCommand.Run(["Why does the report drop its last day?", "--id", "daily-report"], _home, said, At));

        Assert.Equal("daily-report", Assert.Single(Files.Requests()).Id);
        Assert.Contains("the same brainstorm, never a second", said.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, BrainstormCommand.Run(["Why is the report slow?", "--id", "daily-report"], _home, TextWriter.Null, At));
        Assert.Equal(2, BrainstormCommand.Run(["Why is the report slow?", "--id", "../other"], _home, TextWriter.Null, At));
        Assert.Single(Files.Requests());
    }
}
