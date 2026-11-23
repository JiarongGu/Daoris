using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitLibraryParity;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>status --porcelain=v2</c>, with or without <c>--branch</c>: the tree walk's one read and the Changes view's, answered in process
/// as git prints them (each changed entry's modes and ids, the untracked paths, and the branch's two headers), on real git, with no git
/// started. A branch with an upstream (git prints its name and how far it is ahead and behind), a rename git may pair and what the
/// version 1 form leaves to git stay git's.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryStatusV2Tests : LandedFixture
{
    private static readonly DateTime Earlier = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // The tree walk's read, the Changes view's, and the walk's with each of the options a person's flow adds.
    private static readonly string[][] Forms =
    [
        ["--no-optional-locks", "status", "--porcelain=v2", "--branch"],
        ["--no-optional-locks", "-c", "core.quotePath=false", "status", "--porcelain=v2", "-z", "--untracked-files=all", "--no-renames"],
        ["status", "--porcelain=v2"],
        ["--no-optional-locks", "status", "--porcelain=v2", "--branch", "-z", "--untracked-files=no"],
    ];

    [GitLegFact]
    public async Task A_clean_tree_says_its_head_and_its_branch()
    {
        var root = await StampedAsync("statusv2-clean");
        foreach (var form in Forms) await SameAsync(root, form);
        var head = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var answer = await GitLibraryReads.AnswerAsync(root, Forms[0], CancellationToken.None);
        Assert.Equal($"# branch.oid {head}\n# branch.head main\n", answer!.Stdout);
    }

    [GitLegFact]
    public async Task A_detached_head_and_an_unborn_branch_are_said_as_git_says_them()
    {
        var root = await StampedAsync("statusv2-heads");
        await GitAsync(root, "checkout", "-q", "--detach");
        await SameAsync(root, Forms[0]);
        await GitAsync(root, "checkout", "-q", "main");

        var unborn = Path.Combine(Scratch, "statusv2-unborn");
        Directory.CreateDirectory(unborn);
        await GitAsync(unborn, "init", "--quiet", "-b", "fresh");
        await GitSeed.NoMaintenanceAsync(unborn);
        await GitStamp.OfAsync(unborn, CancellationToken.None);
        foreach (var form in Forms) await SameAsync(unborn, form);
        await File.WriteAllTextAsync(Path.Combine(unborn, "first.txt"), "first\n");
        await GitAsync(unborn, "add", "first.txt");
        await File.WriteAllTextAsync(Path.Combine(unborn, "loose.txt"), "loose\n");
        foreach (var form in Forms) await SameAsync(unborn, form);
    }

    [GitLegFact]
    public async Task A_tree_s_changes_and_untracked_paths_carry_the_modes_and_ids_git_prints()
    {
        var root = await StampedAsync("statusv2-changes");
        Directory.CreateDirectory(Path.Combine(root, "docs", "deep"));
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "kept.md"), "kept\n");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "deep", "gone.md"), "gone\n");
        await File.WriteAllTextAsync(Path.Combine(root, "same.txt"), "abc\n");
        await File.WriteAllTextAsync(Path.Combine(root, "tool.sh"), "run\n");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-q", "-m", "more");
        foreach (var form in Forms) await SameAsync(root, form);

        // Unstaged: a size changed, an edit of the same size, a deletion; staged: a change and a deletion; then both on one path.
        await File.AppendAllTextAsync(Path.Combine(root, "shared.txt"), "two\n");
        await File.WriteAllTextAsync(Path.Combine(root, "same.txt"), "xyz\n");
        File.Delete(Path.Combine(root, "docs", "deep", "gone.md"));
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# staged\n");
        await GitAsync(root, "add", "README.md");
        await File.AppendAllTextAsync(Path.Combine(root, "README.md"), "and after\n");
        await GitAsync(root, "rm", "-q", "--cached", "docs/kept.md");
        await File.WriteAllTextAsync(Path.Combine(root, "dir.txt"), "untracked\n");
        Directory.CreateDirectory(Path.Combine(root, "fresh", "inner"));
        await File.WriteAllTextAsync(Path.Combine(root, "fresh", "inner", "one.txt"), "1\n");
        foreach (var form in Forms) await SameAsync(root, form);

        // A staged addition beside the staged deletion is a rename git may pair; the form that asks for none is still answered.
        await File.WriteAllTextAsync(Path.Combine(root, "added.txt"), "added\n");
        await GitAsync(root, "add", "added.txt");
        await GitsAsync(root, Forms[0]);
        await SameAsync(root, Forms[1]);
        await GitAsync(root, "add", "docs/kept.md");
        foreach (var form in Forms) await SameAsync(root, form);

        // A staged mode change, which git prints in the mode columns only.
        await GitAsync(root, "update-index", "--chmod=+x", "tool.sh");
        foreach (var form in Forms) await SameAsync(root, form);

        // A name the newline form quotes is git's to print; the NUL form says it as it is.
        await File.WriteAllTextAsync(Path.Combine(root, "a name.txt"), "spaced\n");
        await GitsAsync(root, Forms[0]);
        await SameAsync(root, Forms[1]);
    }

    [GitLegFact]
    public async Task A_branch_with_an_upstream_a_rename_and_an_ignored_listing_are_git_s()
    {
        var root = await StampedAsync("statusv2-git");
        await GitAsync(root, "branch", "other");
        await GitAsync(root, "config", "branch.main.remote", ".");
        await GitAsync(root, "config", "branch.main.merge", "refs/heads/other");
        await GitsAsync(root, Forms[0]);
        await SameAsync(root, Forms[1]);
        await GitAsync(root, "config", "--unset", "branch.main.remote");
        await GitAsync(root, "config", "--unset", "branch.main.merge");
        await SameAsync(root, Forms[0]);

        await GitAsync(root, "mv", "shared.txt", "moved.txt");
        await GitsAsync(root, Forms[0]);
        await GitsAsync(root, Forms[2]);
        await SameAsync(root, Forms[1]);
        await GitsAsync(root, "--no-optional-locks", "status", "--porcelain=v2", "--branch", "--ignored");
        await GitsAsync(root, "--no-optional-locks", "status", "--porcelain=v2", "--show-stash");
    }

    [GitLegFact]
    public async Task A_stat_git_trusts_is_trusted_in_the_version_2_form_too()
    {
        var root = await StampedAsync("statusv2-stat");
        var path = Path.Combine(root, "same.txt");
        await File.WriteAllTextAsync(path, "abcd\n");
        File.SetLastWriteTimeUtc(path, Earlier);
        await GitAsync(root, "add", "same.txt");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-q", "-m", "same");
        await File.WriteAllTextAsync(path, "wxyz\n");
        File.SetLastWriteTimeUtc(path, Earlier);
        await SameAsync(root, Forms[0]);

        // A content git converts is git's to compare, as in the version 1 form.
        // (Replaced by a new file, as an editor saves, so the creation time moved and the bytes are read.)
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "*.txt filter=lfs\n");
        var written = Path.Combine(root, "same.next");
        await File.WriteAllTextAsync(written, "kept\n");
        File.SetLastWriteTimeUtc(written, Earlier);
        File.Move(written, path, overwrite: true);
        await GitsAsync(root, Forms[0]);
    }

    private async Task<string> StampedAsync(string name)
    {
        var root = await RepositoryAsync(name);
        await GitStamp.OfAsync(root, CancellationToken.None);
        return root;
    }
}
