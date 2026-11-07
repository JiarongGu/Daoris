using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git: Switch through the act journal. Refused where tracked files are
/// changed, an operation is in progress, the branch is out in another tree; undone by switching back. The switch off, a session in the
/// checkout and an unread ledger each refuse; a post-checkout hook is named in the plan and run.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class SwitchActsGitTests : TreeSyncFixture
{
    private static readonly IReadOnlyList<TreeHolder> NoSessions = [];

    private ActJournal Journal(string run = "run-a") => new(Home, () => StoreSettled.Yes, null, run, StoreProcess.Shell);

    private SwitchActs Acts() => new(Journal());

    private static StageContext Context(string root, bool allowed = true, Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null) =>
        new("engine", root, () => allowed, beside ?? (_ => Task.FromResult(NoSessions)));

    /// <summary>A checkout on main with `a.txt` committed and a second branch `other` one commit ahead.</summary>
    private async Task<string> TwoBranchesAsync()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "a.txt", "one\n", "first");
        await GitAsync(root, "branch", "other");
        return root;
    }

    private async Task<SwitchPress> PressAsync(string root, string branch)
    {
        var plan = (await Acts().PlanAsync(root, branch, CancellationToken.None)).Value!;
        return new SwitchPress(branch, plan.From, plan.Head!);
    }

    private static async Task<string> BranchAsync(string root) => (await GitAsync(root, "branch", "--show-current")).Trim();

    [Fact]
    public async Task The_plan_names_where_HEAD_is_the_branch_and_the_hooks()
    {
        var root = await TwoBranchesAsync();
        await HookAsync(root, "post-checkout", "exit 0\n");

        var plan = (await Acts().PlanAsync(root, "other", CancellationToken.None)).Value!;

        Assert.Equal("main", plan.From);
        Assert.Equal("other", plan.To);
        Assert.NotNull(plan.ToHead);
        Assert.False(plan.Dirty);
        Assert.Null(plan.Why);
        Assert.Equal(["post-checkout"], plan.Hooks);
    }

    [Fact]
    public async Task A_clean_checkout_switches_and_switching_back_undoes_it()
    {
        var root = await TwoBranchesAsync();

        var there = await Acts().RunAsync(Context(root), await PressAsync(root, "other"), CancellationToken.None);
        Assert.False(there.Refused, there.Said);
        Assert.Equal(ActOutcomes.Done, there.Outcome);
        Assert.Equal("other", await BranchAsync(root));
        Assert.Contains("Switch back to `main`", there.Said);

        var back = await Acts().RunAsync(Context(root), await PressAsync(root, "main"), CancellationToken.None);
        Assert.Equal(ActOutcomes.Done, back.Outcome);
        Assert.Equal("main", await BranchAsync(root));
    }

    /// <summary>🔴 A tracked change is never carried across: the switch is refused and the file keeps its contents.</summary>
    [Fact]
    public async Task A_dirty_checkout_is_refused_and_nothing_changes()
    {
        var root = await TwoBranchesAsync();
        var press = await PressAsync(root, "other");
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "changed\n");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("tracked files are changed", run.Said);
        Assert.Equal("main", await BranchAsync(root));
        Assert.Equal("changed\n", await File.ReadAllTextAsync(Path.Combine(root, "a.txt")));
    }

    [Fact]
    public async Task An_untracked_file_does_not_make_it_dirty()
    {
        var root = await TwoBranchesAsync();
        await File.WriteAllTextAsync(Path.Combine(root, "notes.txt"), "mine\n");

        var run = await Acts().RunAsync(Context(root), await PressAsync(root, "other"), CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal("mine\n", await File.ReadAllTextAsync(Path.Combine(root, "notes.txt")));
    }

    /// <summary>🔴 A branch another tree has out is refused, naming the tree's folder and never its path.</summary>
    [Fact]
    public async Task A_branch_checked_out_elsewhere_is_refused()
    {
        var root = await TwoBranchesAsync();
        var place = Path.Combine(Scratch, "elsewhere-tree");
        await GitAsync(root, "worktree", "add", "--quiet", place, "other");

        var run = await Acts().RunAsync(Context(root), await PressAsync(root, "other"), CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("checked out in another tree, `elsewhere-tree`", run.Said);
        Assert.DoesNotContain(Scratch, run.Said);
        Assert.Equal("main", await BranchAsync(root));
    }

    [Fact]
    public async Task A_missing_branch_the_current_one_and_a_daoris_branch_are_refused()
    {
        var root = await TwoBranchesAsync();
        await GitAsync(root, "branch", "daoris/mine");

        var missing = await Acts().RunAsync(Context(root), new SwitchPress("nowhere", "main", await HeadAsync(root)), CancellationToken.None);
        var same = await Acts().RunAsync(Context(root), new SwitchPress("main", "main", await HeadAsync(root)), CancellationToken.None);
        var own = await Acts().RunAsync(Context(root), new SwitchPress("daoris/mine", "main", await HeadAsync(root)), CancellationToken.None);

        Assert.Contains("no local branch `nowhere`", missing.Said);
        Assert.Contains("on `main` already", same.Said);
        Assert.Contains("Daoris's own", own.Said);
        Assert.Equal("main", await BranchAsync(root));
    }

    [Fact]
    public async Task A_press_made_from_an_old_read_is_refused()
    {
        var root = await TwoBranchesAsync();
        var press = await PressAsync(root, "other");
        await CommitAsync(root, "b.txt", "two\n", "second");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("HEAD moved", run.Said);
    }

    [Fact]
    public async Task An_operation_in_progress_and_git_s_own_lock_refuse()
    {
        var root = await TwoBranchesAsync();
        var press = await PressAsync(root, "other");
        var merge = Path.Combine(root, ".git", "MERGE_HEAD");
        await File.WriteAllTextAsync(merge, await HeadAsync(root) + "\n");
        var inOperation = await Acts().RunAsync(Context(root), press, CancellationToken.None);
        File.Delete(merge);
        var held = Path.Combine(root, ".git", "HEAD.lock");
        await File.WriteAllTextAsync(held, "");

        var locked = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.Contains("MERGE_HEAD", inOperation.Said);
        Assert.True(locked.Refused);
        Assert.Contains("HEAD.lock", locked.Said);
        Assert.True(File.Exists(held), "Daoris never removes git's lock");
        Assert.Equal("main", await BranchAsync(root));
    }

    [Fact]
    public async Task The_switch_off_a_session_in_the_checkout_and_an_unread_ledger_each_refuse()
    {
        var root = await TwoBranchesAsync();
        var press = await PressAsync(root, "other");

        var off = await Acts().RunAsync(Context(root, allowed: false), press, CancellationToken.None);
        var session = await Acts().RunAsync(Context(root, beside: _ => Task.FromResult<IReadOnlyList<TreeHolder>>([new TreeHolder(root, "s-running")])), press, CancellationToken.None);
        var unread = await Acts().RunAsync(Context(root, beside: _ => throw new IOException("the service did not answer")), press, CancellationToken.None);
        var none = await Acts().RunAsync(new StageContext("engine", root, () => true, null), press, CancellationToken.None);

        Assert.Contains("Daoris may change this checkout's files", off.Said);
        Assert.Contains("`s-running`", session.Said);
        Assert.Contains("ledger could not be read", unread.Said);
        Assert.Contains("ledger of sessions was not read", none.Said);
        Assert.All(new[] { off, session, unread, none }, run => Assert.True(run.Refused));
        Assert.Equal("main", await BranchAsync(root));
    }

    /// <summary>The act is journaled: its record closes done, with the switch step done, and a post-checkout hook ran once.</summary>
    [Fact]
    public async Task The_act_is_journaled_and_closes_done_with_the_hook_run_once()
    {
        var root = await TwoBranchesAsync();
        var noted = Path.Combine(root, ".git", "noted.txt").Replace('\\', '/');
        await HookAsync(root, "post-checkout", $"echo ran >> '{noted}'\n");

        var run = await Acts().RunAsync(Context(root), await PressAsync(root, "other"), CancellationToken.None);

        var record = Journal().Act(run.Act!)!;
        Assert.Equal((ActPhases.Closed, ActOutcomes.Done, SwitchVerbs.Switch), (record.Phase, record.Outcome, record.Kind));
        Assert.Equal(ActOutcomes.Done, Assert.Single(Journal().Steps(run.Act!)).Outcome);
        Assert.Equal("ran", (await File.ReadAllTextAsync(Path.Combine(root, ".git", "noted.txt"))).Trim());
        Assert.Empty(Journal().Unclosed());
    }

    /// <summary>A switch git refuses (an untracked file in the way) is closed not done, with git's words.</summary>
    [Fact]
    public async Task A_switch_git_refuses_closes_not_done_and_says_why()
    {
        var root = await TwoBranchesAsync();
        await GitAsync(root, "checkout", "--quiet", "other");
        await CommitAsync(root, "extra.txt", "tracked on other\n", "extra");
        await GitAsync(root, "checkout", "--quiet", "main");
        await File.WriteAllTextAsync(Path.Combine(root, "extra.txt"), "mine\n");

        var run = await Acts().RunAsync(Context(root), await PressAsync(root, "other"), CancellationToken.None);

        Assert.False(run.Refused);
        Assert.Equal(ActOutcomes.NotDone, run.Outcome);
        Assert.Contains("Git refused the switch", run.Said);
        Assert.Equal("main", await BranchAsync(root));
        Assert.Equal("mine\n", await File.ReadAllTextAsync(Path.Combine(root, "extra.txt")));
    }

    private static async Task HookAsync(string root, string name, string body)
    {
        var hook = Path.Combine(root, ".git", "hooks", name);
        Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        await File.WriteAllTextAsync(hook, ("#!/bin/sh\n" + body).Replace("\r\n", "\n"));
    }

    private static async Task<string> HeadAsync(string root) => (await GitAsync(root, "rev-parse", "HEAD")).Trim();
}
