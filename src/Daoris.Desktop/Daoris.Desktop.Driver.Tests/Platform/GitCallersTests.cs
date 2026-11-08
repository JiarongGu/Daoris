using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A caller from outside the process told as beneath a placed git by its ancestry alone: a hook's ask and a diff helper's ask each
/// placed in the place their git lends, while every place of the four is held, so no git waits on a place its descendant needs. No
/// process: each place is bound to an id the test names, and each ask is made from a flow of its own, as a pipe request is.
/// </summary>
public sealed class GitCallersTests
{
    private static readonly TimeSpan Soon = Poll.Patience;

    private static GitScheduler Scheduler() => new(GitScheduler.Bound, GitScheduler.OutsideBound, GitScheduler.RepositoryBound) { Said = (_, _) => { } };

    private static string Folder(string name) => Path.GetFullPath(Path.Combine("_scheduler", name));

    private static GitRunAsk Read(string repository) => new(Folder(repository), Folder(repository), GitWait.Read);

    private static GitRunAsk Outside(string repository) => new(Folder(repository), Folder(repository), GitWait.Outside) { Writes = true };

    // A request over the pipe: its own flow, carrying nothing of the git it descends from but its ancestry.
    private static Task<int> Asked(GitScheduler scheduler, int[] ancestry, GitRunAsk ask) => Task.Run(async () =>
    {
        using (GitCallers.Beneath(scheduler, () => ancestry)) return await scheduler.RunAsync(ask, _ => Task.FromResult(1), CancellationToken.None);
    });

    // The run as a git of id pid: bound to its place until the run returns, as a process is until it exits.
    private static async Task<int> AsGit(int pid, Func<Task<int>> body)
    {
        var gone = new TaskCompletionSource();
        GitPlace.Current!.Bind(pid, () => gone.Task.IsCompleted, () => gone.Task);
        try
        {
            return await body();
        }
        finally
        {
            gone.SetResult();
        }
    }

    [Fact]
    public async Task Two_hooks_told_by_their_ancestry_alone_are_placed_in_the_places_their_pushes_lend()
    {
        var scheduler = Scheduler();
        var hold = new TaskCompletionSource();
        var both = new TaskCompletionSource();
        var filled = new TaskCompletionSource();
        var holding = 0;

        // The hook, a shell beneath this git and the verb beneath it, asks a read over the pipe once every place is held.
        Task<int> Push(string repository, int pid) => scheduler.RunAsync(Outside(repository), _ => AsGit(pid, async () =>
        {
            if (Interlocked.Increment(ref holding) == 2) both.SetResult();
            await filled.Task;
            return await Asked(scheduler, [9000 + pid, 8000 + pid, pid], Read(repository)) + 1;
        }), CancellationToken.None);

        var pushes = Task.WhenAll(Push("one", 101), Push("two", 102));
        await both.Task.WaitAsync(Soon);
        var reads = Enumerable.Range(0, 4).Select(at => scheduler.RunAsync(Read($"tick{at}"), async _ => { await hold.Task; return 0; }, CancellationToken.None)).ToList();
        await Poll.Until(() => scheduler.Running == GitScheduler.Bound - 1);
        filled.SetResult();

        var done = await pushes.WaitAsync(Soon);
        Assert.Equal([2, 2], done);
        hold.SetResult();
        await Task.WhenAll(reads).WaitAsync(Soon);
    }

    [Fact]
    public async Task A_diff_helpers_ask_is_lent_a_place_while_three_background_diffs_and_a_persons_hold_all_four()
    {
        var scheduler = Scheduler();
        var full = new TaskCompletionSource();
        var go = new TaskCompletionSource();
        var running = 0;

        // Each diff runs a textconv helper that asks the window a read before the diff can end.
        Task<int> Diff(int at) => scheduler.RunAsync(Read($"diff{at}"), _ => AsGit(200 + at, async () =>
        {
            if (Interlocked.Increment(ref running) == GitScheduler.Bound) full.SetResult();
            await go.Task;
            return await Asked(scheduler, [7000 + at, 200 + at], Read($"diff{at}"));
        }), CancellationToken.None);

        List<Task<int>> diffs = [Diff(0), Diff(1), Diff(2)];
        using (GitScheduler.AsPerson()) diffs.Add(Diff(3));
        await full.Task.WaitAsync(Soon);
        Assert.Equal(GitScheduler.Bound, scheduler.Running);
        go.SetResult();

        var answered = await Task.WhenAll(diffs).WaitAsync(Soon);
        Assert.Equal([1, 1, 1, 1], answered);
        await Poll.Until(() => scheduler.Lent == 0);
    }

    [Fact]
    public async Task A_caller_beneath_no_placed_git_is_queued_as_asked()
    {
        var scheduler = Scheduler();
        var hold = new TaskCompletionSource();
        var reads = Enumerable.Range(0, 3).Select(at => scheduler.RunAsync(Read($"tick{at}"), async _ => { await hold.Task; return 0; }, CancellationToken.None)).ToList();
        await Poll.Until(() => scheduler.Running == GitScheduler.Bound - 1);

        var stranger = Asked(scheduler, [4242, 4243], Read("elsewhere"));
        await Task.Delay(200);

        Assert.False(stranger.IsCompleted);
        Assert.Equal(0, scheduler.Lent);
        hold.SetResult();
        Assert.Equal(1, await stranger.WaitAsync(Soon));
        await Task.WhenAll(reads).WaitAsync(Soon);
    }
}
