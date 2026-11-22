using Daoris.Driver;
using GitAsk = Daoris.Ports.Git.GitAsk;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A git read that failed (its place or its run did not come in time: no exit) is unknown, never "clean", "not held" or "no change"
///: each act whose guard reads git leaves the state as it stood and changes nothing. A read of one tree fails beside the others
/// that answer, over real git, so a guard that reads a failure as an empty answer would move a branch, a tree or a line here.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class FailedReadsDecideNothingTests : LandedFixture
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
}
