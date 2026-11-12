using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git, a fixture act, a ref step, a configuration step and an index step,
/// run under the checkout lock with each effect step in a job named for the act, its deadline journaled; then cut at every boundary, past its
/// bound and with a child that would not end, and reconciled by another run, nothing run again. The reconciler at a writer's lock (a sync's
/// look, a promotion's step) and at the driver's start; git's own locks; the act refs, never a holder and expired at their journaled id.
/// </summary>
/// <remarks>
/// A crash is the act's flow thrown out of at a boundary, its job's last handle closed as a dead process's is, and the run it wrote under then
/// read as dead; the reconciler is a journal of another run. Windows only, where the job is: elsewhere an act is refused, never run unheld.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ActRunGitTests : TreeSyncFixture
{
    private sealed class Killed : Exception;

    private const string Act = "refs/heads/feature/act";

    private ActJournal Journal(string run, Func<StoreRun, bool>? alive = null) => new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    private static Func<StoreRun, bool> DeadAre(params string[] runs) => run => !runs.Contains(run.Run);

    private ActReconciler Reconciler(string run, Func<StoreRun, bool>? alive = null) => new(Journal(run, alive)) { ExitBound = TimeSpan.FromSeconds(2) };

    private ActRuns Runs(string run = "run-a") => new(Journal(run)) { ExitBound = TimeSpan.FromSeconds(2) };

    /// <summary>The run's process read as one that ended long ago, as the lease's test reads a dead holder.</summary>
    private void KillRun(string run)
    {
        using var connection = new SqliteConnection($"Data Source={DesktopStore.LocationIn(Home)};Pooling=false");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE store_runs SET pid = 2147483000, started = 1 WHERE run = $run";
        command.Parameters.AddWithValue("$run", run);
        Assert.Equal(1, command.ExecuteNonQuery());
    }

    /// <summary>A checkout with a `topic` section to move and `shared.txt` changed in its tree, and what the act's three steps would do there.</summary>
    private async Task<(string Root, string Tip, string Before, string After)> CheckoutAsync()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "config", "branch.topic.remote", "origin");
        await GitAsync(root, "config", "branch.topic.merge", "refs/heads/topic");
        await File.WriteAllTextAsync(Path.Combine(root, "shared.txt"), "two\n");
        var after = (await GitAsync(root, "hash-object", "-w", "shared.txt")).Trim();
        var before = (await GitAsync(root, "rev-parse", ":shared.txt")).Trim();
        return (root, await RevAsync(root, "main"), before, after);
    }

    private static ActStepPlan RefStep(string tip) => ActSteps.Ref(new RefTransaction().Create(Act, tip), [(Act, null, tip)]);

    private static async Task<ActStepPlan> ConfigStepAsync(string root) =>
        ActSteps.Config(Path.Combine(root, ".git", "config"), "local", "branch.topic", "branch.feature/act",
            (await ActConfig.SectionAsync(Path.Combine(root, ".git", "config"), "branch.topic"))!);

    private static ActStepPlan IndexStep(string root, string before, string after) =>
        ActSteps.Index(Path.Combine(root, ".git"), [new ActEntry("100644", before, 0, "shared.txt")], [new ActEntry("100644", after, 0, "shared.txt")]);

    private static ActPlan Plan(string root, params ActStepPlan[] steps) => new("fixture", "test", "engine", root, steps);

    private async Task<ActPlan> FixtureAsync(string root, string tip, string before, string after) =>
        Plan(root, RefStep(tip), await ConfigStepAsync(root), IndexStep(root, before, after)) with { Keep = [new ActEntry("100644", before, 0, "shared.txt")] };

    /// <summary>A `reference-transaction` hook that notes each state it is run for, and at `prepared` starts a child that outlives it and waits.</summary>
    private static async Task<(string Noted, string Child)> HookAsync(string root)
    {
        var noted = Path.Combine(root, ".git", "noted.txt");
        var child = Path.Combine(root, ".git", "child.txt");
        var hook = Path.Combine(root, ".git", "hooks", "reference-transaction");
        Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        await File.WriteAllTextAsync(hook, $$"""
            #!/bin/sh
            echo "$1" >> '{{noted.Replace('\\', '/')}}'
            if [ "$1" = "prepared" ]; then
              sleep 600 &
              cat /proc/$!/winpid > '{{child.Replace('\\', '/')}}'
              sleep 600
            fi
            """.Replace("\r\n", "\n"));
        return (noted, child);
    }

    /// <summary>The hook's child's pid, once the hook has written it whole.</summary>
    private static async Task<int> ChildOfAsync(string file)
    {
        for (var tries = 0; tries < 400; tries++)
        {
            try
            {
                if (File.Exists(file) && File.ReadAllText(file) is var said && said.EndsWith('\n') && int.TryParse(said.Trim(), out var pid)) return pid;
            }
            catch (IOException)
            {
                // The hook is writing it.
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("the hook never wrote its child's pid");
    }

    private static bool Lives(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task<string?> RefAsync(string root, string name)
    {
        var listed = (await GitAsync(root, "for-each-ref", "--format=%(refname) %(objectname)", name)).Trim();
        return listed.Split('\n').Select(row => row.Split(' ')).FirstOrDefault(parts => parts[0] == name)?[1];
    }

    // ——— The act run whole.

    [Fact]
    public async Task The_fixture_act_moves_its_ref_section_and_entry_each_in_its_step_and_closes_done()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, before, after) = await CheckoutAsync();
        var plan = await FixtureAsync(root, tip, before, after);

        var run = await Runs().RunAsync(plan, CancellationToken.None);

        Assert.Equal((ActOutcomes.Done, false), (run.Outcome, run.Refused));
        Assert.Equal(tip, await RefAsync(root, Act));
        var config = await File.ReadAllTextAsync(Path.Combine(root, ".git", "config"));
        Assert.Contains("[branch \"feature/act\"]", config);
        Assert.DoesNotContain("[branch \"topic\"]", config);
        Assert.Equal(after, (await GitAsync(root, "rev-parse", ":shared.txt")).Trim());
        Assert.False(File.Exists(Path.Combine(root, ".git", "index.lock")));
        Assert.False(File.Exists(Path.Combine(root, ".git", "config.lock")));

        var act = Journal("run-a").Act(run.Act!)!;
        Assert.Equal((ActPhases.Closed, ActOutcomes.Done), (act.Phase, act.Outcome));
        var steps = Journal("run-a").Steps(run.Act!);
        Assert.All(steps, step => Assert.Equal(ActOutcomes.Done, step.Outcome));
        Assert.True(steps[0].Pid > 0 && steps[0].Deadline > steps[0].BeganAt, "the ref step's process and deadline are journaled");
        // The touched entry's blob is held by the act's own ref until the record expires, and that ref holds nothing for the clean-ups.
        var kept = (await GitAsync(root, "ls-tree", ActRefs.Of(run.Act!))).Trim();
        Assert.Contains(before, kept);
        Assert.True(act.ExpiresAt > DateTimeOffset.UtcNow.AddDays(29));
        Assert.False(ContentHolds.AnyRef(ActRefs.Of(run.Act!)));
    }

    [Fact]
    public async Task An_act_is_refused_while_another_writer_holds_the_checkout_and_before_its_first_effect_a_cancel_closes_it_not_done()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, _, _) = await CheckoutAsync();
        // Another writer's hold, taken on a flow of its own: a flow that holds the checkout alone runs its own acts under its hold.
        using (await Task.Run(() => TreeLock.TryWriting(Home, root, "engine", TreeLock.Updating)))
        {
            var held = await Runs().RunAsync(Plan(root, RefStep(tip)), CancellationToken.None);
            Assert.True(held.Refused);
            Assert.Null(held.Act);
            Assert.Contains("brought up to date", held.Said);
        }

        using var watch = new CancellationTokenSource();
        var runs = Runs();
        runs.Seam = (point, _) =>
        {
            if (point == ActSeams.Prepared) watch.Cancel();
            return Task.CompletedTask;
        };

        var cancelled = await runs.RunAsync(Plan(root, RefStep(tip)), watch.Token);

        Assert.Equal(ActOutcomes.NotDone, cancelled.Outcome);
        Assert.Null(await RefAsync(root, Act));
        Assert.All(Journal("run-a").Steps(cancelled.Act!), step => Assert.Null(step.BeganAt));
        Assert.False(Journal("run-a").Act(cancelled.Act!)!.Open);
    }

    /// <summary>🔴 Closing the terminal, or Ctrl-C, after the first effect step has begun ends only the watch: the act runs to its end.</summary>
    [Fact]
    public async Task The_watch_closed_mid_step_ends_only_the_watch_and_the_act_runs_to_its_close()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, before, after) = await CheckoutAsync();
        using var watch = new CancellationTokenSource();
        var runs = Runs();
        runs.Seam = (point, ordinal) =>
        {
            if (point == ActSeams.AfterLaunch && ordinal == 0) watch.Cancel();
            return Task.CompletedTask;
        };

        var run = await runs.RunAsync(await FixtureAsync(root, tip, before, after), watch.Token);

        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal(tip, await RefAsync(root, Act));
        Assert.False(Journal("run-a").Act(run.Act!)!.Open);
    }

    // ——— Cut at every boundary, then reconciled by another run, nothing run again.

    /// <summary>
    /// 🔴 A crash before the step's process, before its pid is written, and in a hook's child: the job's last handle goes with the dead process
    /// and ends everything in it, the hook's child too; another run then takes the act, reads its postconditions, and closes it, the hook never
    /// run again.
    /// </summary>
    [Theory]
    [InlineData(ActSeams.BeforeLaunch)]
    [InlineData(ActSeams.BeforePid)]
    [InlineData("in-hook")]
    public async Task A_crash_at_each_boundary_is_reconciled_by_another_run_and_nothing_is_run_again(string boundary)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, _, _) = await CheckoutAsync();
        var (noted, child) = await HookAsync(root);
        var runs = Runs();
        var descendant = 0;
        runs.Seam = async (point, _) =>
        {
            if (point == boundary) throw new Killed();
            if (boundary == "in-hook" && point == ActSeams.AfterLaunch)
            {
                descendant = await ChildOfAsync(child);
                Assert.True(ProcessJob.ActiveOf(ActJournal.JobOf(runs.Running!)) >= 3, "git, the hook and its child are in the act's job");
                throw new Killed();
            }
        };

        await Assert.ThrowsAsync<Killed>(() => runs.RunAsync(Plan(root, RefStep(tip)), CancellationToken.None));
        var act = Journal("run-a").Unclosed().Single();
        Assert.False(ProcessJob.Exists(act.Job), "the act's job went with its last handle");
        if (descendant != 0) Assert.False(Lives(descendant), "the hook's child ended with the job");
        var hooked = File.Exists(noted) ? await File.ReadAllTextAsync(noted) : "";
        Assert.Equal(boundary == "in-hook" ? "prepared\n" : "", hooked.Replace("\r\n", "\n"));

        KillRun("run-a");
        var hold = await Reconciler("run-b", DeadAre("run-a")).UnderLockAsync(root, CancellationToken.None);

        Assert.Null(hold);
        var closed = Journal("run-b").Act(act.Id)!;
        Assert.Equal((ActPhases.Closed, ActOutcomes.NotDone, 2L), (closed.Phase, closed.Outcome, closed.ClaimGeneration));
        Assert.Null(await RefAsync(root, Act));
        Assert.Equal(hooked, File.Exists(noted) ? await File.ReadAllTextAsync(noted) : "");
        var step = Journal("run-b").Steps(act.Id).Single();
        Assert.Equal(boundary == ActSeams.BeforeLaunch ? null : step.Pid, step.Pid);
        Assert.Contains("no end", step.Said);
    }

    /// <summary>🔴 A step past its bound: its tree is ended, the hook's child with it, and its postconditions decide; the hook is not run again.</summary>
    [Fact]
    public async Task A_step_past_its_bound_is_ended_with_its_hooks_child_and_its_postconditions_decide()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, _, _) = await CheckoutAsync();
        var (noted, child) = await HookAsync(root);
        var runs = Runs();
        runs.Bound = _ => TimeSpan.FromSeconds(3);
        var descendant = 0;
        runs.Seam = async (point, _) =>
        {
            if (point == ActSeams.AfterLaunch) descendant = await ChildOfAsync(child);
        };

        var run = await runs.RunAsync(Plan(root, RefStep(tip)), CancellationToken.None);

        Assert.Equal(ActOutcomes.NotDone, run.Outcome);
        Assert.False(Lives(descendant));
        var step = Journal("run-a").Steps(run.Act!).Single();
        Assert.Equal((false, (int?)null), (step.Unended, step.ExitCode));
        Assert.Contains("bound", step.Said);
        Assert.Equal("prepared\n", (await File.ReadAllTextAsync(noted)).Replace("\r\n", "\n"));
        Assert.False(Journal("run-a").Act(run.Act!)!.Open);
    }

    /// <summary>A step's bound runs from the moment its git is placed: one that queued past the whole bound still gets it.</summary>
    [Fact]
    public async Task A_step_whose_git_queued_past_its_bound_still_has_its_bound_once_placed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, _, _) = await CheckoutAsync();
        var runs = Runs();
        runs.Bound = _ => TimeSpan.FromSeconds(2);
        runs.Seam = async (point, _) =>
        {
            if (point == ActSeams.BeforeLaunch) await Task.Delay(TimeSpan.FromSeconds(3));
        };

        var run = await runs.RunAsync(Plan(root, RefStep(tip)), CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal(tip, await RefAsync(root, Act));
        var step = Journal("run-a").Steps(run.Act!).Single();
        Assert.True(step.Deadline > step.BeganAt.GetValueOrDefault().AddSeconds(3), "the journalled deadline is taken once the git is placed");
    }

    /// <summary>🔴 A step whose processes would not end: said unended with its pid, its outcome unknown, the act held open for a reconciler, never failed.</summary>
    [Fact]
    public async Task A_step_whose_child_would_not_end_is_said_unended_and_the_act_is_held_for_the_reconciler()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, _, _) = await CheckoutAsync();
        await HookAsync(root);
        var runs = Runs();
        runs.Bound = _ => TimeSpan.FromSeconds(2);
        runs.Ending = _ => { };

        var run = await runs.RunAsync(Plan(root, RefStep(tip)), CancellationToken.None);

        Assert.Equal(ActOutcomes.Unknown, run.Outcome);
        var step = Journal("run-a").Steps(run.Act!).Single();
        Assert.True(step.Unended);
        Assert.Contains($"pid {step.Pid}", run.Said);
        Assert.Contains("nothing was retried", run.Said);
        Assert.True(Journal("run-a").Act(run.Act!)!.Open);

        // Its process ended with the job's last handle as the run let go: the next writer's reconciliation closes it, nothing retried.
        KillRun("run-a");
        Assert.Null(await Reconciler("run-b", DeadAre("run-a")).UnderLockAsync(root, CancellationToken.None));
        Assert.Equal(ActOutcomes.NotDone, Journal("run-b").Act(run.Act!)!.Outcome);
    }

    // ——— A live step, an orphan, two reconcilers, the store busy, a newer build's record.

    /// <summary>An open act left by <paramref name="run"/> in <paramref name="root"/>, its ref step begun with <paramref name="deadline"/> and, where given, a process in its job.</summary>
    private (ActRecord Act, ProcessJob? Job, HeldChild? Sleeper) Orphan(string root, string tip, string run, DateTimeOffset deadline, bool sleeper)
    {
        var id = Guid.NewGuid().ToString("N");
        var journal = Journal(run);
        var claim = journal.Open(new ActOpening(id, "fixture", "test", "engine", root, TreeLock.CommonDirOf(root), "{}", [RefStep(tip)]));
        journal.Prepared(claim);
        journal.Begin(claim, 0, deadline);
        ProcessJob? job = null;
        HeldChild? child = null;
        if (sleeper)
        {
            job = ProcessJob.CreateNamed(ActJournal.JobOf(id)).Job!;
            child = HeldChild.Start(job, new ProcessStartInfo("ping", ["-n", "600", "127.0.0.1"]) { WorkingDirectory = root }, input: false);
            journal.Launched(claim, 0, child.Pid, child.Started);
            child.Resume();
        }

        return (journal.Act(id)!, job, child);
    }

    [Fact]
    public async Task A_live_step_refuses_every_writer_naming_the_act_and_past_its_deadline_is_ended_and_reconciled()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, _, _) = await CheckoutAsync();
        var (act, job, sleeper) = Orphan(root, tip, "run-a", DateTimeOffset.UtcNow.AddMinutes(5), sleeper: true);
        using var _ = job;
        using var __ = sleeper;
        KillRun("run-a");

        var held = await Reconciler("run-b", DeadAre("run-a")).UnderLockAsync(root, CancellationToken.None);

        Assert.NotNull(held);
        Assert.Equal(act.Id, held!.Act);
        Assert.Contains($"pid {sleeper!.Pid}", held.Said);
        Assert.Contains("runs until", held.Said);
        Assert.True(Journal("run-b").Act(act.Id)!.Open);
        Assert.True(Lives(sleeper.Pid));

        // Unended: the end refused, said with its pid and a Stop, still open.
        var stubborn = Reconciler("run-c", DeadAre("run-a", "run-b"));
        stubborn.Ending = _ => { };
        stubborn.Now = () => DateTimeOffset.UtcNow.AddMinutes(10);
        var unended = await stubborn.UnderLockAsync(root, CancellationToken.None);
        Assert.Contains("did not end", unended!.Said);
        Assert.Contains($"pid {sleeper.Pid}", unended.Said);
        Assert.Contains("Stop", unended.Said);
        Assert.True(Lives(sleeper.Pid));

        // Past its deadline: the job ended and awaited empty, then the postconditions decide.
        var later = Reconciler("run-d", DeadAre("run-a", "run-b", "run-c"));
        later.Now = () => DateTimeOffset.UtcNow.AddMinutes(10);
        Assert.Null(await later.UnderLockAsync(root, CancellationToken.None));
        Assert.False(Lives(sleeper.Pid));
        var closed = Journal("run-d").Act(act.Id)!;
        Assert.Equal((ActPhases.Closed, ActOutcomes.NotDone), (closed.Phase, closed.Outcome));
        Assert.Contains("deadline", Journal("run-d").Steps(act.Id).Single().Said);
    }

    [Fact]
    public async Task An_act_of_a_live_run_refuses_the_writer_and_two_reconcilers_at_once_close_a_dead_ones_once()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, _, _) = await CheckoutAsync();
        var (live, _, _) = Orphan(root, tip, "run-a", DateTimeOffset.UtcNow.AddMinutes(5), sleeper: false);

        var held = await Reconciler("run-b").UnderLockAsync(root, CancellationToken.None);
        Assert.Contains("still runs", held!.Said);
        Assert.Equal(live.Id, held.Act);

        KillRun("run-a");
        var dead = DeadAre("run-a");
        var both = await Task.WhenAll(
            Task.Run(() => Reconciler("run-b", dead).UnderLockAsync(root, CancellationToken.None)),
            Task.Run(() => Reconciler("run-c", dead).UnderLockAsync(root, CancellationToken.None)));

        // One wins the take and closes it; the other is refused the take, and is held for its look unless the winner closed it first.
        Assert.Contains(null, both);
        Assert.All(both.OfType<ActHold>(), hold => Assert.Contains("another reconciler took it first", hold.Said));
        var closed = Journal("run-b").Act(live.Id)!;
        Assert.Equal((ActPhases.Closed, 2L), (closed.Phase, closed.ClaimGeneration));
    }

    [Fact]
    public async Task A_reconciler_that_reads_the_act_after_another_took_it_is_held_as_taken_first_and_the_taker_closes_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, _, _) = await CheckoutAsync();
        var (act, _, _) = Orphan(root, tip, "run-a", DateTimeOffset.UtcNow.AddMinutes(5), sleeper: false);
        KillRun("run-a");
        var dead = DeadAre("run-a");

        // The at-once race forced: run-b's take lands, then run-c reads the act, before run-b reads its steps and closes it.
        Assert.NotNull(Journal("run-b", dead).Take(act.Id, act.ClaimGeneration).Claim);
        var late = await Reconciler("run-c", dead).UnderLockAsync(root, CancellationToken.None);
        Assert.Contains("another reconciler took it first", late!.Said);

        Assert.Null(await Reconciler("run-b", dead).UnderLockAsync(root, CancellationToken.None));
        var closed = Journal("run-b").Act(act.Id)!;
        Assert.Equal((ActPhases.Closed, "run-b", 2L), (closed.Phase, closed.ClaimRun, closed.ClaimGeneration));
    }

    [Fact]
    public async Task The_driver_s_start_leaves_a_checkout_while_the_store_is_busy_and_reconciles_it_at_the_next_look()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, _, _) = await CheckoutAsync();
        var (act, _, _) = Orphan(root, tip, "run-a", DateTimeOffset.UtcNow.AddMinutes(5), sleeper: false);
        KillRun("run-a");

        using (var busy = new SqliteConnection($"Data Source={DesktopStore.LocationIn(Home)};Pooling=false"))
        {
            busy.Open();
            using var hold = busy.BeginTransaction(deferred: false);
            var started = Stopwatch.StartNew();
            var said = await Reconciler("run-b", DeadAre("run-a")).StartAsync(CancellationToken.None);
            Assert.Contains(said, line => line.Contains("busy", StringComparison.Ordinal) && line.Contains("next look", StringComparison.Ordinal));
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(30));
            hold.Rollback();
        }

        Assert.True(Journal("run-b").Act(act.Id)!.Open);
        Assert.Empty(await Reconciler("run-b", DeadAre("run-a")).StartAsync(CancellationToken.None));
        Assert.False(Journal("run-b").Act(act.Id)!.Open);
    }

    [Fact]
    public async Task A_record_a_newer_build_wrote_is_held_never_reconciled_and_refuses_the_writer()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, _, _) = await CheckoutAsync();
        var (act, _, _) = Orphan(root, tip, "run-a", DateTimeOffset.UtcNow.AddMinutes(5), sleeper: false);
        KillRun("run-a");
        using (var connection = new SqliteConnection($"Data Source={DesktopStore.LocationIn(Home)};Pooling=false"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE git_acts SET format = 2";
            command.ExecuteNonQuery();
        }

        var held = await Reconciler("run-b", DeadAre("run-a")).UnderLockAsync(root, CancellationToken.None);

        Assert.Contains("newer Daoris", held!.Said);
        var still = Journal("run-b").Act(act.Id)!;
        Assert.Equal(("run-a", true), (still.ClaimRun, still.Open));
    }

    // ——— At a writer's lock: a sync's look and a promotion's step.

    [Fact]
    public async Task A_sync_look_reconciles_a_dead_act_before_its_pull_and_leaves_the_line_while_one_runs()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        var trees = new SessionTrees(Home);
        var tip = await RevAsync(root, "main");
        var (live, _, _) = Orphan(root, tip, "run-live", DateTimeOffset.UtcNow.AddMinutes(5), sleeper: false);

        var held = (await trees.SyncAsync(Repositories(root), Nobody, fetch: true, scope: SyncScope.Everything)).Lines.Single();

        Assert.False(held.Moved, held.Message);
        Assert.Contains($"act {live.Id}", held.Message);
        Assert.Equal(tip, await RevAsync(root, "main"));

        KillRun("run-live");
        var moved = (await trees.SyncAsync(Repositories(root), Nobody, scope: SyncScope.Everything)).Lines.Single();

        Assert.True(moved.Moved, moved.Message);
        Assert.False(Journal("run-x").Act(live.Id)!.Open);
    }

    [Fact]
    public async Task A_promotion_step_reconciles_a_dead_act_first_and_is_held_while_one_runs()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/a", "main");
        await CommitAsync(root, "a.txt", "a\n", "work on feature/a");
        var tip = await RevAsync(root, "feature/a");
        await GitAsync(root, "checkout", "--quiet", "main");
        var store = new IntegrationStore(Home, () => StoreSettled.Yes, null, "run-i", StoreProcess.Shell);
        Assert.Null(store.Add(new IntegrationRequest(
            "ra", "engine", "aurora", "feature/a", tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.Card,
            new LandingIntegrate(Gates: LandingIntegrate.NoGates).Resolved(), DateTimeOffset.UtcNow)));
        var asks = new IntegrationSweepAsks(
            new IntegrationReplayAsk("engine", "aurora", root, "main") { Config = DriverConfig.Empty },
            new IntegrationPromoteAsk("engine", "aurora", root, "main", DriverConfig.Empty, Home));
        var lines = new IntegrationLines(store, DaorisHome.Entry(Home, HomeEntry.Trees));
        var (live, _, _) = Orphan(root, await RevAsync(root, "main"), "run-live", DateTimeOffset.UtcNow.AddMinutes(5), sleeper: false);

        var held = await lines.RunAsync(asks);

        Assert.Contains(held.Said, said => said.Contains($"act {live.Id}", StringComparison.Ordinal));
        Assert.Null(store.Request("ra")!.ClosedAt);

        KillRun("run-live");
        await lines.RunAsync(asks);

        Assert.Equal(IntegrationRequestCloses.Settled, store.Request("ra")!.ClosedWhy);
        Assert.False(Journal("run-x").Act(live.Id)!.Open);
    }

    // ——— Git's own locks.

    [Fact]
    public async Task A_stale_index_lock_is_named_with_the_command_to_copy_and_never_removed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, _, before, after) = await CheckoutAsync();
        var stale = Path.Combine(root, ".git", "index.lock");
        await File.WriteAllTextAsync(stale, "");

        var run = await Runs().RunAsync(Plan(root, IndexStep(root, before, after)), CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains(stale, run.Said);
        Assert.Contains($"del \"{stale}\"", run.Said);
        Assert.True(File.Exists(stale));
        Assert.Equal(before, (await GitAsync(root, "rev-parse", ":shared.txt")).Trim());
    }

    /// <summary>🔴 Between the judgement and git's lock the section or the entry changed: refused under the lock, only Daoris's own lock removed.</summary>
    [Theory]
    [InlineData("config")]
    [InlineData("index")]
    public async Task A_section_or_entry_changed_since_the_judgement_is_refused_under_gits_own_lock(string what)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, _, before, after) = await CheckoutAsync();
        var step = what == "config" ? await ConfigStepAsync(root) : IndexStep(root, before, after);
        var runs = Runs();
        runs.Seam = async (point, _) =>
        {
            if (point != ActSeams.BeforeLaunch) return;
            if (what == "config") await GitAsync(root, "config", "branch.topic.remote", "elsewhere");
            else
            {
                await File.WriteAllTextAsync(Path.Combine(root, "shared.txt"), "three\n");
                await GitAsync(root, "add", "shared.txt");
            }
        };

        var run = await runs.RunAsync(Plan(root, step), CancellationToken.None);

        Assert.NotEqual(ActOutcomes.Done, run.Outcome);
        Assert.Contains("changed since", Journal("run-a").Steps(run.Act!).Single().Said);
        Assert.False(File.Exists(Path.Combine(root, ".git", what == "config" ? "config.lock" : "index.lock")));
        if (what == "config") Assert.Contains("[branch \"topic\"]", await File.ReadAllTextAsync(Path.Combine(root, ".git", "config")));
        else Assert.DoesNotContain((await GitAsync(root, "rev-parse", ":shared.txt")).Trim(), new[] { before, after });
        Assert.NotNull(Journal("run-a").Act(run.Act!)!.Recovery);
    }

    // ——— The act refs.

    [Fact]
    public async Task An_act_ref_expires_in_one_transaction_at_its_journaled_id_and_one_moved_since_is_kept()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var tip = await RevAsync(root, "main");
        await GitAsync(root, "update-ref", ActRefs.Of("a1"), tip);
        await GitAsync(root, "update-ref", ActRefs.Of("a2"), tip);
        await CommitAsync(root, "later.txt", "later\n", "later");
        await GitAsync(root, "update-ref", ActRefs.Of("a2"), await RevAsync(root, "main"));

        var expired = await ActRefs.ExpireAsync(root, "a1", tip, CancellationToken.None);
        var moved = await ActRefs.ExpireAsync(root, "a2", tip, CancellationToken.None);

        Assert.True(expired.Done, expired.Said);
        Assert.Null(await RefAsync(root, ActRefs.Of("a1")));
        Assert.False(moved.Done);
        Assert.NotNull(await RefAsync(root, ActRefs.Of("a2")));
    }

    /// <summary>🔴 A cut-off opening's branch whose commit only an act ref holds is someone's work, kept: an act ref holds nothing for the clean-ups.</summary>
    [Fact]
    public async Task A_stray_branch_held_only_by_an_act_ref_is_kept()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        const string branch = "daoris/s-0031408e";
        var cut = DaorisHome.Entry(Home, HomeEntry.Trees, RemoteTarget.Workspace(null), "engine", "s-0031408e");
        await GitAsync(root, "worktree", "add", "--quiet", "-b", branch, cut, "main");
        await CommitAsync(cut, "work.txt", "work\n", "work only an act ref holds");
        await GitAsync(root, "worktree", "lock", "--reason", TreeOpenings.GitsOwn, cut);
        var work = await RevAsync(root, branch);
        await GitAsync(root, "update-ref", ActRefs.Of("a1"), work);

        var results = await new SessionTrees(Home).TidyStraysAsync(root, "engine", null, _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>()), DateTimeOffset.UtcNow);

        Assert.Empty(results);
        Assert.True(Directory.Exists(cut));
        Assert.Equal(work, await RevAsync(root, branch));
    }
}
