using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the page's reads keep, on real git: an answer is kept under the asker's stamp only where the stamp's fence held across the read,
/// and only where git read objects alone for it, so a commit's changes are asked in the tree that asks, under its attributes.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitReadsStampTests : LandedFixture
{
    [Fact]
    public async Task A_replacement_added_before_a_graph_read_and_removed_after_it_is_refused_by_the_fence()
    {
        using var _ = SettledClock.Ahead();
        var root = await RepositoryAsync("engine");
        var first = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await CommitAsync(root, "more.txt", "more\n", "the tip");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "checkout", "-q", "-b", "alt", first);
        await CommitAsync(root, "alt.txt", "alt\n", "the replacement");
        var alt = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "checkout", "-q", "main");

        WorkingTree.GitRead replacedDuring = async (folder, arguments, take, ct) =>
        {
            await GitAsync(root, "replace", tip, alt);
            var code = await WorkingTree.ReadGitAsync(folder, arguments, take, ct);
            await GitAsync(root, "replace", "-d", tip);
            return code;
        };
        var cache = new GitReadCache();

        var during = await new GitReads(cache, replacedDuring).GraphAsync(root, [tip]);
        Assert.Equal("the replacement", during.Value!.Commits[0].Subject);

        var after = await new GitReads(cache, WorkingTree.ReadGitAsync).GraphAsync(root, [tip]);
        Assert.False(after.Remembered);
        Assert.Equal("the tip", after.Value!.Commits[0].Subject);

        var again = await new GitReads(cache, WorkingTree.ReadGitAsync).GraphAsync(root, [tip]);
        Assert.True(again.Remembered);
        Assert.Equal("the tip", again.Value!.Commits[0].Subject);
    }

    [Fact]
    public async Task Two_trees_one_marking_a_changed_file_minus_diff_each_read_a_commits_changes_as_git_does_there()
    {
        using var _ = SettledClock.Ahead();
        var a = await RepositoryAsync("engine");
        await CommitAsync(a, "data.txt", "one\ntwo\n", "the data");
        var commit = (await GitAsync(a, "rev-parse", "HEAD")).Trim();
        var b = Path.Combine(Scratch, "b");
        await GitAsync(a, "worktree", "add", "-q", "--detach", b, commit);
        await File.WriteAllTextAsync(Path.Combine(b, ".gitattributes"), "data.txt -diff\n");
        var reads = new GitReads();

        var inA = await reads.CommitAsync(a, commit);
        var inB = await reads.CommitAsync(b, commit);

        Assert.True(inB.Remembered);
        Assert.Equal(inA.Value!.Message, inB.Value!.Message);
        Assert.Equal(await BinaryAsync(a, commit), Binary(inA));
        Assert.Equal(await BinaryAsync(b, commit), Binary(inB));
        Assert.False(Binary(inA));
        Assert.True(Binary(inB));
    }

    [Fact]
    public async Task An_attribute_file_edited_between_two_asks_is_read_at_the_second()
    {
        using var _ = SettledClock.Ahead();
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "data.txt", "one\ntwo\n", "the data");
        var commit = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var reads = new GitReads();

        var before = await reads.CommitAsync(root, commit);
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "data.txt -diff\n");
        var after = await reads.CommitAsync(root, commit);

        Assert.False(Binary(before));
        Assert.True(after.Remembered);
        Assert.True(Binary(after));
        Assert.Equal(await BinaryAsync(root, commit), Binary(after));
    }

    private static bool Binary(GitAnswer<GitCommitDetail> page) => page.Value!.Changes!.Files.Single(file => file.Path == "data.txt").Added is null;

    /// <summary>git's own answer in <paramref name="tree"/>: <c>--numstat</c> says a binary file's counts as <c>-</c>.</summary>
    private static async Task<bool> BinaryAsync(string tree, string commit) =>
        (await GitAsync(tree, "diff", "--numstat", $"{commit}^", commit, "--", "data.txt")).StartsWith('-');
}
