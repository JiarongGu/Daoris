using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git scheduler's places, queues, shared answers and ends, with no process: each run is a function the test holds, and the
/// places are the scheduler's own count, never the job's processes.
/// </summary>
public sealed class GitSchedulerTests
{
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(10);

    private static GitScheduler Scheduler(int bound = 4, int outside = 2, int repository = 2, List<string>? said = null) =>
        new(bound, outside, repository) { Said = (said is null ? (_, _) => { } : (each, _) => { lock (said) said.Add(each); }) };

    private static string Folder(string name) => Path.GetFullPath(Path.Combine("_scheduler", name));

    private static GitRunAsk Read(string repository, string? start = null, bool endable = false, string? folder = null) =>
        new(folder ?? Folder(repository), Folder(repository), GitWait.Read) { Start = start, Endable = endable };

    private static GitRunAsk Outside(string repository) => new(Folder(repository), Folder(repository), GitWait.Outside) { Writes = true };

    private static GitRunAsk Write(string repository) => new(Folder(repository), Folder(repository), GitWait.Read) { Writes = true };

    private static Task<T> Person<T>(Func<Task<T>> ask)
    {
        using (GitScheduler.AsPerson()) return ask();
    }

    private static void Most(ref int most, int now)
    {
        int seen;
        while (now > (seen = Volatile.Read(ref most)) && Interlocked.CompareExchange(ref most, now, seen) != seen)
        {
        }
    }

    [Fact]
    public async Task A_fan_out_of_hundreds_runs_no_more_than_the_bound_and_the_background_never_takes_the_last_place()
    {
        var scheduler = Scheduler();
        var now = 0;
        var background = 0;
        var all = 0;
        var asks = Enumerable.Range(0, 300).Select(at =>
        {
            Task<int> Ask() => scheduler.RunAsync(Read($"r{at % 12}"), async _ =>
            {
                var running = Interlocked.Increment(ref now);
                Most(ref all, scheduler.Running);
                if (at % 3 != 0) Most(ref background, running);
                await Task.Delay(1);
                Interlocked.Decrement(ref now);
                return at;
            }, CancellationToken.None);
            return at % 3 == 0 ? Person(Ask) : Ask();
        }).ToList();

        var answers = await Task.WhenAll(asks).WaitAsync(Poll.Patience);

        Assert.Equal(Enumerable.Range(0, 300), answers);
        Assert.InRange(all, 1, GitScheduler.Bound);
        Assert.Equal(0, scheduler.Running);
        Assert.Equal(300, scheduler.Started);
    }

    [Fact]
    public async Task The_background_alone_holds_all_but_one_place_and_one_repository_holds_two()
    {
        var scheduler = Scheduler();
        var hold = new TaskCompletionSource();
        var reads = Enumerable.Range(0, 10).Select(_ => scheduler.RunAsync(Read("one"), async _ => { await hold.Task; return 0; }, CancellationToken.None)).ToList();
        await Poll.Until(() => scheduler.Running == 2);
        var others = Enumerable.Range(0, 10).Select(at => scheduler.RunAsync(Read($"other{at}"), async _ => { await hold.Task; return 0; }, CancellationToken.None)).ToList();
        await Poll.Until(() => scheduler.Running == 3);

        Assert.Equal(3, scheduler.Running);
        hold.SetResult();
        await Task.WhenAll(reads.Concat(others)).WaitAsync(Poll.Patience);
    }

    [Fact]
    public async Task A_persons_read_is_placed_while_every_other_place_is_held_by_long_running_gits()
    {
        var scheduler = Scheduler();
        var hold = new TaskCompletionSource();
        // Two pushes of a person's, each running a hook, and the background's reads queued behind every place they leave.
        var pushes = Enumerable.Range(0, 3).Select(at => Person(() => scheduler.RunAsync(Outside($"push{at}"), async _ => { await hold.Task; return 0; }, CancellationToken.None))).ToList();
        var reads = Enumerable.Range(0, 8).Select(at => scheduler.RunAsync(Read($"tick{at}"), async _ => { await hold.Task; return 0; }, CancellationToken.None)).ToList();
        await Poll.Until(() => scheduler.Running == 3);

        var mine = await Person(() => scheduler.RunAsync(Read("mine"), _ => Task.FromResult(7), CancellationToken.None)).WaitAsync(Soon);

        Assert.Equal(7, mine);
        // The outside class held two places, never three, so the reads kept the rest.
        Assert.Equal(3, scheduler.Running);
        hold.SetResult();
        await Task.WhenAll(pushes.Concat(reads)).WaitAsync(Poll.Patience);
    }

