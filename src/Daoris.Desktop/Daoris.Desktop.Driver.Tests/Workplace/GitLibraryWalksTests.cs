using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitLibraryParity;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The library's merge-bases, ancestry and counts held to git's for the same question on real git, value for value: in the checkout
/// and a linked tree, by id, by <c>HEAD</c>, by full and short ref names and through an annotated tag; histories that share nothing;
/// <c>--independent</c> with a repeat and an ancestor among its commits; counts over two refs and with ranges and exclusions; a
/// criss-cross whose two bases are committed in the same second, which is git's; a graft; a shallow boundary before and after a deepen.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryWalksTests : LandedFixture
{
    private static readonly Dictionary<string, string> SameSecond = new()
    {
        ["GIT_AUTHOR_DATE"] = "2026-11-09T12:00:00Z",
        ["GIT_COMMITTER_DATE"] = "2026-11-09T12:00:00Z",
    };

    [GitLegFact]
    public async Task Merge_bases_ancestry_and_counts_are_git_s_in_the_checkout_and_a_linked_tree()
    {
        var (root, _) = await RepositoryWithOriginAsync("walks");
        var first = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var linked = Path.Combine(Scratch, "walks-topic");
        await GitAsync(root, "worktree", "add", "-b", "topic", linked);
        await CommitAsync(linked, "topic.txt", "topic\n", "on topic");
        await CommitAsync(linked, "topic.txt", "topic again\n", "on topic again");
        await GitAsync(linked, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "tag", "-a", "marked", "-m", "a tag");
        await CommitAsync(root, "main.txt", "main\n", "on main");
        var lone = (await GitAsync(root, "commit-tree", "-m", "lone", (await GitAsync(root, "rev-parse", "HEAD^{tree}")).Trim())).Trim();
        await GitAsync(root, "update-ref", "refs/heads/lone", lone);

        foreach (var folder in new[] { root, linked })
        {
            await SameAsync(folder, "merge-base", "main", "topic");
            await SameAsync(folder, "merge-base", "HEAD", "refs/heads/main");
            await SameAsync(folder, "merge-base", "refs/heads/topic", first);
            await SameAsync(folder, "merge-base", "main", "lone");
            await SameAsync(folder, "merge-base", "--is-ancestor", first, "HEAD");
            await SameAsync(folder, "merge-base", "--is-ancestor", "main", first);
            await SameAsync(folder, "merge-base", "--is-ancestor", "main", "main");
            await SameAsync(folder, "merge-base", "--is-ancestor", "marked", "topic");
            await SameAsync(folder, "merge-base", "--independent", "main", "topic", first, "main");
            await SameAsync(folder, "merge-base", "--independent", "topic", first, "main", "marked");
            await SameAsync(folder, "rev-list", "--count", "refs/heads/main", "refs/remotes/origin/main", "--not", "HEAD");
            await SameAsync(folder, "rev-list", "--count", "HEAD", "--not", "refs/heads/main", "refs/remotes/origin/main");
            await SameAsync(folder, "rev-list", "--count", $"{first}..topic");
            await SameAsync(folder, "rev-list", "--count", "topic", "lone", $"^{first}");
            await SameAsync(folder, "rev-list", "--left-right", "--count", "main...topic");
            await SameAsync(folder, "--no-optional-locks", "merge-base", "--is-ancestor", "topic", "main");
        }
    }

    [GitLegFact]
    public async Task A_criss_cross_whose_two_bases_share_a_second_is_git_s_and_its_other_reads_the_library_s()
    {
        var root = await BuildRepositoryAsync("criss");
        await GitAsync(root, "branch", "b");
        await GitAsync(root, "checkout", "-q", "-b", "a");
        var one = await CommittedAsync(root, "a.txt", "a\n");
        await GitAsync(root, "checkout", "-q", "b");
        var other = await CommittedAsync(root, "b.txt", "b\n");
        await RunAsync(root, SameSecond, null, "merge", "-q", "--no-ff", "-m", "a's first into b", one);
        await GitAsync(root, "checkout", "-q", "a");
        await RunAsync(root, SameSecond, null, "merge", "-q", "--no-ff", "-m", "b's first into a", other);
        await CommittedAsync(root, "a2.txt", "a2\n");
        await GitAsync(root, "checkout", "-q", "b");
        await CommittedAsync(root, "b2.txt", "b2\n");

        var bases = await RunAsync(root, null, null, "merge-base", "--all", "a", "b");
        Assert.Equal(2, bases.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        await GitsAsync(root, "merge-base", "a", "b");
        await GitsAsync(root, "merge-base", "b", "a");
        await SameAsync(root, "merge-base", "--is-ancestor", one, "b");
        await SameAsync(root, "merge-base", "--independent", "a", "b", one, other);
        await SameAsync(root, "rev-list", "--left-right", "--count", "a...b");
        await SameAsync(root, "rev-list", "--count", "a", "--not", "b");
    }

    [GitLegFact]
    public async Task A_shallow_boundary_is_read_as_git_reads_it_before_and_after_a_deepen()
    {
        var source = await BuildRepositoryAsync("deep");
        for (var at = 0; at < 4; at++) await CommitAsync(source, "deep.txt", $"{at}\n", $"deep {at}");
        await GitAsync(source, "branch", "side", "HEAD~3");
        await GitAsync(source, "checkout", "-q", "side");
        await CommitAsync(source, "side.txt", "side\n", "on side");
        await GitAsync(source, "checkout", "-q", "main");
        var shallow = Path.Combine(Scratch, "shallow");
        await GitAsync(Scratch, "clone", "--quiet", "--depth", "2", "--no-single-branch", "file:///" + source.Replace('\\', '/'), shallow);

        foreach (var deepen in new[] { false, true })
        {
            if (deepen) await GitAsync(shallow, "fetch", "--quiet", "--deepen=2");
            await SameAsync(shallow, "rev-list", "--count", "HEAD");
            await SameAsync(shallow, "rev-list", "--count", "refs/remotes/origin/side", "--not", "HEAD");
            await SameAsync(shallow, "rev-list", "--left-right", "--count", "HEAD...refs/remotes/origin/side");
            await SameAsync(shallow, "merge-base", "HEAD", "refs/remotes/origin/side");
            await SameAsync(shallow, "merge-base", "--is-ancestor", "refs/remotes/origin/side", "HEAD");
        }
    }

    [GitLegFact]
    public async Task A_graft_is_read_as_git_reads_it()
    {
        var root = await BuildRepositoryAsync("grafted");
        var first = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await CommitAsync(root, "two.txt", "two\n", "two");
        await CommitAsync(root, "three.txt", "three\n", "three");
        Directory.CreateDirectory(Path.Combine(root, ".git", "info"));
        await File.WriteAllTextAsync(Path.Combine(root, ".git", "info", "grafts"), (await GitAsync(root, "rev-parse", "HEAD~1")).Trim() + "\n");

        await SameAsync(root, "rev-list", "--count", "HEAD");
        await SameAsync(root, "merge-base", "--is-ancestor", first, "HEAD");
        await SameAsync(root, "merge-base", first, "HEAD");
        await SameAsync(root, "diff", "--name-only", "-z", "--no-renames", first, "HEAD");
    }

    [GitLegFact]
    public async Task A_read_under_a_test_s_stand_in_runner_is_the_stand_in_s()
    {
        var root = await BuildRepositoryAsync("stood");
        GitFlow.Runs.Value = (arguments, _, _) => Task.FromResult(new ChildRun(true, 0, "stood in\n", "", false));
        try
        {
            Assert.Equal("stood in\n", (await WorkingTree.GitAsync(root, ["merge-base", "HEAD", "main"], CancellationToken.None)).Stdout);
        }
        finally
        {
            GitFlow.Runs.Value = null;
        }
    }

    private static async Task<string> CommittedAsync(string root, string file, string content)
    {
        await File.WriteAllTextAsync(Path.Combine(root, file), content);
        await GitAsync(root, "add", "-A");
        await RunAsync(root, SameSecond, null, "commit", "-q", "-m", file);
        return (await GitAsync(root, "rev-parse", "HEAD")).Trim();
    }
}
