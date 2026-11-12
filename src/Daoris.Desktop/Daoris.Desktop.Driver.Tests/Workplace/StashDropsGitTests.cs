using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Stash Drop over real git: the act drops one entry of the shared stash by its commit id, only
/// where a Save of Daoris's own made it, and says the commit that puts it back. The ordinals move under it; a stash of a terminal's, an unknown
/// commit, the switch off and a session in the checkout each refuse with nothing changed.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class StashDropsGitTests : TreeSyncFixture
{
    private static readonly IReadOnlyList<TreeHolder> NoSessions = [];

    private ActJournal Journal(string run = "run-a") => new(Home, () => StoreSettled.Yes, null, run, StoreProcess.Shell);

    private static StageContext Context(string root, bool allowed = true, Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null) =>
        new("engine", root, () => allowed, beside ?? (_ => Task.FromResult(NoSessions)));

    private async Task<string> SaveAsync(string root, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), content);
        var stashes = new StashActs(Journal());
        var plan = (await stashes.PlanAsync(root, CancellationToken.None)).Value!;
        var run = await stashes.RunAsync(Context(root), new StashPress(false, message, plan.Head!, plan.Stash), CancellationToken.None);
        Assert.False(run.Refused, run.Said);
        return (await GitAsync(root, "rev-parse", "refs/stash")).Trim();
    }

    private async Task<string> RepositoryWithOneCommitAsync()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "a.txt", "one\n", "first");
        return root;
    }

    private static async Task<IReadOnlyList<string>> ListAsync(string root) => [.. (await GitAsync(root, "reflog", "show", "--format=%H", "refs/stash")).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    /// <summary>🔴 Dropped by commit id after another save moved the ordinals: that entry goes, the other stays, and the answer names the command that stores it again.</summary>
    [Fact]
    public async Task A_drop_by_id_after_the_ordinals_moved_removes_that_stash_and_keeps_the_commit()
    {
        var root = await RepositoryWithOneCommitAsync();
        var first = await SaveAsync(root, "first\n", "first work");
        var second = await SaveAsync(root, "second\n", "second work");

        var run = await new StashDrops(Journal()).RunAsync(Context(root), new StashDropPress(first), CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal([second], await ListAsync(root));
        Assert.Equal("commit", (await GitAsync(root, "cat-file", "-t", first)).Trim());
        Assert.Contains("git stash store -m", run.Notes, StringComparison.Ordinal);
        Assert.Contains(first, run.Notes, StringComparison.Ordinal);
        var record = Journal().Act(run.Act!)!;
        Assert.Equal((ActPhases.Closed, ActOutcomes.Done, StashVerbs.Drop), (record.Phase, record.Outcome, record.Kind));
        Assert.Contains(first, record.Plan);
    }

    /// <summary>🔴 What the answer says puts it back: storing the commit again restores the entry.</summary>
    [Fact]
    public async Task The_commit_the_act_kept_stores_the_stash_again()
    {
        var root = await RepositoryWithOneCommitAsync();
        var id = await SaveAsync(root, "first\n", "work");
        await new StashDrops(Journal()).RunAsync(Context(root), new StashDropPress(id), CancellationToken.None);
        Assert.Empty(await ListAsync(root));

        await GitAsync(root, "stash", "store", "-m", "work", id);

        Assert.Equal([id], await ListAsync(root));
    }

    /// <summary>🔴 A message proves no ownership: a stash made in a terminal is listed but never dropped here.</summary>
    [Fact]
    public async Task A_stash_a_save_of_daoris_did_not_make_is_refused_and_stays()
    {
        var root = await RepositoryWithOneCommitAsync();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "typed\n");
        var made = (await GitAsync(root, "stash", "create", "On master: work")).Trim();
        await GitAsync(root, "stash", "store", "-m", "On master: work", made);

        var plan = (await new StashActs(Journal()).PlanAsync(root, CancellationToken.None, "engine")).Value!;
        var run = await new StashDrops(Journal()).RunAsync(Context(root), new StashDropPress(made), CancellationToken.None);

        Assert.False(plan.Entries.Single().Mine);
        Assert.True(run.Refused);
        Assert.Contains("did not save that stash", run.Said);
        Assert.Equal([made], await ListAsync(root));
    }

    /// <summary>
    /// 🔴 Another tree saves after the step read the list and before its git ran, so the place now names another commit: git drops that one, the
    /// step stores it again by its commit, and the act reports not done with the stash asked for still on the list.
    /// </summary>
    [Fact]
    public async Task A_save_between_the_read_and_the_drop_stores_the_wrong_stash_back_and_reports_not_done()
    {
        var root = await RepositoryWithOneCommitAsync();
        var asked = await SaveAsync(root, "first\n", "first work");
        var neighbour = await SaveAsync(root, "second\n", "second work");
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "third\n");
        var other = (await GitAsync(root, "stash", "create", "On master: other tree")).Trim();
        await GitAsync(root, "checkout", "--", "a.txt");

        var drops = new StashDrops(Journal());
        var moved = false;
        drops.Runs.Seam = async (point, _) =>
        {
            if (point != ActSeams.BeforePid || moved) return;
            moved = true;
            await GitAsync(root, "stash", "store", "-m", "other tree", other);
        };
        var run = await drops.RunAsync(Context(root), new StashDropPress(asked), CancellationToken.None);

        Assert.True(moved);
        Assert.False(run.Refused, run.Said);
        Assert.Equal(ActOutcomes.NotDone, run.Outcome);
        Assert.Contains("stored again", run.Notes, StringComparison.Ordinal);
        Assert.Contains(neighbour, run.Notes, StringComparison.Ordinal);
        var left = await ListAsync(root);
        Assert.Equal(3, left.Count);
        Assert.Contains(asked, left);
        Assert.Contains(neighbour, left);
        Assert.Contains(other, left);
    }

    [Fact]
    public async Task The_plan_marks_the_stashes_a_save_of_daoris_made()
    {
        var root = await RepositoryWithOneCommitAsync();
        var id = await SaveAsync(root, "first\n", "work");

        var plan = (await new StashActs(Journal()).PlanAsync(root, CancellationToken.None, "engine")).Value!;

        Assert.Equal(id, plan.Entries.Single().Id);
        Assert.True(plan.Entries.Single().Mine);
    }

    [Fact]
    public async Task A_commit_that_is_not_on_the_list_refuses()
    {
        var root = await RepositoryWithOneCommitAsync();
        await SaveAsync(root, "first\n", "work");
        var head = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        var run = await new StashDrops(Journal()).RunAsync(Context(root), new StashDropPress(head), CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("not in the stash list", run.Said);
        Assert.Single(await ListAsync(root));
    }

    [Fact]
    public async Task The_switch_off_and_a_session_in_the_checkout_each_refuse_a_drop()
    {
        var root = await RepositoryWithOneCommitAsync();
        var id = await SaveAsync(root, "first\n", "work");

        var off = await new StashDrops(Journal()).RunAsync(Context(root, allowed: false), new StashDropPress(id), CancellationToken.None);
        var held = await new StashDrops(Journal()).RunAsync(
            Context(root, beside: _ => Task.FromResult<IReadOnlyList<TreeHolder>>([new TreeHolder(root, "s-running")])), new StashDropPress(id), CancellationToken.None);

        Assert.True(off.Refused);
        Assert.Contains("may not change", off.Said);
        Assert.True(held.Refused);
        Assert.Equal([id], await ListAsync(root));
    }
}