    [Fact]
    public async Task Two_hook_running_acts_in_two_repositories_whose_hooks_each_ask_a_remote_start_both_complete()
    {
        var scheduler = Scheduler();
        var both = new TaskCompletionSource();
        var lent = new TaskCompletionSource();
        var holding = 0;
        var placed = 0;
        var hold = new TaskCompletionSource();
        var filled = new TaskCompletionSource();

        Task<int> Act(string repository) => scheduler.RunAsync(Outside(repository), async _ =>
        {
            // Each act holds its outside place before either hook asks, as two pushes running their hooks do.
            if (Interlocked.Increment(ref holding) == 2) both.SetResult();
            await filled.Task;
            using (GitScheduler.Beneath(GitPlace.Current))
            {
                return await scheduler.RunAsync(Outside(repository), async _ =>
                {
                    if (Interlocked.Increment(ref placed) == 2) lent.SetResult();
                    await lent.Task;
                    return 1;
                }, CancellationToken.None) + 1;
            }
        }, CancellationToken.None);

        var acts = Task.WhenAll(Act("one"), Act("two"));
        await both.Task.WaitAsync(Soon);
        // The background's reads fill every place the two acts leave, so a hook's start finds none of the four.
        var reads = Enumerable.Range(0, 4).Select(at => scheduler.RunAsync(Read($"tick{at}"), async _ => { await hold.Task; return 0; }, CancellationToken.None)).ToList();
        await Poll.Until(() => scheduler.Running == GitScheduler.Bound - 1);
        filled.SetResult();
        await lent.Task.WaitAsync(Soon);

        Assert.Equal(2, scheduler.Lent);
        Assert.Equal(GitScheduler.Bound - 1, scheduler.Running);
        var done = await acts.WaitAsync(Soon);
        Assert.Equal([2, 2], done);
        Assert.Equal(0, scheduler.Lent);
        hold.SetResult();
        await Task.WhenAll(reads).WaitAsync(Soon);
    }

    [Fact]
    public async Task A_re_entry_three_deep_is_refused_as_too_deep_and_said()
    {
        var said = new List<string>();
        var scheduler = Scheduler(said: said);

        Task<int> Nested(int depth) => scheduler.RunAsync(Outside("r"), async _ =>
        {
            using (GitScheduler.Beneath(GitPlace.Current)) return depth == 0 ? 0 : await Nested(depth - 1) + 1;
        }, CancellationToken.None);

        Assert.Equal(2, await Nested(2).WaitAsync(Soon));
        var refused = await Assert.ThrowsAsync<GitUnplacedException>(() => Nested(3)).WaitAsync(Soon);
        Assert.Equal("depth", refused.Word);
        lock (said) Assert.Equal(["git.start.depth"], said);
    }

    [Fact]
    public async Task Identical_queued_reads_share_one_start_and_a_start_differing_in_anything_is_its_own()
    {
        var scheduler = Scheduler(bound: 1, repository: 1);
        var hold = new TaskCompletionSource();
        var blocker = scheduler.RunAsync(Read("held"), async _ => { await hold.Task; return "held"; }, CancellationToken.None);
        await Poll.Until(() => scheduler.Running == 1);
        var runs = 0;
        Func<CancellationToken, Task<string>> read = _ => Task.FromResult($"answer {Interlocked.Increment(ref runs)}");

        var first = scheduler.RunAsync(Read("r", start: "rev-parse HEAD"), read, CancellationToken.None);
        var second = scheduler.RunAsync(Read("r", start: "rev-parse HEAD"), read, CancellationToken.None);
        var other = scheduler.RunAsync(Read("r", start: "rev-parse HEAD|GIT_DIR=x"), read, CancellationToken.None);
        hold.SetResult();
        await Task.WhenAll(blocker, first, second, other).WaitAsync(Poll.Patience);

        Assert.Equal(first.Result, second.Result);
        Assert.NotEqual(first.Result, other.Result);
        Assert.Equal(2, runs);
        Assert.Equal(3, scheduler.Started);
    }

