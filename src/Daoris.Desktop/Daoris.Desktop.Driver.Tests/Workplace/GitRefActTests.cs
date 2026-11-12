using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git: a branch or a lightweight tag created by one <c>create</c> through
/// the act journal, refused where the name stands, undone only at the id it points at, and reconciled by its postconditions where the
/// act's run died between its start and its end, nothing run again.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitRefActTests : TreeSyncFixture
{
    private ActJournal Journal(string run, Func<StoreRun, bool>? alive = null) => new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    private ActRuns Runs(string run = "run-a") => new(Journal(run)) { ExitBound = TimeSpan.FromSeconds(2) };

    private static RefAsk Ask(string root, string kind, string name, string? at = null) => new("engine", root, kind, name, at);

    private static async Task<string?> RefAsync(string root, string name)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", name], CancellationToken.None);
        return code == 0 ? said.Trim() : null;
    }

    private void KillRun(string run)
    {
        using var connection = new SqliteConnection($"Data Source={DesktopStore.LocationIn(Home)};Pooling=false");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE store_runs SET pid = 2147483000, started = 1 WHERE run = $run";
        command.Parameters.AddWithValue("$run", run);
        Assert.Equal(1, command.ExecuteNonQuery());
    }

    [Fact]
    public async Task A_branch_is_created_at_the_commit_named_and_the_act_is_journaled_closed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var tip = await RevAsync(root, "main");

        var run = await GitRefActs.CreateAsync(Runs(), Ask(root, "branch", "feature/new"), "window", "window", CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/feature/new"));
        var act = Journal("run-a").Act(run.Act!)!;
        Assert.Equal((GitRefActs.CreateKind, "window", false), (act.Kind, act.Press, act.Open));
        Assert.Contains("refs/heads/feature/new", Journal("run-a").Steps(act.Id).Single().Postconditions);
    }

    [Fact]
    public async Task A_lightweight_tag_is_created_and_one_naming_no_commit_is_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var tip = await RevAsync(root, "main");

        var made = await GitRefActs.CreateAsync(Runs(), Ask(root, "tag", "v1", tip), "window", "window", CancellationToken.None);
        var tree = (await GitAsync(root, "rev-parse", "main^{tree}")).Trim();
        var refused = await GitRefActs.CreateAsync(Runs(), Ask(root, "tag", "v2", tree), "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, made.Outcome);
        Assert.Equal(tip, await RefAsync(root, "refs/tags/v1"));
        Assert.True(refused.Refused);
        Assert.Contains("no commit", refused.Said);
        Assert.Null(await RefAsync(root, "refs/tags/v2"));
        Assert.Empty(Journal("run-a").Unclosed());
    }

    [Fact]
    public async Task A_create_refuses_a_name_that_stands_and_moves_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "taken", "main");
        var other = await RevAsync(root, "main");

        var branch = await GitRefActs.CreateAsync(Runs(), Ask(root, "branch", "taken"), "window", "window", CancellationToken.None);
        var tag = await GitRefActs.CreateAsync(Runs(), Ask(root, "tag", "main"), "window", "window", CancellationToken.None);
        await GitAsync(root, "tag", "held", "main");
        var again = await GitRefActs.CreateAsync(Runs(), Ask(root, "tag", "held"), "window", "window", CancellationToken.None);

        Assert.All(new[] { branch, again }, each => { Assert.True(each.Refused); Assert.Contains("already", each.Said); });
        Assert.False(tag.Refused, "a tag may share a branch's short name; the two are different refs");
        Assert.Equal(other, await RefAsync(root, "refs/heads/taken"));
    }

    [Theory]
    [InlineData("branch", "bad name")]
    [InlineData("branch", "-dash")]
    [InlineData("branch", "a..b")]
    [InlineData("branch", "daoris/s-1")]
    [InlineData("tag", "")]
    [InlineData("tag", "x.lock")]
    public async Task A_name_git_would_not_take_or_that_is_daoris_s_own_is_refused_before_anything_is_written(string kind, string name)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");

        var run = await GitRefActs.CreateAsync(Runs(), Ask(root, kind, name), "window", "window", CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Null(run.Act);
        Assert.Empty(Journal("run-a").Unclosed());
    }

    [Fact]
    public async Task A_name_that_stands_between_the_judgement_and_the_effect_is_not_overwritten()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var later = (await GitAsync(root, "commit-tree", "main^{tree}", "-p", "main", "-m", "later")).Trim();
        var runs = Runs();
        runs.Seam = async (point, _) =>
        {
            if (point == ActSeams.BeforeLaunch) await GitAsync(root, "update-ref", "refs/heads/raced", later);
        };

        var run = await GitRefActs.CreateAsync(runs, Ask(root, "branch", "raced"), "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Unknown, run.Outcome);
        Assert.Equal(later, await RefAsync(root, "refs/heads/raced"));
    }

    [Fact]
    public async Task Undo_deletes_the_ref_only_at_the_id_the_act_made_and_not_a_second_time()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var made = await GitRefActs.CreateAsync(Runs(), Ask(root, "branch", "topic"), "window", "window", CancellationToken.None);

        var undone = await GitRefActs.UndoAsync(Runs(), "engine", root, made.Act!, "window", "window", CancellationToken.None);
        var twice = await GitRefActs.UndoAsync(Runs(), "engine", root, made.Act!, "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, undone.Outcome);
        Assert.Null(await RefAsync(root, "refs/heads/topic"));
        Assert.True(twice.Refused);
        Assert.Equal(GitRefActs.UndoKind, Journal("run-a").Act(undone.Act!)!.Kind);
    }

    [Fact]
    public async Task Undo_leaves_a_ref_that_was_moved_since_and_one_another_act_made()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var made = await GitRefActs.CreateAsync(Runs(), Ask(root, "branch", "topic"), "window", "window", CancellationToken.None);
        var moved = (await GitAsync(root, "commit-tree", "main^{tree}", "-p", "main", "-m", "moved")).Trim();
        await GitAsync(root, "update-ref", "refs/heads/topic", moved);
        var elsewhere = await GitRefActs.UndoAsync(Runs(), "engine", root, made.Act!, "window", "window", CancellationToken.None);
        var unknown = await GitRefActs.UndoAsync(Runs(), "engine", root, "no-such-act", "window", "window", CancellationToken.None);
        var other = await GitRefActs.UndoAsync(Runs(), "elsewhere", root, made.Act!, "window", "window", CancellationToken.None);

        Assert.All(new[] { elsewhere, unknown, other }, each => Assert.True(each.Refused));
        Assert.Contains("no longer", elsewhere.Said);
        Assert.Equal(moved, await RefAsync(root, "refs/heads/topic"));
    }

    [Fact]
    public async Task An_undo_is_not_itself_undone_and_a_fixture_act_is_not_a_create()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var made = await GitRefActs.CreateAsync(Runs(), Ask(root, "tag", "t"), "window", "window", CancellationToken.None);
        var undone = await GitRefActs.UndoAsync(Runs(), "engine", root, made.Act!, "window", "window", CancellationToken.None);

        var again = await GitRefActs.UndoAsync(Runs(), "engine", root, undone.Act!, "window", "window", CancellationToken.None);

        Assert.True(again.Refused);
        Assert.Contains("not a create", again.Said);
    }

    [Fact]
    public async Task An_act_whose_run_died_before_its_effect_is_reconciled_not_done_and_not_run_again()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var runs = Runs();
        runs.Seam = (point, _) => point == ActSeams.BeforeLaunch ? throw new InvalidOperationException("killed") : Task.CompletedTask;
        await Assert.ThrowsAsync<InvalidOperationException>(() => GitRefActs.CreateAsync(runs, Ask(root, "branch", "cut"), "window", "window", CancellationToken.None));
        var act = Journal("run-a").Unclosed().Single();
        KillRun("run-a");

        var hold = await new ActReconciler(Journal("run-b", run => run.Run != "run-a")) { ExitBound = TimeSpan.FromSeconds(2) }.UnderLockAsync(root, CancellationToken.None);

        Assert.Null(hold);
        Assert.Equal((ActPhases.Closed, ActOutcomes.NotDone), (Journal("run-b").Act(act.Id)!.Phase, Journal("run-b").Act(act.Id)!.Outcome));
        Assert.Null(await RefAsync(root, "refs/heads/cut"));
    }

    [Fact]
    public async Task An_act_whose_effect_landed_before_its_run_died_is_reconciled_done_and_not_run_again()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var tip = await RevAsync(root, "main");
        var runs = Runs();
        runs.Seam = (point, _) => point == ActSeams.AfterLaunch ? throw new InvalidOperationException("killed") : Task.CompletedTask;
        await Assert.ThrowsAsync<InvalidOperationException>(() => GitRefActs.CreateAsync(runs, Ask(root, "branch", "landed"), "window", "window", CancellationToken.None));
        // git, let go, finishes without its run: the ref stands or, if the cut came first, it does not and is never made again.
        await Task.Delay(500);
        KillRun("run-a");
        var act = Journal("run-b").Unclosed().Single();

        await new ActReconciler(Journal("run-b", run => run.Run != "run-a")) { ExitBound = TimeSpan.FromSeconds(2) }.UnderLockAsync(root, CancellationToken.None);

        var closed = Journal("run-b").Act(act.Id)!;
        var stands = await RefAsync(root, "refs/heads/landed");
        Assert.Equal(stands == tip ? ActOutcomes.Done : ActOutcomes.NotDone, closed.Outcome);
        Assert.Contains(stands, new[] { tip, null });
    }
}
