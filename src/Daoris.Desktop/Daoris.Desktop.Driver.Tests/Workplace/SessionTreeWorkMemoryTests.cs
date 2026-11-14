using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What one session's own tree holds that no branch of the person's does, by the landed proof, on a repository kept in memory: its
/// uncommitted changes and its unlanded commits, the fact <i>To review</i> is read from.
/// </summary>
public sealed class SessionTreeWorkMemoryTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly SessionTreeMemoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private async Task CommitAsync(string tree, string file)
    {
        _fixture.Repo.Write(tree, file, file + "\n");
        Assert.True((await _fixture.Repo.CommitAllAsync(tree, file, Ct)).Ok);
    }

    [Fact]
    public async Task Uncommitted_changes_are_the_trees_work()
    {
        using var _ = _fixture.Use();
        var tree = await _fixture.Trees.OpenAsync(_fixture.Root, "engine", "aurora");
        _fixture.Repo.Write(tree.Path, "draft.txt", "half done\n");

        Assert.Equal(new TreeWork(0, 1) { Unlanded = 0 }, await _fixture.Trees.WorkAsync(tree.Path));
    }

    [Fact]
    public async Task A_tree_whose_work_a_branch_of_the_persons_holds_has_none()
    {
        using var _ = _fixture.Use();
        var tree = await _fixture.Trees.OpenAsync(_fixture.Root, "engine", "aurora");
        await CommitAsync(tree.Path, "one.txt");
        var line = await _fixture.Repo.HeadAsync(_fixture.Root, Ct);
        Assert.True((await _fixture.Repo.UpdateRefAsync(_fixture.Root, "refs/heads/main", (await _fixture.Repo.HeadAsync(tree.Path, Ct))!, line, Ct)).Ok);

        var work = await _fixture.Trees.WorkAsync(tree.Path);

        Assert.Equal(new TreeWork(0, 0) { Unlanded = 0 }, work);
        Assert.False(work!.Holds);
    }

    [Fact]
    public async Task A_folder_that_is_no_tree_of_this_homes_is_never_asked()
    {
        using var _ = _fixture.Use();
        var stray = Path.Combine(_fixture.Trees.TreesRoot, "aurora", "engine", "s-0000beef");
        Directory.CreateDirectory(stray);
        await File.WriteAllTextAsync(Path.Combine(stray, "left.txt"), "left behind\n");

        Assert.Null(await _fixture.Trees.WorkAsync(stray));
        Assert.Null(await _fixture.Trees.WorkAsync(_fixture.Root));
        Assert.Null(await _fixture.Trees.WorkAsync(Path.Combine(_fixture.Trees.TreesRoot, "aurora", "engine", "s-gone")));
    }
}
