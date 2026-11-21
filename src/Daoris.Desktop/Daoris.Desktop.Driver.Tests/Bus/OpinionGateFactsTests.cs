using System.Collections.Concurrent;
using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The opinion's gate judged at every fact its verdict reads, none left to a look: a person's ask at each press, the person's word at the
/// gate, the quests' move heard on the change stream, a node begun (one begun while a judgment read before it too), and the standing
/// choices written. A judgment that throws keeps its turn, and a delivery a drain held is judged again. Homes under <c>_fixtures/</c>.
/// </summary>
public sealed class OpinionGateFactsTests : IDisposable
{
    private static readonly TimeSpan Soon = Poll.Patience;

    /// <summary>The machine's clock, which the log's writer and the queue's frames wait on; the gate's own turns wait on a moved one where a test reads them.</summary>
    private static readonly TimeProvider Machine = TimeProvider.System;

    private static readonly DateTimeOffset At = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private static readonly BusQueueOptions Quick = new() { Clock = Machine, RetryFirst = TimeSpan.FromMilliseconds(20), RetryMost = TimeSpan.FromMilliseconds(60) };

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "opinion-gate-facts", Guid.NewGuid().ToString("N")[..8]);

    private readonly ConcurrentQueue<string> _judged = new();

    /// <summary>Two owed opinions: session `s-1`'s step under `steps`, and session `s-2`'s landing, each of its own chain.</summary>
    public OpinionGateFactsTests()
    {
        Directory.CreateDirectory(_home);
        var dues = new OpinionDues(_home);
        dues.Due(new OpinionDue("s-1", "q-1", "engine", "", "tree-1", OpinionRules.Steps, At));
        dues.Due(new OpinionDue("s-2", "q-2", "engine", "", "tree-2", OpinionRules.Landing, At));
    }

    public void Dispose() => GitSeed.Discard(_home);

    /// <summary>
    /// 🔴 Each press that asks again on work whose opinion is owed (<i>Try again</i> and <i>Ask again</i>, the same agent fresh, a named
    /// reviewer, <i>Ask another agent for help</i>) reaches that session's owed opinion at once, though its occasion is the ask's and not
    /// the owed one's; the judgment reads the request, and nothing owed to the gate is left armed.
    /// </summary>
    [Theory]
    [InlineData("asked", null, false)]
    [InlineData("asked", null, true)]
    [InlineData("asked", "codex-acp", false)]
    [InlineData("failure", null, false)]
    public async Task A_persons_ask_reaches_its_sessions_owed_opinion_at_once(string occasion, string? reviewer, bool sameAgent)
    {
        var requests = new ConcurrentQueue<int>();
        await using var bus = Open();
        using var gate = Gate(due =>
        {
            if (due.Session == "s-1") requests.Enqueue(new OpinionGates(_home).Read("q-1", "engine").Requests.Count);
            return Waits();
        });
        await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
        await Started();

        new OpinionGates(_home).Requested("q-1", "engine", new OpinionRequest(At, "s-1", occasion) { Reviewer = reviewer, SameAgent = sameAgent });

        await Poll.Until(() => Judged("s-1") == 2, () => $"the ask was not judged: {string.Join(", ", _judged)}", Soon);
        Assert.Equal([0, 1], requests);
        Assert.Equal(1, Judged("s-2"));
        await Unarmed(bus);
    }

    /// <summary>🔴 The person's word at the gate (<i>Go on anyway</i>, <i>I looked myself</i>, an Accept's answer) judges that session's owed opinion at once.</summary>
    [Theory]
    [InlineData(OpinionPersonSaid.Anyway)]
    [InlineData(OpinionPersonSaid.Myself)]
    [InlineData(OpinionPersonSaid.Press)]
    public async Task The_persons_word_at_the_gate_judges_its_session_at_once(string said)
    {
        await using var bus = Open();
        using var gate = Gate(_ => Waits());
        await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
        await Started();

        new OpinionGates(_home).Said("q-1", "engine", "s-1", new OpinionPersonWord(said, "abc1234", At) { Door = ReviewDoors.Screen });

        await Poll.Until(() => Judged("s-1") == 2, () => $"the word was not judged: {string.Join(", ", _judged)}", Soon);
        Assert.Equal(1, Judged("s-2"));
        await Unarmed(bus);
    }

    /// <summary>
    /// 🔴 A burst of moves folds: those waiting in a row on one subject into the last, and those of several subjects into one round of
    /// judgments, so nine facts cost at most four rounds, and none stays armed.
    /// </summary>
    [Fact]
    public async Task A_burst_of_moves_folds_into_few_rounds()
    {
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var bus = Open();
        using var gate = Gate(async due =>
        {
            if (due.Session == "s-2" && Judged("s-2") == 2)
            {
                reading.TrySetResult();
                await release.Task;
            }

            return Waits();
        });
        await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
        await Started();

        try
        {
            bus.Append(OpinionBusFacts.QuestMoved(["quests"]));
            await reading.Task.WaitAsync(Soon);
            for (var each = 0; each < 5; each++) bus.Append(OpinionBusFacts.QuestMoved(["quests"]));
            for (var each = 0; each < 3; each++) bus.Append(OpinionBusFacts.ChoicesWritten());
            await bus.FlushAsync();
        }
        finally
        {
            release.TrySetResult();
        }

        await Unarmed(bus);
        Assert.InRange(Judged("s-1"), 3, 5);
    }

    /// <summary>🔴 The quests' move heard on the service's change stream (a hold lifted, a later step declined) judges every owed opinion.</summary>
    [Fact]
    public async Task The_quests_move_heard_on_the_change_stream_judges_every_owed_opinion()
    {
        using var ledger = new StandInLedger { Changes = StandInLedger.ChangeDoor.Answers };
        await using var bus = Open();
        await using var run = await OpinionGateRun.StartAsync(bus, Gate(_ => Waits()), null, CancellationToken.None, ledger: ledger.Client());
        await Started();

        await Poll.Until(() => Judged("s-1") >= 2 && Judged("s-2") >= 2, () => $"the move was not judged: {string.Join(", ", _judged)}", Soon);
        await Unarmed(bus);
    }

    /// <summary>
    /// 🔴 A node begun judges every owed opinion, and one begun while a judgment of the same opinion read the node before it is judged
    /// again after that judgment: the owed fact goes out at the session's end, and the graph's opinion node begins after the plan.
    /// </summary>
    [Fact]
    public async Task A_node_begun_while_a_judgment_read_before_it_is_judged_again_after_it()
    {
        var begun = 0;
        var reads = new ConcurrentQueue<bool>();
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var bus = Open();
        using var gate = Gate(async due =>
        {
            if (due.Session != "s-1") return Waits();
            var saw = Volatile.Read(ref begun) == 1;
            reads.Enqueue(saw);
            if (reads.Count == 2)
            {
                reading.TrySetResult();
                await release.Task;
            }

            return Waits();
        });
        await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
        await Started();

        try
        {
            // The session ended: its owed fact's judgment reads the node not begun, and is slow.
            bus.Append(OpinionBusFacts.OpinionOwed("s-1", "q-1", OpinionRules.Steps, "due"));
            await reading.Task.WaitAsync(Soon);
            Volatile.Write(ref begun, 1);
            new NodeRecordFiles(_home, () => true).Edit("run-1", record => (record with
            {
                Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal) { ["second-opinion"] = new("run-1", "second-opinion", "running") },
            }, true));
            // Its fact judges the other owed opinion at once, and this one only after the judgment in flight.
            await Poll.Until(() => Judged("s-2") == 2, () => "the node begun was not judged", Soon);
            Assert.Equal(2, reads.Count);
        }
        finally
        {
            release.TrySetResult();
        }

        await Poll.Until(() => reads.LastOrDefault(), () => $"the opinion read the node only before it began: {string.Join(", ", reads)}", Soon);
        Assert.Equal([false, false, true], reads);
        await Unarmed(bus);
    }

    /// <summary>🔴 The standing choices written (a reviewer added to a rule among them) judge every owed opinion.</summary>
    [Fact]
    public async Task The_standing_choices_written_judge_every_owed_opinion()
    {
        await using var bus = Open();
        await using var run = await OpinionGateRun.StartAsync(bus, Gate(_ => Waits()), _home, CancellationToken.None);
        await Started();

        StandingChoices.Write(DaorisHome.Entry(_home, HomeEntry.DriverConfig), config => OpinionRules.Apply(config, new OpinionEdit
        {
            Repository = "engine", Set = new OpinionSet { Reviewers = ["codex-acp"] },
        }));

        await Poll.Until(() => Judged("s-1") == 2 && Judged("s-2") == 2, () => $"the choices were not judged: {string.Join(", ", _judged)}", Soon);
        await Unarmed(bus);
    }

    /// <summary>🔴 A judgment that throws keeps the turn a judgment before it set (a cool-off's reset): the opinion is judged again then.</summary>
    [Fact]
    public async Task A_judgment_that_throws_keeps_the_turn_it_would_have_replaced()
    {
        var clock = new MovedClock();
        var reset = clock.GetUtcNow() + TimeSpan.FromMinutes(5);
        var throwing = 0;
        await using var bus = Open();
        using var gate = Gate(due => Volatile.Read(ref throwing) == 1 && due.Session == "s-1"
            ? throw new IOException("the service did not answer")
            : Waits() with { Turns = due.Session == "s-1" ? [reset] : [] }, clock);
        await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
        await Started();
        await Poll.Until(() => clock.Pending.Contains(reset), () => "the reset was not set", Soon);

        Volatile.Write(ref throwing, 1);
        bus.Append(OpinionBusFacts.OpinionOwed("s-1", "q-1", OpinionRules.Steps, "due"));
        await Poll.Until(() => Logged().Any(entry => entry.Kind == BusTypes.GiveUp.Kind), () => "the throwing judgment was not given up", Soon);
        var before = Judged("s-1");
        Volatile.Write(ref throwing, 0);

        clock.Advance(TimeSpan.FromMinutes(6));
        await Poll.Until(() => Judged("s-1") == before + 1, () => "the kept reset never judged it", Soon);
    }

    /// <summary>🔴 A delivery an update's drain held is judged again a minute on, so the drain's end reaches it with no fact.</summary>
    [Fact]
    public async Task A_delivery_a_drain_held_is_judged_again()
    {
        var clock = new MovedClock();
        await using var bus = Open();
        using var gate = Gate(due => Waits() with { Drained = due.Session == "s-1" }, clock);
        await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
        await Started();
        await Poll.Until(() => clock.Pending.Count == 1, () => "no turn was set for the held delivery", Soon);

        clock.Advance(OpinionGateConsumer.Drained);
        await Poll.Until(() => Judged("s-1") == 2, () => "the held delivery was not judged again", Soon);
        Assert.Equal(1, Judged("s-2"));
    }

    private static OpinionFact Waits() => new(new Dictionary<string, OpinionSit>(), null, null);

    private int Judged(string session) => _judged.Count(subject => subject.StartsWith(session + "/", StringComparison.Ordinal));

    /// <summary>Nothing owed to the gate left armed once what is written is acknowledged.</summary>
    private async Task Unarmed(BusLog bus)
    {
        await bus.FlushAsync();
        await Poll.Until(
            () => !Armed().Any(timer => timer.Obliged == BusTypes.OpinionGate),
            () => "armed: " + string.Join(", ", Armed().Where(timer => timer.Obliged == BusTypes.OpinionGate).Select(timer => timer.Kind)), Soon);
    }

    private Task Started() => Poll.Until(() => Judged("s-1") >= 1 && Judged("s-2") >= 1, () => "the start did not judge both owed opinions", Soon);

    private BusLog Open() =>
        BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = Machine, Interval = TimeSpan.FromMilliseconds(20), Home = _home });

    private IReadOnlyList<BusTimer> Armed()
    {
        using var store = new DesktopBusLogStore(_home);
        return store.Timers(null, 1000);
    }

    private IReadOnlyList<BusEnvelope> Logged()
    {
        using var store = new DesktopBusLogStore(_home);
        return store.Read(0, 100_000);
    }

    private OpinionGateConsumer Gate(Func<OpinionDue, OpinionFact?> rule, TimeProvider? clock = null) => Gate(due => Task.FromResult(rule(due)), clock);

    /// <summary>The gate's consumer over the home's owed opinions, its rule a stand-in that keeps each subject it judged.</summary>
    private OpinionGateConsumer Gate(Func<OpinionDue, Task<OpinionFact?>> rule, TimeProvider? clock = null) => new(
        (due, _, _) =>
        {
            _judged.Enqueue(OpinionGateJudge.Subject(due));
            return rule(due);
        },
        (_, _) => Task.FromResult<OpinionView?>(null),
        () => new OpinionDues(_home).Open(), clock ?? Machine, (_, _) => { });
}
