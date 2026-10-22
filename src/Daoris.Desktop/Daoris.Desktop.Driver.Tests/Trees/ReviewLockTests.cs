using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWLOCK1: opening Review takes no lock in a running session's tree. Review reads a session's tree while its agent may be
/// writing there; a read that refreshed the index took <c>index.lock</c> to write it back, and an agent's <c>git add</c> or
/// <c>git commit</c> in that moment failed on the lock. Each read here is the one the bridge's <c>SESSION_DIFF</c> makes, in its
/// order, against a real session tree in scratch: once while a stand-in for the session holds the tree's <c>index.lock</c>,
/// and once over an index whose stat data is stale, which a refreshing read would write.
/// </summary>
/// <remarks>Real git in scratch repositories under the repository's <c>_fixtures</c>, with a home of the fixture's own; never the install's.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ReviewLockTests : LandedFixture
{
    /// <summary>
    /// A session's tree with a commit of its own, a tracked file changed and an untracked one beside it, and a committed file whose
    /// stat data alone moved, so a read that refreshes the index has something to write.
    /// </summary>
    private async Task<(SessionTrees Trees, TreeOpened Opened, string Began, string Index)> TreeAsync()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        var tree = opened.Path;
        var began = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        await CommitAsync(tree, "kept.txt", "kept\n", "a file the session keeps");
        await CommitAsync(tree, "own.txt", "mine\n", "its own work");
        await File.WriteAllTextAsync(Path.Combine(tree, "own.txt"), "mine, changed\n");
        await File.WriteAllTextAsync(Path.Combine(tree, "fresh.txt"), "new\n");
        File.SetLastWriteTimeUtc(Path.Combine(tree, "kept.txt"), DateTime.UtcNow.AddHours(1));
        var index = (await GitAsync(tree, "rev-parse", "--path-format=absolute", "--git-path", "index")).Trim();
        return (trees, opened, began, index);
    }

    /// <summary>What <c>SESSION_DIFF</c> reads of a session's own tree (the bridge's <c>TreesRoutes.DiffAsync</c>), each answered.</summary>
    private static async Task ReviewAsync(SessionTrees trees, TreeOpened opened, string began)
    {
        var tree = opened.Path;
        var dirty = await WorkingTree.UncommittedDiffAsync(tree);
        Assert.NotNull(dirty);
        // The changed file and the new one; the file whose stat data alone moved changed nothing.
        Assert.Equal(["fresh.txt", "own.txt"], dirty!.Files.Select(file => file.Path).Order(StringComparer.Ordinal));

        var range = await trees.ReviewRangeAsync(tree, began);
        var diff = await WorkingTree.DiffAsync(tree, range.Reads);
        Assert.Equal(["kept.txt", "own.txt"], diff!.Files.Select(file => file.Path).Order(StringComparer.Ordinal));
        Assert.Equal(opened.Branch, await WorkingTree.BranchAsync(tree));
        Assert.NotNull(await trees.WorkAsync(tree));
        Assert.Equal(2, await trees.UncommittedAsync(tree));
        Assert.False((await WorkingTree.CleanAsync(tree)).Clean);
    }

    [Fact]
    public async Task Review_opens_while_the_session_holds_its_index_lock_and_the_session_commits_after()
    {
        var (trees, opened, began, index) = await TreeAsync();
        var tree = opened.Path;
        var lockPath = index + ".lock";
        byte[] held = [.. "the session's index, being written\n"u8];

        // The stand-in: a session's git in the middle of a write, holding the lock as git does, made exclusively.
        using (var stand = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete))
        {
            await stand.WriteAsync(held);
            await stand.FlushAsync();

            await ReviewAsync(trees, opened, began);

            // The lock is still the session's, as it left it: read beside the stand-in's own open handle.
            using var seen = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var copy = new MemoryStream();
            await seen.CopyToAsync(copy);
            Assert.Equal(held, copy.ToArray());
        }

        // The session's write goes on once its lock is its own to let go: Review left nothing behind to refuse it.
        File.Delete(lockPath);
        var add = await GitFixture.RunAsync(tree, "add", "-A");
        Assert.True(add.ExitCode == 0, add.Stderr);
        var commit = await GitFixture.RunAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "the session's next commit");
        Assert.True(commit.ExitCode == 0, commit.Stderr);
    }

    [Fact]
    public async Task Review_writes_no_index_and_leaves_no_lock_in_the_tree()
    {
        var (trees, opened, began, index) = await TreeAsync();
        var tree = opened.Path;
        var before = await File.ReadAllBytesAsync(index);
        var written = File.GetLastWriteTimeUtc(index);

        await ReviewAsync(trees, opened, began);

        // A read that refreshed the stale stat data would have taken the lock and written the index back.
        Assert.Equal(before, await File.ReadAllBytesAsync(index));
        Assert.Equal(written, File.GetLastWriteTimeUtc(index));
        Assert.False(File.Exists(index + ".lock"));
    }
}
