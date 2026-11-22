using static Daoris.Desktop.Driver.Tests.GitLibraryParity;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>status --porcelain</c> (v1, with or without <c>-z</c>): a tree's staged and unstaged changes and its untracked paths answered in
/// process as git prints them, on real git, with no git started; what the library cannot say exactly (a rename git may pair, a
/// submodule, a sparse or intent-to-add entry, a content git would convert, an ignored listing) stays git's.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryStatusTests : LandedFixture
{
    // A write time long before any index the test writes, so the entry is not racy and only its stat tells.
    private static readonly DateTime Earlier = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string[][] Forms =
    [
        ["status", "--porcelain", "-z", "--untracked-files=all"],
        ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all"],
        ["--no-optional-locks", "status", "--porcelain"],
        ["--no-optional-locks", "status", "--porcelain=v1", "-z", "--untracked-files=no", "--no-renames"],
        ["--no-optional-locks", "-c", "core.quotePath=false", "status", "--porcelain", "-z", "--untracked-files=all", "--ignore-submodules=none"],
    ];

    [GitLegFact]
    public async Task A_tree_s_changes_and_untracked_paths_are_git_s()
    {
        var root = await RepositoryAsync("status-changes");
        await AllFormsAsync(root);

        Directory.CreateDirectory(Path.Combine(root, "docs", "deep"));
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "kept.md"), "kept\n");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "deep", "gone.md"), "gone\n");
        await File.WriteAllTextAsync(Path.Combine(root, "same.txt"), "abc\n");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-q", "-m", "more");

        // Unstaged: a size changed, an edit of the same size, a deletion; staged: an addition and a change; then both on one path.
        await File.AppendAllTextAsync(Path.Combine(root, "shared.txt"), "two\n");
        await File.WriteAllTextAsync(Path.Combine(root, "same.txt"), "xyz\n");
        File.Delete(Path.Combine(root, "docs", "deep", "gone.md"));
        await File.WriteAllTextAsync(Path.Combine(root, "added.txt"), "added\n");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# staged\n");
        await GitAsync(root, "add", "added.txt", "README.md");
        await File.AppendAllTextAsync(Path.Combine(root, "README.md"), "and after\n");

        // Untracked: a file, a folder of its own, a folder beside tracked files, names with a space and past ASCII.
        Directory.CreateDirectory(Path.Combine(root, "fresh", "inner"));
        await File.WriteAllTextAsync(Path.Combine(root, "fresh", "inner", "one.txt"), "1\n");
        await File.WriteAllTextAsync(Path.Combine(root, "fresh", "two.txt"), "2\n");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "deep", "new.md"), "new\n");
        Directory.CreateDirectory(Path.Combine(root, "empty"));
        await File.WriteAllTextAsync(Path.Combine(root, "a name.txt"), "spaced\n");
        await File.WriteAllTextAsync(Path.Combine(root, "dir.txt"), "sorted before dir/\n");
        await AllFormsAsync(root, quoted: true);

        // A file touched to the same bytes is no change; the same size with other bytes is.
        File.SetLastWriteTimeUtc(Path.Combine(root, "docs", "kept.md"), Earlier);
        await AllFormsAsync(root, quoted: true);

        await File.WriteAllTextAsync(Path.Combine(root, "naïve.txt"), "past ascii\n");
        await SameAsync(root, Forms[0]);
        await SameAsync(root, Forms[4]);
        await GitsAsync(root, Forms[2]);
    }

    [GitLegFact]
    public async Task What_is_ignored_is_never_listed_as_untracked()
    {
        var root = await RepositoryAsync("status-ignored");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.log\nbuild/\n!keep.log\n");
        Directory.CreateDirectory(Path.Combine(root, "build", "out"));
        Directory.CreateDirectory(Path.Combine(root, "traces"));
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        await File.WriteAllTextAsync(Path.Combine(root, "build", "out", "x.bin"), "x\n");
        await File.WriteAllTextAsync(Path.Combine(root, "traces", "a.log"), "a\n");
        await File.WriteAllTextAsync(Path.Combine(root, "keep.log"), "kept\n");
        await File.WriteAllTextAsync(Path.Combine(root, "sub", ".gitignore"), "local.txt\n");
        await File.WriteAllTextAsync(Path.Combine(root, "sub", "local.txt"), "local\n");
        await File.WriteAllTextAsync(Path.Combine(root, "sub", "other.txt"), "other\n");
        await File.AppendAllTextAsync(Path.Combine(root, ".git", "info", "exclude"), "private/\n");
        Directory.CreateDirectory(Path.Combine(root, "private"));
        await File.WriteAllTextAsync(Path.Combine(root, "private", "p.txt"), "p\n");
        await AllFormsAsync(root);

        // A tracked file inside an ignored folder is still read; the ignored listing is git's.
        await File.WriteAllTextAsync(Path.Combine(root, "build", "tracked.txt"), "t\n");
        await GitAsync(root, "add", "-f", "build/tracked.txt");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-q", "-m", "forced");
        await File.AppendAllTextAsync(Path.Combine(root, "build", "tracked.txt"), "changed\n");
        await AllFormsAsync(root);
        await GitsAsync(root, "--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all", "--ignored=matching");
    }

    [GitLegFact]
    public async Task A_rename_git_may_pair_is_git_s_unless_renames_are_off()
    {
        var root = await RepositoryAsync("status-renamed");
        await GitAsync(root, "mv", "shared.txt", "moved.txt");
        await GitsAsync(root, Forms[0]);
        await GitsAsync(root, Forms[2]);
        await SameAsync(root, Forms[3]);

        // A deletion staged beside a change and no addition pairs nothing; nor does an addition beside a change and no deletion.
        await GitAsync(root, "mv", "moved.txt", "shared.txt");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# changed\n");
        await GitAsync(root, "add", "README.md");
        await GitAsync(root, "rm", "-q", "--cached", "shared.txt");
        await SameAsync(root, Forms[0]);
        await GitAsync(root, "add", "shared.txt");
        await File.WriteAllTextAsync(Path.Combine(root, "other.txt"), "other\n");
        await GitAsync(root, "add", ".");
        await SameAsync(root, Forms[0]);
    }

    [GitLegFact]
    public async Task A_submodule_a_sparse_tree_and_an_intent_to_add_are_git_s()
    {
        var root = await RepositoryAsync("status-entries");
        var commit = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        // A nested repository nobody tracks is one untracked folder.
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        await GitAsync(Path.Combine(root, "nested"), "init", "-q");
        await File.WriteAllTextAsync(Path.Combine(root, "nested", "n.txt"), "n\n");
        await AllFormsAsync(root);

        await GitAsync(root, "update-index", "--add", "--cacheinfo", $"160000,{commit},module");
        await GitsAsync(root, Forms[0]);
        await GitAsync(root, "update-index", "--force-remove", "module");

        await File.WriteAllTextAsync(Path.Combine(root, "intent.txt"), "intent\n");
        await GitAsync(root, "add", "-N", "intent.txt");
        await GitsAsync(root, Forms[0]);
        await GitAsync(root, "rm", "-q", "--cached", "intent.txt");

        Directory.CreateDirectory(Path.Combine(root, "inside"));
        await File.WriteAllTextAsync(Path.Combine(root, "inside", "i.txt"), "i\n");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-q", "-m", "inside");
        await GitAsync(root, "sparse-checkout", "set", "--no-cone", "/inside/");
        await GitsAsync(root, Forms[0]);
        await GitsAsync(root, Forms[2]);
    }

    [GitLegFact]
    public async Task A_content_git_would_convert_is_git_s_to_compare()
    {
        var root = await RepositoryAsync("status-converted");
        await File.WriteAllTextAsync(Path.Combine(root, "crlf.txt"), "one\ntwo\n");
        await GitAsync(root, "add", "crlf.txt");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-q", "-m", "crlf");

        // The same size, other bytes, one of them a carriage return: whether it changed rests on git's conversion.
        await File.WriteAllTextAsync(Path.Combine(root, "crlf.txt"), "one\r\ntwo\n"[..8]);
        File.SetLastWriteTimeUtc(Path.Combine(root, "crlf.txt"), Earlier);
        await GitsAsync(root, Forms[0]);

        // A filter an attribute may name is git's to run.
        await File.WriteAllTextAsync(Path.Combine(root, "crlf.txt"), "one\ntwo\n");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "*.txt filter=lfs\n");
        File.SetLastWriteTimeUtc(Path.Combine(root, "crlf.txt"), Earlier);
        await GitsAsync(root, Forms[0]);
    }

    private static async Task AllFormsAsync(string root, bool quoted = false)
    {
        foreach (var form in Forms)
        {
            // The newline form quotes a path with a space, so a quoted name is git's to print.
            if (quoted && !form.Contains("-z")) await GitsAsync(root, form);
            else await SameAsync(root, form);
        }
    }
}
