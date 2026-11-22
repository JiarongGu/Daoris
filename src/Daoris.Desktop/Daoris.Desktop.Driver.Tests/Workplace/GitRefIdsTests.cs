using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A history read that names HEAD or a full ref name is asked by the ids the files give them, so the object memo keys it: the same read of
/// an unchanged ref starts no git the second time, a moved ref is asked afresh, and a read the memo would not keep as ids is left as written.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitRefIdsTests : LandedFixture
{
    private static readonly string[] Who = ["-c", "user.email=fixture@example.test", "-c", "user.name=Fixture"];

    private async Task<(string Root, string Main, string Feature)> RepositoryAsync()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(Scratch, "engine"), "engine");
        await GitAsync(root, "checkout", "-q", "-b", "feature");
        await CommitAsync(root, "work.txt");
        await GitAsync(root, "checkout", "-q", "main");
        await CommitAsync(root, "line.txt");
        return (root, (await GitAsync(root, "rev-parse", "main")).Trim(), (await GitAsync(root, "rev-parse", "feature")).Trim());
    }

    private static async Task CommitAsync(string root, string file)
    {
        await File.WriteAllTextAsync(Path.Combine(root, file), file + "\n");
        await GitAsync(root, "add", "-A");
        await GitAsync(root, [.. Who, "commit", "-q", "-m", file]);
    }

    [Fact]
    public async Task A_read_naming_head_or_a_ref_is_asked_by_their_ids()
    {
        var (root, main, feature) = await RepositoryAsync();
        Assert.Equal(["log", "--format=%H", "--no-show-signature", $"{main}..{feature}"],
            GitRefIds.Resolved(root, ["log", "--format=%H", "--no-show-signature", "refs/heads/main..refs/heads/feature"]));
        Assert.Equal(["log", "--format=%H", "--no-show-signature", main, "--not", feature],
            GitRefIds.Resolved(root, ["log", "--format=%H", "--no-show-signature", "HEAD", "--not", "refs/heads/feature"]));
        Assert.Equal(["--no-optional-locks", "rev-list", "--count", $"{main}...{feature}", "--left-right"],
            GitRefIds.Resolved(root, ["--no-optional-locks", "rev-list", "--count", "HEAD...refs/heads/feature", "--left-right"]));
        Assert.Equal(["cherry", main, feature], GitRefIds.Resolved(root, ["cherry", "HEAD", "refs/heads/feature"]));

        // Left as written: a name that is no full ref, a read the memo would not keep, a ref that is absent, a path.
        string[][] left =
        [
            ["log", "--format=%H", "--no-show-signature", "main"],
            ["log", "--format=%an", "--no-show-signature", "HEAD"],
            ["log", "--format=%H", "--no-show-signature", "refs/heads/absent"],
            ["log", "--format=%H", "--no-show-signature", "HEAD", "--", "line.txt"],
            ["status", "--porcelain"],
        ];
        foreach (var arguments in left) Assert.Equal(arguments, GitRefIds.Resolved(root, arguments));
    }

    [Fact]
    public async Task The_same_read_of_an_unchanged_ref_starts_no_git_and_a_moved_ref_is_asked_afresh()
    {
        var (root, _, _) = await RepositoryAsync();
        using var home = GitObjectMemo.Within(Home);
        string[] ask = ["log", "--format=%H", "--no-show-signature", "refs/heads/feature..HEAD"];

        async Task<(int Starts, string Output)> AskedAsync()
        {
            using var count = GitLaunch.Count();
            var (code, output, _) = await WorkingTree.GitAsync(root, ask, CancellationToken.None);
            Assert.Equal(0, code);
            return (count.Starts.Count, output);
        }

        var first = await AskedAsync();
        var again = await AskedAsync();
        Assert.Equal(1, first.Starts);
        Assert.Equal((0, first.Output), again);
        Assert.Equal(first.Output.Trim(), (await GitAsync(root, ask)).Trim());

        await CommitAsync(root, "more.txt");
        var moved = await AskedAsync();
        Assert.Equal(1, moved.Starts);
        Assert.NotEqual(first.Output, moved.Output);
        Assert.Equal(moved.Output.Trim(), (await GitAsync(root, ask)).Trim());
    }
}
