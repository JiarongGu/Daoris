using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP2i over real git (D204; the git-writes design §5, §8, §12 row i): commit and amend through the act journal. A formatting
/// <c>pre-commit</c> hook's rewrite is said; an amend keeps a recovery ref at the old tip first; a commit cut off by its owner's death is
/// reconciled by HEAD, its hook's children ended and nothing run again. The switch off, a session in the checkout, an unread ledger, a stale
/// HEAD or index and an operation in progress each refuse.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class CommitActsGitTests : TreeSyncFixture
{
    private sealed class Killed : Exception;

    private static readonly IReadOnlyList<TreeHolder> Nobody = [];

    private ActJournal Journal(string run, Func<StoreRun, bool>? alive = null) => new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    private CommitActs Acts(string run = "run-a") => new(Journal(run));

    private static StageContext Context(string root, bool allowed = true, Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null) =>
        new("engine", root, () => allowed, beside ?? (_ => Task.FromResult(Nobody)));

    private static Task WriteAsync(string root, string file, string text) => File.WriteAllTextAsync(Path.Combine(root, file), text);

    /// <summary>A checkout with `a.txt` committed and a changed `a.txt` staged.</summary>
    private async Task<string> StagedAsync()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "a.txt", "one\n", "first");
        await WriteAsync(root, "a.txt", "ugly  \n");
        await GitAsync(root, "add", "a.txt");
        return root;
    }

    /// <summary>The press as the page would send it from the plan read.</summary>
    private async Task<CommitPress> PressAsync(string root, string verb, string message)
    {
        var plan = (await Acts().PlanAsync(root, verb, CancellationToken.None)).Value!;
        return new CommitPress(verb, message, plan.Head, plan.Tree!);
    }

    private static async Task HookAsync(string root, string name, string body)
    {
        var hook = Path.Combine(root, ".git", "hooks", name);
        Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        await File.WriteAllTextAsync(hook, ("#!/bin/sh\n" + body).Replace("\r\n", "\n"));
    }

    private static async Task<string> HeadAsync(string root) => (await GitAsync(root, "rev-parse", "HEAD")).Trim();

    [Fact]
    public async Task The_plan_names_the_tip_the_tree_the_hooks_and_who_commits()
    {
        var root = await StagedAsync();
        await HookAsync(root, "pre-commit", "exit 0\n");
        await HookAsync(root, "commit-msg", "exit 0\n");

        var plan = (await Acts().PlanAsync(root, CommitVerbs.Commit, CancellationToken.None)).Value!;
        var amend = (await Acts().PlanAsync(root, CommitVerbs.Amend, CancellationToken.None)).Value!;

        Assert.Equal(await HeadAsync(root), plan.Head);
        Assert.Equal("main", plan.Branch?.Replace("refs/heads/", ""));
        Assert.Matches("^[0-9a-f]{40,64}$", plan.Tree);
        Assert.False(plan.Empty);
        Assert.Equal(["pre-commit", "commit-msg"], plan.Hooks);
        Assert.Contains("<", plan.Identity);
        Assert.Equal("first", amend.Message?.Trim());
        Assert.False(amend.Empty);
    }

    [Fact]
    public async Task A_commit_is_made_with_its_message_and_the_index_as_it_was()
    {
        var root = await StagedAsync();
        var old = await HeadAsync(root);
        var press = await PressAsync(root, CommitVerbs.Commit, "second\n\nwith a body  \n");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal(old, (await GitAsync(root, "rev-parse", "HEAD^")).Trim());
        Assert.Equal("second\n\nwith a body", (await GitAsync(root, "log", "-1", "--format=%B")).Trim());
        Assert.Equal("ugly  \n", await GitAsync(root, "show", "HEAD:a.txt"));
        Assert.DoesNotContain("hook changed", run.Said);
    }

    /// <summary>🔴 A formatting pre-commit rewrites the file and stages it: the commit holds the formatted bytes and the act says so.</summary>
    [Fact]
    public async Task A_formatting_pre_commit_hook_s_rewrite_is_said()
    {
        var root = await StagedAsync();
        await HookAsync(root, "pre-commit", "printf 'formatted\\n' > a.txt\ngit add a.txt\n");
        var press = await PressAsync(root, CommitVerbs.Commit, "second");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal("formatted\n", await GitAsync(root, "show", "HEAD:a.txt"));
        Assert.Contains("A hook changed what was committed", run.Said);
        Assert.Contains("`a.txt`", run.Said);
        Assert.StartsWith("A hook changed what was committed", run.Notes);
    }

    [Fact]
    public async Task A_message_a_hook_rewrites_is_said_and_the_commit_is_still_done()
    {
        var root = await StagedAsync();
        await HookAsync(root, "commit-msg", "printf 'signed: %s\\n' \"$(cat \"$1\")\" > \"$1\"\n");
        var press = await PressAsync(root, CommitVerbs.Commit, "second");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal("signed: second", (await GitAsync(root, "log", "-1", "--format=%B")).Trim());
        Assert.Contains("A hook changed the message", run.Said);
    }

    [Fact]
    public async Task A_hook_that_refuses_leaves_HEAD_and_says_what_it_printed()
    {
        var root = await StagedAsync();
        var old = await HeadAsync(root);
        await HookAsync(root, "pre-commit", "echo 'lint failed' >&2\nexit 1\n");
        var press = await PressAsync(root, CommitVerbs.Commit, "second");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.False(run.Refused);
        Assert.Equal(ActOutcomes.NotDone, run.Outcome);
        Assert.Contains("lint failed", run.Said);
        Assert.StartsWith("Git refused the commit", run.Notes);
        Assert.Equal(old, await HeadAsync(root));
        Assert.Contains("ugly", await GitAsync(root, "show", ":a.txt"));
    }

    /// <summary>🔴 An amend keeps the old tip under the discarded namespace before it rewrites the commit.</summary>
    [Fact]
    public async Task An_amend_keeps_a_recovery_ref_at_the_old_tip_first()
    {
        var root = await StagedAsync();
        var old = await HeadAsync(root);
        var parent = (await GitAsync(root, "rev-parse", "HEAD^")).Trim();
        var press = await PressAsync(root, CommitVerbs.Amend, "first, amended");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.NotEqual(old, await HeadAsync(root));
        Assert.Equal(parent, (await GitAsync(root, "rev-parse", "HEAD^")).Trim());
        Assert.Equal("first, amended", (await GitAsync(root, "log", "-1", "--format=%B")).Trim());
        var kept = (await GitAsync(root, "for-each-ref", "--format=%(refname) %(objectname)", "refs/daoris/discarded/")).Trim();
        Assert.Equal($"refs/daoris/discarded/amended/main-{old[..12]} {old}", kept);
        var steps = Journal("run-a").Steps(run.Act!);
        Assert.Equal([ActStepKinds.Ref, ActStepKinds.Commit], steps.Select(each => each.Kind));
    }

    [Fact]
    public async Task An_amend_that_a_hook_refuses_lets_its_recovery_ref_go()
    {
        var root = await StagedAsync();
        var old = await HeadAsync(root);
        await HookAsync(root, "pre-commit", "exit 1\n");
        var press = await PressAsync(root, CommitVerbs.Amend, "first, amended");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.Equal(old, await HeadAsync(root));
        Assert.Contains("Nothing was amended", run.Said);
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/discarded/")).Trim());
    }

    [Fact]
    public async Task An_amend_with_only_a_new_message_is_taken_and_one_with_nothing_new_is_refused()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "a.txt", "one\n", "first");
        var same = await Acts().RunAsync(Context(root), await PressAsync(root, CommitVerbs.Amend, "first"), CancellationToken.None);
        var reworded = await Acts().RunAsync(Context(root), await PressAsync(root, CommitVerbs.Amend, "better"), CancellationToken.None);

        Assert.True(same.Refused);
        Assert.Contains("neither the files nor the message", same.Said);
        Assert.Equal(ActOutcomes.Done, reworded.Outcome);
        Assert.Equal("better", (await GitAsync(root, "log", "-1", "--format=%B")).Trim());
    }

    [Fact]
    public async Task Nothing_staged_an_empty_message_and_a_stale_press_are_refused()
    {
        var root = await StagedAsync();
        var press = await PressAsync(root, CommitVerbs.Commit, "second");
        var blank = await Acts().RunAsync(Context(root), press with { Message = "  \n" }, CancellationToken.None);
        await GitAsync(root, "add", "-A");
        await WriteAsync(root, "b.txt", "b\n");
        await GitAsync(root, "add", "b.txt");
        var changed = await Acts().RunAsync(Context(root), press, CancellationToken.None);
        await CommitAsync(root, "c.txt", "c\n", "moved");
        var moved = await Acts().RunAsync(Context(root), press, CancellationToken.None);
        var empty = await Acts().RunAsync(Context(root), await PressAsync(root, CommitVerbs.Commit, "again"), CancellationToken.None);

        Assert.Contains("needs a message", blank.Said);
        Assert.Contains("the index changed", changed.Said);
        Assert.Contains("HEAD moved", moved.Said);
        Assert.True(empty.Refused);
        Assert.Contains("nothing is staged", empty.Said);
    }

    [Fact]
    public async Task The_switch_off_refuses_and_changes_nothing()
    {
        var root = await StagedAsync();
        var old = await HeadAsync(root);

        var run = await Acts().RunAsync(Context(root, allowed: false), await PressAsync(root, CommitVerbs.Commit, "second"), CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("Daoris may change this checkout's files", run.Said);
        Assert.Equal(old, await HeadAsync(root));
    }

    [Theory]
    [InlineData(CommitVerbs.Commit)]
    [InlineData(CommitVerbs.Amend)]
    public async Task A_session_in_the_checkout_and_an_unread_ledger_refuse(string verb)
    {
        var root = await StagedAsync();
        var old = await HeadAsync(root);
        var press = await PressAsync(root, verb, "second");

        var session = await Acts().RunAsync(Context(root, beside: _ => Task.FromResult<IReadOnlyList<TreeHolder>>([new TreeHolder(root, "s-running")])), press, CancellationToken.None);
        var unread = await Acts().RunAsync(Context(root, beside: _ => throw new IOException("the service did not answer")), press, CancellationToken.None);
        var none = await Acts().RunAsync(new StageContext("engine", root, () => true, null), press, CancellationToken.None);

        Assert.True(session.Refused);
        Assert.Contains("`s-running`", session.Said);
        Assert.Contains("ledger could not be read", unread.Said);
        Assert.Contains("ledger of sessions was not read", none.Said);
        Assert.Equal(old, await HeadAsync(root));
    }

    [Fact]
    public async Task An_operation_in_progress_and_git_s_own_lock_refuse()
    {
        var root = await StagedAsync();
        var press = await PressAsync(root, CommitVerbs.Commit, "second");
        var merge = Path.Combine(root, ".git", "MERGE_HEAD");
        await File.WriteAllTextAsync(merge, await HeadAsync(root) + "\n");
        var inOperation = await Acts().RunAsync(Context(root), press, CancellationToken.None);
        File.Delete(merge);
        var held = Path.Combine(root, ".git", "HEAD.lock");
        await File.WriteAllTextAsync(held, "");

        var locked = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.Contains("MERGE_HEAD", inOperation.Said);
        Assert.Contains("HEAD.lock", locked.Said);
        Assert.True(File.Exists(held), "Daoris never removes git's lock");
    }

    internal static void KillRun(string home, string run)
    {
        using var connection = new SqliteConnection($"Data Source={DesktopStore.LocationIn(home)};Pooling=false");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE store_runs SET pid = 2147483000, started = 1 WHERE run = $run";
        command.Parameters.AddWithValue("$run", run);
        Assert.Equal(1, command.ExecuteNonQuery());
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

    /// <summary>
    /// 🔴 A commit whose owner dies while its post-commit hook runs: the commit is made, the hook's child ends with the act's job, and another
    /// run reads HEAD against the old tip and the plan and closes it done, the hook never run again.
    /// </summary>
    [Fact]
    public async Task A_commit_cut_off_in_its_hook_is_reconciled_by_HEAD_and_the_hook_s_child_ends()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await StagedAsync();
        var old = await HeadAsync(root);
        var noted = Path.Combine(root, ".git", "noted.txt").Replace('\\', '/');
        var childFile = Path.Combine(root, ".git", "child.txt");
        await HookAsync(root, "post-commit", $"echo ran >> '{noted}'\nsleep 600 &\ncat /proc/$!/winpid > '{childFile.Replace('\\', '/')}'\nsleep 600\n");
        var press = await PressAsync(root, CommitVerbs.Commit, "second");
        var acts = Acts();
        var descendant = 0;
        acts.Runs.ExitBound = TimeSpan.FromSeconds(2);
        acts.Runs.Seam = async (point, _) =>
        {
            if (point != ActSeams.AfterLaunch) return;
            descendant = await ChildOfAsync(childFile);
            throw new Killed();
        };

        await Assert.ThrowsAsync<Killed>(() => acts.RunAsync(Context(root), press, CancellationToken.None));

        var act = Journal("run-a").Unclosed().Single();
        Assert.False(ProcessJob.Exists(act.Job), "the act's job went with its last handle");
        Assert.False(Lives(descendant), "the hook's child ended with the job");
        Assert.NotEqual(old, await HeadAsync(root));
        KillRun(Home, "run-a");
        var reconciler = new ActReconciler(Journal("run-b", run => run.Run != "run-a")) { ExitBound = TimeSpan.FromSeconds(2) };

        var hold = await reconciler.UnderLockAsync(root, CancellationToken.None);

        Assert.Null(hold);
        var closed = Journal("run-b").Act(act.Id)!;
        Assert.Equal((ActPhases.Closed, ActOutcomes.Done), (closed.Phase, closed.Outcome));
        Assert.Equal("ran", (await File.ReadAllTextAsync(Path.Combine(root, ".git", "noted.txt"))).Trim());
    }

    /// <summary>🔴 A commit cut off before git ran reads as not done: HEAD is where it was, and nothing is run again.</summary>
    [Fact]
    public async Task A_commit_cut_off_before_git_ran_is_reconciled_as_not_done()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await StagedAsync();
        var old = await HeadAsync(root);
        var press = await PressAsync(root, CommitVerbs.Commit, "second");
        var acts = Acts();
        acts.Runs.Seam = (point, _) => point == ActSeams.BeforePid ? throw new Killed() : Task.CompletedTask;

        await Assert.ThrowsAsync<Killed>(() => acts.RunAsync(Context(root), press, CancellationToken.None));
        KillRun(Home, "run-a");
        var act = Journal("run-b").Unclosed().Single();
        var hold = await new ActReconciler(Journal("run-b", run => run.Run != "run-a")) { ExitBound = TimeSpan.FromSeconds(2) }.UnderLockAsync(root, CancellationToken.None);

        Assert.Null(hold);
        Assert.Equal(ActOutcomes.NotDone, Journal("run-b").Act(act.Id)!.Outcome);
        Assert.Equal(old, await HeadAsync(root));
    }
}
