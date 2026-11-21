using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The brainstorm's scripted bench, which <c>BrainstormBoundsTests</c> and <c>BrainstormKeptAnswerTests</c> share: a driver whose turns
/// are scripted stand-ins on a minute shortened to a tenth of a second, its home, and the readers of a brainstorm's records.
/// </summary>
public abstract class BrainstormBench : IDisposable
{
    protected readonly string _home = Path.Combine(TestHomes.Root, "daoris-brainstorm-bounds-" + Guid.NewGuid().ToString("N")[..8]);

    protected static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-19T10:00:00Z");

    /// <summary>A brainstorm's minute here: a tenth of a second.</summary>
    protected static readonly TimeSpan Minute = TimeSpan.FromMilliseconds(100);

    protected BrainstormBench() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    protected BrainstormFiles Files => new(_home);

    private sealed class Refuses : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"none here\"}") });
    }

    /// <summary>The turns, scripted: each answers from <see cref="Answers"/> unless a behaviour is set for it.</summary>
    protected sealed class Script
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
    protected static async Task<BrainstormTurnRun> Hang(BrainstormTurn turn, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), ct);
        return new BrainstormTurnRun(true, "work", null, $"completed  brainstorm turn {turn.Turn}");
    }

    /// <summary>The driver's poll interval at this test's minute: a clock moved by it fires each wait the driver paces itself by.</summary>
    protected static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(20);

    /// <summary>Spends the moved clock a tick at a time, paced by the poll, until the look has ended: the waits the code reads on the clock are spent, not slept.</summary>
    protected static Task SpendAsync(MovedClock clock, Task look) =>
        Poll.Until(() =>
        {
            clock.Advance(Tick);
            return look.IsCompleted;
        }, () => "the look never ended");

    protected static string Give(params string[] ideas) =>
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

    protected static string Synthesize(params string[] members) =>
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

    protected static BrainstormRule Rule(int pass = 10, int synthesis = 10, int challenge = 10, int request = 30, params string[] agents) =>
        new(BrainstormRules.Ask, agents, new BrainstormMinutes(pass, synthesis, challenge, request));

    protected (Daoris.Driver.Driver Driver, Script Script) Start(int cap = 3, BrainstormRule? rule = null, TimeProvider? clock = null)
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

    protected static async Task LookAsync(Daoris.Driver.Driver driver, params SessionView[] active)
    {
        await driver.BrainstormsAsync(new Snapshot([], [], active), 0, [], CancellationToken.None);
        await driver.Running.SettledAsync().WaitAsync(Poll.Patience);
    }

    /// <summary>A request frozen at a look and sent by the person, ready to run at the next.</summary>
    protected async Task<string> SentAsync(Daoris.Driver.Driver driver, params string[] agents)
    {
        var id = Files.WriteRequest("How should the daily report stop dropping its last day?", null, agents, At);
        await LookAsync(driver);
        Files.WriteSent(id, Files.ReadState(id)!.Packet!, At);
        return id;
    }

    protected JsonNode Pass(string id, string pass) => JsonNode.Parse(File.ReadAllText(Files.PathOf(id, "passes", $"{pass}.json")))!;

    /// <summary>A pass's record: its state, its codes joined by commas, and its turns.</summary>
    protected static (string State, string Codes, int Turns) Of(JsonNode pass) =>
        (pass["state"]!.GetValue<string>(), string.Join(",", pass["codes"]!.AsArray().Select(code => code!.GetValue<string>())), pass["turns"]!.GetValue<int>());

    protected JsonNode Document(string id) => JsonNode.Parse(File.ReadAllText(Files.PathOf(id, "document.json")))!;
}
