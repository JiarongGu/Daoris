using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP2k over real git (D204; the git-writes design §8, §12 row k): stash Save through the act journal. The plan says the stash is shared
/// by every linked tree; a Save takes tracked files only unless asked for the untracked ones; a stash that moved since it was read, the switch
/// off, a session in the checkout, an unread ledger, an operation in progress and git's lock each refuse.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class StashActsGitTests : TreeSyncFixture
{
    private static readonly IReadOnlyList<TreeHolder> NoSessions = [];

    private ActJournal Journal(string run = "run-a") => new(Home, () => StoreSettled.Yes, null, run, StoreProcess.Shell);

    private StashActs Acts() => new(Journal());

    private static StageContext Context(string root, bool allowed = true, Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null) =>
        new("engine", root, () => allowed, beside ?? (_ => Task.FromResult(NoSessions)));

    /// <summary>A checkout with `a.txt` committed and changed, and `notes.txt` untracked.</summary>
    private async Task<string> DirtyAsync()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "a.txt", "one\n", "first");
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "changed\n");
        await File.WriteAllTextAsync(Path.Combine(root, "notes.txt"), "mine\n");
        return root;
    }

    private async Task<StashPress> PressAsync(string root, bool untracked = false, string? message = null)
    {
        var plan = (await Acts().PlanAsync(root, CancellationToken.None)).Value!;
        return new StashPress(untracked, message, plan.Head!, plan.Stash);
    }

    private static async Task<string> StashesAsync(string root) => (await GitAsync(root, "stash", "list")).Trim();

    [Fact]
    public async Task The_plan_counts_the_changes_and_says_the_stash_is_shared_with_the_other_trees()
    {
        var root = await DirtyAsync();
        await GitAsync(root, "worktree", "add", "--quiet", Path.Combine(Scratch, "session-tree"), "-b", "side");

        var plan = (await Acts().PlanAsync(root, CancellationToken.None)).Value!;

        Assert.Equal(1, plan.Tracked);
        Assert.Equal(1, plan.Untracked);
        Assert.Equal(1, plan.SharedWith);
        Assert.Null(plan.Stash);
        Assert.Null(plan.Why);
        Assert.Contains("<", plan.Identity);
    }

    /// <summary>🔴 By default only tracked files go: the untracked file stays where it is.</summary>
    [Fact]
    public async Task A_save_takes_tracked_files_only_by_default()
    {
        var root = await DirtyAsync();

        var run = await Acts().RunAsync(Context(root), await PressAsync(root), CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal("one", (await File.ReadAllTextAsync(Path.Combine(root, "a.txt"))).Trim());
        Assert.Equal("mine\n", await File.ReadAllTextAsync(Path.Combine(root, "notes.txt")));
        Assert.Single((await StashesAsync(root)).Split('\n'));
        Assert.Contains("every linked tree shares", run.Notes);
        var id = (await GitAsync(root, "rev-parse", "refs/stash")).Trim();
        Assert.Contains($"git stash apply {id}", run.Said);
    }

    /// <summary>🔴 Asked for, the untracked files go too.</summary>
    [Fact]
    public async Task A_save_asked_to_include_untracked_takes_them_too()
    {
        var root = await DirtyAsync();

        var run = await Acts().RunAsync(Context(root), await PressAsync(root, untracked: true, message: "before the switch\nsecond line"), CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.False(File.Exists(Path.Combine(root, "notes.txt")));
        Assert.Equal("one", (await File.ReadAllTextAsync(Path.Combine(root, "a.txt"))).Trim());
        Assert.Contains("before the switch", await StashesAsync(root));
        Assert.DoesNotContain("second line", await StashesAsync(root));
    }

    [Fact]
    public async Task Untracked_files_alone_are_not_saved_unless_asked()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "a.txt", "one\n", "first");
        await File.WriteAllTextAsync(Path.Combine(root, "notes.txt"), "mine\n");

        var plain = await Acts().RunAsync(Context(root), await PressAsync(root), CancellationToken.None);
        var asked = await Acts().RunAsync(Context(root), await PressAsync(root, untracked: true), CancellationToken.None);

        Assert.True(plain.Refused);
        Assert.Contains("ask for the untracked ones", plain.Said);
        Assert.Equal(ActOutcomes.Done, asked.Outcome);
    }

    [Fact]
    public async Task A_clean_checkout_has_nothing_to_save()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "a.txt", "one\n", "first");

        var plan = (await Acts().PlanAsync(root, CancellationToken.None)).Value!;
        var run = await Acts().RunAsync(Context(root), await PressAsync(root, untracked: true), CancellationToken.None);

        Assert.Equal("there is nothing to save", plan.Why);
        Assert.True(run.Refused);
        Assert.Contains("nothing to save", run.Said);
    }

    /// <summary>🔴 A stash another tree saved after the page read refuses the press: the stash is shared.</summary>
    [Fact]
    public async Task A_stash_that_moved_since_it_was_read_refuses()
    {
        var root = await DirtyAsync();
        var press = await PressAsync(root);
        await GitAsync(root, "stash", "push", "--message", "someone else's");
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "again\n");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("shared stash moved", run.Said);
        Assert.Equal("again\n", await File.ReadAllTextAsync(Path.Combine(root, "a.txt")));
    }

    [Fact]
    public async Task An_operation_in_progress_and_git_s_own_lock_refuse()
    {
        var root = await DirtyAsync();
        var press = await PressAsync(root);
        var merge = Path.Combine(root, ".git", "MERGE_HEAD");
        await File.WriteAllTextAsync(merge, (await GitAsync(root, "rev-parse", "HEAD")).Trim() + "\n");
        var inOperation = await Acts().RunAsync(Context(root), press, CancellationToken.None);
        File.Delete(merge);
        var held = Path.Combine(root, ".git", "refs", "stash.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(held)!);
        await File.WriteAllTextAsync(held, "");

        var locked = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.Contains("MERGE_HEAD", inOperation.Said);
        Assert.True(locked.Refused);
        Assert.Contains("stash.lock", locked.Said);
        Assert.True(File.Exists(held), "Daoris never removes git's lock");
        Assert.Equal("changed\n", await File.ReadAllTextAsync(Path.Combine(root, "a.txt")));
    }

    [Fact]
    public async Task The_switch_off_a_session_in_the_checkout_and_an_unread_ledger_each_refuse()
    {
        var root = await DirtyAsync();
        var press = await PressAsync(root);

        var off = await Acts().RunAsync(Context(root, allowed: false), press, CancellationToken.None);
        var session = await Acts().RunAsync(Context(root, beside: _ => Task.FromResult<IReadOnlyList<TreeHolder>>([new TreeHolder(root, "s-running")])), press, CancellationToken.None);
        var unread = await Acts().RunAsync(Context(root, beside: _ => throw new IOException("the service did not answer")), press, CancellationToken.None);
        var none = await Acts().RunAsync(new StageContext("engine", root, () => true, null), press, CancellationToken.None);

        Assert.Contains("Daoris may change this checkout's files", off.Said);
        Assert.Contains("`s-running`", session.Said);
        Assert.Contains("ledger could not be read", unread.Said);
        Assert.Contains("ledger of sessions was not read", none.Said);
        Assert.All(new[] { off, session, unread, none }, run => Assert.True(run.Refused));
        Assert.Equal("changed\n", await File.ReadAllTextAsync(Path.Combine(root, "a.txt")));
        Assert.Empty(await StashesAsync(root));
    }

    /// <summary>The act is journaled and closes done.</summary>
    [Fact]
    public async Task The_act_is_journaled_and_closes_done()
    {
        var root = await DirtyAsync();

        var run = await Acts().RunAsync(Context(root), await PressAsync(root), CancellationToken.None);

        var record = Journal().Act(run.Act!)!;
        Assert.Equal((ActPhases.Closed, ActOutcomes.Done, StashVerbs.Save), (record.Phase, record.Outcome, record.Kind));
        Assert.Equal(ActOutcomes.Done, Assert.Single(Journal().Steps(run.Act!)).Outcome);
        Assert.Empty(Journal().Unclosed());
    }

    /// <summary>A Save cut off before git ran reads as not done, from the stash alone, and nothing is run again.</summary>
    [Fact]
    public async Task A_save_cut_off_before_git_ran_is_reconciled_as_not_done()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await DirtyAsync();
        var press = await PressAsync(root);
        var acts = Acts();
        acts.Runs.Seam = (point, _) => point == ActSeams.BeforePid ? throw new KilledException() : Task.CompletedTask;

        await Assert.ThrowsAsync<KilledException>(() => acts.RunAsync(Context(root), press, CancellationToken.None));
        var act = Journal("run-b").Unclosed().Single();
        var reconciler = new ActReconciler(new ActJournal(Home, () => StoreSettled.Yes, run => run.Run != "run-a", "run-b", StoreProcess.Shell)) { ExitBound = TimeSpan.FromSeconds(2) };
        CommitActsGitTests.KillRun(Home, "run-a");

        var hold = await reconciler.UnderLockAsync(root, CancellationToken.None);

        Assert.Null(hold);
        Assert.Equal(ActOutcomes.NotDone, Journal("run-b").Act(act.Id)!.Outcome);
        Assert.Empty(await StashesAsync(root));
        Assert.Equal("changed\n", await File.ReadAllTextAsync(Path.Combine(root, "a.txt")));
    }

    private sealed class KilledException : Exception;
}
