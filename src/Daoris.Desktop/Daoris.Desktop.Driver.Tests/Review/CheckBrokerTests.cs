using System.Collections.Concurrent;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The check broker: asks kept under the home, each started at once and waiting its turn for the machine's one admission, as many run
/// at once as it admits, in the order of the one comparator, each ending in a verdict with the log's tail.
/// </summary>
public sealed class CheckBrokerTests : IAsyncLifetime
{
    private const long Gb = 1L << 30;

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-12T10:00:00Z");

    // A record naming another start than this process's is a dead process's, and holds nothing.
    private static readonly long Started = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "check-broker", Guid.NewGuid().ToString("N")[..8], "home");
    private readonly string _tree;

    public CheckBrokerTests()
    {
        _tree = Path.Combine(_home, "tree");
        Directory.CreateDirectory(_tree);
    }

    private sealed class Real : TimeProvider;

    private static readonly TimeProvider Clock = new Real();

    private readonly CancellationTokenSource _stop = new();
    private readonly List<Task> _runs = [];

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>A test that failed leaves its broker looking: it is stopped and let go of its seat before the scratch goes.</summary>
    public async Task DisposeAsync()
    {
        await _stop.CancelAsync();
        try
        {
            await Task.WhenAll(_runs);
        }
        catch (OperationCanceledException)
        {
        }

        _stop.Dispose();
        GitSeed.Discard(Path.GetDirectoryName(_home)!);
    }

    private Task Look(CheckBroker broker)
    {
        var run = broker.RunAsync(_stop.Token);
        _runs.Add(run);
        return run;
    }

    internal static Func<string, CancellationToken, Task<string?>> Tree(string? folder) => (_, _) => Task.FromResult(folder);

    private static CheckAsk Ask(string id, int minute = 0, string session = "s1", string? gate = "tests") =>
        new(id, session, gate, null, At.AddMinutes(minute));

    private static CheckRules Room(double free, int processors = 16, int? setting = null) =>
        new(setting, processors, 8, () => new MemoryReading((long)(free * Gb), (long)(free * Gb))) { World = RoomWorld.Machine with { Samples = false, Cpu = () => null } };

    private static HeavyWaiting Quick => new(TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(30), Clock.GetUtcNow);

    // The heavy run's own take of its slot, in a repository named for the asker's folder, held until its release is set.
    private static BrokeredRun Holding(ConcurrentQueue<string> started, ConcurrentDictionary<string, TaskCompletionSource> release) =>
        async (_, world, output, ct) =>
        {
            var name = Path.GetFileName(world.Directory);
            var rules = world.Rules();
            var holder = new HeavyHolder(Environment.ProcessId, Started, world.Directory, ["tests"], Clock.GetUtcNow(), null, null) { Now = new HeavyGateNow("tests", 0, Clock.GetUtcNow()) };
            var took = await HeavySlot.TakeUnlessAsync(
                world.Home, name, name, output, ct, world.Waiting, null, null,
                new HeavyAdmit(() => rules, _ => Task.FromResult<int?>(null), () => holder) { Turn = world.Turn }).ConfigureAwait(false);
            using var slot = took.Slot!;
            slot.Keep(holder);
            started.Enqueue(name);
            await release.GetOrAdd(name, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task.WaitAsync(ct).ConfigureAwait(false);
            await output.WriteLineAsync($"{name} passed").ConfigureAwait(false);
            return 0;
        };

    private string TreeOf(string name)
    {
        var folder = Path.Combine(_home, name);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static async Task UntilAsync(Func<bool> condition, string what)
    {
        for (var look = 0; !condition(); look++)
        {
            if (look > 3000) throw new TimeoutException($"waited 30 s for {what}");
            await Task.Delay(TimeSpan.FromMilliseconds(10), Clock);
        }
    }

    [Fact]
    public void An_ask_is_due_while_none_answered_it_and_an_errored_one_after_its_delay_alone()
    {
        var asks = new[] { Ask("a1", minute: 0), Ask("a2", minute: 5), Ask("a3", minute: 1), Ask("a4", minute: 2), Ask("a5", minute: 3) };
        CheckState? State(string id) => id switch
        {
            "a2" => new CheckState(id, CheckStates.Running, At),
            "a3" => new CheckState(id, CheckStates.Reached, At),
            "a4" => new CheckState(id, CheckStates.Errored, At),
            "a5" => new CheckState(id, CheckStates.Queued, At),
            _ => null,
        };

        Assert.Equal(["a1", "a5"], CheckBroker.Pending(asks, State, At).Select(ask => ask.Id));
        Assert.Equal(["a1", "a4", "a5"], CheckBroker.Pending(asks, State, At + CheckBroker.RetryAfter).Select(ask => ask.Id));
    }

    [Fact]
    public void The_files_keep_an_ask_its_state_and_its_log_under_the_home()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1", gate: null) with { Command = ["dotnet", "test"] });
        files.WriteState(new CheckState("a1", CheckStates.Failed, At) { Exit = 1, Tail = "boom", Tip = "abc123", Wait = "a slot: 1 of 1 run" });

        var ask = Assert.Single(files.Asks());
        Assert.Equal(("a1", "s1", null), (ask.Id, ask.Session, ask.Gate));
        Assert.Equal(["dotnet", "test"], ask.Command);
        var state = files.ReadState("a1");
        Assert.Equal((CheckStates.Failed, 1, "boom", "abc123", "a slot: 1 of 1 run"), (state!.State, state.Exit, state.Tail, state.Tip, state.Wait));
        Assert.Null(files.ReadState("zz"));
    }

    [Fact]
    public async Task The_broker_runs_an_ask_in_the_askers_tree_and_keeps_a_pass_with_its_tail()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        HeavyRunAsk? ran = null;
        string? where = null;
        var broker = new CheckBroker(_home, Tree(_tree), async (ask, world, output, ct) =>
        {
            ran = ask;
            where = world.Directory;
            Assert.Equal(CheckStates.Queued, files.ReadState("a1")!.State);
            world.Turn!.Admitted();
            Assert.Equal(CheckStates.Running, files.ReadState("a1")!.State);
            await output.WriteLineAsync("all 12 passed");
            return 0;
        });

        await broker.RunAsync(CancellationToken.None);

        Assert.Equal(("tests", _tree), (ran!.Gate, where));
        var state = files.ReadState("a1")!;
        Assert.Equal((CheckStates.Reached, 0), (state.State, state.Exit));
        Assert.Contains("all 12 passed", state.Tail);
        Assert.Equal(0, broker.Holding);
    }

    [Fact]
    public async Task A_red_check_is_failed_and_keeps_only_the_log_tail()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        var broker = new CheckBroker(_home, Tree(_tree), async (_, _, output, _) =>
        {
            for (var line = 0; line < 400; line++) await output.WriteLineAsync($"line {line}");
            return 1;
        });

        await broker.RunAsync(CancellationToken.None);

        var state = files.ReadState("a1")!;
        Assert.Equal((CheckStates.Failed, 1), (state.State, state.Exit));
        Assert.Contains("line 399", state.Tail);
        Assert.DoesNotContain("line 0\n", state.Tail);
        Assert.True(state.Tail!.Length <= CheckBroker.TailBound);
    }

    [Fact]
    public async Task An_asker_with_no_tree_is_an_error_and_a_run_cut_off_after_its_slots_is_stopped()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        var settled = new List<CheckState>();
        var none = new CheckBroker(_home, Tree(null), (_, _, _, _) => Task.FromResult(0)) { Settled = settled.Add };
        await none.RunAsync(CancellationToken.None);
        var missing = Assert.Single(settled);
        Assert.Equal(CheckStates.Errored, missing.State);
        Assert.Contains("no tree", missing.Error);
        Assert.Null(missing.Exit);
        Assert.Equal(["a1"], CheckBroker.Pending(files.Asks(), files.ReadState, missing.At + CheckBroker.RetryAfter).Select(ask => ask.Id));

        files.WriteAsk(Ask("a2", minute: 1));
        using var cancel = new CancellationTokenSource();
        var broker = new CheckBroker(_home, Tree(_tree), (_, world, _, token) =>
        {
            world.Turn!.Admitted();
            cancel.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(0);
        });
        await broker.RunAsync(cancel.Token);
        Assert.Equal(CheckStates.Stopped, files.ReadState("a2")!.State);
    }

    [Fact]
    public async Task A_run_cut_off_by_the_drivers_end_before_it_was_admitted_is_queued_again()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        using var cancel = new CancellationTokenSource();
        var broker = new CheckBroker(_home, Tree(_tree), (_, _, _, token) =>
        {
            cancel.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(0);
        });

        await broker.RunAsync(cancel.Token);

        Assert.Equal(CheckStates.Queued, files.ReadState("a1")!.State);
        Assert.Equal(["a1"], CheckBroker.Pending(files.Asks(), files.ReadState, At).Select(ask => ask.Id));
    }

    [Fact]
    public async Task With_memory_for_two_two_checks_run_at_once_and_the_third_waits_naming_the_slots()
    {
        var files = new CheckFiles(_home);
        foreach (var (name, minute) in new[] { ("one", 0), ("two", 1), ("three", 2) }) files.WriteAsk(Ask(name, minute, session: name));
        var started = new ConcurrentQueue<string>();
        var release = new ConcurrentDictionary<string, TaskCompletionSource>();
        var broker = new CheckBroker(_home, (session, _) => Task.FromResult<string?>(TreeOf(session)), Holding(started, release))
        {
            Rules = () => Room(20, processors: 8), Waiting = Quick, Poll = TimeSpan.FromMilliseconds(20),
        };

        var running = Look(broker);
        await UntilAsync(() => files.ReadState("three")?.Wait is not null && started.Count == 2, "two running and the third waiting");

        Assert.Equal(["one", "two"], started);
        Assert.Equal((CheckStates.Running, CheckStates.Running), (files.ReadState("one")!.State, files.ReadState("two")!.State));
        var third = files.ReadState("three")!;
        Assert.Equal(CheckStates.Queued, third.State);
        Assert.Equal("a slot: 2 of 2 run", third.Wait);

        release.GetOrAdd("one", _ => new TaskCompletionSource()).SetResult();
        await UntilAsync(() => started.Count == 3, "the third to run once one ended");
        release.GetOrAdd("two", _ => new TaskCompletionSource()).SetResult();
        release.GetOrAdd("three", _ => new TaskCompletionSource()).SetResult();
        await running.WaitAsync(Poll.Patience);

        Assert.Equal(["one", "two", "three"], started);
        Assert.All(new[] { "one", "two", "three" }, name => Assert.Equal(CheckStates.Reached, files.ReadState(name)!.State));
    }

    [Fact]
    public async Task With_memory_for_one_one_check_runs_and_the_next_waits_naming_the_memory()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("one", 0, session: "one"));
        files.WriteAsk(Ask("two", 1, session: "two"));
        var started = new ConcurrentQueue<string>();
        var release = new ConcurrentDictionary<string, TaskCompletionSource>();
        var broker = new CheckBroker(_home, (session, _) => Task.FromResult<string?>(TreeOf(session)), Holding(started, release))
        {
            Rules = () => Room(7, processors: 16), Waiting = Quick, Poll = TimeSpan.FromMilliseconds(20),
        };

        var running = Look(broker);
        await UntilAsync(() => files.ReadState("two")?.Wait is not null, "the second waiting");

        Assert.Equal(["one"], started);
        Assert.Contains("memory", files.ReadState("two")!.Wait);
        release.GetOrAdd("one", _ => new TaskCompletionSource()).SetResult();
        await UntilAsync(() => started.Count == 2, "the second to run once the first ended");
        release.GetOrAdd("two", _ => new TaskCompletionSource()).SetResult();
        await running.WaitAsync(Poll.Patience);
    }

    [Fact]
    public async Task An_urgent_quests_check_asked_after_ordinary_ones_takes_the_next_freed_slot()
    {
        var files = new CheckFiles(_home);
        var rules = Room(20, processors: 8, setting: 1);
        var holder = new HeavyHolder(Environment.ProcessId, Started, _tree, ["x"], Clock.GetUtcNow(), null, null) { Now = new HeavyGateNow("x", 0, Clock.GetUtcNow()) };
        var outside = CheckAdmission.TryTake(_home, rules, new HeavyAsk("outside", "outside", null, () => holder)).Taken!;
        foreach (var (name, minute) in new[] { ("one", 0), ("two", 1), ("three", 2), ("urgent", 3) }) files.WriteAsk(Ask(name, minute, session: name));
        Assert.Null(PriorityBook.Set(_home, new PriorityEvent(At, PriorityBook.Quest, "q-urgent", "games", PriorityClasses.Urgent, "the fix that slows every session", "the person") { Until = Clock.GetUtcNow().AddHours(1) }, At, PriorityRules.Default));
        var started = new ConcurrentQueue<string>();
        var release = new ConcurrentDictionary<string, TaskCompletionSource>();
        var broker = new CheckBroker(_home, (session, _) => Task.FromResult<string?>(TreeOf(session)), Holding(started, release))
        {
            Rules = () => rules, Waiting = Quick, Poll = TimeSpan.FromMilliseconds(20), QuestOf = session => session == "urgent" ? "q-urgent" : null,
        };

        var running = Look(broker);
        await UntilAsync(() => new[] { "one", "two", "three", "urgent" }.All(name => files.ReadState(name)?.Wait is not null), "every ask waiting for the slot an outside run holds");
        Assert.Empty(started);

        outside.Dispose();
        await UntilAsync(() => started.Count == 1, "the first of the order to take the freed slot");

        Assert.Equal(["urgent"], started);
        foreach (var name in new[] { "urgent", "one", "two", "three" })
        {
            release.GetOrAdd(name, _ => new TaskCompletionSource()).SetResult();
            await UntilAsync(() => files.ReadState(name)?.State == CheckStates.Reached, $"{name} to end");
        }

        await running.WaitAsync(Poll.Patience);
        Assert.Equal(["urgent", "one", "two", "three"], started);
    }
}
