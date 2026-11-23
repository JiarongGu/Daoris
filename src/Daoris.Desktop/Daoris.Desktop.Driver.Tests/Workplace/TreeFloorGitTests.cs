using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A finished session's tree goes by the rule (cleaned up once its work is on the line) though it holds a file a tool writes again that
/// the person's rules say so of: the floor is judged in the tree being cleaned, never in the checkout, whose <c>local/</c>
/// holds another repository; and a tree whose branch a landing replayed is held by the end its record kept, though the line changed the
/// files since. A real nested repository in the tree is still kept. Real git, and nothing reaches a network.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeFloorGitTests : LandedFixture
{
    /// <summary>A repository that ignores <c>local/</c>, whose checkout holds another repository there, as the main checkout does.</summary>
    private async Task<string> RepositoryWithLocalAsync()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, ".gitignore", "local/\n", "ignore local");
        var other = Path.Combine(root, "local", "other");
        Directory.CreateDirectory(other);
        await GitAsync(other, "init", "--quiet");
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

    private static void WriteLater(string tree, string relative, string content = "{}\n")
    {
        var path = Path.Combine(tree, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task A_tree_whose_only_extra_is_a_file_the_persons_rule_calls_output_is_tidied_after_its_landing_though_the_checkouts_local_holds_a_repository()
    {
        var (trees, tree) = await LandedAsync(await RepositoryWithLocalAsync());
        WriteLater(tree.Path, "local/quick-base.json");
        FilesEdits.Set(Home, "engine", machine: false, "output", ["local/quick-base.json"], copy: null);

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(tree.Path));
    }

    [Fact]
    public async Task A_rule_that_reaches_a_real_nested_repository_in_the_tree_leaves_it_and_the_tree_is_kept()
    {
        var (trees, tree) = await LandedAsync(await RepositoryWithLocalAsync());
        WriteLater(tree.Path, "local/quick-base.json");
        var inner = Path.Combine(tree.Path, "local", "inner");
        Directory.CreateDirectory(inner);
        await GitAsync(inner, "init", "--quiet");
        WriteLater(tree.Path, "local/inner/notes.txt", "a repository's own file\n");
        FilesEdits.Set(Home, "engine", machine: false, "scratch", ["local/"], copy: null);

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("local/inner/", removal.Message);
        Assert.True(Directory.Exists(tree.Path));
    }

    [Fact]
    public async Task A_file_the_persons_rule_calls_local_is_kept_though_the_repository_calls_it_output()
    {
        var root = await RepositoryWithLocalAsync();
        await CommitAsync(root, "daoris.files.json", """{ "form": 2, "rules": [ { "kind": "output", "paths": ["/local/quick-base.json"] } ] }""", "declare it");
        var (trees, tree) = await LandedAsync(root);
        WriteLater(tree.Path, "local/quick-base.json");
        FilesEdits.Set(Home, "engine", machine: false, "local", ["local/quick-base.json"], copy: null);

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("local/quick-base.json", removal.Message);
    }

    [Fact]
    public async Task A_rule_spelled_in_another_case_covers_the_file_where_the_tree_ignores_case()
    {
        var root = await RepositoryWithLocalAsync();
        await GitAsync(root, "config", "core.ignorecase", "true");
        var (trees, tree) = await LandedAsync(root);
        WriteLater(tree.Path, "local/quick-base.json");
        FilesEdits.Set(Home, "engine", machine: false, "output", ["LOCAL/Quick-Base.json"], copy: null);

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
    }

    /// <summary>
    /// A landing that replayed the session's commit onto a line that had moved leaves it under an id of its own; the line then changes the
    /// file again, so a reading of the files finds it different, and only the end the record kept when the line held it says it is landed.
    /// </summary>
    private async Task<(SessionTrees Trees, TreeOpened Tree)> ReplayedAsync(bool kept)
    {
        var root = await RepositoryWithLocalAsync();
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        var tip = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await CommitAsync(root, "other.txt", "the line moves\n", "the line moves");
        await GitAsync(root, "cherry-pick", tip);
        if (kept)
        {
            using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
            Assert.Equal(1, await trees.Ends.KeepAsync(root, "engine", "aurora", "main", CancellationToken.None));
        }

        await CommitAsync(root, "work.txt", "the work, changed on the line\n", "the line changes the work");
        return (trees, tree);
    }

    [Fact]
    public async Task A_tree_whose_branch_a_landing_replayed_is_tidied_by_the_end_its_record_kept()
    {
        var (trees, tree) = await ReplayedAsync(kept: true);

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(tree.Path));
    }

    [Fact]
    public async Task The_same_replay_with_no_end_kept_leaves_the_tree_for_its_press()
    {
        var (trees, tree) = await ReplayedAsync(kept: false);

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.True(Directory.Exists(tree.Path));
    }
}
