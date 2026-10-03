using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What one session's own tree holds that no branch of the person's does (SESSUX1a, D126 §2.1), by D88's proof: its
/// uncommitted changes and its unlanded commits, the fact *To review* is read from. Real git throughout, so this is the
/// suite's Process half; the reader over a judgement handed in is <see cref="SessionGroupsTests"/>.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class SessionTreeWorkTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;

    public SessionTreeWorkTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "tree-work", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    [Fact]
    public async Task Commits_no_branch_of_the_persons_holds_are_the_trees_work()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "one.txt", "one");
        await CommitAsync(tree.Path, "two.txt", "two");

        Assert.Equal(new TreeWork(2, 0), await trees.WorkAsync(tree.Path));
    }

    [Fact]
    public async Task Uncommitted_changes_are_the_trees_work()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "draft.txt"), "half done\n");

        Assert.Equal(new TreeWork(0, 1), await trees.WorkAsync(tree.Path));
    }

    [Fact]
    public async Task A_tree_whose_work_a_branch_of_the_persons_holds_has_none()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "one.txt", "one");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", tree.Branch);

        var work = await trees.WorkAsync(tree.Path);

        Assert.Equal(new TreeWork(0, 0), work);
        Assert.False(work!.Holds);
    }

    /// <summary>
    /// 🔴 git walks UP: a folder under the trees that is no repository of its own would answer for whatever encloses it,
    /// so it is never asked. Nor is a tree outside this home, or one that is gone.
    /// </summary>
    [Fact]
    public async Task A_folder_that_is_no_tree_of_this_homes_is_never_asked()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var stray = Path.Combine(trees.TreesRoot, "aurora", "engine", "s-0000beef");
        Directory.CreateDirectory(stray);
        await File.WriteAllTextAsync(Path.Combine(stray, "left.txt"), "left behind\n");

        Assert.Null(await trees.WorkAsync(stray));
        Assert.Null(await trees.WorkAsync(root));
        Assert.Null(await trees.WorkAsync(Path.Combine(trees.TreesRoot, "aurora", "engine", "s-gone")));
    }

    private async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await CommitAsync(root, "README.md", $"# {name}");
        return root;
    }

    private static async Task CommitAsync(string tree, string file, string content)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", file);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
