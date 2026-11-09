using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// History edits over real git: pick, revert and rebase onto, on a branch no tree has out,
/// from <c>merge-tree --merge-base</c> and <c>commit-tree</c>, then one ref transaction through the act journal. Each refusal moves
/// nothing and journals nothing; a rebase's commits are unpublished while pick and revert take a published source; a configured merge
/// driver refuses before any <c>merge-tree</c> starts; a branch checked out mid-way is put back; a cut between the objects and the
/// update leaves the branch at its tip.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitHistoryEditTests : TreeSyncFixture
{
    private ActJournal Journal(string run, Func<StoreRun, bool>? alive = null) => new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    private ActRuns Runs(string run = "run-a") => new(Journal(run)) { ExitBound = TimeSpan.FromSeconds(2) };

    private HistoryEditAsk Ask(string root, string edit, string branch, string target) => new(Home, "engine", root, edit, branch, target) { Line = "main" };

    private static async Task<string?> RefAsync(string root, string name)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", name], CancellationToken.None);
        return code == 0 ? said.Trim() : null;
    }

    private static async Task<string> KeptAsync(string root) =>
        (await GitAsync(root, "for-each-ref", "--format=%(refname)", ContentHolds.DiscardedPrefix)).Trim();

    /// <summary>main with `first`; `topic` from it with one commit of its own (t.txt); main then one more (m.txt).</summary>
    private async Task<string> TopicAsync()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "topic", "main");
        await CommitOnAsync(root, "topic", "t.txt", "t\n", "topic one");
        await CommitAsync(root, "m.txt", "m\n", "main one");
        return root;
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

    /// <summary>Nothing moved and nothing journaled: the branch at its tip, no recovery ref, no act open.</summary>
    private async Task NothingMovedAsync(string root, string branch, string tip, HistoryEditRun run)
    {
        Assert.True(run.Run.Refused, run.Run.Said);
        Assert.Null(run.Run.Act);
        Assert.Equal(tip, await RefAsync(root, $"refs/heads/{branch}"));
        Assert.Equal("", await KeptAsync(root));
        Assert.Empty(Journal("run-a").Unclosed());
    }

    [Fact]
    public async Task A_commit_is_picked_onto_a_branch_with_its_author_and_message_and_the_old_tip_kept()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var picked = await RevAsync(root, "main");
        var old = await RevAsync(root, "topic");

        var run = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "pick", "topic", picked), "window", "window", CancellationToken.None);

        Assert.False(run.Run.Refused, run.Run.Said);
        Assert.Equal(ActOutcomes.Done, run.Run.Outcome);
        var tip = await RevAsync(root, "topic");
        Assert.Equal(tip, run.Tip);
        Assert.Equal(old, await RevAsync(root, "topic^"));
        Assert.Equal("m\n", await GitAsync(root, "show", "topic:m.txt"));
        Assert.Equal(
            (await GitAsync(root, "log", "-1", "--format=%an|%ae|%at|%B", picked)).Trim(),
            (await GitAsync(root, "log", "-1", "--format=%an|%ae|%at|%B", tip)).Trim());
        Assert.Equal(old, await RefAsync(root, run.Kept!));
        Assert.StartsWith(ContentHolds.DiscardedPrefix, run.Kept);
        var act = Journal("run-a").Act(run.Run.Act!)!;
        Assert.Equal((GitHistoryEdits.PickKind, false, ActOutcomes.Done), (act.Kind, act.Open, act.Outcome));
    }

    [Fact]
    public async Task A_branch_is_rebased_onto_another_commit_each_commit_picked_in_order()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        await CommitOnAsync(root, "topic", "u.txt", "u\n", "topic two");
        var old = await RevAsync(root, "topic");
        var told = new List<(int, int)>();

        var run = await GitHistoryEdits.EditAsync(
            Runs(), Ask(root, "rebase", "topic", "main") with { Progress = (made, of) => told.Add((made, of)) }, "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, run.Run.Outcome);
        Assert.Equal(await RevAsync(root, "main"), await RevAsync(root, "topic~2"));
        Assert.Equal(["topic two", "topic one"], (await GitAsync(root, "log", "--format=%s", "-2", "topic")).Trim().Split('\n'));
        Assert.Equal(old, await RefAsync(root, run.Kept!));
        Assert.Equal([(1, 2), (2, 2)], told);
    }

    [Fact]
    public async Task A_published_commit_is_reverted_onto_a_branch_with_gits_message()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, _) = await RepositoryWithOriginAsync("engine");
        await CommitAsync(root, "m.txt", "m\n", "main one");
        await GitAsync(root, "push", "--quiet", "origin", "main");
        var published = await RevAsync(root, "main");
        await GitAsync(root, "branch", "topic", "main");

        var run = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "revert", "topic", published), "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, run.Run.Outcome);
        Assert.Equal($"Revert \"main one\"\n\nThis reverts commit {published}.", (await GitAsync(root, "log", "-1", "--format=%B", "topic")).Trim());
        Assert.Equal("", (await GitAsync(root, "ls-tree", "--name-only", "topic", "m.txt")).Trim());
        Assert.Equal(published, await RevAsync(root, "topic^"));
    }

    [Fact]
    public async Task A_rebase_of_published_commits_is_refused_and_nothing_moves()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, _) = await RepositoryWithOriginAsync("engine");
        await GitAsync(root, "branch", "topic", "main");
        await CommitOnAsync(root, "topic", "t.txt", "t\n", "topic one");
        await GitAsync(root, "push", "--quiet", "origin", "topic");
        await CommitAsync(root, "m.txt", "m\n", "main one");
        var tip = await RevAsync(root, "topic");

        var run = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "rebase", "topic", "main"), "window", "window", CancellationToken.None);

        await NothingMovedAsync(root, "topic", tip, run);
        Assert.Contains("origin/topic", run.Run.Said);
    }

    [Fact]
    public async Task A_conflict_anywhere_is_refused_with_its_files_named_and_nothing_moves()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "topic", "main");
        await CommitOnAsync(root, "topic", "shared.txt", "topic\n", "topic's shared");
        await CommitAsync(root, "shared.txt", "main\n", "main's shared");
        var tip = await RevAsync(root, "topic");

        var run = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "rebase", "topic", "main"), "window", "window", CancellationToken.None);

        await NothingMovedAsync(root, "topic", tip, run);
        Assert.Contains("shared.txt", run.Run.Said);
        Assert.Contains("only objects", run.Run.Said);
    }

    [Fact]
    public async Task A_merge_in_the_range_is_refused_and_nothing_moves()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        await GitAsync(root, "branch", "side", "main~1");
        await CommitOnAsync(root, "side", "s.txt", "s\n", "side one");
        var merge = (await GitAsync(root, "commit-tree", "topic^{tree}", "-p", "topic", "-p", "side", "-m", "a merge")).Trim();
        await GitAsync(root, "update-ref", "refs/heads/topic", merge);

        var run = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "rebase", "topic", "main"), "window", "window", CancellationToken.None);

        await NothingMovedAsync(root, "topic", merge, run);
        Assert.Contains("merge", run.Run.Said);
    }

    [Fact]
    public async Task A_root_commit_is_refused_and_nothing_moves()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var first = (await GitAsync(root, "rev-list", "--max-parents=0", "main")).Trim();
        var tip = await RevAsync(root, "topic");

        var run = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "pick", "topic", first), "window", "window", CancellationToken.None);

        await NothingMovedAsync(root, "topic", tip, run);
        Assert.Contains("root", run.Run.Said);
    }

    [Theory]
    [InlineData("shallow")]
    [InlineData("info/grafts")]
    public async Task A_commit_git_reads_with_no_parent_a_shallow_boundary_or_a_grafted_root_is_refused_as_a_root(string file)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        await GitAsync(root, "branch", "other", "main");
        var picked = await RevAsync(root, "topic");
        var tip = await RevAsync(root, "other");
        await File.WriteAllTextAsync(Path.Combine(root, ".git", file), picked + "\n");

        var run = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "pick", "other", picked), "window", "window", CancellationToken.None);

        await NothingMovedAsync(root, "other", tip, run);
        Assert.Contains("root", run.Run.Said);
    }

    [Fact]
    public async Task A_signed_commit_is_refused_and_nothing_moves()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var raw = await GitAsync(root, "cat-file", "commit", "main");
        var cut = raw.IndexOf("\n\n", StringComparison.Ordinal);
        var signed = raw[..cut] + "\ngpgsig -----BEGIN PGP SIGNATURE-----\n \n iQEz\n -----END PGP SIGNATURE-----" + raw[cut..];
        var file = Path.Combine(Scratch, "signed.commit");
        await File.WriteAllTextAsync(file, signed);
        var id = (await GitAsync(root, "hash-object", "-t", "commit", "-w", file)).Trim();
        var tip = await RevAsync(root, "topic");

        var run = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "pick", "topic", id), "window", "window", CancellationToken.None);

        await NothingMovedAsync(root, "topic", tip, run);
        Assert.Contains("signed", run.Run.Said);
    }

    [Fact]
    public async Task An_empty_result_is_refused_and_nothing_moves()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var tip = await RevAsync(root, "topic");

        var run = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "pick", "topic", tip), "window", "window", CancellationToken.None);

        await NothingMovedAsync(root, "topic", tip, run);
        Assert.Contains("changes nothing", run.Run.Said);
    }

    [Fact]
    public async Task More_than_fifty_commits_are_refused_with_the_command_to_copy()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var at = await RevAsync(root, "topic");
        for (var n = 0; n < GitHistoryEdits.MostCommits; n++)
        {
            at = (await GitAsync(root, "commit-tree", "topic^{tree}", "-p", at, "-m", $"step {n}")).Trim();
        }

        await GitAsync(root, "update-ref", "refs/heads/topic", at);

        var run = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "rebase", "topic", "main"), "window", "window", CancellationToken.None);

        await NothingMovedAsync(root, "topic", at, run);
        Assert.Contains("git rebase main topic", run.Run.Said);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("daoris/s-1")]
    public async Task The_line_and_daoriss_own_branches_are_refused(string branch)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        if (branch != "main") await GitAsync(root, "branch", branch, "topic");
        var tip = await RevAsync(root, branch);

        var run = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "rebase", branch, "topic"), "window", "window", CancellationToken.None);

        await NothingMovedAsync(root, branch, tip, run);
    }

    [Fact]
    public async Task A_landings_branch_a_together_bound_one_and_one_checked_out_are_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        foreach (var name in new[] { "landed", "bound", "out" }) await GitAsync(root, "branch", name, "topic");
        var tip = await RevAsync(root, "topic");
        new LandedBranches(Home).Record(new LandedBranch("engine", "aurora", "landed", "main", tip, "s-1", null, null, DateTimeOffset.UtcNow));
        new TogetherOperations(Home).Open("engine", new TogetherHead("main", "rec-1", "tree-1", "bound", tip), [], DateTimeOffset.UtcNow);
        await GitAsync(root, "worktree", "add", "--quiet", Path.Combine(Scratch, "out-tree"), "out");

        var landed = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "rebase", "landed", "main"), "window", "window", CancellationToken.None);
        var bound = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "rebase", "bound", "main"), "window", "window", CancellationToken.None);
        var checkedOut = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "rebase", "out", "main"), "window", "window", CancellationToken.None);

        await NothingMovedAsync(root, "landed", tip, landed);
        await NothingMovedAsync(root, "bound", tip, bound);
        await NothingMovedAsync(root, "out", tip, checkedOut);
        Assert.Contains("landing", landed.Run.Said);
        Assert.Contains("brought together", bound.Run.Said);
        Assert.Contains("out-tree", checkedOut.Run.Said);
        Assert.DoesNotContain(Scratch, checkedOut.Run.Said);
    }

    [Theory]
    [InlineData("local")]
    [InlineData("worktree")]
    public async Task A_merge_driver_in_the_checkouts_own_configuration_is_refused_before_merge_tree_starts(string scope)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        if (scope == "worktree") await GitAsync(root, "config", "extensions.worktreeConfig", "true");
        await GitAsync(root, "config", $"--{scope}", "merge.ours.driver", "true");
        var tip = await RevAsync(root, "topic");
        var merges = 0;
        WorkingTree.Started.Value = arguments => { if (arguments.Contains("merge-tree")) Interlocked.Increment(ref merges); };
        try
        {
            var run = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "rebase", "topic", "main"), "window", "window", CancellationToken.None);

            await NothingMovedAsync(root, "topic", tip, run);
            Assert.Contains("merge.ours.driver", run.Run.Said);
            Assert.Contains(scope, run.Run.Said);
            Assert.Equal(0, merges);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }
    }

    [Fact]
    public async Task Renormalizing_beside_a_filter_and_signing_by_configuration_are_refused_before_merge_tree_starts()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var tip = await RevAsync(root, "topic");
        await GitAsync(root, "config", "merge.renormalize", "true");
        await GitAsync(root, "config", "filter.lfs.clean", "cat");

        var renormalize = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "rebase", "topic", "main"), "window", "window", CancellationToken.None);
        await GitAsync(root, "config", "--unset", "merge.renormalize");
        await GitAsync(root, "config", "commit.gpgSign", "true");
        var signing = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "rebase", "topic", "main"), "window", "window", CancellationToken.None);

        await NothingMovedAsync(root, "topic", tip, renormalize);
        await NothingMovedAsync(root, "topic", tip, signing);
        Assert.Contains("merge.renormalize", renormalize.Run.Said);
        Assert.Contains("commit.gpgSign", signing.Run.Said);
    }

    [Fact]
    public async Task A_branch_that_moved_since_its_plan_was_shown_is_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var (plan, refusal) = await GitHistoryEdits.PlanAsync(Ask(root, "rebase", "topic", "main"), CancellationToken.None);
        Assert.Null(refusal);
        await CommitOnAsync(root, "topic", "late.txt", "late\n", "late");
        var tip = await RevAsync(root, "topic");

        var run = await GitHistoryEdits.EditAsync(Runs(), Ask(root, "rebase", "topic", "main") with { Pins = plan!.Pins }, "window", "window", CancellationToken.None);

        await NothingMovedAsync(root, "topic", tip, run);
        Assert.Contains("since", run.Run.Said);
    }

    [Fact]
    public async Task A_branch_checked_out_mid_way_is_put_back_where_it_was()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var old = await RevAsync(root, "topic");
        var runs = Runs();
        runs.Seam = async (point, _) =>
        {
            if (point == ActSeams.BeforeLaunch) await GitAsync(root, "worktree", "add", "--quiet", Path.Combine(Scratch, "late-tree"), "topic");
        };

        var run = await GitHistoryEdits.EditAsync(runs, Ask(root, "rebase", "topic", "main"), "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.NotDone, run.Run.Outcome);
        Assert.Contains("late-tree", run.Run.Said);
        Assert.Equal(old, await RevAsync(root, "topic"));
        Assert.Equal("", await KeptAsync(root));
        Assert.Empty(Journal("run-a").Unclosed());
    }

    [Fact]
    public async Task A_cut_after_the_objects_and_before_the_update_leaves_the_branch_where_it_was()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var old = await RevAsync(root, "topic");
        var runs = Runs();
        runs.Seam = (point, _) => point == ActSeams.BeforeLaunch ? throw new InvalidOperationException("killed") : Task.CompletedTask;
        await Assert.ThrowsAsync<InvalidOperationException>(() => GitHistoryEdits.EditAsync(runs, Ask(root, "rebase", "topic", "main"), "window", "window", CancellationToken.None));
        var act = Journal("run-a").Unclosed().Single();
        KillRun("run-a");

        var hold = await new ActReconciler(Journal("run-b", run => run.Run != "run-a")) { ExitBound = TimeSpan.FromSeconds(2) }.UnderLockAsync(root, CancellationToken.None);

        Assert.Null(hold);
        Assert.Equal((ActPhases.Closed, ActOutcomes.NotDone), (Journal("run-b").Act(act.Id)!.Phase, Journal("run-b").Act(act.Id)!.Outcome));
        Assert.Equal(old, await RevAsync(root, "topic"));
        Assert.Equal("", await KeptAsync(root));
    }

    [Theory]
    [InlineData("git version 2.39.5", false)]
    [InlineData("git version 2.40.0.windows.1", true)]
    [InlineData("git version 2.53.0.windows.3", true)]
    [InlineData("nothing", false)]
    public void History_edits_are_offered_from_git_2_40(string said, bool offered) =>
        Assert.Equal(offered, GitHistoryEdits.Floor(said) is null);
}