    [Fact]
    public async Task A_running_read_is_never_joined_so_an_ask_after_a_write_outside_the_service_reads_the_moved_ref()
    {
        var scheduler = Scheduler();
        var reading = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var reference = "before";

        var running = scheduler.RunAsync(Read("r", start: "rev-parse main"), async _ =>
        {
            var seen = reference;
            reading.SetResult();
            await release.Task;
            return seen;
        }, CancellationToken.None);
        await reading.Task.WaitAsync(Soon);
        // A session's own git moves the ref while the first read runs.
        reference = "after";
        var asked = await scheduler.RunAsync(Read("r", start: "rev-parse main"), _ => Task.FromResult(reference), CancellationToken.None).WaitAsync(Soon);
        release.SetResult();

        Assert.Equal("after", asked);
        Assert.Equal("before", await running.WaitAsync(Soon));
        Assert.Equal(2, scheduler.Started);
    }

    [Fact]
    public async Task A_write_keeps_its_order_in_its_repository_and_a_persons_read_never_passes_it()
    {
        var scheduler = Scheduler(bound: 1, repository: 1);
        var hold = new TaskCompletionSource();
        var order = new List<string>();
        Func<CancellationToken, Task<int>> Run(string name) => _ =>
        {
            lock (order) order.Add(name);
            return Task.FromResult(0);
        };

        var blocker = scheduler.RunAsync(Read("r"), async _ => { await hold.Task; return 0; }, CancellationToken.None);
        await Poll.Until(() => scheduler.Running == 1);
        var asks = new[]
        {
            scheduler.RunAsync(Read("r"), Run("read before"), CancellationToken.None),
            scheduler.RunAsync(Write("r"), Run("write"), CancellationToken.None),
            scheduler.RunAsync(Read("r"), Run("read after"), CancellationToken.None),
            Person(() => scheduler.RunAsync(Read("r"), Run("person after"), CancellationToken.None)),
        };
        hold.SetResult();
        await Task.WhenAll(asks.Append(blocker)).WaitAsync(Poll.Patience);

        Assert.Equal("read before", order[0]);
        Assert.Equal("write", order[1]);
        Assert.Equal(["person after", "read after"], order[2..]);
    }

