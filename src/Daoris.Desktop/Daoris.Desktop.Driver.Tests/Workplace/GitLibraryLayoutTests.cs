using static Daoris.Desktop.Driver.Tests.GitLibraryParity;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The library's layout reads held to git's, value for value on real git, in the checkout and in linked trees: <c>--show-toplevel</c>,
/// the one combined <c>rev-parse</c>, <c>worktree list --porcelain</c> (a locked tree with a reason and without, a detached one, two
/// linked trees) and <c>core.ignorecase</c>; what stays git's (a subfolder, a branch another ref makes ambiguous, a tree whose
/// directory is gone, an <c>ignorecase</c> the repository does not hold).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryLayoutTests : LandedFixture
{
    private static readonly string[] Combined = ["rev-parse", "--path-format=absolute", "--git-common-dir", "--show-toplevel", "--abbrev-ref", "HEAD"];

    [GitLegFact]
    public async Task The_layout_is_git_s_in_the_checkout_and_in_linked_trees()
    {
        var (root, _) = await RepositoryWithOriginAsync("layout");
        var first = Path.Combine(Scratch, "layout-first");
        var second = Path.Combine(Scratch, "layout-second");
        var third = Path.Combine(Scratch, "layout-third");
        await GitAsync(root, "worktree", "add", "-b", "topic", first);
        await GitAsync(root, "worktree", "add", "--detach", second);
        await GitAsync(root, "worktree", "add", "-b", "other", third);
        await GitAsync(root, "worktree", "lock", "--reason", "being used by an agent", first);
        await GitAsync(root, "worktree", "lock", third);
        await GitAsync(root, "config", "core.ignorecase", "true");

        foreach (var folder in new[] { root, first, second, third })
        {
            await SameAsync(folder, "rev-parse", "--show-toplevel");
            await SameAsync(folder, Combined);
            await SameAsync(folder, "--no-optional-locks", "worktree", "list", "--porcelain");
            await SameAsync(folder, "worktree", "list", "--porcelain");
            await SameAsync(folder, "--no-optional-locks", "config", "--bool", "core.ignorecase");
            await SameAsync(folder, "config", "--type=bool", "--get", "core.ignorecase");
        }

        if (OperatingSystem.IsWindows())
        {
            // Spelt in another case, the folder is the same folder, and git names it as the file system does.
            await SameAsync(root.ToUpperInvariant(), "rev-parse", "--show-toplevel");
            await SameAsync(first.ToUpperInvariant(), Combined);
        }

        await GitAsync(root, "config", "core.ignorecase", "false");
        await SameAsync(root, "worktree", "list", "--porcelain");
        await SameAsync(first, "config", "--bool", "core.ignorecase");
    }

    [GitLegFact]
    public async Task An_upstream_is_read_from_the_branch_s_configuration_as_git_says_it()
    {
        var (root, _) = await RepositoryWithOriginAsync("layout-upstream");
        var linked = Path.Combine(Scratch, "layout-upstream-tree");
        await GitAsync(root, "worktree", "add", "-b", "topic", linked);
        await GitAsync(root, "branch", "--track", "following", "origin/main");
        await GitAsync(root, "branch", "--track", "lost", "origin/main");
        await GitAsync(root, "config", "branch.lost.merge", "refs/heads/never-pushed");

        foreach (var folder in new[] { root, linked })
        {
            foreach (var branch in new[] { "topic", "following", "main" })
            {
                await SayingAsync(folder, "rev-parse", "--abbrev-ref", "--symbolic-full-name", $"{branch}@{{upstream}}");
                await SayingAsync(folder, "rev-parse", "--verify", "--quiet", $"{branch}@{{upstream}}^{{commit}}");
            }

            await SameAsync(folder, "rev-parse", "--show-toplevel", "--abbrev-ref", "HEAD");
            await GitsAsync(folder, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "lost@{upstream}");
            await GitsAsync(folder, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "absent@{upstream}");
        }
    }

    // The library's answer to a read, held to git's in its exit code and both streams.
    private static async Task SayingAsync(string folder, params string[] arguments)
    {
        var git = await GitFixture.RunAsync(folder, arguments);
        var asked = string.Join(' ', arguments);
        var library = await Daoris.Driver.GitLibraryReads.AnswerAsync(folder, arguments, CancellationToken.None);
        Assert.True(library is not null, $"the library answered no `{asked}`");
        Assert.Equal((git.ExitCode, git.Stdout, git.Stderr), (library!.Code, library.Stdout, library.Stderr));
        await SameAsync(folder, arguments);
    }

    [GitLegFact]
    public async Task What_the_library_cannot_say_exactly_is_git_s()
    {
        var (root, _) = await RepositoryWithOriginAsync("layout-git");
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        await GitsAsync(Path.Combine(root, "sub"), "rev-parse", "--show-toplevel");
        await GitAsync(root, "config", "--unset", "core.ignorecase");
        await GitsAsync(root, "config", "--bool", "core.ignorecase");

        var gone = Path.Combine(Scratch, "layout-gone");
        await GitAsync(root, "worktree", "add", "-b", "gone", gone);
        Directory.Delete(gone, recursive: true);
        await GitsAsync(root, "worktree", "list", "--porcelain");
        await GitAsync(root, "worktree", "prune");

        await GitAsync(root, "tag", "main");
        await GitsAsync(root, Combined);
    }
}
