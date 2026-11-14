using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What one session's own tree holds that no branch of the person's does, by the landed proof: its
/// uncommitted changes and its unlanded commits, the fact *To review* is read from. Real git throughout, so this is the
/// suite's Process half; the reader over a judgement handed in is <see cref="SessionGroupsTests"/>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
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
        GitSeed.Discard(_scratch);
    }

    [Fact]
    public async Task Commits_no_branch_of_the_persons_holds_are_the_trees_work()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "one.txt", "one");
        await CommitAsync(tree.Path, "two.txt", "two");

        Assert.Equal(new TreeWork(2, 0) { Unlanded = 2 }, await trees.WorkAsync(tree.Path));
    }

    /// <summary>A copy of the run's plain seed (<see cref="GitSeed.PlainAsync"/>): no test here reads its first commit.</summary>
    private Task<string> RepositoryAsync(string name) => GitSeed.PlainAsync(Path.Combine(_scratch, name), name);

    private static async Task CommitAsync(string tree, string file, string content)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", file);
    }

    private static string RepoRoot() => GitSeed.RepoRoot();
}
