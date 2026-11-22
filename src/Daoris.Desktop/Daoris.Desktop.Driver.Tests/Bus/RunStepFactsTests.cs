using System.Collections.Concurrent;
using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's next step starts from the fact that ended its last, through the bus's queue (BUS2e1): each ending fact asks the pass for its run
/// at once and its acknowledgement ends the fact's timer only once the pass judged the run; a settle is taken from its box at the fact; a
/// restart resumes the facts queued and passes every open run; a fact tried again asks again and clears nothing before the run moved; and a
/// node begun is told apart as the consumer's or the look's. Homes under <c>_fixtures/</c>.
/// </summary>
public sealed class RunStepFactsTests : IDisposable
{
    private static readonly TimeSpan Soon = Poll.Patience;

    private static readonly TimeProvider Machine = TimeProvider.System;

    private static readonly BusQueueOptions Quick = new() { Clock = Machine, RetryFirst = TimeSpan.FromMilliseconds(20), RetryMost = TimeSpan.FromMilliseconds(60) };

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "run-step-facts", Guid.NewGuid().ToString("N")[..8]);

    private readonly ConcurrentQueue<string> _steps = new();

    private int _tries;

    public RunStepFactsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    /// <summary>🔴 A check's pass, a policy pass, a landing's end and a session's turn end each ask their run's pass at once, and nothing stays armed to the step.</summary>
    [Fact]
    public async Task Each_ending_fact_asks_its_runs_pass_and_is_acknowledged_after_it()
    {
        await using var bus = Open();
        var consumer = Consumer(bus, _ => Task.FromResult(true));
        await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick);

        bus.Append(new BusDraft(BusTypes.CheckPassed, "r-1") { Commit = true });
        bus.Append(StepBusFacts.PolicyPassed("r-2", "opinion", "abc1234"));
        LandOutcomes.Kept(_home, "run:r-3", "abc1234", new TreeLanding(true, "merged"));
        bus.Append(StepBusFacts.StepTurnEnded("run:r-4", "build", "inv-1"));

        await Poll.Until(() => _steps.Count(each => each.StartsWith("pass run:r-", StringComparison.Ordinal)) >= 4, () => string.Join(", ", _steps), Soon);
        Assert.Contains("pass run:r-1", _steps);
        Assert.Contains("pass run:r-2", _steps);
        Assert.Contains("pass run:r-3", _steps);
        Assert.Contains("pass run:r-4", _steps);
        await Unarmed(bus);
    }

    /// <summary>🔴 A session's settle is taken from its box at the fact, before the run's pass reads the record, and a look takes none.</summary>
    [Fact]
    public async Task A_settle_is_taken_at_its_fact_before_the_pass()
    {
        await using var bus = Open();
        var consumer = Consumer(bus, _ => Task.FromResult(true));
        await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick);
        _steps.Clear();

        bus.Append(StepBusFacts.StepSettled("s-worker", "#r-1", "build"));

        await Poll.Until(() => _steps.Contains("pass run:r-1"), () => string.Join(", ", _steps), Soon);
        var order = _steps.ToList();
        Assert.Equal("take s-worker", order[order.IndexOf("pass run:r-1") - 1]);
        await Unarmed(bus);
        Assert.Empty(typeof(RunStepConsumer).Assembly.GetTypes().Where(type => type.Name == "StepSettlesLook"));
    }

    /// <summary>🔴 A run the pass did not judge (a drain, a closing loop, no lease) is acknowledged with its obligation armed, and spends no retry.</summary>
    [Fact]
    public async Task A_run_the_pass_did_not_judge_keeps_the_facts_timer_and_spends_no_retry()
    {
        await using var bus = Open();
        var consumer = Consumer(bus, _ =>
        {
            Interlocked.Increment(ref _tries);
            return Task.FromResult(false);
        });
        await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick);

        bus.Append(new BusDraft(BusTypes.CheckPassed, "r-1") { Commit = true });
        await bus.FlushAsync();
        await Poll.Until(() => Volatile.Read(ref _tries) >= 1, () => "no pass asked", Soon);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        await bus.FlushAsync();
        Assert.Contains(Armed(), timer => timer.Obliged == BusTypes.Step);
        Assert.Equal(1, Volatile.Read(ref _tries));
    }

    /// <summary>🔴 A settle's fact takes only the box its session named; a start takes every box and asks no pass.</summary>
    [Fact]
    public async Task A_settle_takes_its_own_box_and_a_start_takes_none_by_pass()
    {
        await using var bus = Open();
        var consumer = Consumer(bus, _ => Task.FromResult(true));
        await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick);
        bus.Append(StepBusFacts.StepSettled("s-a", "r-1", "build"));
        await Poll.Until(() => _steps.Contains("take s-a"), () => string.Join(", ", _steps), Soon);
        Assert.DoesNotContain("take s-b", _steps);
        Assert.Contains("take", _steps);
        Assert.DoesNotContain(_steps, each => each.StartsWith("pass", StringComparison.Ordinal) && !each.Contains("r-1", StringComparison.Ordinal));
    }

    /// <summary>🔴 Facts queued while no queue ran are handled at the next start, which takes the boxes and asks no pass of its own.</summary>
    [Fact]
    public async Task A_restart_resumes_the_queued_facts_and_asks_no_pass_of_its_own()
    {
        await using var bus = Open();
        // The consumer's first run claims its cursor; the facts below are written while no queue runs.
        await (await BusQueue.StartAsync(bus, [Consumer(bus, _ => Task.FromResult(true))], Quick)).DisposeAsync();
        bus.Append(new BusDraft(BusTypes.CheckFailed, "r-1") { Commit = true });
        bus.Append(StepBusFacts.StepTurnEnded("run:r-2", "build", "inv-2"));
        await bus.FlushAsync();
        _steps.Clear();

        var consumer = Consumer(bus, _ => Task.FromResult(true));
        await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick);

        await Poll.Until(() => _steps.Contains("pass run:r-1") && _steps.Contains("pass run:r-2"), () => string.Join(", ", _steps), Soon);
        Assert.Contains("take", _steps);
        Assert.Equal(2, _steps.Count(each => each.StartsWith("pass", StringComparison.Ordinal)));
        await Unarmed(bus);
    }

    /// <summary>🔴 The comparer tells a node begun in the pass the consumer asked from one a look began, with the lag from the ending fact.</summary>
    [Fact]
    public async Task A_node_begun_is_told_as_the_consumers_or_the_looks()
    {
        // The measures are written with the log's next write or at its close, so they are read once it closed.
        await using (var bus = Open())
        {
            var consumer = Consumer(bus, async unit =>
            {
                if (unit == "run:r-1") bus.Append(OpinionBusFacts.NodeBegun("run:r-1", ["next"]));
                await Task.Yield();
                return true;
            });
            await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick);

            bus.Append(new BusDraft(BusTypes.CheckPassed, "r-1") { Commit = true });
            await Poll.Until(() => _steps.Contains("pass run:r-1"), () => string.Join(", ", _steps), Soon);
            await Unarmed(bus);

            // A node begun after the pass ended, with an ending fact standing before it that the pass began nothing for, is the look's.
            bus.Append(new BusDraft(BusTypes.CheckFailed, "r-2") { Commit = true });
            await Poll.Until(() => _steps.Contains("pass run:r-2"), () => string.Join(", ", _steps), Soon);
            await Unarmed(bus);
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            bus.Append(OpinionBusFacts.NodeBegun("run:r-2", ["next"]));
            bus.Append(OpinionBusFacts.NodeBegun("run:r-3", ["next"]));
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        var began = Began().ToDictionary(row => row.Name, row => row.Count);
        Assert.Equal(1, began.GetValueOrDefault(BusMeasures.ByConsumer));
        Assert.Equal(1, began.GetValueOrDefault(BusMeasures.ByLook));
        Assert.Equal(1, began.GetValueOrDefault(BusMeasures.ByOther));

        var steps = Steps();
        Assert.Equal(2, steps.GetValueOrDefault("judged"));
        Assert.Equal(1, steps.GetValueOrDefault(BusMeasures.StepBegan));
    }

    /// <summary>🔴 A run's binding asks the whole read only where its run's pass does not govern the run (BUS2e2).</summary>
    [Fact]
    public async Task A_runs_binding_asks_the_whole_read_only_where_its_pass_does_not_govern_the_run()
    {
        await using var bus = Open();
        var consumer = new RunStepConsumer(
            (unit, _) =>
            {
                _steps.Enqueue("pass " + unit);
                return Task.FromResult(unit == "run:r-1" ? PassOutcome.NotGoverned : PassOutcome.Judged);
            },
            (_, _) => Task.FromResult(0), bus, (_, _) => { }, TimeSpan.Zero,
            whole: (unit, _) =>
            {
                _steps.Enqueue("whole " + unit);
                return Task.FromResult(PassOutcome.Judged);
            });
        await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick);

        bus.Append(StepBusFacts.RunBound("r-1", "docs", 1));
        bus.Append(StepBusFacts.RunBound("r-2", "docs", 1));

        await Poll.Until(() => _steps.Contains("whole run:r-1") && _steps.Contains("pass run:r-2"), () => string.Join(", ", _steps), Soon);
        Assert.Equal(["pass run:r-1", "whole run:r-1"], _steps.Where(each => each.EndsWith("r-1", StringComparison.Ordinal)));
        await Unarmed(bus);
        Assert.DoesNotContain("whole run:r-2", _steps);
    }

    /// <summary>🔴 A fact whose pass did not judge its run is counted by why, and asks no whole read.</summary>
    [Fact]
    public async Task A_fact_whose_pass_did_not_judge_is_counted_by_why()
    {
        var wholes = 0;
        await using (var bus = Open())
        {
            var outcomes = new Dictionary<string, PassOutcome> { ["run:r-1"] = PassOutcome.NotGoverned, ["run:r-2"] = PassOutcome.Bound };
            var asked = 0;
            var consumer = new RunStepConsumer(
                (unit, _) =>
                {
                    Interlocked.Increment(ref asked);
                    return Task.FromResult(outcomes[unit]);
                },
                (_, _) => Task.FromResult(0), bus, (_, _) => { }, TimeSpan.Zero,
                whole: (_, _) =>
                {
                    Interlocked.Increment(ref wholes);
                    return Task.FromResult(PassOutcome.Judged);
                });
            await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick);

            bus.Append(new BusDraft(BusTypes.CheckPassed, "r-1") { Commit = true });
            bus.Append(new BusDraft(BusTypes.CheckPassed, "r-2") { Commit = true });
            await Poll.Until(() => Volatile.Read(ref asked) >= 2, () => "the passes were not asked", Soon);
        }

        var steps = Steps();
        Assert.Equal(1, steps.GetValueOrDefault("not-governed"));
        Assert.Equal(1, steps.GetValueOrDefault("bound-passed"));
        Assert.Equal(0, steps.GetValueOrDefault("judged"));
        Assert.Equal(0, Volatile.Read(ref wholes));
    }

    private Dictionary<string, long> Steps()
    {
        using var store = new DesktopBusLogStore(_home);
        return store.Measures("2000-01-01").Where(row => row.Measure == BusMeasures.Step).ToDictionary(row => row.Name, row => (long)row.Count);
    }

    private IReadOnlyList<BusMeasureRow> Began()
    {
        using var store = new DesktopBusLogStore(_home);
        return [.. store.Measures("2000-01-01").Where(row => row.Measure == BusMeasures.Began)];
    }

    private async Task Unarmed(BusLog bus)
    {
        await bus.FlushAsync();
        await Poll.Until(
            () => !Armed().Any(timer => timer.Obliged == BusTypes.Step),
            () => "armed: " + string.Join(", ", Armed().Where(timer => timer.Obliged == BusTypes.Step).Select(timer => timer.Kind)), Soon);
    }

    private BusLog Open() =>
        BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = Machine, Interval = TimeSpan.FromMilliseconds(20), Home = _home });

    private IReadOnlyList<BusTimer> Armed()
    {
        using var store = new DesktopBusLogStore(_home);
        return store.Timers(null, 1000);
    }

    private RunStepConsumer Consumer(BusLog bus, Func<string, Task<bool>> pass) => new(
        async (unit, _) =>
        {
            _steps.Enqueue("pass " + unit);
            return await pass(unit) ? PassOutcome.Judged : PassOutcome.NotGoverned;
        },
        (session, _) =>
        {
            _steps.Enqueue(session is null ? "take" : "take " + session);
            return Task.FromResult(0);
        },
        bus, (_, _) => { }, TimeSpan.Zero);
}
