using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP2e over real git (D204; the git-writes design §6 <i>Rename</i>, §12 row e): a branch moved by one transaction through the act journal
/// (the old name deleted and the new one made, both checked at once, the old tip kept on a recovery ref), then its section renamed under git's
/// own <c>config.lock</c>; the held branches and an occupied one refused; a rename stopped between its ref and its section step offering
/// <i>Finish</i> and <i>Undo</i>, each conditional; the reversal checking both names.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitRefRenameTests : TreeSyncFixture
{
    private ActJournal Journal(string run) => new(Home, () => StoreSettled.Yes, null, run, StoreProcess.Shell);

    private ActRuns Runs(string run = "run-a") => new(Journal(run)) { ExitBound = TimeSpan.FromSeconds(2) };

    private static RefRenameAsk Ask(string root, string name, string to, string kind = "branch", string? at = null, string? line = null) =>
        new("engine", root, kind, name, to, at, line);

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

    /// <summary>A rename whose section step finds git's own lock standing: its ref step done, its section step refused, the act partial.</summary>
    private async Task<(string Root, string Tip, ActRun Run, string Lock)> PartialAsync()
    {
        var (root, tip) = await WithBranchAsync();
        var held = Path.Combine(root, ".git", "config.lock");
        var runs = Runs();
        runs.Seam = async (point, ordinal) =>
        {
            if (point == ActSeams.BeforeLaunch && ordinal == 1) await File.WriteAllTextAsync(held, "");
        };
        var run = await GitRefActs.RenameAsync(runs, Ask(root, "topic", "work"), "window", "window", CancellationToken.None);
        return (root, tip, run, held);
    }

    [Fact]
    public async Task A_branch_moves_with_a_recovery_ref_for_its_old_tip_its_section_renamed_and_the_act_journaled_closed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();

        var run = await GitRefActs.RenameAsync(Runs(), Ask(root, "topic", "work", at: tip), "window", "window", CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Null(await RefAsync(root, "refs/heads/topic"));
        Assert.Equal(tip, await RefAsync(root, "refs/heads/work"));
        Assert.Equal(tip, await RefAsync(root, "refs/daoris/discarded/topic"));
        Assert.DoesNotContain("branch \"topic\"", ConfigOf(root));
        Assert.Contains("branch \"work\"", ConfigOf(root));
        Assert.Equal("origin", (await GitAsync(root, "config", "branch.work.remote")).Trim());
        var act = Journal("run-a").Act(run.Act!)!;
        Assert.Equal((GitRefActs.RenameKind, "window", false), (act.Kind, act.Press, act.Open));
        Assert.Equal([ActStepKinds.Ref, ActStepKinds.Config], Journal("run-a").Steps(act.Id).Select(step => step.Kind));
    }

    [Fact]
    public async Task A_lightweight_tag_moves_with_no_section_and_an_annotated_one_is_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var tip = await RevAsync(root, "main");
        await GitAsync(root, "tag", "light", "main");
        await GitAsync(root, "tag", "-a", "noted", "-m", "noted", "main");

        var light = await GitRefActs.RenameAsync(Runs(), Ask(root, "light", "v1", "tag"), "window", "window", CancellationToken.None);
        var noted = await GitRefActs.RenameAsync(Runs(), Ask(root, "noted", "v2", "tag"), "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, light.Outcome);
        Assert.Equal((null, tip), (await RefAsync(root, "refs/tags/light"), await RefAsync(root, "refs/tags/v1")));
        Assert.Equal([ActStepKinds.Ref], Journal("run-a").Steps(light.Act!).Select(step => step.Kind));
        Assert.True(noted.Refused);
        Assert.Contains("annotated", noted.Said);
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
            await GitRefActs.RenameAsync(Runs(), Ask(root, "daoris/s-1", "mine"), "window", "window", CancellationToken.None),
            await GitRefActs.RenameAsync(Runs(), Ask(root, "landed-one", "mine"), "window", "window", CancellationToken.None),
            await GitRefActs.RenameAsync(Runs(), Ask(root, "topic", "mine", line: "topic"), "window", "window", CancellationToken.None),
            await GitRefActs.RenameAsync(Runs(), Ask(root, "bound", "mine"), "window", "window", CancellationToken.None),
            await GitRefActs.RenameAsync(Runs(), Ask(root, "topic", "daoris/mine"), "window", "window", CancellationToken.None),
        };

        Assert.All(held, each => { Assert.True(each.Refused); Assert.Null(each.Act); });
        Assert.Contains("daoris/", held[0].Said);
        Assert.Contains("landing", held[1].Said);
        Assert.Contains("line", held[2].Said);
        Assert.Contains(opened!.Id, held[3].Said);
        Assert.Contains("daoris/", held[4].Said);
        Assert.All(held, each => Assert.Contains("nothing was renamed", each.Said));
        foreach (var name in new[] { "daoris/s-1", "landed-one", "topic", "bound" }) Assert.NotNull(await RefAsync(root, $"refs/heads/{name}"));
        Assert.Null(await RefAsync(root, "refs/heads/mine"));
        Assert.Empty(Journal("run-a").Unclosed());
    }

    [Fact]
    public async Task A_branch_a_tree_has_checked_out_and_a_name_already_taken_are_refused_before_anything_is_written()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();
        await GitAsync(root, "branch", "taken", "main");
        await GitAsync(root, "branch", "deep/er", "main");
        await GitAsync(root, "worktree", "add", Path.Combine(Path.GetDirectoryName(root)!, "held-tree"), "topic");

        var occupied = await GitRefActs.RenameAsync(Runs(), Ask(root, "topic", "work"), "window", "window", CancellationToken.None);
        var free = await GitRefActs.RenameAsync(Runs(), Ask(root, "deep/er", "taken"), "window", "window", CancellationToken.None);
        var above = await GitRefActs.RenameAsync(Runs(), Ask(root, "deep/er", "taken/x"), "window", "window", CancellationToken.None);

        Assert.True(occupied.Refused);
        Assert.Contains("checked out", occupied.Said);
        Assert.True(free.Refused);
        Assert.Contains("already exists", free.Said);
        Assert.True(above.Refused);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/topic"));
        Assert.Null(await RefAsync(root, "refs/heads/work"));
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

        var run = await GitRefActs.RenameAsync(runs, Ask(root, "topic", "work"), "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.NotDone, run.Outcome);
        Assert.Contains("checked out", run.Said);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/topic"));
        Assert.Null(await RefAsync(root, "refs/heads/work"));
        Assert.Null(await RefAsync(root, "refs/daoris/discarded/topic"));
        Assert.Contains("branch \"topic\"", ConfigOf(root));
        Assert.Empty(Journal("run-a").Unclosed());
    }

    [Fact]
    public async Task A_branch_that_moved_since_the_page_showed_it_is_refused_and_a_target_made_before_the_effect_keeps_the_branch()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();
        var shown = (await GitAsync(root, "commit-tree", "main^{tree}", "-p", "main", "-m", "shown")).Trim();
        var later = (await GitAsync(root, "commit-tree", "main^{tree}", "-p", "main", "-m", "later")).Trim();
        var runs = Runs();
        runs.Seam = async (point, _) =>
        {
            if (point == ActSeams.BeforeLaunch) await GitAsync(root, "update-ref", "refs/heads/work", later);
        };

        var stale = await GitRefActs.RenameAsync(Runs(), Ask(root, "topic", "work", at: shown), "window", "window", CancellationToken.None);
        var raced = await GitRefActs.RenameAsync(runs, Ask(root, "topic", "work"), "window", "window", CancellationToken.None);

        Assert.True(stale.Refused);
        Assert.Contains("no longer", stale.Said);
        // A target that appeared at another value reads as neither before nor after (git's transaction is atomic, but the postcondition is read per ref).
        Assert.Equal(ActOutcomes.Unknown, raced.Outcome);
        Assert.Equal((tip, later), (await RefAsync(root, "refs/heads/topic"), await RefAsync(root, "refs/heads/work")));
        Assert.Null(await RefAsync(root, "refs/daoris/discarded/topic"));
    }

    [Fact]
    public async Task Undo_moves_the_branch_back_checking_both_names_restores_its_section_and_lets_the_recovery_ref_go()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();
        var before = ConfigOf(root);
        var renamed = await GitRefActs.RenameAsync(Runs(), Ask(root, "topic", "work"), "window", "window", CancellationToken.None);

        var undone = await GitRefActs.UndoAsync(Runs(), "engine", root, renamed.Act!, "window", "window", CancellationToken.None);
        var twice = await GitRefActs.UndoAsync(Runs(), "engine", root, renamed.Act!, "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, undone.Outcome);
        Assert.Equal((tip, null), (await RefAsync(root, "refs/heads/topic"), await RefAsync(root, "refs/heads/work")));
        Assert.Equal(before, ConfigOf(root));
        Assert.Null(await RefAsync(root, "refs/daoris/discarded/topic"));
        Assert.True(twice.Refused);
        Assert.Equal(GitRefActs.UndoKind, Journal("run-a").Act(undone.Act!)!.Kind);
    }

    [Fact]
    public async Task Undo_is_refused_where_the_new_name_moved_or_the_old_name_was_made_again_and_nothing_is_written()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip) = await WithBranchAsync();
        var renamed = await GitRefActs.RenameAsync(Runs(), Ask(root, "topic", "work"), "window", "window", CancellationToken.None);
        var later = (await GitAsync(root, "commit-tree", "main^{tree}", "-p", "main", "-m", "later")).Trim();
        await GitAsync(root, "update-ref", "refs/heads/work", later);
        var moved = await GitRefActs.UndoAsync(Runs(), "engine", root, renamed.Act!, "window", "window", CancellationToken.None);
        await GitAsync(root, "update-ref", "refs/heads/work", tip);
        await GitAsync(root, "branch", "topic", "main");
        var again = await GitRefActs.UndoAsync(Runs(), "engine", root, renamed.Act!, "window", "window", CancellationToken.None);

        Assert.True(moved.Refused);
        Assert.Contains("work", moved.Said);
        Assert.True(again.Refused);
        Assert.Contains("topic", again.Said);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/work"));
        Assert.Contains("branch \"work\"", ConfigOf(root));
    }

    [Fact]
    public async Task A_rename_whose_section_step_was_refused_is_partial_and_names_the_lock_it_never_removed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, run, held) = await PartialAsync();

        Assert.False(run.Refused);
        Assert.Equal(ActOutcomes.Partial, run.Outcome);
        Assert.Contains("config.lock", Journal("run-a").Steps(run.Act!)[1].Said);
        Assert.True(File.Exists(held));
        Assert.Equal((null, tip), (await RefAsync(root, "refs/heads/topic"), await RefAsync(root, "refs/heads/work")));
        Assert.Contains("branch \"topic\"", ConfigOf(root));
    }

    [Fact]
    public async Task Finish_moves_the_section_once_the_lock_is_gone_and_only_where_the_ref_and_the_section_stand_as_journaled()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, partial, held) = await PartialAsync();
        var blocked = await GitRefActs.FinishAsync(Runs(), "engine", root, partial.Act!, "window", "window", CancellationToken.None);
        File.Delete(held);

        var finished = await GitRefActs.FinishAsync(Runs(), "engine", root, partial.Act!, "window", "window", CancellationToken.None);
        var twice = await GitRefActs.FinishAsync(Runs(), "engine", root, partial.Act!, "window", "window", CancellationToken.None);

        Assert.True(blocked.Refused || blocked.Outcome != ActOutcomes.Done);
        Assert.Equal(ActOutcomes.Done, finished.Outcome);
        Assert.Contains("branch \"work\"", ConfigOf(root));
        Assert.DoesNotContain("branch \"topic\"", ConfigOf(root));
        Assert.Equal(GitRefActs.FinishKind, Journal("run-a").Act(finished.Act!)!.Kind);
        Assert.True(twice.Refused);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/work"));
    }

    [Fact]
    public async Task Finish_is_refused_where_the_section_changed_the_ref_moved_or_the_act_was_not_a_partial_rename()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, _, partial, held) = await PartialAsync();
        File.Delete(held);
        await GitAsync(root, "config", "branch.topic.rebase", "true");
        var changed = await GitRefActs.FinishAsync(Runs(), "engine", root, partial.Act!, "window", "window", CancellationToken.None);
        await GitAsync(root, "config", "--unset", "branch.topic.rebase");
        var later = (await GitAsync(root, "commit-tree", "main^{tree}", "-p", "main", "-m", "later")).Trim();
        await GitAsync(root, "update-ref", "refs/heads/work", later);
        var moved = await GitRefActs.FinishAsync(Runs(), "engine", root, partial.Act!, "window", "window", CancellationToken.None);
        var done = await GitRefActs.CreateAsync(Runs(), new RefAsk("engine", root, "branch", "other", null), "window", "window", CancellationToken.None);
        var notRename = await GitRefActs.FinishAsync(Runs(), "engine", root, done.Act!, "window", "window", CancellationToken.None);

        Assert.True(changed.Refused);
        Assert.Contains("changed", changed.Said);
        Assert.True(moved.Refused);
        Assert.True(notRename.Refused);
        Assert.Contains("branch \"topic\"", ConfigOf(root));
    }

    [Fact]
    public async Task Undo_of_a_partial_rename_moves_only_the_ref_back_and_Finish_is_then_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, partial, held) = await PartialAsync();
        File.Delete(held);

        var undone = await GitRefActs.UndoAsync(Runs(), "engine", root, partial.Act!, "window", "window", CancellationToken.None);
        var finish = await GitRefActs.FinishAsync(Runs(), "engine", root, partial.Act!, "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, undone.Outcome);
        Assert.Equal((tip, null), (await RefAsync(root, "refs/heads/topic"), await RefAsync(root, "refs/heads/work")));
        Assert.Null(await RefAsync(root, "refs/daoris/discarded/topic"));
        Assert.Contains("branch \"topic\"", ConfigOf(root));
        Assert.True(finish.Refused);
    }

    [Fact]
    public async Task Undo_of_a_partial_rename_after_Finish_is_refused_and_the_Finish_act_undoes_both_steps()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, tip, partial, held) = await PartialAsync();
        File.Delete(held);
        var finished = await GitRefActs.FinishAsync(Runs(), "engine", root, partial.Act!, "window", "window", CancellationToken.None);
        var before = ConfigOf(root);

        var stale = await GitRefActs.UndoAsync(Runs(), "engine", root, partial.Act!, "window", "window", CancellationToken.None);
        var whole = await GitRefActs.UndoAsync(Runs(), "engine", root, finished.Act!, "window", "window", CancellationToken.None);

        Assert.True(stale.Refused);
        Assert.Equal(before.Contains("branch \"work\""), true);
        Assert.Equal(ActOutcomes.Done, whole.Outcome);
        Assert.Equal((tip, null), (await RefAsync(root, "refs/heads/topic"), await RefAsync(root, "refs/heads/work")));
        Assert.Contains("branch \"topic\"", ConfigOf(root));
        Assert.DoesNotContain("branch \"work\"", ConfigOf(root));
    }
}
