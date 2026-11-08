using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The memos partitioned by the asking tree's configuration, on real git: in one flow the checkout A reads a replaced commit's tree and
/// tree B, with replacements off in its <c>config.worktree</c>, is answered as its own git answers, never from A's memo or listing.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitAskerContextTests : LandedFixture
{
    private async Task<(string A, string B, string Commit, string Replaced, string Own)> ReplacedAsync()
    {
        var a = await RepositoryAsync("engine");
        var commit = (await GitAsync(a, "rev-parse", "HEAD")).Trim();
        await GitAsync(a, "checkout", "-q", "-b", "alt");
        await File.WriteAllTextAsync(Path.Combine(a, "shared.txt"), "two\n");
        await GitAsync(a, "commit", "-q", "-am", "alt");
        var alt = (await GitAsync(a, "rev-parse", "HEAD")).Trim();
        await GitAsync(a, "checkout", "-q", "main");
        await GitAsync(a, "replace", commit, alt);
        var b = Path.Combine(Scratch, "b");
        await GitAsync(a, "worktree", "add", "-q", "--detach", b, "main");
        await GitAsync(a, "config", "extensions.worktreeConfig", "true");
        await GitAsync(b, "config", "--worktree", "core.useReplaceRefs", "false");
        var replaced = (await GitAsync(a, "rev-parse", $"{commit}^{{tree}}")).Trim();
        var own = (await GitAsync(b, "rev-parse", $"{commit}^{{tree}}")).Trim();
        Assert.NotEqual(replaced, own);
        return (a, b, commit, replaced, own);
    }

    [Fact]
    public async Task A_tree_reading_replacements_another_way_is_never_answered_from_the_checkouts_fact_in_one_flow()
    {
        var (a, b, commit, replaced, own) = await ReplacedAsync();
        string[] asked = ["rev-parse", "--verify", "--quiet", $"{commit}^{{tree}}"];

        using (GitFacts.Within())
        {
            GitFacts.SameRepository(b, (await GitAsync(b, "rev-parse", "--path-format=absolute", "--git-common-dir")).Trim());
            var first = await WorkingTree.GitAsync(a, asked, CancellationToken.None);
            var second = await WorkingTree.GitAsync(b, asked, CancellationToken.None);

            Assert.Equal(replaced, first.Stdout.Trim());
            Assert.Equal(own, second.Stdout.Trim());
        }

        Assert.False(GitAskerContext.Same(a, b));
    }

    [Fact]
    public async Task A_listing_the_checkout_read_answers_no_derived_tree_for_a_tree_reading_replacements_another_way()
    {
        var (a, b, commit, _, _) = await ReplacedAsync();
        var snapshot = RefSnapshot.Take(a);
        Assert.NotNull(await snapshot.ListedAsync(CancellationToken.None));

        Assert.NotNull(RefSnapshot.TreeHeld(a, commit));
        Assert.Null(RefSnapshot.TreeHeld(b, commit));
        Assert.Null(await snapshot.TreeOfAsync(commit, CancellationToken.None, asker: b));
        Assert.Null(RefSnapshot.ListedFor(b));
        Assert.NotNull(RefSnapshot.ListedFor(a));
    }
}
