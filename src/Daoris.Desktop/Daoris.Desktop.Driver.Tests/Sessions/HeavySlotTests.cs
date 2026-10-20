using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HEAVYRUN1a (D180's HEAVYRUN1 note): one heavy-run slot per repository, across its trees. Two driven sessions on one repository
/// each ran its full build and suites at once; the slot is what makes the second wait. Files only, so it is the fast half's: the
/// processes a run holds are <see cref="HeavyRunProcessTests"/>'.
/// </summary>
public sealed class HeavySlotTests : IDisposable
{
    private readonly HeavyFixture _fixture = new("heavy-slot");

    public void Dispose() => _fixture.Dispose();

    private string Key(string checkout) => HeavySlot.KeyOf(checkout) ?? throw new InvalidOperationException($"{checkout} is no checkout");

    /// <summary>The handle is the lock: a second take is refused while the first holds it, and taken once it lets go.</summary>
    [Fact]
    public void A_second_take_is_refused_while_the_slot_is_held_and_taken_once_it_is_let_go()
    {
        var key = Key(_fixture.Checkout);
        var first = HeavySlot.TryTake(_fixture.Home, key);
        Assert.NotNull(first);

        Assert.Null(HeavySlot.TryTake(_fixture.Home, key));

        first!.Dispose();
        using var second = HeavySlot.TryTake(_fixture.Home, key);
        Assert.NotNull(second);
    }

    /// <summary>
    /// 🔴 Keyed by the repository, never the tree: a root checkout and a linked tree of it share one slot, read from git's own files
    /// (its common folder), and another repository is its own.
    /// </summary>
    [Fact]
    public void A_checkout_and_its_linked_tree_share_one_slot_and_another_repository_has_its_own()
    {
        Assert.Equal(Key(_fixture.Checkout), Key(_fixture.Tree));
        Assert.NotEqual(Key(_fixture.Checkout), Key(_fixture.Other));

        using var root = HeavySlot.TryTake(_fixture.Home, Key(_fixture.Checkout));
        Assert.NotNull(root);
        Assert.Null(HeavySlot.TryTake(_fixture.Home, Key(_fixture.Tree)));
        using var other = HeavySlot.TryTake(_fixture.Home, Key(_fixture.Other));
        Assert.NotNull(other);
    }

    /// <summary>A folder inside a checkout is that checkout's; the spelling that reaches it does not change the slot.</summary>
    [Fact]
    public void A_folder_inside_a_checkout_and_another_spelling_of_it_name_the_same_slot()
    {
        var inside = Directory.CreateDirectory(Path.Combine(_fixture.Tree, "src", "web")).FullName;

        Assert.Equal(_fixture.Tree, HeavySlot.CheckoutOf(inside));
        Assert.Equal(Key(_fixture.Checkout), Key(inside));
        Assert.Equal(Key(_fixture.Checkout), Key(_fixture.Checkout + Path.DirectorySeparatorChar));
        if (OperatingSystem.IsWindows()) Assert.Equal(Key(_fixture.Checkout), Key(_fixture.Checkout.ToUpperInvariant()));
    }

    /// <summary>The repository's name a waiting run is told: the folder its common git folder is in, whichever tree asks.</summary>
    [Fact]
    public void The_repository_is_named_by_its_checkout_from_any_tree()
    {
        Assert.Equal("engine", HeavySlot.RepositoryOf(_fixture.Tree));
        Assert.Equal("engine", HeavySlot.RepositoryOf(_fixture.Checkout));
    }

    /// <summary>Under the home's own <c>locks/</c>, as every lock is, through the home's table, never a path joined by hand.</summary>
    [Fact]
    public void The_slot_and_its_record_live_under_the_home_s_locks()
    {
        var key = Key(_fixture.Checkout);
        var locks = DaorisHome.Entry(_fixture.Home, HomeEntry.Locks) + Path.DirectorySeparatorChar;

        Assert.StartsWith(locks, HeavySlot.PathOf(_fixture.Home, key), StringComparison.Ordinal);
        Assert.StartsWith(locks, HeavySlot.RecordOf(_fixture.Home, key), StringComparison.Ordinal);
        Assert.NotEqual(HeavySlot.PathOf(_fixture.Home, key), HeavySlot.RecordOf(_fixture.Home, key));
    }

    /// <summary>
    /// The holder says what it runs beside the lock, for a waiting run to say, and takes it away as it lets go; a run that takes the
    /// slot after one that died holding it finds that one's record, and with it the job its processes were in.
    /// </summary>
    [Fact]
    public void The_holder_s_record_is_read_while_it_holds_and_gone_once_it_lets_go_and_a_dead_holder_s_is_found_by_the_next()
    {
        var key = Key(_fixture.Checkout);
        var holder = new HeavyHolder(4242, 638000000000000000, _fixture.Tree, ["build", "web"], DateTimeOffset.Parse("2026-10-19T09:00:00Z"), "s-12", "daoris-heavy-x");

        var slot = HeavySlot.TryTake(_fixture.Home, key)!;
        Assert.Null(slot.Before);
        slot.Keep(holder);

        var read = HeavySlot.Holder(_fixture.Home, key);
        Assert.NotNull(read);
        Assert.Equal((4242, 638000000000000000L, _fixture.Tree, "s-12", "daoris-heavy-x"), (read!.Pid, read.Started, read.Checkout, read.Session, read.Job));
        Assert.Equal(["build", "web"], read.Gates);
        Assert.Equal(holder.Since, read.Since);

        slot.Dispose();
        Assert.Null(HeavySlot.Holder(_fixture.Home, key));

        // One that died holding it never let go of its record: the next to take the slot reads it as the run before.
        File.WriteAllText(HeavySlot.RecordOf(_fixture.Home, key), HeavySlot.Serialize(holder));
        using var next = HeavySlot.TryTake(_fixture.Home, key)!;
        Assert.Equal(4242, next.Before?.Pid);
        Assert.Equal("daoris-heavy-x", next.Before?.Job);
    }

    /// <summary>A record that does not read is no holder, never a reason to refuse the slot.</summary>
    [Fact]
    public void A_record_that_does_not_read_is_no_holder()
    {
        var key = Key(_fixture.Checkout);
        Directory.CreateDirectory(Path.GetDirectoryName(HeavySlot.RecordOf(_fixture.Home, key))!);
        File.WriteAllText(HeavySlot.RecordOf(_fixture.Home, key), "{ not json");

        Assert.Null(HeavySlot.Holder(_fixture.Home, key));
        using var slot = HeavySlot.TryTake(_fixture.Home, key);
        Assert.NotNull(slot);
        Assert.Null(slot!.Before);
    }

    /// <summary>
    /// The waiting side says it: behind which run (its session, its gates, its tree) and how long that run has run, then again at its
    /// cadence with how long this one has waited, until the slot is its own. Never a timed lease: it waits as long as the holder holds.
    /// </summary>
    [Fact]
    public async Task A_waiting_run_says_whom_it_waits_behind_and_for_how_long_and_takes_the_slot_once_it_is_let_go()
    {
        var key = Key(_fixture.Checkout);
        var since = DateTimeOffset.Parse("2026-10-19T09:00:00Z");
        var holding = HeavySlot.TryTake(_fixture.Home, key)!;
        holding.Keep(new HeavyHolder(4242, 1, _fixture.Checkout, ["build", "web"], since, "s-12", null));

        var said = new SaidWriter();
        var waiting = new HeavyWaiting(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(100), () => since + TimeSpan.FromSeconds(135));
        var taking = HeavySlot.TakeAsync(_fixture.Home, key, "engine", said, CancellationToken.None, waiting);

        await Poll.Until(() => said.Lines.Count(line => line.Contains("waits for it", StringComparison.Ordinal)) >= 2, () => said.Text);
        Assert.False(taking.IsCompleted);

        var first = said.Lines[0];
        Assert.Contains("`engine`'s heavy-run slot is held by session s-12 (process 4242)", first);
        Assert.Contains("running build, web for 2m 15s", first);
        Assert.Contains(_fixture.Checkout, first);

        holding.Dispose();
        using var taken = await taking.WaitAsync(Poll.Patience);
        Assert.Contains("`engine`'s heavy-run slot is this run's, after waiting", said.Lines[^1]);
    }

    /// <summary>A run that is stopped while it waits holds nothing, and says nothing more.</summary>
    [Fact]
    public async Task A_run_stopped_while_it_waits_holds_nothing()
    {
        var key = Key(_fixture.Checkout);
        using var holding = HeavySlot.TryTake(_fixture.Home, key)!;
        using var stop = new CancellationTokenSource();

        var said = new SaidWriter();
        var taking = HeavySlot.TakeAsync(_fixture.Home, key, "engine", said, stop.Token, new HeavyWaiting(TimeSpan.FromMilliseconds(20), TimeSpan.FromMinutes(1), () => DateTimeOffset.UtcNow));
        await Poll.Until(() => said.Text.Contains("a run that has not said what it runs yet", StringComparison.Ordinal), () => said.Text);

        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => taking.WaitAsync(Poll.Patience));
        holding.Dispose();
        using var free = HeavySlot.TryTake(_fixture.Home, key);
        Assert.NotNull(free);
    }

    /// <summary>
    /// 🔴 HEAVYSLOT1 (the UI/UX design §3.5), two runs: the holder's record says the gate it runs and since when, the run waiting
    /// behind it keeps a record of its own while it waits, and the look reads both; once the holder lets go, the waiter takes the slot
    /// and its wait is gone. Read from the records, never a run's output.
    /// </summary>
    [Fact]
    public async Task Two_runs_the_look_says_the_holder_at_its_gate_and_the_one_waiting_until_it_takes_the_slot()
    {
        var key = Key(_fixture.Checkout);
        var since = DateTimeOffset.Parse("2026-10-20T09:00:00Z");
        var holding = HeavySlot.TryTake(_fixture.Home, key)!;
        holding.Keep(new HeavyHolder(4242, 1, _fixture.Checkout, ["build", "web"], since, "s-12", null)
        {
            Now = new HeavyGateNow("web", 1, since + TimeSpan.FromMinutes(5)),
        });

        var waiter = new HeavyWaiter(5151, 2, _fixture.Tree, since + TimeSpan.FromMinutes(6), "s-34");
        var waiting = new HeavyWaiting(TimeSpan.FromMilliseconds(20), TimeSpan.FromMinutes(1), () => since);
        var taking = HeavySlot.TakeAsync(_fixture.Home, key, "engine", new SaidWriter(), CancellationToken.None, waiting, waiter);
        await Poll.Until(() => HeavySlot.Look(_fixture.Home, key, (_, _) => true).Waiters.Count == 1, () => "no waiter was recorded");

        var look = HeavySlot.Look(_fixture.Home, key, (_, _) => true);
        Assert.Equal(("s-12", "web", 1), (look.Holder?.Session, look.Holder?.Now?.Gate, look.Holder?.Now?.At));
        Assert.Equal(since + TimeSpan.FromMinutes(5), look.Holder?.Now?.Since);
        Assert.Equal(waiter, Assert.Single(look.Waiters));

        holding.Dispose();
        using var taken = await taking.WaitAsync(Poll.Patience);
        var after = HeavySlot.Look(_fixture.Home, key, (_, _) => true);
        Assert.Null(after.Holder);
        Assert.Empty(after.Waiters);
        Assert.Empty(Directory.EnumerateFiles(HeavySlot.WaitersOf(_fixture.Home, key)));
    }

    /// <summary>
    /// 🔴 The handle is the lock: a holder's or a waiter's record whose process (its id and its start) is gone holds nothing and waits
    /// for nothing, though its file is still there.
    /// </summary>
    [Fact]
    public void A_record_left_by_a_run_that_died_is_no_holder_and_no_waiter()
    {
        var key = Key(_fixture.Checkout);
        var since = DateTimeOffset.Parse("2026-10-20T09:00:00Z");
        Directory.CreateDirectory(HeavySlot.WaitersOf(_fixture.Home, key));
        File.WriteAllText(HeavySlot.RecordOf(_fixture.Home, key), HeavySlot.Serialize(new HeavyHolder(4242, 1, _fixture.Checkout, ["build"], since, "s-12", null)));
        File.WriteAllText(
            Path.Combine(HeavySlot.WaitersOf(_fixture.Home, key), "5151-2.json"),
            HeavySlot.SerializeWaiter(new HeavyWaiter(5151, 2, _fixture.Tree, since, "s-34")));
        File.WriteAllText(
            Path.Combine(HeavySlot.WaitersOf(_fixture.Home, key), "6161-3.json"),
            HeavySlot.SerializeWaiter(new HeavyWaiter(6161, 3, _fixture.Tree, since, null)));

        // A reused id is another process: alive only where the id and its start both match.
        var look = HeavySlot.Look(_fixture.Home, key, (pid, started) => pid == 6161 && started == 3);

        Assert.Null(look.Holder);
        Assert.Equal(6161, Assert.Single(look.Waiters).Pid);
        Assert.False(HeavySlot.Running(4242, 1));
        Assert.True(HeavySlot.Running(Environment.ProcessId, System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks));
    }

    /// <summary>The gate running and the gate that failed read back as written; a record older than them reads with neither.</summary>
    [Fact]
    public void The_gate_running_and_the_gate_that_failed_read_back_and_an_older_record_has_neither()
    {
        var key = Key(_fixture.Checkout);
        var since = DateTimeOffset.Parse("2026-10-20T09:00:00Z");
        using var slot = HeavySlot.TryTake(_fixture.Home, key)!;
        slot.Keep(new HeavyHolder(4242, 1, _fixture.Checkout, ["build", "web"], since, null, null)
        {
            Now = new HeavyGateNow("web", 1, since + TimeSpan.FromMinutes(2)),
            Failed = new HeavyGateFailed("web", 3),
        });

        var read = HeavySlot.Holder(_fixture.Home, key)!;
        Assert.Equal(new HeavyGateNow("web", 1, since + TimeSpan.FromMinutes(2)), read.Now);
        Assert.Equal(new HeavyGateFailed("web", 3), read.Failed);

        File.WriteAllText(HeavySlot.RecordOf(_fixture.Home, key),
            """{ "pid": 4242, "started": 1, "checkout": "x", "gates": ["build"], "since": "2026-10-20T09:00:00Z" }""");
        var older = HeavySlot.Holder(_fixture.Home, key)!;
        Assert.Null(older.Now);
        Assert.Null(older.Failed);
    }

    /// <summary>A free slot is taken at once, with nothing said.</summary>
    [Fact]
    public async Task A_free_slot_is_taken_at_once_saying_nothing()
    {
        var said = new SaidWriter();
        using var slot = await HeavySlot.TakeAsync(_fixture.Home, Key(_fixture.Checkout), "engine", said, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal("", said.Text);
    }
}
