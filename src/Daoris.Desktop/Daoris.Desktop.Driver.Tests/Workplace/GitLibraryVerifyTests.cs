using static Daoris.Desktop.Driver.Tests.GitLibraryParity;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The library's <c>rev-parse --verify --quiet</c> held to git's, value for value on real git, in the checkout and a linked tree: a
/// branch, a remote branch, a tag annotated and not, <c>HEAD</c>, with and without <c>^{commit}</c>, a ref that is absent, a tag of a
/// tree; what stays git's (a short name, an operator, a failure that is not quiet, an unborn <c>HEAD</c>'s plain read).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryVerifyTests : LandedFixture
{
    [GitLegFact]
    public async Task A_verified_revision_is_git_s_in_the_checkout_and_a_linked_tree()
    {
        var (root, _) = await RepositoryWithOriginAsync("verify");
        var linked = Path.Combine(Scratch, "verify-topic");
        await GitAsync(root, "worktree", "add", "-b", "topic", linked);
        await CommitAsync(linked, "topic.txt", "topic\n", "on topic");
        await GitAsync(linked, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "tag", "-a", "marked", "-m", "a tag");
        await GitAsync(root, "tag", "plain", "main");
        var tree = (await GitAsync(root, "rev-parse", "main^{tree}")).Trim();
        await GitAsync(root, "update-ref", "refs/tags/of-tree", tree);

        foreach (var folder in new[] { root, linked })
        {
            await SameAsync(folder, "rev-parse", "--verify", "--quiet", "HEAD");
            await SameAsync(folder, "rev-parse", "--verify", "--quiet", "HEAD^{commit}");
            await SameAsync(folder, "--no-optional-locks", "rev-parse", "--verify", "--quiet", "HEAD^{commit}");
            await SameAsync(folder, "rev-parse", "--verify", "HEAD");
            await SameAsync(folder, "rev-parse", "HEAD");
            await SameAsync(folder, "rev-parse", "--verify", "--quiet", "refs/heads/main");
            await SameAsync(folder, "rev-parse", "--verify", "--quiet", "refs/heads/main^{commit}");
            await SameAsync(folder, "rev-parse", "--verify", "-q", "refs/heads/topic^{commit}");
            await SameAsync(folder, "rev-parse", "--verify", "--quiet", "refs/remotes/origin/main^{commit}");
            await SameAsync(folder, "rev-parse", "--verify", "--quiet", "refs/tags/marked");
            await SameAsync(folder, "rev-parse", "--verify", "--quiet", "refs/tags/marked^{commit}");
            await SameAsync(folder, "rev-parse", "--verify", "--quiet", "refs/tags/plain^{commit}");
            await SameAsync(folder, "rev-parse", "--verify", "--quiet", "refs/tags/of-tree^{commit}");
            await SameAsync(folder, "rev-parse", "--verify", "--quiet", "refs/heads/absent");
            await SameAsync(folder, "rev-parse", "--verify", "--quiet", "refs/heads/absent^{commit}");
            await SameAsync(folder, "rev-parse", "--verify", "--quiet", "refs/remotes/origin/daoris/s-none^{commit}");
            await GitsAsync(folder, "rev-parse", "--verify", "--quiet", "main^{commit}");
            await GitsAsync(folder, "rev-parse", "--verify", "--quiet", "refs/heads/main~1");
            await GitsAsync(folder, "rev-parse", "--verify", "--quiet", "refs/heads/main@{upstream}^{commit}");
            await GitsAsync(folder, "rev-parse", "--verify", "refs/heads/absent");
        }
    }
}