    [Fact]
    public async Task A_lock_free_read_nobody_waits_on_is_ended_and_every_other_git_runs_to_its_end()
    {
        var scheduler = Scheduler();
        using var leaves = new CancellationTokenSource();
        var started = new TaskCompletionSource();
        var ended = new TaskCompletionSource();
        var endable = scheduler.RunAsync(Read("r", endable: true), async token =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException)
            {
                ended.SetResult();
                throw;
            }

            return 0;
        }, leaves.Token);
        await started.Task.WaitAsync(Soon);
        leaves.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => endable);
        await ended.Task.WaitAsync(Soon);

        // A status that takes the index's lock, or a write: handed no token, and run on once its asker has left.
        using var left = new CancellationTokenSource();
        var running = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var handed = new CancellationToken(true);
        var locking = scheduler.RunAsync(Read("r"), async token =>
        {
            handed = token;
            running.SetResult();
            await release.Task;
            return 1;
        }, left.Token);
        await running.Task.WaitAsync(Soon);
        left.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => locking);
        Assert.False(handed.CanBeCanceled);
        Assert.Equal(1, scheduler.Running);
        release.SetResult();
        await Poll.Until(() => scheduler.Running == 0);
    }

    [Fact]
    public async Task A_queued_git_whose_askers_all_left_is_dropped_with_nothing_run()
    {
        var scheduler = Scheduler(bound: 1, repository: 1);
        var hold = new TaskCompletionSource();
        var blocker = scheduler.RunAsync(Read("r"), async _ => { await hold.Task; return 0; }, CancellationToken.None);
        await Poll.Until(() => scheduler.Running == 1);
        using var leaves = new CancellationTokenSource();
        var ran = false;
        var write = scheduler.RunAsync(Write("r"), _ =>
        {
            ran = true;
            return Task.FromResult(1);
        }, leaves.Token);
        leaves.Cancel();
        hold.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);
        await blocker.WaitAsync(Soon);
        await Poll.Until(() => scheduler.Running == 0);
        Assert.False(ran);
        Assert.Equal(1, scheduler.Started);
    }

    [Fact]
    public async Task A_removal_drops_the_trees_lock_free_reads_waits_for_its_unendable_git_and_holds_what_is_asked_after()
    {
        var scheduler = Scheduler(bound: 1, repository: 1);
        var hold = new TaskCompletionSource();
        var ran = new List<string>();
        var tree = Folder("trees/one");
        Func<CancellationToken, Task<string>> Run(string name) => _ =>
        {
            lock (ran) ran.Add(name);
            return Task.FromResult(name);
        };

        var blocker = scheduler.RunAsync(Read("other"), async _ => { await hold.Task; return "held"; }, CancellationToken.None);
        await Poll.Until(() => scheduler.Running == 1);
        var unendable = scheduler.RunAsync(Read("checkout", folder: tree), Run("status that locks"), CancellationToken.None);
        var endable = scheduler.RunAsync(Read("checkout", endable: true, folder: Path.Combine(tree, "src")), Run("lock-free read"), CancellationToken.None);
        var removing = scheduler.RemovingAsync(tree, CancellationToken.None);
        var after = scheduler.RunAsync(Read("checkout", folder: tree), Run("asked after"), CancellationToken.None);

        Assert.Equal("going", (await Assert.ThrowsAsync<GitUnplacedException>(() => endable)).Word);
        Assert.False(removing.IsCompleted);
        hold.SetResult();
        var removal = await removing.WaitAsync(Soon);

        Assert.Equal("status that locks", await unendable);
        lock (ran) Assert.Equal(["status that locks"], ran);
        removal.Dispose();
        Assert.Equal("asked after", await after.WaitAsync(Soon));
        await blocker;
    }

    [Fact]
    public async Task A_git_that_outlived_its_kill_is_counted_apart_and_its_repositorys_background_asks_are_answered_stalled()
    {
        var said = new List<string>();
        var scheduler = Scheduler(bound: 1, repository: 1, said: said);
        var exit = new TaskCompletionSource();

        Assert.Equal(1, await scheduler.RunAsync(Read("hung"), _ =>
        {
            GitPlace.Current!.Bind(() => exit.Task.IsCompleted, () => exit.Task);
            return Task.FromResult(1);
        }, CancellationToken.None).WaitAsync(Soon));

        // Its place left the bound: another repository's read runs in the one place, and the hung repository's background is refused.
        Assert.Equal(1, scheduler.Apart);
        Assert.Equal(0, scheduler.Running);
        Assert.Equal(2, await scheduler.RunAsync(Read("other"), _ => Task.FromResult(2), CancellationToken.None).WaitAsync(Soon));
        var stalled = await Assert.ThrowsAsync<GitUnplacedException>(() => scheduler.RunAsync(Read("hung"), _ => Task.FromResult(3), CancellationToken.None));
        Assert.Equal("stalled", stalled.Word);
        Assert.Equal(4, await Person(() => scheduler.RunAsync(Read("hung"), _ => Task.FromResult(4), CancellationToken.None)).WaitAsync(Soon));

        exit.SetResult();
        await Poll.Until(() => scheduler.Apart == 0);
        Assert.Equal(5, await scheduler.RunAsync(Read("hung"), _ => Task.FromResult(5), CancellationToken.None).WaitAsync(Soon));
        await Poll.Until(() => { lock (said) return said.Count == 3; });
        lock (said) Assert.Equal(["git.place.apart", "git.stalled", "git.place.exited"], said);
    }

    [Fact]
    public async Task The_gate_refuses_a_gits_image_started_outside_the_scheduler_for_every_purpose_and_starts_it_inside_a_place()
    {
        var made = 0;
        var starts = new ProcessStarts(4, _ =>
        {
            Interlocked.Increment(ref made);
            return null;
        });

        foreach (var purpose in Enum.GetValues<ProcessPurpose>())
        {
            var refused = await Assert.ThrowsAsync<GitStartRefusedException>(
                () => starts.StartAsync(new ProcessStartInfo(Path.Combine("tools", "git.exe")), purpose, StartClass.Person, CancellationToken.None));
            Assert.Equal(purpose, refused.Purpose);
        }

        Assert.Equal(0, made);
        await Scheduler().RunAsync(Read("r"), _ => starts.StartAsync(new ProcessStartInfo("git"), ProcessPurpose.Tool, CancellationToken.None), CancellationToken.None);
        Assert.Equal(1, made);
    }
}
