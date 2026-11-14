using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A landed tree whose only extra is what its repository's rule file says its tools write again goes with its branch, though its
/// bytes are not the checkout's; the same files with no such rule keep it. Real git, and nothing reaches a network.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class FileOutputsGitTests : LandedFixture
{
    private const string DeclaresOutput = """{ "form": 2, "rules": [ { "kind": "output", "paths": ["/gen/"] } ] }""";

    /// <summary>A repository that ignores a generated folder, which the checkout holds one build of.</summary>
    private async Task<string> RepositoryWithAsync(string? rules)
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, ".gitignore", "gen/\n", "ignore the generated folder");
        if (rules is not null) await CommitAsync(root, "daoris.files.json", rules, "declare the output");
        Directory.CreateDirectory(Path.Combine(root, "gen"));
        await File.WriteAllTextAsync(Path.Combine(root, "gen", "routes.ts"), "the checkout's build\n");
        return root;
    }

    private async Task<(SessionTrees Trees, TreeOpened Tree)> LandedWithOutputAsync(string root)
    {
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        var made = Path.Combine(tree.Path, "gen", "routes.ts");
        Directory.CreateDirectory(Path.GetDirectoryName(made)!);
        await File.WriteAllTextAsync(made, "the tree's build\n");
        File.SetLastWriteTimeUtc(made, DateTime.UtcNow.AddMinutes(1));
        return (trees, tree);
    }

    [Fact]
    public async Task A_landed_tree_whose_only_extra_is_output_its_rule_file_declares_goes_with_its_branch()
    {
        var (trees, tree) = await LandedWithOutputAsync(await RepositoryWithAsync(DeclaresOutput));

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(tree.Path));
    }

    [Fact]
    public async Task The_same_files_under_no_rule_keep_the_tree()
    {
        var (trees, tree) = await LandedWithOutputAsync(await RepositoryWithAsync(null));

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("gen/routes.ts", removal.Message);
        Assert.True(Directory.Exists(tree.Path));
    }
}
