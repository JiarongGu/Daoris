using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A finished session's tree goes once its work is on the line though the session wrote scratch into an ignored folder (<c>local/</c>):
/// a file it wrote after the tree opened is its scratch, said in the removal's record. A tree whose extra files hold anything else is
/// kept: an uncommitted tracked change, an untracked file outside the ignored folders, a copy of Daoris's the session edited, and a folder
/// that holds another repository. Real git, and nothing reaches a network.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeSessionScratchGitTests : LandedFixture
{
    /// <summary>A repository that ignores <c>local/</c>; its checkout holds a private note there, as the main checkout does.</summary>
    private async Task<string> RepositoryWithLocalAsync(bool checkoutHoldsLocal = true)
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, ".gitignore", "local/\n", "ignore local");
        if (checkoutHoldsLocal)
        {
            Directory.CreateDirectory(Path.Combine(root, "local"));
            File.WriteAllText(Path.Combine(root, "local", "private.md"), "the owner's own note\n");
        }

        return root;
    }

    private async Task<(SessionTrees Trees, TreeOpened Tree)> LandedAsync(string root)
    {
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        return (trees, tree);
    }

    private static void WriteLater(string tree, string relative, string content = "log\n")
    {
        var path = Path.Combine(tree, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_tree_whose_only_extras_are_files_the_session_wrote_under_local_is_tidied_after_its_landing_and_says_so(bool checkoutHoldsLocal)
    {
        var (trees, tree) = await LandedAsync(await RepositoryWithLocalAsync(checkoutHoldsLocal));
        WriteLater(tree.Path, "local/reached-1.log");
        WriteLater(tree.Path, "local/base/contracts.log");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(tree.Path));
        Assert.Contains("2 scratch file(s)", removal.Message);
        Assert.Contains("local/reached-1.log", removal.Message);
    }

    [Fact]
    public async Task A_tree_with_an_uncommitted_tracked_change_beside_its_scratch_is_kept()
    {
        var (trees, tree) = await LandedAsync(await RepositoryWithLocalAsync());
        WriteLater(tree.Path, "local/reached-1.log");
        File.WriteAllText(Path.Combine(tree.Path, "work.txt"), "changed after the landing\n");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("uncommitted", removal.Message);
        Assert.True(File.Exists(Path.Combine(tree.Path, "local", "reached-1.log")));
    }

    [Fact]
    public async Task A_tree_with_an_untracked_file_outside_the_ignored_folders_beside_its_scratch_is_kept()
    {
        var (trees, tree) = await LandedAsync(await RepositoryWithLocalAsync());
        WriteLater(tree.Path, "local/reached-1.log");
        WriteLater(tree.Path, "notes.txt");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("uncommitted work", removal.Message);
        Assert.True(File.Exists(Path.Combine(tree.Path, "local", "reached-1.log")));
    }

    [Fact]
    public async Task A_copy_of_Daoris_the_session_edited_keeps_the_tree_while_one_it_left_as_made_goes_with_it()
    {
        var (trees, tree) = await LandedAsync(await RepositoryWithLocalAsync());
        WriteLater(tree.Path, "local/private.md", "the owner's own note\n");
        WriteLater(tree.Path, "local/copied.md", "as copied\n");
        TreeFilesRecord.Change(Home, tree.Path, state => state with
        {
            Copies = new Dictionary<string, string>
            {
                ["local/private.md"] = TreeCopies.HashOf(Path.Combine(tree.Path, "local", "private.md"))!,
                ["local/copied.md"] = TreeCopies.HashOf(Path.Combine(tree.Path, "local", "copied.md"))!,
            },
        });
        File.WriteAllText(Path.Combine(tree.Path, "local", "copied.md"), "the session's edit of it\n");
        File.SetLastWriteTimeUtc(Path.Combine(tree.Path, "local", "copied.md"), new DateTime(2099, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("local/copied.md", removal.Message);
        Assert.DoesNotContain("local/private.md", removal.Message);
    }

    [Fact]
    public async Task A_folder_under_local_that_holds_another_repository_keeps_the_tree()
    {
        var (trees, tree) = await LandedAsync(await RepositoryWithLocalAsync());
        WriteLater(tree.Path, "local/reached-1.log");
        var inner = Path.Combine(tree.Path, "local", "clone");
        Directory.CreateDirectory(inner);
        await GitAsync(inner, "init", "--quiet");
        WriteLater(tree.Path, "local/clone/notes.txt", "a repository's own file\n");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("local/clone/", removal.Message);
        Assert.True(Directory.Exists(tree.Path));
    }

    [Fact]
    public async Task A_tree_whose_work_is_not_on_the_line_is_kept_though_its_extras_are_only_scratch()
    {
        var root = await RepositoryWithLocalAsync();
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work, not landed\n", "the work");
        WriteLater(tree.Path, "local/reached-1.log");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.True(Directory.Exists(tree.Path));
    }
}
