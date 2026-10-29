using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDRETIRE1 part 1 over real git: a landing's branch whose work the person says the line will never take is withdrawn with a
/// recovery ref kept and its record saying why, and gone from the next clean; a checked-out, line-held, pull-request, un-recorded or
/// person's own branch is refused and changed in no way.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandedWithdrawTests : LandedFixture
{
    private const string Why = "the row landed another way, by hand";

    private static async Task<string> TipOfAsync(string root, string reference) => (await GitAsync(root, "rev-parse", reference)).Trim();

    /// <summary>A landing whose row the line took another way: another commit changed the same file, so its files read otherwise there.</summary>
    private async Task<(string Root, SessionTrees Trees, string Branch, string Tip)> DiffersAsync()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        await CommitAsync(root, "shared.txt", "the line's own take\n", "the row by another commit");
        var plan = await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());
        Assert.Equal(LandedKind.Differs, Landed(plan, landed.Branch!).Kind);
        return (root, trees, landed.Branch!, await TipOfAsync(root, landed.Branch!));
    }

    [Fact]
    public async Task A_landing_branch_the_line_will_never_hold_is_withdrawn_with_its_recovery_ref_and_its_why()
    {
        var (root, trees, branch, tip) = await DiffersAsync();

        var done = await trees.WithdrawLandedAsync(root, "engine", branch, Why);

        Assert.True(done.Done, done.Message);
        var kept = $"refs/daoris/discarded/{branch}";
        Assert.Equal(kept, done.KeptRef);
        Assert.Equal(tip, await TipOfAsync(root, kept));
        Assert.DoesNotContain(branch, await GitAsync(root, "branch", "--list"));

        // The record keeps it as a trace: when, why, and the ref its commits stay on, never dropped.
        Assert.Empty(new LandedBranches(Home).All());
        var trace = Assert.Single(new LandedBranches(Home).Entries(), entry => entry.Branch == branch);
        Assert.NotNull(trace.GoneAt);
        Assert.Equal(LandedGone.Withdrawn, trace.GoneAs);
        Assert.Equal(Why, trace.GoneWhy);
        Assert.Equal(kept, trace.KeptOn);
        Assert.Null(trace.RemovedAs);

        // Gone from the next clean: the list names nothing, and a press changes nothing.
        var plan = await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());
        Assert.Empty(plan.Landed);
        var cleaned = await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());
        Assert.Empty(cleaned.Landed);
        Assert.Equal(tip, await TipOfAsync(root, kept));
    }

    [Fact]
    public async Task A_plan_says_what_would_happen_and_changes_nothing()
    {
        var (root, trees, branch, tip) = await DiffersAsync();
        var before = (await GitAsync(root, "for-each-ref")).Trim();

        var planned = await trees.WithdrawLandedAsync(root, "engine", branch, Why, plan: true);

        Assert.False(planned.Done);
        Assert.True(planned.Planned);
        Assert.Contains(branch, planned.Message);
        Assert.Contains("would", planned.Message);
        Assert.Equal(before, (await GitAsync(root, "for-each-ref")).Trim());
        Assert.Equal(tip, await TipOfAsync(root, branch));
        Assert.Single(new LandedBranches(Home).All());
    }

    [Fact]
    public async Task A_second_withdrawal_of_the_same_name_keeps_the_first_ones_ref()
    {
        var (root, trees, branch, tip) = await DiffersAsync();
        // A recovery ref of that name already holds another commit: the new one is made under the next number.
        var other = (await GitAsync(root, "rev-parse", "main")).Trim();
        await GitAsync(root, "update-ref", $"refs/daoris/discarded/{branch}", other);

        var done = await trees.WithdrawLandedAsync(root, "engine", branch, Why);

        Assert.True(done.Done, done.Message);
        Assert.Equal($"refs/daoris/discarded/{branch}-2", done.KeptRef);
        Assert.Equal(other, await TipOfAsync(root, $"refs/daoris/discarded/{branch}"));
        Assert.Equal(tip, await TipOfAsync(root, $"refs/daoris/discarded/{branch}-2"));
    }

    [Fact]
    public async Task A_branch_checked_out_in_a_tree_or_at_the_checkout_is_refused_and_stays()
    {
        var (root, trees, branch, tip) = await DiffersAsync();
        var elsewhere = Path.Combine(Scratch, "elsewhere");
        await GitAsync(root, "worktree", "add", "--quiet", elsewhere, branch);

        var inTree = await trees.WithdrawLandedAsync(root, "engine", branch, Why);
        Assert.False(inTree.Done);
        Assert.Contains("checked out", inTree.Message);

        await GitAsync(root, "worktree", "remove", elsewhere);
        await GitAsync(root, "checkout", "--quiet", branch);
        var atHead = await trees.WithdrawLandedAsync(root, "engine", branch, Why);
        Assert.False(atHead.Done);
        Assert.Contains("checked out", atHead.Message);

        Assert.Equal(tip, await TipOfAsync(root, branch));
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/discarded/")).Trim());
        Assert.Single(new LandedBranches(Home).All());
    }

    [Fact]
    public async Task A_branch_the_line_holds_is_refused_to_the_normal_clean_up()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        await SquashAsync(root, landed.Branch!);

        var done = await trees.WithdrawLandedAsync(root, "engine", landed.Branch!, Why);

        Assert.False(done.Done);
        Assert.True(done.Message.Contains("trees clean", StringComparison.Ordinal), done.Message);
        Assert.Contains(landed.Branch!, await GitAsync(root, "branch", "--list"));
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/discarded/")).Trim());
        Assert.Single(new LandedBranches(Home).All());
    }

    [Fact]
    public async Task A_branch_a_plugin_pushed_whose_pull_request_may_still_carry_it_is_refused()
    {
        var (root, trees, branch, tip) = await DiffersAsync();
        trees.Recorded.Pushed("engine", branch, new PluginLanding("acme.lands", true, "https://example.test/pr/7", "pushed it."), tip);

        var done = await trees.WithdrawLandedAsync(root, "engine", branch, Why);

        Assert.False(done.Done);
        Assert.Contains("pull request", done.Message);
        Assert.Equal(tip, await TipOfAsync(root, branch));
        Assert.Single(new LandedBranches(Home).All());
    }

    [Fact]
    public async Task A_branch_no_landing_recorded_and_a_persons_own_branch_are_refused()
    {
        var (root, trees, branch, tip) = await DiffersAsync();
        await GitAsync(root, "branch", "feature/mine", "main~1");
        var unrecorded = await trees.WithdrawLandedAsync(root, "engine", "feature/mine", Why);
        Assert.False(unrecorded.Done);
        Assert.Contains("no landing", unrecorded.Message);
        Assert.Contains("feature/mine", await GitAsync(root, "branch", "--list"));

        // The landing's branch deleted, its name taken by the person's own commits: not the landing's, so not Daoris's to take.
        await GitAsync(root, "branch", "-D", branch);
        await GitAsync(root, "branch", branch, "main");
        await CommitOnAsync(root, branch, "mine.txt", "mine\n", "the person's own");
        var mine = await TipOfAsync(root, branch);
        var taken = await trees.WithdrawLandedAsync(root, "engine", branch, Why);
        Assert.False(taken.Done);
        Assert.Equal(mine, await TipOfAsync(root, branch));
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/discarded/")).Trim());
        Assert.NotEqual(tip, mine);
    }

    [Fact]
    public async Task A_withdrawal_with_no_reason_is_refused()
    {
        var (root, trees, branch, tip) = await DiffersAsync();

        var done = await trees.WithdrawLandedAsync(root, "engine", branch, "  ");

        Assert.False(done.Done);
        Assert.Contains("why", done.Message);
        Assert.Equal(tip, await TipOfAsync(root, branch));
    }

    [Fact]
    public async Task A_branch_that_moved_since_it_was_judged_is_kept()
    {
        var (root, trees, branch, _) = await DiffersAsync();
        // The branch moves between the judgement and the delete, as a session or a person could move it.
        var moved = new SessionTrees(Home)
        {
            BeforeDeleting = async name =>
            {
                await CommitOnAsync(root, name, "late.txt", "late\n", "arrived as it went");
            },
        };

        var done = await moved.WithdrawLandedAsync(root, "engine", branch, Why);

        Assert.False(done.Done);
        Assert.Contains(branch, await GitAsync(root, "branch", "--list"));
        // The recovery ref made for the delete that did not happen goes again, and the standing entry stays.
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/discarded/")).Trim());
        Assert.Single(new LandedBranches(Home).All());
    }
}
