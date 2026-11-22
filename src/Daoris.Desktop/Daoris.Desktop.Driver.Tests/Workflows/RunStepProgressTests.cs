using Daoris.Driver;
using Daoris.Testing;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's next step begins from the fact that ended its last, through the bus's queue and a real runtime's pass for that one unit, with no look
/// running: a step's answer heard, a session's <c>step_settle</c> taken from its box, a restart with facts queued, and a fact redelivered.
/// The unit's pass reads no units (a wake's cost), against the whole pass's one read. Homes under <c>_fixtures/</c>.
/// </summary>
public sealed class RunStepProgressTests(ITestOutputHelper output) : NodeRunFixture
{
    private const string Chain = """
        {"schemaVersion":2,"entries":["work"],"end":"config",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"release","kind":"acme.deploy/deploy","version":1,"reach":"performed"},
                  {"id":"config","kind":"task","version":1,"reach":"pressed"}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"release"},{"id":"e","kind":"after","from":"release","to":"config"}]}
        """;

    private const string Decision = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"config-needed","kind":"task","version":1,"reach":"pressed","outcomes":["needed","not-needed"],"role":"release-manager"},
                  {"id":"config-change","kind":"task","version":1,"reach":"pressed","outcomes":["done"],"role":"config-changer"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>n","kind":"after","from":"claimed","to":"config-needed"},
                  {"id":"n>g","kind":"when","from":"config-needed","to":"config-change","outcome":"needed"},
                  {"id":"g>h","kind":"after","from":"config-change","to":"handed"}]}
        """;

    private const string Work = "s-work";

    private static readonly TimeSpan Soon = Poll.Patience;

    private static readonly TimeProvider Machine = TimeProvider.System;

    private static readonly BusQueueOptions Quick = new() { Clock = Machine, RetryFirst = TimeSpan.FromMilliseconds(20), RetryMost = TimeSpan.FromMilliseconds(60) };

    private int _tipsRead;

    private int _passes;

    private IReadOnlyList<SessionRecord> _sessions = [];

    protected new void Dispose() => base.Dispose();

    private void Read()
    {
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")) with { Executors = [Work] }, RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
    }

    private NodeRuntimeLook Counted(bool whole)
    {
        var look = RunLook(whole: whole);
        var tips = look.Tips;
        return look with
        {
            Tips = (root, branches, ct) =>
            {
                Interlocked.Increment(ref _tipsRead);
                return tips(root, branches, ct);
            },
        };
    }

    private NodeRuntime Telling()
    {
        var runtime = Runtime();
        runtime.Say = (_, _, _, _) => Task.FromResult(new SayAnswer(true, "kept"));
        runtime.SessionsRead = _ => Task.FromResult<IReadOnlyList<SessionRecord>?>(_sessions);
        return runtime;
    }

    /// <summary>The pass the consumer asks for one unit: no read of the units, that unit judged first.</summary>
    private async Task<PassOutcome> OneAsync(NodeRuntime runtime, string unit, CancellationToken ct)
    {
        Interlocked.Increment(ref _passes);
        Read();
        await runtime.PassAsync(Counted(whole: false) with { Asked = new HashSet<string>([unit]), Judged = _ => { } }, ct);
        return PassOutcome.Judged;
    }

    private RunStepConsumer Consumer(BusLog bus, NodeRuntime runtime) => new(
        (unit, ct) => OneAsync(runtime, unit, ct),
        async (session, ct) => (await StepSettleAsks.TakeAsync(
            Files, _ => "claude", (_, _, _, _) => Task.FromResult(new SayAnswer(true, "kept")), () => _now, ct, only: session)).Count,
        bus, (_, _) => { });

    private BusLog Open() =>
        BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = Machine, Interval = TimeSpan.FromMilliseconds(20), Home = _home });

    /// <summary>Run <c>q7</c> bound to the chain, its release sent at the first whole pass and its answer heard, not yet judged.</summary>
    private async Task<(NodeRuntime Runtime, string Invocation)> SentAsync()
    {
        BoundTo(Chain);
        Read();
        var runtime = Telling();
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("release").State);
        return (runtime, Assert.Single(_port.Sent).Invocation);
    }

    private void Answer(string invocation) =>
        _port.Heard.Enqueue(new(Plugin, 1, new WorkStepFrame("deploy", new WorkOutcome(WorkStates.Reached), _now, "e1", Invocation: invocation)));

    /// <summary>🔴 A step's answer ends it, a check's fact says so, and the run's next node begins from the fact: no look, no read of the units.</summary>
    [Fact]
    public async Task A_passed_check_begins_the_next_node_from_its_fact_with_no_look_and_no_read_of_the_units()
    {
        var (runtime, invocation) = await SentAsync();
        var (read, tips) = (_unitsRead, _tipsRead);
        Assert.NotEqual(NodeStates.Active, RunStatus("config").State);
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [Consumer(bus, runtime)], Quick);

        Answer(invocation);
        bus.Append(new BusDraft(BusTypes.CheckPassed, RunId) { Commit = true });

        await Poll.Until(() => RunStatus("config").State == NodeStates.Active, () => $"config stayed {RunStatus("config").State}", Soon);
        Assert.Equal(NodeStates.Reached, RunStatus("release").State);
        Assert.Equal(read, _unitsRead);
        Assert.Single(_port.Sent);
        output.WriteLine($"one fact's handling: units read {_unitsRead - read}, tip reads {_tipsRead - tips}, passes {_passes}");
    }

    /// <summary>🔴 The unit's pass beside a whole pass over the same fixture: the whole one reads the units, the unit's none.</summary>
    [Fact]
    public async Task A_units_pass_reads_no_units_where_a_whole_pass_reads_them_all()
    {
        var (runtime, invocation) = await SentAsync();
        var (read, tips, gates) = (_unitsRead, _tipsRead, _gatesRead);
        await OneAsync(runtime, RunUnit, CancellationToken.None);
        var one = (_unitsRead - read, _tipsRead - tips, _gatesRead - gates);
        (read, tips, gates) = (_unitsRead, _tipsRead, _gatesRead);
        Read();
        await runtime.PassAsync(Counted(whole: true), CancellationToken.None);
        var whole = (_unitsRead - read, _tipsRead - tips, _gatesRead - gates);

        output.WriteLine($"a unit's pass (units, tips, gates): {one}; a whole pass: {whole}");
        Assert.Equal(0, one.Item1);
        Assert.Equal(1, whole.Item1);
        Assert.True(one.Item2 <= whole.Item2);
        Assert.NotNull(invocation);
    }

    /// <summary>The decision step waiting on the agent held by session <c>s-work</c>, whose <c>step_settle</c> box holds a settle of it.</summary>
    private async Task<NodeRuntime> BoxedAsync()
    {
        new PlaceCastFiles(_home, () => true).Cast("machine", StepRoles.ReleaseManager, CastHolders.Agent, "the release path", _now);
        _sessions = [new SessionRecord(Work, RunHome.Repository, SessionStates.AwaitingStep) { NoteParts = new StepWait(RunId).Note().Parts }];
        BoundTo(Decision);
        Read();
        var runtime = Telling();
        await RunPassAsync(runtime);
        Read();
        await RunPassAsync(runtime, whole: false);
        Assert.Equal(NodeStates.Active, RunStatus("config-needed").State);
        var path = DaorisHome.Entry(_home, HomeEntry.Sessions, Work + StepSettleAsks.AskSuffix);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""
            {"id":"a1","session":"{{Work}}","run":"{{RunId}}","node":"config-needed","outcome":"needed","words":"a setting","epoch":1,"evidence":{},"at":"2026-11-18T10:00:00Z"}
            """);
        return runtime;
    }

    /// <summary>🔴 Several takes of one box at once settle it once: none is refused, none tells its session, and its state stays kept.</summary>
    [Fact]
    public async Task Concurrent_takes_of_one_box_settle_it_once_and_refuse_nothing()
    {
        await BoxedAsync();
        var told = 0;
        var takes = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => StepSettleAsks.TakeAsync(
            Files, _ => "claude", (_, _, _, _) =>
            {
                Interlocked.Increment(ref told);
                return Task.FromResult(new SayAnswer(true, "kept"));
            }, () => _now, CancellationToken.None)));

        var settled = takes.SelectMany(each => each).ToList();
        Assert.True(Assert.Single(settled).Done);
        Assert.Equal(0, told);
        Assert.Contains("\"state\":\"kept\"", File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, Work + StepSettleAsks.StateSuffix)).Replace(" ", ""), StringComparison.Ordinal);
    }

    /// <summary>🔴 A session's <c>step_settle</c> is taken from its box at its fact and the run's next node begins, with no look to take it.</summary>
    [Fact]
    public async Task A_session_settle_is_taken_at_its_fact_and_begins_the_next_node()
    {
        var runtime = await BoxedAsync();
        var read = _unitsRead;
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [Consumer(bus, runtime)], Quick);
        bus.Append(StepBusFacts.StepSettled(Work, "#" + RunId, "config-needed"));

        await Poll.Until(() => RunStatus("config-change").State == NodeStates.Active, () => $"config-change stayed {RunStatus("config-change").State}", Soon);
        Assert.Equal(NodeStates.Reached, RunStatus("config-needed").State);
        Assert.Equal(read, _unitsRead);
        Assert.Equal("a1", StepSettleAsks.AnsweredId(_home, Work));
    }

    /// <summary>🔴 A unit asked while a whole pass works is judged by that pass at its next unit boundary; without it the ask waits out its bound.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_unit_asked_while_a_whole_pass_works_is_judged_by_that_pass_and_its_node_begins(bool late)
    {
        var runtime = await BoxedAsync();
        var asks = new PassAsks(() => { });
        var taken = asks.Take();
        var (reached, release) = (new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        var blocked = 0;
        Func<Task> hold = async () =>
        {
            if (Interlocked.Exchange(ref blocked, 1) == 1) return;
            reached.TrySetResult();
            await release.Task;
        };
        var look = RunLook(whole: true);
        var (tips, remotes, gates) = (look.Tips, look.Remotes, look.Gates);
        look = look with
        {
            Tips = async (root, branches, ct) => { await hold(); return await tips(root, branches, ct); },
            Remotes = async (root, branches, ct) => { await hold(); return await remotes(root, branches, ct); },
            Gates = async (unit, commit, ct) => { await hold(); return await gates(unit, commit, ct); },
            Asked = taken.Units,
            Judged = taken.Judged,
            Late = late ? taken.Late : look.Late,
            Ungoverned = taken.Ungoverned,
        };
        Read();
        var whole = Task.Run(() => runtime.PassAsync(look, CancellationToken.None));
        await Task.WhenAny(reached.Task, whole);
        Assert.True(reached.Task.IsCompleted, "the whole pass read no tip, so nothing holds it where a unit's ask can reach it");

        await StepSettleAsks.TakeAsync(Files, _ => "claude", (_, _, _, _) => Task.FromResult(new SayAnswer(true, "kept")), () => _now, CancellationToken.None, only: Work);
        var wait = asks.WaitUnitAsync(RunUnit, TimeSpan.FromSeconds(3), CancellationToken.None);
        release.SetResult();
        await whole;
        taken.Done(passed: true);

        if (!late)
        {
            Assert.Equal(PassOutcome.Bound, await wait);
            Assert.NotEqual(NodeStates.Active, RunStatus("config-change").State);
            return;
        }

        Assert.Equal(PassOutcome.Judged, await wait);
        Assert.Equal(NodeStates.Active, RunStatus("config-change").State);
        Assert.Empty(asks.Take().Units);
    }

    /// <summary>🔴 Facts queued while no queue ran begin the node at the next start, and a fact delivered again begins nothing twice.</summary>
    [Fact]
    public async Task A_restart_resumes_queued_facts_and_a_redelivered_fact_begins_nothing_twice()
    {
        var (runtime, invocation) = await SentAsync();
        await using var bus = Open();
        await (await BusQueue.StartAsync(bus, [Consumer(bus, runtime)], Quick)).DisposeAsync();
        Answer(invocation);
        bus.Append(new BusDraft(BusTypes.CheckPassed, RunId) { Commit = true });
        await bus.FlushAsync();

        await using var queue = await BusQueue.StartAsync(bus, [Consumer(bus, runtime)], Quick);
        await Poll.Until(() => RunStatus("config").State == NodeStates.Active, () => $"config stayed {RunStatus("config").State}", Soon);
        var (before, sent) = (RunStatus("config"), _port.Sent.Count);

        bus.Append(new BusDraft(BusTypes.CheckPassed, RunId) { Commit = true });
        bus.Append(new BusDraft(BusTypes.CheckPassed, RunId) { Commit = true });
        var passes = _passes;
        await Poll.Until(() => _passes > passes, () => "the redelivered fact asked no pass", Soon);

        var after = RunStatus("config");
        Assert.Equal((before.State, before.Attempt), (after.State, after.Attempt));
        Assert.Equal(sent, _port.Sent.Count);
    }
}
