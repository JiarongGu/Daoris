using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1e part e3a over real git (D215's note; the integration design §5.2 steps 2 and 3): a candidate formed from a line's queued
/// requests and each member replayed onto it in the line's own warm integration tree, containment judged first by the frozen tip; a
/// conflict aborted and handed back with the tree clean for the next member; a crash inside a replay recovered by putting the tree back at
/// the step's recorded start. Called from no production path. Never moves the line: promotion is row e4's.
/// </summary>
/// <remarks>The case names follow the merge tool's candidate tests (<c>merge-queue.test.ts</c>, INTEGRATE1c).</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationFormGitTests : LandedFixture
{
    private sealed class Killed() : Exception("the process died here");

    private IntegrationStore Store(string run, Func<StoreRun, bool>? alive = null) => new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    /// <summary>The second process's store: run-b, which reads run-a as dead.</summary>
    private IntegrationStore Taker() => Store("run-b", run => run.Run != "run-a");

    private string TreesHome => DaorisHome.Entry(Home, HomeEntry.Trees);

    private IntegrationReplays Replays(IntegrationStore store) => new(store, TreesHome);

    private static IntegrationReplayAsk Ask(string root) => new("engine", "aurora", root, "main");

    private static async Task<string> CommitOfAsync(string cwd, string reference) => (await GitAsync(cwd, "rev-parse", reference)).Trim();

    /// <summary>A branch from <c>main</c> with one commit for each file given, the checkout left on <c>main</c>; the branch's tip.</summary>
    private static async Task<string> BranchAsync(string root, string name, params (string File, string Content)[] commits)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", name, "main");
        foreach (var (file, content) in commits) await CommitAsync(root, file, content, $"work on {name}: {file}");
        var tip = await CommitOfAsync(root, name);
        await GitAsync(root, "checkout", "--quiet", "main");
        return tip;
    }

    /// <summary>The request of a branch at its frozen tip, the <paramref name="order"/>th to arrive; a rule that takes up to eight members.</summary>
    private static void Queue(IntegrationStore store, string id, string branch, string tip, int order) =>
        Assert.Null(store.Add(new IntegrationRequest(
            id, "engine", "aurora", branch, tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.Card,
            new LandingIntegrate(Batch: 8).Resolved(), new DateTimeOffset(2026, 10, 30, 9, 0, order, TimeSpan.Zero))));

    private static async Task<string[]> ParentsOfAsync(string cwd, string commit) =>
        (await GitAsync(cwd, "rev-list", "--parents", "-n", "1", commit)).Trim().Split(' ').Skip(1).ToArray();

    private static async Task<string> StatusAsync(string tree) => (await GitAsync(tree, "status", "--porcelain")).Trim();

    private static IntegrationMember Member(IntegrationStore store, IntegrationCandidate candidate, string request) =>
        store.Members(candidate.Id).Single(each => each.Request == request);

    /// <summary>
    /// 🔴 Two queued requests form one candidate on the line's commit and are replayed in order, each merged <c>--no-ff</c> onto the last in a
    /// warm tree of Daoris's own under the trees home: the merge commit's second parent is the frozen tip, the line never moves, and the
    /// person's checkout is left as it was.
    /// </summary>
    [Fact]
    public async Task Two_members_are_formed_and_replayed_in_order_in_a_tree_of_the_lines_own()
    {
        var root = await RepositoryAsync("engine");
        var @base = await CommitOfAsync(root, "main");
        var a = await BranchAsync(root, "feature/a", ("a.txt", "a\n"));
        var b = await BranchAsync(root, "feature/b", ("b.txt", "b\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1);
        Queue(store, "rb", "feature/b", b, 2);

        var run = await Replays(store).RunAsync(Ask(root));

        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        var candidate = store.Candidate(run.Candidate!.Id)!;
        Assert.Equal(@base, candidate.Base);
        var first = Member(store, candidate, "ra");
        var second = Member(store, candidate, "rb");
        Assert.Equal((IntegrationMemberStates.Replayed, IntegrationMemberStates.Replayed), (first.State, second.State));
        Assert.Equal(second.Replayed, candidate.Tip);
        Assert.Equal([@base, a], await ParentsOfAsync(root, first.Replayed!));
        Assert.Equal([first.Replayed!, b], await ParentsOfAsync(root, second.Replayed!));
        Assert.Equal("Merge branch 'feature/b'", (await GitAsync(root, "log", "-1", "--format=%s", second.Replayed!)).Trim());

        // The tree is the line's own, under the trees home, detached at the candidate's tip, clean and locked; no one's checkout was used.
        var tree = run.Tree!;
        Assert.StartsWith(Path.GetFullPath(TreesHome), Path.GetFullPath(tree), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetFullPath(root), Path.GetFullPath(tree), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(second.Replayed, await CommitOfAsync(tree, "HEAD"));
        Assert.Equal("", await StatusAsync(tree));
        Assert.Equal("HEAD", (await GitAsync(tree, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Contains("locked", await GitAsync(root, "worktree", "list", "--porcelain"), StringComparison.Ordinal);

        Assert.Equal(@base, await CommitOfAsync(root, "main"));
        Assert.Equal("main", (await GitAsync(root, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", await StatusAsync(root));

        // Each end wrote its step: the form, then a replay for each member, in the order given.
        Assert.Equal(
            [(IntegrationStepNames.Form, null, 1), (IntegrationStepNames.Replay, "ra", 1), (IntegrationStepNames.Replay, "rb", 1)],
            store.Steps(candidate.Id).Select(each => (each.Step, each.Request, each.Attempt)));
        Assert.All(store.Steps(candidate.Id), each => Assert.Equal((IntegrationStepOutcomes.Done, @base), (each.Outcome, each.LineCommit)));
    }

    /// <summary>
    /// 🔴 A member's tip is the one its request froze, never the branch's live one: a commit made on the branch after the request is not in
    /// the merge, and the merge commit's second parent is the frozen tip.
    /// </summary>
    [Fact]
    public async Task A_members_frozen_tip_is_replayed_and_never_the_branchs_live_one()
    {
        var root = await RepositoryAsync("engine");
        var frozen = await BranchAsync(root, "feature/a", ("a.txt", "a\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", frozen, 1);
        await GitAsync(root, "checkout", "--quiet", "feature/a");
        await CommitAsync(root, "later.txt", "made after the request froze\n", "after the freeze");
        var live = await CommitOfAsync(root, "feature/a");
        await GitAsync(root, "checkout", "--quiet", "main");

        var run = await Replays(store).RunAsync(Ask(root));

        var replayed = Member(store, run.Candidate!, "ra").Replayed!;
        Assert.Equal(frozen, (await ParentsOfAsync(root, replayed))[1]);
        Assert.DoesNotContain(live, await GitAsync(root, "rev-list", replayed), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(run.Tree!, "later.txt")));
    }

    /// <summary>
    /// 🔴 A member the line holds is contained and settled at once (nothing replayed, its request closed); one an earlier member's replay holds
    /// stays in the candidate contained by it, provisional, its request open, and is queued again where its candidate ends unsettled.
    /// </summary>
    [Fact]
    public async Task A_member_the_line_holds_is_settled_at_once_and_one_an_earlier_member_holds_is_provisional()
    {
        var root = await RepositoryAsync("engine");
        var held = await CommitOfAsync(root, "main");
        await CommitAsync(root, "line.txt", "the line moves\n", "the line moves");
        var x = await BranchAsync(root, "feature/x", ("x1.txt", "1\n"), ("x2.txt", "2\n"));
        var inside = await CommitOfAsync(root, "feature/x~1");
        var store = Store("run-a");
        Queue(store, "rz", "feature/z", held, 1);
        Queue(store, "rx", "feature/x", x, 2);
        Queue(store, "ry", "feature/y", inside, 3);

        var run = await Replays(store).RunAsync(Ask(root));

        var candidate = store.Candidate(run.Candidate!.Id)!;
        var z = Member(store, candidate, "rz");
        Assert.Equal((IntegrationMemberStates.Contained, null, false, null), (z.State, z.ContainedBy, z.Stands, z.Replayed));
        Assert.Equal(IntegrationRequestCloses.Settled, store.Request("rz")!.ClosedWhy);
        var replayed = Member(store, candidate, "rx");
        Assert.Equal(IntegrationMemberStates.Replayed, replayed.State);
        var y = Member(store, candidate, "ry");
        Assert.Equal((IntegrationMemberStates.Contained, "rx", true, null), (y.State, y.ContainedBy, y.Stands, y.Replayed));
        Assert.Null(store.Request("ry")!.ClosedAt);
        Assert.Equal(replayed.Replayed, candidate.Tip);
        Assert.Equal([await CommitOfAsync(root, "main"), x], await ParentsOfAsync(root, candidate.Tip!));

        // Said, and each its own step: z and y ended their replay without moving the tree.
        Assert.Equal(
            [IntegrationStepNames.Form, IntegrationStepNames.Replay, IntegrationStepNames.Replay, IntegrationStepNames.Replay],
            store.Steps(candidate.Id).Select(each => each.Step));

        // The candidate ends unsettled: the provisional member goes back to the queue beside its container, in arrival order.
        await new IntegrationSteps(store).RunAsync(
            run.Claim!, IntegrationStepNames.Settle,
            (_, _) => Task.FromResult(new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Ends = IntegrationCandidateEnds.FormedAgain }));
        Assert.Equal(["rx", "ry"], store.Queued("engine", "main").Select(each => each.Id));
    }

    /// <summary>
    /// 🔴 A member whose work the line holds by patch (a cherry-pick, so no ancestry shows it) is judged contained by D87 point 1's judgement and
    /// never merged, and never read as a conflict.
    /// </summary>
    [Fact]
    public async Task A_member_the_line_holds_by_patch_is_contained_and_not_a_conflict()
    {
        var root = await RepositoryAsync("engine");
        var x = await BranchAsync(root, "feature/x", ("x.txt", "x\n"));
        await GitAsync(root, "cherry-pick", "-x", x);
        var @base = await CommitOfAsync(root, "main");
        var store = Store("run-a");
        Queue(store, "rx", "feature/x", x, 1);

        var run = await Replays(store).RunAsync(Ask(root));

        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        var candidate = store.Candidate(run.Candidate!.Id)!;
        var member = Member(store, candidate, "rx");
        Assert.Equal((IntegrationMemberStates.Contained, null, false), (member.State, member.ContainedBy, member.Stands));
        Assert.Equal(IntegrationRequestCloses.Settled, store.Request("rx")!.ClosedWhy);
        // Nothing left to promote: the candidate ended, its line free, its tree where it was formed.
        Assert.Equal((IntegrationCandidateEnds.Empty, null), (candidate.EndedWhy, candidate.Tip));
        Assert.Null(store.StandingFor("engine", "main"));
        Assert.Equal(@base, await CommitOfAsync(run.Tree!, "HEAD"));
    }

    /// <summary>
    /// 🔴 Two members that conflict: the second is aborted, the tree put back at the candidate so far, and it is <c>conflict</c> then handed
    /// back at its own step with its request closed; the third goes on onto the first, so the warm tree was clean for it, and the line stays.
    /// </summary>
    [Fact]
    public async Task One_member_conflicting_is_handed_back_and_the_next_goes_on_in_a_clean_tree()
    {
        var root = await RepositoryAsync("engine");
        var @base = await CommitOfAsync(root, "main");
        var a = await BranchAsync(root, "feature/a", ("shared.txt", "from a\n"));
        var b = await BranchAsync(root, "feature/b", ("shared.txt", "from b\n"));
        var c = await BranchAsync(root, "feature/c", ("c.txt", "c\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1);
        Queue(store, "rb", "feature/b", b, 2);
        Queue(store, "rc", "feature/c", c, 3);

        var run = await Replays(store).RunAsync(Ask(root));

        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        var candidate = store.Candidate(run.Candidate!.Id)!;
        Assert.True(candidate.Stands);
        var first = Member(store, candidate, "ra");
        var conflicted = Member(store, candidate, "rb");
        var third = Member(store, candidate, "rc");
        Assert.Equal((IntegrationMemberStates.HandedBack, false, null), (conflicted.State, conflicted.Stands, conflicted.Replayed));
        Assert.Contains("shared.txt", conflicted.Said, StringComparison.Ordinal);
        Assert.Equal(IntegrationRequestCloses.HandedBack, store.Request("rb")!.ClosedWhy);
        Assert.Null(store.Request("ra")!.ClosedAt);

        // The third was merged onto the first's replay, not onto the conflicted attempt, and the tree holds only theirs.
        Assert.Equal([first.Replayed!, c], await ParentsOfAsync(root, third.Replayed!));
        Assert.Equal(third.Replayed, candidate.Tip);
        var tree = run.Tree!;
        Assert.Equal(third.Replayed, await CommitOfAsync(tree, "HEAD"));
        Assert.Equal("", await StatusAsync(tree));
        Assert.Equal("from a", (await File.ReadAllTextAsync(Path.Combine(tree, "shared.txt"))).Trim());
        Assert.True(File.Exists(Path.Combine(tree, "c.txt")));
        // No merge is left in progress in the warm tree: git keeps a linked tree's MERGE_HEAD in its own git directory.
        var mergeHead = (await GitAsync(tree, "rev-parse", "--path-format=absolute", "--git-path", "MERGE_HEAD")).Trim();
        Assert.False(File.Exists(mergeHead), mergeHead);
        Assert.Equal(@base, await CommitOfAsync(root, "main"));

        // The conflict was a replay that ended, then a step of its own that handed it back, before the next member's replay.
        Assert.Equal(
            [
                (IntegrationStepNames.Form, null, IntegrationStepOutcomes.Done), (IntegrationStepNames.Replay, "ra", IntegrationStepOutcomes.Done),
                (IntegrationStepNames.Replay, "rb", IntegrationStepOutcomes.Done), (IntegrationStepNames.Conflict, "rb", IntegrationStepOutcomes.HandedBack),
                (IntegrationStepNames.Replay, "rc", IntegrationStepOutcomes.Done),
            ],
            store.Steps(candidate.Id).Select(each => (each.Step, each.Request, each.Outcome)));
    }

    /// <summary>
    /// 🔴 A crash inside the second member's replay, once its merge is made (the tree mid-merge) or its commit is (the tree one commit on),
    /// before its end is written: another process takes the candidate and resumes it, the tree goes back to the step's recorded start, and the
    /// step is run again as its next attempt, so the candidate's chain holds one merge for the member and the first member's replay stands.
    /// </summary>
    [Theory]
    [InlineData("after-merge")]
    [InlineData("after-commit")]
    public async Task A_crash_after_the_replays_start_and_before_its_end_is_run_again_from_the_recorded_start(string crash)
    {
        var root = await RepositoryAsync("engine");
        var @base = await CommitOfAsync(root, "main");
        var a = await BranchAsync(root, "feature/a", ("a.txt", "a\n"));
        var b = await BranchAsync(root, "feature/b", ("b.txt", "b\n"));
        var first = Store("run-a");
        Queue(first, "ra", "feature/a", a, 1);
        Queue(first, "rb", "feature/b", b, 2);
        var seen = 0;
        var dying = Replays(first);
        dying.Seam = point => point == crash && ++seen == 2 ? throw new Killed() : Task.CompletedTask;
        await Assert.ThrowsAsync<Killed>(() => dying.RunAsync(Ask(root)));

        var old = first.Standing().Single();
        var recorded = first.Members(old.Id).Single(each => each.Request == "ra").Replayed!;
        Assert.Equal(recorded, old.Tip);
        var open = first.OpenStep(old.Id)!;
        Assert.Equal((IntegrationStepNames.Replay, "rb", 1), (open.Step, open.Request, open.Attempt));

        // The come-up: the taker reads run-a as dead, takes the candidate and goes on from the resume.
        var store = Taker();
        var claim = store.Take(old.Id, old.ClaimGeneration).Claim!;
        var run = await Replays(store).ContinueAsync(claim, Ask(root));

        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        var candidate = store.Candidate(old.Id)!;
        var second = Member(store, candidate, "rb");
        Assert.Equal(IntegrationMemberStates.Replayed, second.State);
        Assert.Equal(second.Replayed, candidate.Tip);
        Assert.Equal([recorded, b], await ParentsOfAsync(root, second.Replayed!));
        Assert.Equal(second.Replayed, await CommitOfAsync(run.Tree!, "HEAD"));
        Assert.Equal("", await StatusAsync(run.Tree!));
        Assert.Equal(@base, await CommitOfAsync(root, "main"));
        Assert.Equal(
            [(IntegrationStepNames.Replay, "ra", 1, IntegrationStepOutcomes.Done), (IntegrationStepNames.Replay, "rb", 1, IntegrationStepOutcomes.Interrupted),
                (IntegrationStepNames.Replay, "rb", 2, IntegrationStepOutcomes.Done)],
            store.Steps(old.Id).Where(each => each.Step == IntegrationStepNames.Replay).Select(each => (each.Step, each.Request, each.Attempt, each.Outcome)));
        Assert.Equal(1, store.Steps(old.Id).Count(each => each.Step == IntegrationStepNames.Form));
    }

    /// <summary>
    /// A line with nothing queued forms nothing; a candidate already standing is answered, with no claim, and nothing is written; the tree
    /// stays warm for the next candidate, which finds it at the same place and makes none beside it.
    /// </summary>
    [Fact]
    public async Task A_standing_candidate_is_answered_and_the_next_candidate_finds_the_tree_warm()
    {
        var root = await RepositoryAsync("engine");
        var a = await BranchAsync(root, "feature/a", ("a.txt", "a\n"));
        var b = await BranchAsync(root, "feature/b", ("b.txt", "b\n"));
        var store = Store("run-a");
        var replays = Replays(store);
        Assert.Equal(IntegrationReplayRuns.Idle, (await replays.RunAsync(Ask(root))).Kind);
        Assert.Empty(store.Standing());

        Queue(store, "ra", "feature/a", a, 1);
        var first = await replays.RunAsync(Ask(root));
        Queue(store, "rb", "feature/b", b, 2);
        var standing = await replays.RunAsync(Ask(root));
        Assert.Equal(IntegrationReplayRuns.Standing, standing.Kind);
        Assert.Null(standing.Claim);
        Assert.Equal(first.Candidate!.Id, standing.Candidate!.Id);
        Assert.Single(store.Standing());

        // The first candidate formed again (its request is queued again beside the new one); the second finds the warm tree.
        await new IntegrationSteps(store).RunAsync(
            first.Claim!, IntegrationStepNames.Settle,
            (_, _) => Task.FromResult(new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Ends = IntegrationCandidateEnds.FormedAgain }));
        var second = await replays.RunAsync(Ask(root));

        Assert.Equal(IntegrationReplayRuns.Replayed, second.Kind);
        Assert.NotEqual(first.Candidate.Id, second.Candidate!.Id);
        Assert.Equal(first.Tree, second.Tree);
        Assert.Equal(1, (await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n').Count(each => each.Contains($"/{IntegrationReplays.Prefix}", StringComparison.Ordinal)));
        Assert.Equal(second.Candidate.Tip, await CommitOfAsync(second.Tree!, "HEAD"));
    }

    /// <summary>A folder where the tree goes that git does not list is never taken over: the form step is held, said why, and nothing is merged in it.</summary>
    [Fact]
    public async Task A_folder_git_does_not_list_is_left_alone_and_the_form_is_held()
    {
        var root = await RepositoryAsync("engine");
        var a = await BranchAsync(root, "feature/a", ("a.txt", "a\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1);
        var replays = Replays(store);
        var tree = replays.TreeOf("aurora", "engine", "main");
        Directory.CreateDirectory(tree);
        await File.WriteAllTextAsync(Path.Combine(tree, "someones.txt"), "not Daoris's\n");

        var run = await replays.RunAsync(Ask(root));

        Assert.Equal(IntegrationReplayRuns.Held, run.Kind);
        Assert.Contains("git does not list it", run.Said, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(tree, "someones.txt")));
        var step = Assert.Single(store.Steps(run.Candidate!.Id));
        Assert.Equal((IntegrationStepNames.Form, IntegrationStepOutcomes.Held), (step.Step, step.Outcome));
        Assert.Equal(IntegrationMemberStates.Formed, Member(store, run.Candidate, "ra").State);
    }
}
