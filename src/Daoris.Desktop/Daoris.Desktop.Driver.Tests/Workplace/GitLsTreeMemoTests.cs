using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A commit's listing narrowed to plain paths (<c>ls-tree -z ID -- path</c>), by its id or by HEAD or a full ref name, is a function of
/// objects alone, so it is kept: the second ask starts no git, equals git's own, and a moved HEAD is asked afresh. A pathspec with
/// a magic prefix or a pattern, a listing that is not <c>-z</c> with a path, and a leading option of the caller's own stay git's.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLsTreeMemoTests : LandedFixture
{
    private static readonly string[] Who = ["-c", "user.email=fixture@example.test", "-c", "user.name=Fixture"];

    private static async Task CommitAsync(string root, string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, file))!);
        await File.WriteAllTextAsync(Path.Combine(root, file), file + "\n");
        await GitAsync(root, "add", "-A");
        await GitAsync(root, [.. Who, "commit", "-q", "-m", file]);
    }

    [Fact]
    public void Only_plain_paths_after_the_commit_qualify()
    {
        var id = new string('a', 40);
        Assert.True(GitObjectArguments.Qualifies(["ls-tree", "-z", id, "--", "docs"]));
        Assert.True(GitObjectArguments.Qualifies(["ls-tree", "--full-tree", "-z", id, "--", "a.md", "docs/b.md"]));
        Assert.True(GitObjectArguments.Qualifies(["ls-tree", "-r", "--name-only", "-z", id]));
        Assert.False(GitObjectArguments.Qualifies(["ls-tree", id, "--", "docs"]));
        Assert.False(GitObjectArguments.Qualifies(["ls-tree", "-z", id, "--", ":(glob)*.md"]));
        Assert.False(GitObjectArguments.Qualifies(["ls-tree", "-z", id, "--", "d*"]));
        Assert.False(GitObjectArguments.Qualifies(["ls-tree", "-z", id, "--", "../docs"]));
        Assert.False(GitObjectArguments.Qualifies(["ls-tree", "-z", id, "--"]));
        Assert.False(GitObjectArguments.Qualifies(["--literal-pathspecs", "ls-tree", "-z", id, "--", "docs"]));
    }

    [Fact]
    public async Task A_listing_by_head_or_an_id_is_kept_and_a_moved_head_is_asked_afresh()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(Scratch, "engine"), "engine");
        await CommitAsync(root, "docs/deep/note.md");
        using var home = GitObjectMemo.Within(Home);
        var id = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        async Task<(int Starts, string Output)> AskedAsync(string[] ask)
        {
            using var count = GitLaunch.Count();
            var (code, output, _) = await WorkingTree.GitAsync(root, ask, CancellationToken.None);
            Assert.Equal(0, code);
            return (count.Starts.Count, output);
        }

        foreach (var (ask, path) in new[] { (new[] { "ls-tree", "-z", id, "--", "docs" }, "docs"), (["ls-tree", "--full-tree", "-z", "HEAD", "--", "docs/deep"], "docs/deep") })
        {
            var first = await AskedAsync(ask);
            Assert.Equal(1, first.Starts);
            Assert.Equal((0, first.Output), await AskedAsync(ask));
            Assert.Contains((await GitAsync(root, "rev-parse", $"HEAD:{path}")).Trim(), first.Output);
        }

        await CommitAsync(root, "docs/deep/second.md");
        var moved = await AskedAsync(["ls-tree", "--full-tree", "-z", "HEAD", "--", "docs/deep"]);
        Assert.Equal(1, moved.Starts);
        Assert.Contains((await GitAsync(root, "rev-parse", "HEAD:docs/deep")).Trim(), moved.Output);
    }
}
