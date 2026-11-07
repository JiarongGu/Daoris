using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Delete over real git: a branch or a lightweight tag deleted by one
/// transaction through the act journal (the delete, the recovery ref's create, each proof ref's verify), then the branch's section removed
/// under git's own <c>config.lock</c>; refused for the held branches, an annotated tag and an occupied branch; put back where a tree checked
/// it out between the judgement and the effect; undone, the section included.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitRefDeleteTests : TreeSyncFixture
{
    private ActJournal Journal(string run, Func<StoreRun, bool>? alive = null) => new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    private ActRuns Runs(string run = "run-a") => new(Journal(run)) { ExitBound = TimeSpan.FromSeconds(2) };

    private static RefDeleteAsk Ask(string root, string kind, string name, string? at = null, string? line = null) => new("engine", root, kind, name, at, line);

    private static async Task<string?> RefAsync(string root, string name)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", name], CancellationToken.None);
        return code == 0 ? said.Trim() : null;
    }

    private static string ConfigOf(string root) => File.ReadAllText(Path.Combine(root, ".git", "config"));

    private async Task<(string Root, string Tip)> WithBranchAsync(string name = "topic")
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", name, "main");
        await GitAsync(root, "config", $"branch.{name}.remote", "origin");
        await GitAsync(root, "config", $"branch.{name}.merge", $"refs/heads/{name}");
        return (root, await RevAsync(root, name));
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
    public async Task A_branch_goes_with_a_recovery_ref_its_section_removed_and_the_act_journaled_closed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();

        var run = await GitRefActs.DeleteAsync(Runs(), Ask(root, "branch", "topic", tip), "window", "window", CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Null(await RefAsync(root, "refs/heads/topic"));
        Assert.Equal(tip, await RefAsync(root, "refs/daoris/discarded/topic"));
        Assert.DoesNotContain("branch \"topic\"", ConfigOf(root));
        Assert.NotEmpty((await GitAsync(root, "reflog", "show", "refs/daoris/discarded/topic")).Trim());
        var act = Journal("run-a").Act(run.Act!)!;
        Assert.Equal((GitRefActs.DeleteKind, "window", false), (act.Kind, act.Press, act.Open));
        Assert.Equal([ActStepKinds.Ref, ActStepKinds.Config], Journal("run-a").Steps(act.Id).Select(step => step.Kind));
    }

    [Fact]
    public async Task The_recovery_is_listed_among_the_repository_s_recovery_refs()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();
        await GitRefActs.DeleteAsync(Runs(), Ask(root, "branch", "topic"), "window", "window", CancellationToken.None);

        var (refs, unread) = await RecoveryRetention.ReadAsync(root, "engine", new HashSet<string>(), CancellationToken.None);

        Assert.Null(unread);
        var kept = Assert.Single(refs!);
        Assert.Equal(("refs/daoris/discarded/topic", tip), (kept.Ref, kept.Commit));
    }

    [Fact]
    public async Task A_recovery_name_that_stands_at_another_commit_is_not_overwritten_and_the_next_name_is_taken()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();
        var other = (await GitAsync(root, "commit-tree", "main^{tree}", "-p", "main", "-m", "other")).Trim();
        await GitAsync(root, "update-ref", "refs/daoris/discarded/topic", other);

        var run = await GitRefActs.DeleteAsync(Runs(), Ask(root, "branch", "topic"), "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal(other, await RefAsync(root, "refs/daoris/discarded/topic"));
        Assert.Equal(tip, await RefAsync(root, "refs/daoris/discarded/topic-2"));
    }

    [Theory]
    [InlineData("refs/heads/main")]
    [InlineData("refs/tags/holder")]
    public async Task Each_proof_ref_is_verified_so_one_that_moves_before_the_effect_keeps_the_branch(string moving)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();
        await GitAsync(root, "tag", "holder", "main");
        var later = (await GitAsync(root, "commit-tree", "main^{tree}", "-p", "main", "-m", "later")).Trim();
        var runs = Runs();
        runs.Seam = async (point, _) =>
        {
            if (point == ActSeams.BeforeLaunch) await GitAsync(root, "update-ref", moving, later);
        };

        var run = await GitRefActs.DeleteAsync(runs, Ask(root, "branch", "topic"), "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.NotDone, run.Outcome);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/topic"));
        Assert.Null(await RefAsync(root, "refs/daoris/discarded/topic"));
        Assert.Contains("branch \"topic\"", ConfigOf(root));
    }

    [Fact]
    public async Task A_branch_that_moved_since_the_page_showed_it_is_refused_and_nothing_is_written()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, _) = await WithBranchAsync();
        var shown = await RevAsync(root, "main");
        var later = (await GitAsync(root, "commit-tree", "main^{tree}", "-p", "main", "-m", "later")).Trim();
        await GitAsync(root, "update-ref", "refs/heads/topic", later);

        var run = await GitRefActs.DeleteAsync(Runs(), Ask(root, "branch", "topic", shown), "window", "window", CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("no longer", run.Said);
        Assert.Equal(later, await RefAsync(root, "refs/heads/topic"));
        Assert.Empty(Journal("run-a").Unclosed());
    }

    [Fact]
    public async Task A_branch_a_tree_checks_out_between_the_judgement_and_the_effect_is_put_back_and_the_recovery_ref_goes_again()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();
        var tree = Path.Combine(Path.GetDirectoryName(root)!, "late-tree");
        var runs = Runs();
        runs.Seam = async (point, ordinal) =>
        {
            if (point == ActSeams.BeforeLaunch && ordinal == 0) await GitAsync(root, "worktree", "add", tree, "topic");
        };

        var run = await GitRefActs.DeleteAsync(runs, Ask(root, "branch", "topic"), "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.NotDone, run.Outcome);
        Assert.Contains("checked out", run.Said);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/topic"));
        Assert.Null(await RefAsync(root, "refs/daoris/discarded/topic"));
        Assert.Contains("branch \"topic\"", ConfigOf(root));
        Assert.Empty(Journal("run-a").Unclosed());
    }

    [Fact]
    public async Task A_branch_a_tree_has_checked_out_is_refused_before_anything_is_written()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();
        await GitAsync(root, "worktree", "add", Path.Combine(Path.GetDirectoryName(root)!, "held-tree"), "topic");

        var run = await GitRefActs.DeleteAsync(Runs(), Ask(root, "branch", "topic"), "window", "window", CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("checked out", run.Said);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/topic"));
    }

    [Fact]
    public async Task A_lightweight_tag_is_deleted_with_a_recovery_ref_and_an_annotated_one_is_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var tip = await RevAsync(root, "main");
        await GitAsync(root, "tag", "light", "main");
        await GitAsync(root, "tag", "-a", "noted", "-m", "noted", "main");
        var tree = (await GitAsync(root, "rev-parse", "main^{tree}")).Trim();
        await GitAsync(root, "update-ref", "refs/tags/of-a-tree", tree);

        var light = await GitRefActs.DeleteAsync(Runs(), Ask(root, "tag", "light"), "window", "window", CancellationToken.None);
        var noted = await GitRefActs.DeleteAsync(Runs(), Ask(root, "tag", "noted"), "window", "window", CancellationToken.None);
        var treed = await GitRefActs.DeleteAsync(Runs(), Ask(root, "tag", "of-a-tree"), "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, light.Outcome);
        Assert.Null(await RefAsync(root, "refs/tags/light"));
        Assert.Equal(tip, await RefAsync(root, "refs/daoris/discarded/light"));
        Assert.True(noted.Refused);
        Assert.Contains("annotated", noted.Said);
        Assert.NotNull(await RefAsync(root, "refs/tags/noted"));
        Assert.True(treed.Refused);
        Assert.Contains("no commit", treed.Said);
        Assert.NotNull(await RefAsync(root, "refs/tags/of-a-tree"));
    }

    [Fact]
    public async Task The_held_branches_are_refused_the_daoris_namespace_a_landing_s_branch_the_line_and_one_a_together_operation_binds()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();
        foreach (var name in new[] { "daoris/s-1", "landed-one", "bound" }) await GitAsync(root, "branch", name, "main");
        new LandedBranches(Home).Record(new LandedBranch("engine", "work", "landed-one", "main", tip, "s2", "q1", "Landed", DateTimeOffset.UtcNow));
        var (opened, refused) = new TogetherOperations(Home).Open(
            "engine", new TogetherHead("s-line", "s-head", Path.Combine(Scratch, "head-tree"), "daoris/head", "h1"),
            [new TogetherMember("s-beside", Path.Combine(Scratch, "member-tree"), "bound", tip, "t1")], DateTimeOffset.UtcNow);
        Assert.Null(refused);

        var held = new[]
        {
            await GitRefActs.DeleteAsync(Runs(), Ask(root, "branch", "daoris/s-1"), "window", "window", CancellationToken.None),
            await GitRefActs.DeleteAsync(Runs(), Ask(root, "branch", "landed-one"), "window", "window", CancellationToken.None),
            await GitRefActs.DeleteAsync(Runs(), Ask(root, "branch", "topic", line: "topic"), "window", "window", CancellationToken.None),
            await GitRefActs.DeleteAsync(Runs(), Ask(root, "branch", "bound"), "window", "window", CancellationToken.None),
        };

        Assert.All(held, each => { Assert.True(each.Refused); Assert.Null(each.Act); });
        Assert.Contains("daoris/", held[0].Said);
        Assert.Contains("landing", held[1].Said);
        Assert.Contains("line", held[2].Said);
        Assert.Contains(opened!.Id, held[3].Said);
        foreach (var name in new[] { "daoris/s-1", "landed-one", "topic", "bound" }) Assert.NotNull(await RefAsync(root, $"refs/heads/{name}"));
        Assert.Empty(Journal("run-a").Unclosed());
    }

    [Fact]
    public async Task Undo_makes_the_branch_again_restores_its_section_under_the_config_lock_and_lets_the_recovery_ref_go()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();
        var before = ConfigOf(root);
        var deleted = await GitRefActs.DeleteAsync(Runs(), Ask(root, "branch", "topic"), "window", "window", CancellationToken.None);

        var undone = await GitRefActs.UndoAsync(Runs(), "engine", root, deleted.Act!, "window", "window", CancellationToken.None);
        var twice = await GitRefActs.UndoAsync(Runs(), "engine", root, deleted.Act!, "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, undone.Outcome);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/topic"));
        Assert.Equal(before, ConfigOf(root));
        Assert.Null(await RefAsync(root, "refs/daoris/discarded/topic"));
        Assert.False(File.Exists(Path.Combine(root, ".git", "config.lock")));
        Assert.True(twice.Refused);
        Assert.Equal(GitRefActs.UndoKind, Journal("run-a").Act(undone.Act!)!.Kind);
    }

    [Fact]
    public async Task Undo_is_refused_where_a_branch_of_that_name_was_made_since_and_a_standing_config_lock_is_named_never_removed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, _) = await WithBranchAsync();
        var deleted = await GitRefActs.DeleteAsync(Runs(), Ask(root, "branch", "topic"), "window", "window", CancellationToken.None);
        await GitAsync(root, "branch", "topic", "main");
        var taken = await GitRefActs.UndoAsync(Runs(), "engine", root, deleted.Act!, "window", "window", CancellationToken.None);
        await GitAsync(root, "branch", "-D", "topic");
        var locked = Path.Combine(root, ".git", "config.lock");
        await File.WriteAllTextAsync(locked, "");

        var held = await GitRefActs.UndoAsync(Runs(), "engine", root, deleted.Act!, "window", "window", CancellationToken.None);

        Assert.True(taken.Refused);
        Assert.Contains("already", taken.Said);
        Assert.True(held.Refused);
        Assert.Contains("config.lock", held.Said);
        Assert.True(File.Exists(locked));
        Assert.Null(await RefAsync(root, "refs/heads/topic"));
    }

    [Fact]
    public async Task A_section_changed_before_the_config_step_is_not_removed_and_the_deletion_stands_with_its_recovery()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();
        var runs = Runs();
        runs.Seam = async (point, ordinal) =>
        {
            if (point == ActSeams.BeforeLaunch && ordinal == 1) await GitAsync(root, "config", "branch.topic.rebase", "true");
        };

        var run = await GitRefActs.DeleteAsync(runs, Ask(root, "branch", "topic"), "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Partial, run.Outcome);
        Assert.Contains("branch \"topic\"", ConfigOf(root));
        Assert.Equal(tip, await RefAsync(root, "refs/daoris/discarded/topic"));
    }

    [Fact]
    public async Task An_act_whose_run_died_between_its_ref_step_and_its_section_step_is_reconciled_partial_and_not_run_again()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();
        var runs = Runs();
        runs.Seam = (point, ordinal) => point == ActSeams.BeforeLaunch && ordinal == 1 ? throw new InvalidOperationException("killed") : Task.CompletedTask;
        await Assert.ThrowsAsync<InvalidOperationException>(() => GitRefActs.DeleteAsync(runs, Ask(root, "branch", "topic"), "window", "window", CancellationToken.None));
        var act = Journal("run-a").Unclosed().Single();
        KillRun("run-a");

        var hold = await new ActReconciler(Journal("run-b", run => run.Run != "run-a")) { ExitBound = TimeSpan.FromSeconds(2) }.UnderLockAsync(root, CancellationToken.None);

        Assert.Null(hold);
        Assert.Equal((ActPhases.Closed, ActOutcomes.Partial), (Journal("run-b").Act(act.Id)!.Phase, Journal("run-b").Act(act.Id)!.Outcome));
        Assert.Null(await RefAsync(root, "refs/heads/topic"));
        Assert.Contains("branch \"topic\"", ConfigOf(root));
        Assert.Equal(tip, await RefAsync(root, "refs/daoris/discarded/topic"));
    }
}
