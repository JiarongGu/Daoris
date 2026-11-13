using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A tree whose only extra is the index its repository declares and never commits, which every checkout writes again, goes with
/// its branch; an uncommitted edit beside it still keeps the tree. Real git, and nothing reaches a network.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GeneratedIndexGitTests : LandedFixture
{
    private const string DeclaresIndex = """{ "documents": { "index": "docs/index/README.md" } }""";

    /// <summary>A repository that ignores its index folder and declares the index in it, with the checkout's own index written.</summary>
    private async Task<string> IndexedRepositoryAsync()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, ".gitignore", "docs/index/\n", "ignore the index");
        await CommitAsync(root, "daoris.json", DeclaresIndex, "declare the index");
        Directory.CreateDirectory(Path.Combine(root, "docs", "index"));
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "index", "README.md"), "the checkout's index\n");
        return root;
    }

    /// <summary>The index the tree's own checks wrote after it opened, which the checkout does not hold as written.</summary>
    private static async Task RegenerateAsync(string tree)
    {
        foreach (var name in new[] { "README.md", "decisions.md", Path.Combine("outlines", "driver.md") })
        {
            var path = Path.Combine(tree, "docs", "index", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "the tree's index\n");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        }
    }

    [Fact]
    public async Task A_landed_tree_whose_only_extra_is_its_regenerated_index_goes_with_its_branch()
    {
        var root = await IndexedRepositoryAsync();
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        await RegenerateAsync(tree.Path);

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(tree.Path));
        Assert.True(File.Exists(Path.Combine(root, "docs", "index", "README.md")));
    }

    [Fact]
    public async Task An_uncommitted_edit_beside_the_regenerated_index_keeps_the_tree()
    {
        var root = await IndexedRepositoryAsync();
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        await RegenerateAsync(tree.Path);
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "work.txt"), "the work, edited and not committed\n");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("has uncommitted work — 1 path(s)", removal.Message);
        Assert.DoesNotContain("docs/index", removal.Message, StringComparison.Ordinal);
        Assert.True(Directory.Exists(tree.Path));
    }

    [Fact]
    public async Task A_file_written_in_an_index_folder_the_line_commits_is_still_the_trees_own()
    {
        var root = await IndexedRepositoryAsync();
        // The index is committed now (the add takes it once its folder is no longer ignored); only its notes folder is ignored.
        await CommitAsync(root, ".gitignore", "docs/index/notes/\n", "commit the index, ignore its notes");
        Directory.CreateDirectory(Path.Combine(root, "docs", "index", "notes"));
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "index", "notes", "owner.md"), "the owner's note\n");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        var note = Path.Combine(tree.Path, "docs", "index", "notes", "session.md");
        Directory.CreateDirectory(Path.GetDirectoryName(note)!);
        await File.WriteAllTextAsync(note, "what the session wrote\n");
        File.SetLastWriteTimeUtc(note, DateTime.UtcNow.AddMinutes(1));

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("docs/index/notes/session.md", removal.Message);
        Assert.True(File.Exists(note));
    }
}
