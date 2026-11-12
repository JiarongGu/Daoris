using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The <c>rebase</c> step's replay over real git (<see cref="RebaseReplay"/>): one test per outcome, a chain's later step onto the chain's branch,
/// a squash chain, 2026-11-09's sequence of works archiving their rows while the line moved, a stop mid-replay, and a member an operation brought in.
/// Each leaves the person's checkout, the work's branch and its tree byte for byte as they were, and no tree of its own behind.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class RebaseReplayGitTests : LandedFixture
{
    private const string Rows = "# Tasks\n\n- [ ] **A1** one.\n- [ ] **A2** two.\n- [ ] **A3** three.\n";

    private RebaseReplay Replay => new(new SessionTrees(Home).Settles);

    [Fact]
    public async Task A_work_the_line_moved_under_is_ready_replayed_onto_its_tip()
    {
        var root = await RepositoryAsync("engine");
        var tree = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(root, "line.txt", "the line's\n", "the line's own");
        var before = await StateAsync(root, tree);

        var made = await Replay.ReplayAsync(await WorkAsync(root, tree), tree.Path, await LineAsync(root), [], CancellationToken.None);

        Assert.Equal(RebaseOutcomes.Ready, made.Outcome);
        Assert.Equal("Rebased 1 commit(s) onto `main`.", made.Said);
        Assert.Equal(await HeadAsync(root), (await GitAsync(root, "rev-parse", $"{made.Commit}^")).Trim());
        Assert.Equal(await HeadAsync(root), made.Onto);
        Assert.Equal("the work\n", await GitAsync(root, "show", $"{made.Commit}:work.txt"));
        Assert.Equal(before, await StateAsync(root, tree));
        Assert.Equal(2, await TreesAsync(root));
    }

    [Fact]
    public async Task A_work_grown_from_the_targets_tip_is_ready_at_its_own_tip()
    {
        var root = await RepositoryAsync("engine");
        var tree = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");

        var made = await Replay.ReplayAsync(await WorkAsync(root, tree), tree.Path, await LineAsync(root), [], CancellationToken.None);

        Assert.Equal(RebaseOutcomes.Ready, made.Outcome);
        Assert.Equal(await HeadAsync(tree.Path), made.Commit);
        Assert.Equal(0, made.Replayed);
    }

    [Fact]
    public async Task A_code_conflict_is_conflicted_with_its_stop_named_and_nothing_written()
    {
        var root = await RepositoryAsync("engine");
        var tree = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "shared.txt", "from the work\n", "Add the reader");
        await CommitAsync(root, "shared.txt", "from the line\n", "the line's own");
        var before = await StateAsync(root, tree);
        var work = await WorkAsync(root, tree);

        var made = await Replay.ReplayAsync(work, tree.Path, await LineAsync(root), [], CancellationToken.None);

        Assert.Equal(RebaseOutcomes.Conflicted, made.Outcome);
        Assert.StartsWith($"Could not settle `shared.txt` at `{work.Tip[..7]}` “Add the reader”: CONFLICT", made.Said);
        Assert.Equal(ProblemSorts.Conflict, made.Stop!.Sort);
        Assert.Equal(["shared.txt"], made.Stop.Paths);
        Assert.NotNull(made.Stop.PatchId);
        Assert.Equal(work.Tip, made.StopCommit);
        Assert.Equal(before, await StateAsync(root, tree));
        Assert.Equal(2, await TreesAsync(root));
    }

    [Fact]
    public async Task A_tree_holding_work_no_commit_carries_is_uncommitted_with_its_blobs()
    {
        var root = await RepositoryAsync("engine");
        var tree = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "loose.txt"), "not committed\n");
        var before = await StateAsync(root, tree);

        var made = await Replay.ReplayAsync(await WorkAsync(root, tree), tree.Path, await LineAsync(root), [], CancellationToken.None);

        Assert.Equal(RebaseOutcomes.Uncommitted, made.Outcome);
        var loose = Assert.Single(made.Uncommitted);
        Assert.Equal("loose.txt", loose.Path);
        Assert.Equal((await GitAsync(tree.Path, "hash-object", "loose.txt")).Trim(), loose.Blob);
        Assert.Equal(ProblemSorts.Uncommitted, made.Stop!.Sort);
        Assert.Equal(before, await StateAsync(root, tree));
    }

    /// <summary>TreeBranches' advance, through the kind: step 2 onto the chain's branch, the line moved meanwhile, replays nothing of step 1.</summary>
    [Fact]
    public async Task A_chains_step_two_onto_the_chains_branch_replays_only_its_own_and_fast_forwards()
    {
        var root = await RepositoryAsync("engine");
        var tree = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "one.txt", "step one\n", "step one");
        await GitAsync(root, "branch", "feature/chain", await HeadAsync(tree.Path));
        await CommitAsync(tree.Path, "two.txt", "step two\n", "step two");
        await CommitAsync(root, "line.txt", "the line moved\n", "the line's own");
        var chain = new RebaseTarget("refs/heads/feature/chain", (await GitAsync(root, "rev-parse", "feature/chain")).Trim());

        var made = await Replay.ReplayAsync(await WorkAsync(root, tree), tree.Path, chain, [], CancellationToken.None);

        Assert.Equal(RebaseOutcomes.Ready, made.Outcome);
        Assert.Equal(await HeadAsync(tree.Path), made.Commit);
        Assert.Equal(0, made.Replayed);
        Assert.Equal("", (await GitAsync(root, "merge-base", "--is-ancestor", chain.Tip, made.Commit!)).Trim());
    }

    /// <summary>A squash chain: the branch holds step 1 squashed, which its landing record names; step 2 replays its own commit alone onto the squash.</summary>
    [Fact]
    public async Task A_squash_chains_step_two_replays_its_own_commits_onto_the_squash()
    {
        var root = await RepositoryAsync("engine");
        var line = await HeadAsync(root);
        var tree = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "one.txt", "step one\n", "step one");
        await CommitAsync(tree.Path, "one-b.txt", "step one, again\n", "step one again");
        var stepOne = await HeadAsync(tree.Path);
        var squash = (await GitAsync(root, "commit-tree", $"{stepOne}^{{tree}}", "-p", line, "-m", "step one, squashed")).Trim();
        await GitAsync(root, "branch", "feature/chain", squash);
        await CommitAsync(tree.Path, "two.txt", "step two\n", "step two");
        await CommitAsync(root, "line.txt", "the line moved\n", "the line's own");

        var made = await Replay.ReplayAsync(await WorkAsync(root, tree), tree.Path, new RebaseTarget("refs/heads/feature/chain", squash), [stepOne],
            CancellationToken.None);

        Assert.Equal(RebaseOutcomes.Ready, made.Outcome);
        Assert.Equal(1, made.Replayed);
        Assert.Equal(squash, (await GitAsync(root, "rev-parse", $"{made.Commit}^")).Trim());
        Assert.Equal("step two\n", await GitAsync(root, "show", $"{made.Commit}:two.txt"));
        Assert.Equal("step one, again\n", await GitAsync(root, "show", $"{made.Commit}:one-b.txt"));
    }

    /// <summary>
    /// 2026-11-09: three works each archive their own row while the line moves under them, one landing after another. Each is <c>ready</c> with
    /// no session asked; a fourth, whose row the line rewrote while it archived it, is <c>conflicted</c>, naming the backlog.
    /// </summary>
    [Fact]
    public async Task Works_archiving_their_rows_one_after_another_are_ready_and_one_whose_row_the_line_changed_is_conflicted()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "TASKS.md", Rows + "- [ ] **A4** four.\n", "the backlog");
        var trees = new SessionTrees(Home);
        var works = new List<TreeOpened>();
        foreach (var (row, name) in new[] { ("A1", "w1"), ("A2", "w2"), ("A3", "w3"), ("A4", "w4") })
        {
            var tree = await trees.OpenAsync(root, "engine", name);
            await File.WriteAllTextAsync(Path.Combine(tree.Path, $"{name}.txt"), $"{name}\n");
            var text = await File.ReadAllTextAsync(Path.Combine(tree.Path, "TASKS.md"));
            await CommitAsync(tree.Path, "TASKS.md", string.Join("", text.Split('\n').Where(each => !each.Contains($"**{row}**", StringComparison.Ordinal)).Select(each => each + "\n")).TrimEnd('\n') + "\n", $"archive {row}");
            works.Add(tree);
        }

        foreach (var tree in works.Take(3))
        {
            var made = await Replay.ReplayAsync(await WorkAsync(root, tree), tree.Path, await LineAsync(root), [], CancellationToken.None);
            Assert.True(made.Outcome == RebaseOutcomes.Ready, made.Said);
            await GitAsync(root, "merge", "--ff-only", made.Commit!);
        }

        Assert.Equal("# Tasks\n\n- [ ] **A4** four.\n", await GitAsync(root, "show", "HEAD:TASKS.md"));
        Assert.Equal("w3\n", await GitAsync(root, "show", "HEAD:w3.txt"));
        await CommitAsync(root, "TASKS.md", "# Tasks\n\n- [ ] **A4** four, as the line says it now.\n", "the line's word on A4");

        var fourth = await Replay.ReplayAsync(await WorkAsync(root, works[3]), works[3].Path, await LineAsync(root), [], CancellationToken.None);

        Assert.Equal(RebaseOutcomes.Conflicted, fourth.Outcome);
        Assert.Contains("TASKS.md", fourth.Said, StringComparison.Ordinal);
        Assert.Equal(["TASKS.md"], fourth.Stop!.Paths);
    }

    /// <summary>A stop while the tree of its own is being made, and one while git replays in it: either way the tree is gone.</summary>
    [Theory]
    [InlineData(300)]
    [InlineData(3000)]
    public async Task A_stop_mid_replay_leaves_no_tree_of_its_own(int after)
    {
        var root = await RepositoryAsync("engine");
        var tree = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(root, "line.txt", "the line's\n", "the line's own");
        var hooks = (await GitAsync(root, "rev-parse", "--git-path", "hooks")).Trim();
        Directory.CreateDirectory(Path.Combine(root, hooks));
        await File.WriteAllTextAsync(Path.Combine(root, hooks, "pre-rebase"), "#!/bin/sh\nsleep 30\n");
        var work = await WorkAsync(root, tree);
        var line = await LineAsync(root);
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(after));

        await Record.ExceptionAsync(() => Replay.ReplayAsync(work, tree.Path, line, [], stop.Token));

        Assert.Equal(2, await TreesAsync(root));
        var own = DaorisHome.Entry(Home, HomeEntry.Trees);
        Assert.DoesNotContain(Directory.Exists(own) ? Directory.EnumerateDirectories(own, $"{BranchReplays.Prefix}*", SearchOption.AllDirectories) : [], _ => true);
    }

    /// <summary>
    /// A member an operation brought in, replayed by its head as new ids and with a path the head settled its own way, and one whose line the head
    /// then reverted: by the inclusion record each replays nothing, and is ready at the line's tip.
    /// </summary>
    [Fact]
    public async Task A_member_brought_in_replays_nothing_whatever_its_head_made_of_it()
    {
        var root = await RepositoryAsync("engine");
        var tree = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "member.txt", "the member's\n", "the member's work");
        var bound = await HeadAsync(tree.Path);
        await CommitAsync(root, "member.txt", "the member's, as the head settled it\n", "the member's work, brought in");
        TogetherOperation[] landed =
        [
            new("t-1", "engine", new TogetherHead("main", "s-head", Path.Combine(Scratch, "head"), "feature/head", bound),
                [new TogetherMember("s-member", tree.Path, tree.Branch, tree.BasedOn, bound)], TogetherPhase.Landed, DateTimeOffset.UtcNow)
            {
                Attempt = new TogetherAttempt(TogetherForms.Merge, "main", null, bound, DateTimeOffset.UtcNow),
            },
        ];
        var held = TogetherBinding.BroughtIn(landed, "engine", tree.Branch, tree.Path);
        Assert.Equal(bound, held);

        var made = await Replay.ReplayAsync(await WorkAsync(root, tree), tree.Path, await LineAsync(root), [held!], CancellationToken.None);
        Assert.Equal(RebaseOutcomes.Ready, made.Outcome);
        Assert.Equal(await HeadAsync(root), made.Commit);

        await GitAsync(root, "revert", "--no-edit", "HEAD");
        var reverted = await Replay.ReplayAsync(await WorkAsync(root, tree), tree.Path, await LineAsync(root), [held!], CancellationToken.None);
        Assert.Equal(RebaseOutcomes.Ready, reverted.Outcome);
        Assert.Equal(await HeadAsync(root), reverted.Commit);
        Assert.Equal(0, reverted.Replayed);
    }

    private static RebaseWork Work(string root, TreeOpened tree, string tip) => new(root, "engine", "default", tree.Branch, tip);

    private static async Task<RebaseWork> WorkAsync(string root, TreeOpened tree) => Work(root, tree, await HeadAsync(tree.Path));

    private static async Task<RebaseTarget> LineAsync(string root) => new("refs/heads/main", await HeadAsync(root));

    private static async Task<string> HeadAsync(string place) => (await GitAsync(place, "rev-parse", "HEAD")).Trim();

    private static async Task<int> TreesAsync(string root) =>
        (await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n').Count(line => line.StartsWith("worktree ", StringComparison.Ordinal));

    /// <summary>The person's checkout, the work's branch and its tree, as a replay must leave them.</summary>
    private static async Task<string> StateAsync(string root, TreeOpened tree) =>
        string.Join("\n",
            (await GitAsync(root, "for-each-ref", "--format=%(objectname) %(refname)", "refs/heads/")).Trim(),
            (await GitAsync(root, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
}
