using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A brainstorm never spends past its bounds or hangs. The generators run side by side
/// within one deadline that keeps the synthesis's and the challenge's minutes reserved; a pass past its minutes, or the request past
/// its deadline, is stopped and what answered is kept; a crash fails its pass alone; a limit starts the pass once more on the next
/// account; a full cap waits within the deadline; the person's stop ends every pass; a restart ends what ran as interrupted and runs
/// nothing again; and the policy's agents bound the request's. Every turn is a scripted stand-in on a minute shortened to a tenth of
/// a second, so nothing starts a process; the deadline's stop on a real agent is <c>BrainstormProcessTests</c>'.
/// </summary>
public sealed class BrainstormBoundsTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-brainstorm-bounds-" + Guid.NewGuid().ToString("N")[..8]);

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

    /// <summary>The driver's poll interval at this test's minute: a clock moved by it fires each wait the driver paces itself by.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(20);

    /// <summary>Spends the moved clock a tick at a time, paced by the poll, until the look has ended: the waits the code reads on the clock are spent, not slept.</summary>
    private static Task SpendAsync(MovedClock clock, Task look) =>
        Poll.Until(() =>
        {
            clock.Advance(Tick);
            return look.IsCompleted;
        }, () => "the look never ended");

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

    private (Daoris.Driver.Driver Driver, Script Script) Start(int cap = 3, BrainstormRule? rule = null, TimeProvider? clock = null)
    {
        var script = new Script();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient("http://127.0.0.1:9", null, new HttpClient(new Refuses())),
            DriverConfig.Empty with { Actors = StaffedRoles.On("stub"), Cap = cap, Brainstorms = rule }, ProviderRegistry.Built(), _home)
        {
            BrainstormTurns = script.RunAsync,
            BrainstormMinute = Minute,
            BrainstormClock = clock ?? TimeProvider.System,
        };
        return (driver, script);
    }

    private static async Task LookAsync(Daoris.Driver.Driver driver, params SessionView[] active)
    {
        await driver.BrainstormsAsync(new Snapshot([], [], active), 0, [], CancellationToken.None);
        await driver.Running.SettledAsync().WaitAsync(Poll.Patience);
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
    /// default rule's 10 minutes, a second at this test's minute, a loaded run's second generator could start after the first's pass
    /// is spent, and the first would read <c>failed</c>.
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
            await both.Task.WaitAsync(Poll.Patience, ct);
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
        var clock = new MovedClock();
        var (driver, script) = Start(rule: Rule(pass: 10, request: 60), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Answers["g1"] = Give("Make the window inclusive");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        script.Behaviours["g2"] = (turn, ct) =>
        {
            entered.TrySetResult();
            return Hang(turn, ct);
        };
        script.Answers["s1"] = Synthesize("P1");

        var look = LookAsync(driver);
        await entered.Task.WaitAsync(Poll.Patience);
        // Spend the hanging generator's own limit, then hold the later stages' reserve through any machine load.
        var armed = clock.Pending.Contains(clock.GetUtcNow() + 10 * Minute);
        clock.Advance(10 * Minute);
        await look.WaitAsync(Poll.Patience);

        Assert.True(armed, "the pass must arm its own cancellation on the clock before its turn runs");
        Assert.Equal(("failed", "timed-out", 1), Of(Pass(id, "g2")));
        Assert.Contains("g2", script.Cancelled);
        Assert.DoesNotContain("g2r", script.Started);
        var document = Document(id);
        Assert.True(document["ranked"]!.GetValue<bool>());
        var unanswered = Assert.Single(document["unanswered"]!.AsArray())!;
        Assert.Equal(("acp-stub", "timed-out"), (unanswered["door"]!.GetValue<string>(), unanswered["codes"]![0]!.GetValue<string>()));
        Assert.Contains("Did not answer: `acp-stub` (maker stub): timed-out", Files.DocumentText(id)!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The request's deadline stops a generator before its own minutes, since the synthesis's and the challenge's are reserved
    /// from the start: the synthesis still runs, in its reserve, on what answered.
    /// </summary>
    [Fact]
    public async Task The_request_s_deadline_stops_a_generator_with_the_synthesis_s_and_the_challenge_s_minutes_reserved()
    {
        // The generators' 2.8 seconds (230 less the 200 and 2 reserved) end before the pass's own five; the synthesis's reserve is
        // twenty seconds, not the two a loaded queue's driver gate outran (candidate 7da959c, 2026-10-29) nor the 200 ms before that
        // (2026-10-19), and the synthesis answers at once, so the case still ends in about three seconds.
        var (driver, script) = Start(rule: Rule(pass: 50, synthesis: 200, challenge: 2, request: 230));
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
        // The synthesis must start and then hang past its minutes, so its window is wide enough to start in. At two minutes of
        // 100 ms a loaded machine can let the window pass before the synthesis first asks for its slot, which the deadline answers first,
        // and the pass would read `cap-full` instead of `timed-out`. The request's own bound stays
        // past the generators' and the synthesis's together, so the synthesis's end is its own.
        var (driver, script) = Start(rule: Rule(synthesis: 20, request: 60));
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
        // Nothing here is about a deadline, so the passes' and the synthesis's are held far past the work; at the default
        // ten minutes of 100 ms a loaded machine can let the synthesis's end first, and the answers would be left unranked.
        var (driver, script) = Start(rule: Rule(pass: 100, synthesis: 100, request: 300));
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
    /// cap's, not the deadline's: with the default rule's 10 minutes, a second at this test's minute, a loaded run's first pass could use
    /// it up and the second would read <c>cap-full</c>; the deadline's own case is the cap-full case below.
    /// </remarks>
    [Fact]
    public async Task A_full_cap_waits_and_the_next_pass_runs_when_a_slot_is_free()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(cap: 1, rule: Rule(request: 300), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Behaviours["g1"] = async (turn, ct) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), clock, ct);
            return script.Answer(turn);
        };
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Answers["g2"] = Give("Cache the report");
        script.Answers["s1"] = Synthesize("P1", "P2");

        var look = LookAsync(driver);
        await SpendAsync(clock, look);
        await look;

        Assert.Equal(1, script.Most);
        Assert.Equal("answered", Of(Pass(id, "g2")).State);
    }

    /// <summary>
    /// The cap counts what the driver starts. On a cap of one a driven session in the look holds the only slot
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
        Assert.Equal((BrainstormStates.Ended, BrainstormStates.ShortHanded), (Files.ReadState(id)!.State, Files.ReadState(id)!.Code));
        var text = Files.DocumentText(id)!;
        Assert.Contains("found no free slot under the machine's cap", text, StringComparison.Ordinal);
        Assert.Contains("ask the brainstorm again once the cap has a free slot", text, StringComparison.Ordinal);
    }

    /// <summary>A pass that cooled, waited, then found no slot before the deadline is said by its last cause: the cap, not the cool-off that lifted.</summary>
    [Fact]
    public async Task A_pass_that_cooled_then_found_no_slot_is_said_as_the_cap()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(cap: 1, rule: Rule(pass: 10, synthesis: 2, challenge: 2, request: 6), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        var asked = 0;
        script.Behaviours["g1"] = (_, _) => Task.FromResult(Interlocked.Increment(ref asked) == 1
            ? new BrainstormTurnRun(false, null, "cooling", "held  brainstorm turn g1: cooling", "its account cools", clock.GetUtcNow() + TimeSpan.FromMilliseconds(100))
            : throw new InvalidOperationException("g1 must not start again"));
        script.Behaviours["g2"] = Hang;

        var look = LookAsync(driver);
        await SpendAsync(clock, look);
        await look;

        Assert.Equal(("unavailable", "cooling,cap-full"), (Of(Pass(id, "g1")).State, Of(Pass(id, "g1")).Codes));
        var text = Files.DocumentText(id)!;
        Assert.Contains("found no free slot under the machine's cap", text, StringComparison.Ordinal);
        Assert.DoesNotContain("is cooling", text, StringComparison.Ordinal);
        Assert.DoesNotContain("once its account lifts", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 A maker whose accounts all cool, the first lifting within the deadline, is waited for: its pass starts again when
    /// it lifts and answers, and the synthesis has both makers.
    /// </summary>
    [Fact]
    public async Task A_maker_whose_accounts_cool_is_waited_for_when_the_first_lifts_within_the_deadline()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(rule: Rule(pass: 100, synthesis: 100, request: 300), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        var asked = 0;
        script.Behaviours["g2"] = (turn, _) => Task.FromResult(Interlocked.Increment(ref asked) == 1
            ? new BrainstormTurnRun(false, null, "cooling", "held  brainstorm turn g2: cooling", "its account cools", clock.GetUtcNow() + TimeSpan.FromMilliseconds(150))
            : script.Answer(turn));
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Answers["g2"] = Give("Cache the report");
        script.Answers["s1"] = Synthesize("P1", "P2");

        var look = LookAsync(driver);
        await SpendAsync(clock, look);
        await look;

        Assert.Equal(2, asked);
        Assert.Equal(("answered", 1), (Of(Pass(id, "g2")).State, Of(Pass(id, "g2")).Turns));
        Assert.True(Document(id)["ranked"]!.GetValue<bool>());
        Assert.Equal("answered", Of(Pass(id, "g1")).State);
        Assert.Contains("s1", script.Started);
        Assert.Equal(2, Document(id)["rows"]!.AsArray().Count);
    }

    /// <summary>
    /// 🔴 A maker cooling past the deadline is not waited for, and the brainstorm says so before it synthesizes: no synthesis
    /// runs on the one maker left, what answered is kept unranked, and the document names who is cooling and offers the wait.
    /// </summary>
    [Fact]
    public async Task A_maker_cooling_past_the_deadline_is_said_before_a_synthesis_and_the_wait_is_offered()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(rule: Rule(pass: 100, synthesis: 100, request: 300), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Behaviours["g2"] = (_, _) => Task.FromResult(
            new BrainstormTurnRun(false, null, "cooling", "held  brainstorm turn g2: cooling", "its one account cools for an hour", clock.GetUtcNow() + TimeSpan.FromHours(1)));
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Answers["s1"] = Synthesize("P1");

        await LookAsync(driver);

        Assert.DoesNotContain("s1", script.Started);
        Assert.Equal(1, script.Started.Count(turn => turn == "g2"));
        Assert.Equal(("unavailable", "cooling"), (Of(Pass(id, "g2")).State, Of(Pass(id, "g2")).Codes));
        Assert.Equal((BrainstormStates.Ended, BrainstormStates.ShortHanded), (Files.ReadState(id)!.State, Files.ReadState(id)!.Code));
        var document = Document(id);
        Assert.False(document["ranked"]!.GetValue<bool>());
        Assert.Equal("Make the window inclusive", Assert.Single(document["rows"]!.AsArray())!["idea"]!.GetValue<string>());
        var text = Files.DocumentText(id)!;
        Assert.Contains("short of makers: 1 answered", text, StringComparison.Ordinal);
        Assert.Contains("`acp-stub`", text, StringComparison.Ordinal);
        Assert.Contains("is cooling", text, StringComparison.Ordinal);
        Assert.Contains("ask the brainstorm again once its account lifts", text, StringComparison.Ordinal);
    }

    /// <summary>🔴 A maker that meets its limit on its one restart too is short, though its pass reads failed: no synthesis.</summary>
    [Fact]
    public async Task A_maker_that_meets_its_limit_twice_is_short_and_no_synthesis_runs()
    {
        var (driver, script) = Start(rule: Rule(pass: 100, synthesis: 100, request: 300));
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Behaviours["g2"] = (turn, _) => Task.FromResult(new BrainstormTurnRun(true, "work", "limit", $"limit  brainstorm turn {turn.Turn}"));
        script.Behaviours["g2r"] = script.Behaviours["g2"];
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Answers["s1"] = Synthesize("P1");

        await LookAsync(driver);

        Assert.Equal("failed", Of(Pass(id, "g2")).State);
        Assert.DoesNotContain("s1", script.Started);
        Assert.Equal((BrainstormStates.Ended, BrainstormStates.ShortHanded), (Files.ReadState(id)!.State, Files.ReadState(id)!.Code));
        Assert.Contains("met its account's limit", Files.DocumentText(id)!, StringComparison.Ordinal);
        Assert.Contains("once its account's limit resets", Files.DocumentText(id)!, StringComparison.Ordinal);
    }

    /// <summary>🔴 Where no maker answered and one cooled, the brainstorm still says so and offers the wait, not only that no agent answered.</summary>
    [Fact]
    public async Task Where_nobody_answered_and_a_maker_cooled_the_wait_is_still_offered()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(rule: Rule(pass: 100, synthesis: 100, request: 300), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Behaviours["g1"] = (_, _) => Task.FromResult(
            new BrainstormTurnRun(false, null, "cooling", "held  brainstorm turn g1: cooling", "its account cools", clock.GetUtcNow() + TimeSpan.FromHours(1)));
        script.Behaviours["g2"] = script.Behaviours["g1"];

        await LookAsync(driver);

        Assert.Empty(script.Started.Where(turn => turn == "s1"));
        Assert.Equal((BrainstormStates.Ended, BrainstormStates.ShortHanded), (Files.ReadState(id)!.State, Files.ReadState(id)!.Code));
        Assert.Contains("short of makers: 0 answered", Files.DocumentText(id)!, StringComparison.Ordinal);
    }

    /// <summary>🔴 A cooling pass lets its slot go while it waits: on a cap of one, the other maker runs meanwhile, then the waiter again.</summary>
    [Fact]
    public async Task A_cooling_pass_lets_its_slot_go_while_it_waits()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(cap: 1, rule: Rule(pass: 100, synthesis: 100, request: 300), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        var asked = 0;
        script.Behaviours["g1"] = (turn, _) => Task.FromResult(Interlocked.Increment(ref asked) == 1
            ? new BrainstormTurnRun(false, null, "cooling", "held  brainstorm turn g1: cooling", "cools", clock.GetUtcNow() + TimeSpan.FromMilliseconds(300))
            : script.Answer(turn));
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Answers["g2"] = Give("Cache the report");
        script.Answers["s1"] = Synthesize("P1", "P2");

        var look = LookAsync(driver);
        await SpendAsync(clock, look);
        await look;

        Assert.Equal(["g1", "g2", "g1"], script.Started.Where(turn => turn is "g1" or "g2").ToList());
        Assert.Equal(1, script.Most);
        Assert.Equal("answered", Of(Pass(id, "g1")).State);
    }

    /// <summary>🔴 A pass's own minutes start when it runs again, not when it first asked: a lift later than they would have lasted still answers.</summary>
    [Fact]
    public async Task A_pass_s_own_minutes_start_again_when_the_cool_off_lifts()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(rule: Rule(pass: 5, synthesis: 100, request: 300), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        var asked = 0;
        script.Behaviours["g2"] = (turn, _) => Task.FromResult(Interlocked.Increment(ref asked) == 1
            ? new BrainstormTurnRun(false, null, "cooling", "held  brainstorm turn g2: cooling", "cools", clock.GetUtcNow() + TimeSpan.FromMilliseconds(800))
            : script.Answer(turn));
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Answers["g2"] = Give("Cache the report");
        script.Answers["s1"] = Synthesize("P1", "P2");

        var look = LookAsync(driver);
        await SpendAsync(clock, look);
        await look;

        Assert.Equal(("answered", 1), (Of(Pass(id, "g2")).State, Of(Pass(id, "g2")).Turns));
        Assert.True(Document(id)["ranked"]!.GetValue<bool>());
    }

    /// <summary>🔴 The person's stop during a cooling wait ends the pass and the brainstorm stopped, synthesizes nothing and leaves no slot held.</summary>
    [Fact]
    public async Task The_person_s_stop_during_a_cooling_wait_ends_the_pass_stopped()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(rule: Rule(pass: 100, synthesis: 100, request: 300), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Behaviours["g2"] = (_, _) =>
        {
            _ = Task.Delay(TimeSpan.FromMilliseconds(150), clock).ContinueWith(_ => Files.WriteStop(id, At), TaskScheduler.Default);
            return Task.FromResult(new BrainstormTurnRun(false, null, "cooling", "held  brainstorm turn g2: cooling", "cools", clock.GetUtcNow() + TimeSpan.FromSeconds(10)));
        };
        script.Answers["g1"] = Give("Make the window inclusive");

        var look = LookAsync(driver);
        await SpendAsync(clock, look);
        await look;

        Assert.Equal("stopped", Of(Pass(id, "g2")).State);
        Assert.DoesNotContain("s1", script.Started);
        Assert.Equal((BrainstormStates.Ended, BrainstormStates.Stopped), (Files.ReadState(id)!.State, Files.ReadState(id)!.Code));
        Assert.Equal(0, await driver.BrainstormsAsync(new Snapshot([], [], []), 0, [], CancellationToken.None));
    }

    /// <summary>
    /// 🔴 The person's stop ends every pass that runs and keeps what answered: no synthesis, and the document is the validated
    /// proposals, unranked, saying it is stopped.
    /// </summary>
    [Fact]
    public async Task The_person_s_stop_ends_every_pass_and_keeps_what_answered()
    {
        // The stop is pressed after 100 ms, so the pass's own deadline is held far past it; at the default ten minutes of
        // 100 ms a loaded machine can let the deadline end the pass first, `failed`/`deadline` in place of the person's stop.
        var clock = new MovedClock();
        var (driver, script) = Start(rule: Rule(pass: 100, request: 300), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Answers["g1"] = Give("Make the window inclusive");
        script.Behaviours["g2"] = async (turn, ct) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), clock, ct);
            Files.WriteStop(id, At);
            return await Hang(turn, ct);
        };

        var look = LookAsync(driver);
        await SpendAsync(clock, look);
        await look;

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
        Files.WriteStop(id, At);

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

    private static void SoFar(BrainstormTurn turn, string idea)
    {
        var partial = JsonNode.Parse(Give(idea))!.AsObject();
        partial["partial"] = true;
        Directory.CreateDirectory(Path.GetDirectoryName(turn.Answer)!);
        File.WriteAllText(turn.Answer, partial.ToJsonString());
    }

    /// <summary>🔴 A design pass runs to its own longer deadline; cut there, it keeps its answer so far, which the synthesis reads.</summary>
    [Fact]
    public async Task A_pass_cut_at_its_brainstorm_s_own_deadline_keeps_its_answer_so_far_and_the_synthesis_reads_it()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(rule: Rule(pass: 10, request: 60), clock: clock);
        var id = Files.WriteRequest("What should the palette be for?", null, ["stub", "acp-stub"], At, pass: DesignPasses.Design);
        await LookAsync(driver);
        Files.WriteSent(id, Files.ReadState(id)!.Packet!, At);
        script.Answers["g1"] = Give("A neutral grey ladder");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        script.Behaviours["g2"] = (turn, ct) =>
        {
            SoFar(turn, "Contrast first, at 4.5:1");
            entered.TrySetResult();
            return Hang(turn, ct);
        };
        string? handed = null;
        script.Behaviours["s1"] = (turn, _) =>
        {
            handed = File.ReadAllText(Path.Combine(turn.Room, BrainstormInstruction.ProposalsFile));
            return Task.FromResult(new BrainstormTurnRun(true, "work", null, "completed  brainstorm turn s1"));
        };

        var look = LookAsync(driver);
        await entered.Task.WaitAsync(Poll.Patience);
        var armed = clock.Pending.Contains(clock.GetUtcNow() + 20 * Minute);
        clock.Advance(20 * Minute);
        await SpendAsync(clock, look);

        Assert.True(armed, "a design pass's deadline is twice the policy's");
        Assert.Equal(("answered", BrainstormPassRecord.Partial, 1), Of(Pass(id, "g2")));
        Assert.Contains("Contrast first, at 4.5:1", handed, StringComparison.Ordinal);
        Assert.Contains("A neutral grey ladder", handed, StringComparison.Ordinal);
        Assert.Contains($"answered ({BrainstormPassRecord.Partial})", Files.DocumentText(id)!, StringComparison.Ordinal);
    }

    /// <summary>🔴 An answer so far given before a limit is the pass's, on its account, where the retry is cut with none.</summary>
    [Fact]
    public async Task An_answer_so_far_before_a_limit_is_kept_when_the_retry_is_cut()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(rule: Rule(pass: 10, request: 60), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Behaviours["g1"] = (turn, _) =>
        {
            SoFar(turn, "Contrast first, at 4.5:1");
            return Task.FromResult(new BrainstormTurnRun(true, "work", "limit", $"limit  brainstorm turn {turn.Turn}"));
        };
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        script.Behaviours["g1r"] = (turn, ct) =>
        {
            entered.TrySetResult();
            return Hang(turn, ct);
        };
        script.Answers["g2"] = Give("Cache the report");
        script.Answers["s1"] = Synthesize("P1", "P2");

        var look = LookAsync(driver);
        await entered.Task.WaitAsync(Poll.Patience);
        await SpendAsync(clock, look);

        var pass = Pass(id, "g1");
        Assert.Equal(("answered", BrainstormPassRecord.Partial, 2), Of(pass));
        Assert.Equal("work", pass["account"]!.GetValue<string>());
        Assert.Contains("Contrast first, at 4.5:1", Files.DocumentText(id)!, StringComparison.Ordinal);
    }
}
