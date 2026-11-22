using Daoris.Driver;
using GitAsk = Daoris.Ports.Git.GitAsk;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A git read that failed (its place or its run did not come in time: no exit) is unknown, never "clean", "not held" or "no change":
/// each act whose guard reads git leaves the state as it stood and changes nothing. A read of one tree fails beside the others
/// that answer, over real git, so a guard that reads a failure as an empty answer would move a branch, a tree or a line here.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class FailedReadsDecideNothingTests : TreeSyncFixture
{
    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix the gap");

    private static bool Reads(GitAsk ask, string folder, string verb) =>
        string.Equals(Path.GetFullPath(ask.Folder), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase) && ask.Arguments.Contains(verb);

    private static Task<bool> Idle(CancellationToken _) => Task.FromResult(false);

    [Fact]
    public async Task A_tree_whose_status_failed_is_not_brought_up_to_date()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own");
        await CommitOnOriginAsync(origin, "the line moves on", ("line.txt", "theirs\n"));
        var head = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        var branch = (await GitAsync(root, "rev-parse", tree.Branch)).Trim();

        using (GitFlow.Failing(ask => Reads(ask, tree.Path, "status")))
        {
            var done = await trees.UpdateTreeAsync(tree.Path, Idle);

            Assert.False(done.Done, done.Message);
            Assert.Null(done.Moved);
        }

        Assert.Equal(head, (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
        Assert.Equal(branch, (await GitAsync(root, "rev-parse", tree.Branch)).Trim());
    }

    [Fact]
    public async Task A_tree_whose_status_failed_is_not_merged()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "budget.ts", "cap\n", "cap hydration");
        var line = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        using (GitFlow.Failing(ask => Reads(ask, tree.Path, "status")))
        {
            var merged = await trees.MergeAsync(tree.Path);

            Assert.False(merged.Merged, merged.Message);
        }

        Assert.Equal(line, (await GitAsync(root, "rev-parse", "HEAD")).Trim());
    }

    [Fact]
    public async Task A_tree_whose_status_failed_does_not_follow_what_landed()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(tree.Path, "checkout", "--quiet", "-b", "feature/own");
        await CommitAsync(tree.Path, "picked.txt", "picked\n", "the part picked");
        var picked = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the rest of the work");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "cherry-pick", "-x", picked);
        await CommitAsync(root, "picked.txt", "picked\nand the line's own\n", "the line goes on with it");
        var head = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();

        using (GitFlow.Failing(ask => Reads(ask, tree.Path, "status")))
        {
            await trees.LandAsync(tree.Path, Subject, gate: Lands);
        }

        Assert.Equal(head, (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
        Assert.Equal("feature/own", (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
    }

    /// <summary>A tree on its own branch whose first commit the line took by cherry-pick, so a landing replays it in place.</summary>
    private async Task<(string Root, SessionTrees Trees, TreeOpened Tree, string Head)> OwnBranchAsync()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(tree.Path, "checkout", "--quiet", "-b", "feature/own");
        await CommitAsync(tree.Path, "picked.txt", "picked\n", "the part picked");
        var picked = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the rest of the work");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "cherry-pick", "-x", picked);
        await CommitAsync(root, "picked.txt", "picked\nand the line's own\n", "the line goes on with it");
        return (root, trees, tree, (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
    }

    [Fact]
    public async Task A_tree_whose_status_fails_only_at_the_move_is_not_moved_onto_what_landed()
    {
        var (root, trees, tree, head) = await OwnBranchAsync();
        var reads = 0;

        using (GitFlow.Failing(ask => Reads(ask, tree.Path, "status") && Interlocked.Increment(ref reads) > 1))
        {
            var landed = await trees.LandAsync(tree.Path, Subject, gate: Lands);

            Assert.Contains("did not answer", landed.Message);
        }

        Assert.Equal(head, (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
        Assert.Equal(head, (await GitAsync(root, "rev-parse", "feature/own")).Trim());
    }

    [Fact]
    public async Task A_branch_whose_remote_ref_could_not_be_read_is_not_rewritten_as_if_it_were_not_pushed()
    {
        var (root, trees, tree, head) = await OwnBranchAsync();
        await GitAsync(root, "update-ref", "refs/remotes/origin/feature/own", head);

        using (GitFlow.Failing(ask => ask.Arguments.Contains("refs/remotes/origin/feature/own^{commit}")))
        {
            await trees.LandAsync(tree.Path, Subject, gate: Lands);
        }

        Assert.Equal(head, (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
        Assert.Equal(head, (await GitAsync(root, "rev-parse", "feature/own")).Trim());
    }

    [Fact]
    public async Task A_detached_tree_whose_status_fails_after_it_was_judged_is_not_replayed()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own");
        await GitAsync(tree.Path, "switch", "--quiet", "--detach");
        await CommitOnOriginAsync(origin, "the line moves on", ("line.txt", "theirs\n"));
        var head = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        var reads = 0;

        using (GitFlow.Failing(ask => Reads(ask, tree.Path, "status") && Interlocked.Increment(ref reads) > 1))
        {
            var done = await trees.UpdateTreeAsync(tree.Path, Idle);

            Assert.False(done.Done, done.Message);
        }

        Assert.Equal(head, (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
    }

    [Fact]
    public async Task A_step_whose_recorded_base_could_not_be_read_is_not_replayed_on_a_wider_range()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var source = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(source.Path, "shared.txt", "one\n", "p1");
        var step = await trees.OpenAsync(root, "engine", "aurora", from: source.Branch);
        await CommitAsync(step.Path, "s.txt", "s\n", "s1");
        await CommitAsync(source.Path, "p2.txt", "the source's new work\n", "p2");
        var submitted = (await GitAsync(source.Path, "rev-parse", "HEAD")).Trim();
        var recorded = trees.Grown.Of("engine", step.Branch)!;
        var head = (await GitAsync(step.Path, "rev-parse", "HEAD")).Trim();

        using (GitFlow.Failing(ask => ask.Arguments.Contains("--is-ancestor") && ask.Arguments.Contains(recorded.From!)))
        {
            var started = await trees.GoOnInSyncAsync(step.Path, "#q1", submitted);

            Assert.Null(started.Moved);
        }

        Assert.Equal(head, (await GitAsync(step.Path, "rev-parse", "HEAD")).Trim());
        Assert.Equal(head, (await GitAsync(root, "rev-parse", step.Branch)).Trim());
        Assert.Equal(recorded, trees.Grown.Of("engine", step.Branch));
    }

    [Fact]
    public async Task A_resolution_the_proof_could_not_read_is_kept_on_the_edits_ref_when_the_tree_is_put_back()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own");
        var tip = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await GitAsync(tree.Path, "switch", "--quiet", "--detach");
        await CommitAsync(tree.Path, "resolved.txt", "the resolver's work\n", "resolved");
        var resolved = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        var hand = new ResolveHand(root, tree.Path, "engine", tree.Branch, tip, "main", [], "refs/daoris/resolve-edits/test");

        var put = await ResolveTree.RestoreAsync(hand, keepEdits: true, CancellationToken.None);

        Assert.Null(put);
        Assert.Equal(resolved, (await GitAsync(root, "rev-parse", "refs/daoris/resolve-edits/test^")).Trim());
        Assert.Equal(tip, (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
    }
}
