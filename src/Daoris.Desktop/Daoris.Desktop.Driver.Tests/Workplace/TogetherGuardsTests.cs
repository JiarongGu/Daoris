using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LINESTOGETHER1d (the review-flow design §4, <i>One lock order</i>): a tree bound into an operation refuses every landing door but its
/// combined Accept, naming the operation; a discard, serviceless too, a sync press and a tree's update leave it as it stands, named; and
/// a landing past its guard holds its tree, so a press binding it waits within its bound and binds nothing.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TogetherGuardsTests : TreeSyncFixture
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private static readonly LandingSubject Subject = new("s-beside", "0fda18", "The beside work");

    private TogetherOperation Bind(TreeOpened member, string headTree = "head-tree")
    {
        var (opened, refused) = new TogetherOperations(Home).Open(
            "engine", new TogetherHead("s-line", "s-head", Path.Combine(Scratch, headTree), "daoris/head", "h1"),
            [new TogetherMember("s-beside", member.Path, member.Branch, member.BasedOn, "t1")], At);
        Assert.Null(refused);
        return opened!;
    }

    private static async Task<string> TipAsync(string tree) => (await GitAsync(tree, "rev-parse", "HEAD")).Trim();

    [Fact]
    public async Task A_bound_member_refuses_a_landing_naming_its_operation_and_its_combined_accept_passes()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var member = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(member.Path, "work.txt", "work\n", "the work");
        var bound = Bind(member);

        var pressed = await trees.LandAsync(member.Path, Subject);
        var automatic = await trees.LandAsync(member.Path, Subject, acceptedBy: AcceptedBy.Auto);

        Assert.False(pressed.Landed);
        Assert.Contains(bound.Id, pressed.Message);
        Assert.False(automatic.Landed);
        Assert.Contains(bound.Id, automatic.Message);

        var combined = await trees.LandAsync(member.Path, Subject, together: bound.Id);
        Assert.True(combined.Landed, combined.Message);
    }

    [Fact]
    public async Task A_landing_past_its_guard_holds_the_tree_so_a_press_binding_it_waits_within_its_bound_and_binds_nothing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var member = await trees.OpenAsync(root, "engine", "aurora");

        using (await LandingHolds.TakeAsync(Home, member.Path))
        {
            var (held, refused) = await TogetherLocks.TakeAsync(Home, "aurora", "engine", [member.Path], TimeSpan.FromMilliseconds(200), CancellationToken.None);
            Assert.Null(held);
            Assert.NotNull(refused);
        }

        var (taken, none) = await TogetherLocks.TakeAsync(Home, "aurora", "engine", [member.Path], TimeSpan.FromSeconds(5), CancellationToken.None);
        using (taken)
        {
            Assert.Null(none);
            Assert.NotNull(taken);
            Assert.Null(TreeLock.TryReplaying(Home, "aurora", "engine"));
        }
    }

    [Fact]
    public async Task A_discard_with_no_service_leaves_a_bound_tree_named()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var member = await trees.OpenAsync(root, "engine", "aurora");
        var bound = Bind(member);

        var removed = await trees.RemoveAsync(member.Path, force: true);

        Assert.False(removed.Removed);
        Assert.Contains(bound.Id, removed.Message);
        Assert.True(Directory.Exists(member.Path));
    }

    [Fact]
    public async Task A_tree_update_on_a_moved_line_leaves_a_bound_tree_as_it_stands()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var member = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(member.Path, "work.txt", "work\n", "the work");
        await CommitAsync(root, "line.txt", "the line moves\n", "the line moves");
        var tip = await TipAsync(member.Path);
        var bound = Bind(member);

        var update = await trees.UpdateTreeAsync(member.Path, _ => Task.FromResult(false));

        Assert.False(update.Done);
        Assert.Contains(bound.Id, update.Message);
        Assert.Equal(tip, await TipAsync(member.Path));
    }

    [Fact]
    public async Task A_sync_press_on_a_moved_line_replays_a_free_branch_and_leaves_a_bound_one_named()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var member = await trees.OpenAsync(root, "engine", "aurora");
        var free = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(member.Path, "work.txt", "work\n", "the work");
        await CommitAsync(free.Path, "free.txt", "free\n", "the free work");
        await CommitAsync(root, "line.txt", "the line moves\n", "the line moves");
        var tip = await TipAsync(member.Path);
        var bound = Bind(member);

        var done = await trees.SyncAsync(Repositories(root), Nobody);

        Assert.True(done.Rebases.Single(each => each.Item.Branch == free.Branch).Replayed);
        var left = done.Rebases.Single(each => each.Item.Branch == member.Branch);
        Assert.False(left.Replayed);
        Assert.Contains(bound.Id, left.Message);
        Assert.Equal(tip, await TipAsync(member.Path));
    }
}
