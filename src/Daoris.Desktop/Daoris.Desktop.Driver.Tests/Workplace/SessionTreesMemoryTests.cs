using Daoris.Driver;
using Daoris.Ports.Repositories;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Session trees on a repository kept in memory: a tree opens under the home on its own branch from the canonical line, never beside
/// another repository's checkout, an open cut off at its unlock or refused there leaves nothing, and a tree removes whole or refuses
/// and says why. What stays real-git is git's own: a checkout that fails in a filter, a hook, a path past the platform's limit, and the
/// open's undo while git is still adding.
/// </summary>
public sealed class SessionTreesMemoryTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly SessionTreeMemoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task A_tree_opens_under_the_home_on_its_own_branch_based_on_the_canonical_line()
    {
        using var _ = _fixture.Use();

        var opened = await _fixture.Trees.OpenAsync(_fixture.Root, "engine", "aurora");

        Assert.StartsWith(DaorisHome.Entry(_fixture.Home, HomeEntry.Trees, "aurora", "engine"), opened.Path);
        Assert.StartsWith("daoris/", opened.Branch);
        Assert.Contains("canonical line", opened.BasedOn);
        Assert.Contains("nothing git does not track", opened.Sentence);
        Assert.Equal(opened.Branch, await _fixture.Repo.BranchAsync(opened.Path, Ct));
        Assert.Empty(await _fixture.Repo.StatusAsync(opened.Path, false, Ct));
        Assert.True(Directory.Exists(opened.Path));
    }

    [Fact]
    public async Task Two_opens_never_share_a_path_or_a_branch()
    {
        using var _ = _fixture.Use();

        var first = await _fixture.Trees.OpenAsync(_fixture.Root, "engine", "aurora");
        var second = await _fixture.Trees.OpenAsync(_fixture.Root, "engine", "aurora");

        Assert.NotEqual(first.Path, second.Path);
        Assert.NotEqual(first.Branch, second.Branch);
    }

    [Fact]
    public async Task A_dirty_root_does_not_hold_a_session_tree_and_is_not_touched_by_one()
    {
        using var _ = _fixture.Use();
        _fixture.Repo.Write(_fixture.Root, "wip.txt", "uncommitted work in flight\n");

        var opened = await _fixture.Trees.OpenAsync(_fixture.Root, "engine", "aurora");

        Assert.Empty(await _fixture.Repo.StatusAsync(opened.Path, false, Ct));
        Assert.Equal("uncommitted work in flight\n", _fixture.Repo.Read(_fixture.Root, "wip.txt"));
        Assert.Null(_fixture.Repo.Read(opened.Path, "wip.txt"));
    }

    [Fact]
    public async Task A_clean_merged_tree_removes_whole_branch_and_all()
    {
        using var _ = _fixture.Use();
        var opened = await _fixture.Trees.OpenAsync(_fixture.Root, "engine", "aurora");

        var removal = await _fixture.Trees.RemoveAsync(opened.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(opened.Path));
        Assert.Empty(await _fixture.BranchesAsync());
        Assert.Equal(1, await _fixture.TreesAsync());
    }

    [Fact]
    public async Task A_tree_with_uncommitted_work_refuses_to_be_removed()
    {
        using var _ = _fixture.Use();
        var opened = await _fixture.Trees.OpenAsync(_fixture.Root, "engine", "aurora");
        _fixture.Repo.Write(opened.Path, "half-done.txt", "not yet committed\n");

        var removal = await _fixture.Trees.RemoveAsync(opened.Path);

        Assert.False(removal.Removed);
        Assert.Contains("uncommitted", removal.Message);
        Assert.Contains("--force", removal.Message);
        Assert.Equal("not yet committed\n", _fixture.Repo.Read(opened.Path, "half-done.txt"));
        Assert.Equal(2, await _fixture.TreesAsync());
    }

    [Fact]
    public async Task Removal_refuses_anything_outside_the_trees_home()
    {
        using var _ = _fixture.Use();

        var removal = await _fixture.Trees.RemoveAsync(_fixture.Root);

        Assert.False(removal.Removed);
        Assert.Contains("not a session tree", removal.Message);
        Assert.True(Directory.Exists(_fixture.Root));
    }

    [Fact]
    public async Task The_trees_of_this_machine_are_listable_with_their_state()
    {
        using var _ = _fixture.Use();
        var opened = await _fixture.Trees.OpenAsync(_fixture.Root, "engine", "aurora");

        var listed = await _fixture.Trees.ListAsync();

        var entry = Assert.Single(listed);
        Assert.Equal(opened.Path, entry.Path);
        Assert.Equal("engine", entry.Repository);
        Assert.Equal("aurora", entry.Workspace);
        Assert.Equal(opened.Branch, entry.Branch);
    }

    [Fact]
    public async Task A_folder_inside_another_repository_refuses_rather_than_growing_worktrees_on_it()
    {
        using var _ = _fixture.Use();
        var inside = Path.Combine(_fixture.Root, "not-a-repo");
        Directory.CreateDirectory(inside);

        var error = await Assert.ThrowsAsync<DriverException>(() => _fixture.Trees.OpenAsync(inside, "x", "default"));

        Assert.Contains("INSIDE", error.Message);
        Assert.Equal(1, await _fixture.TreesAsync());
    }

    [Fact]
    public async Task A_rootless_folder_refuses_with_a_sentence_rather_than_a_git_stack()
    {
        using var _ = _fixture.Use();

        var error = await Assert.ThrowsAsync<DriverException>(() => _fixture.Trees.OpenAsync(_fixture.Folder("norepo"), "x", "default"));

        Assert.Contains("not a git repository", error.Message);
    }

    [Fact]
    public async Task An_unlock_git_fails_without_a_word_is_refused_with_what_git_did_and_the_open_is_undone()
    {
        using var _ = _fixture.Use();
        _fixture.Repo.UnlockFault = _ => GitDone.Not("");

        var error = await Assert.ThrowsAsync<DriverException>(() => _fixture.Trees.OpenAsync(_fixture.Root, "engine", "aurora"));

        Assert.Contains("git would not let go of the lock it opened it under", error.Message);
        Assert.Empty(await _fixture.BranchesAsync());
        Assert.Equal(1, await _fixture.TreesAsync());
    }

    [Fact]
    public async Task An_open_cancelled_once_its_tree_is_added_leaves_nothing_and_its_lock_said_who()
    {
        using var _ = _fixture.Use();
        using var closing = new CancellationTokenSource();
        string? lockSaid = null;
        _fixture.Repo.Unlocking = (path, reason) =>
        {
            if (closing.IsCancellationRequested || !Path.GetFileName(path).StartsWith("s-", StringComparison.Ordinal)) return;
            lockSaid = reason;
            closing.Cancel();
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _fixture.Trees.OpenAsync(_fixture.Root, "engine", "aurora", closing.Token));

        Assert.Equal($"Daoris is opening this tree (process {Environment.ProcessId})", lockSaid);
        Assert.Empty(await _fixture.BranchesAsync());
        Assert.Equal(1, await _fixture.TreesAsync());
        var folder = DaorisHome.Entry(_fixture.Home, HomeEntry.Trees, "aurora", "engine");
        Assert.True(!Directory.Exists(folder) || Directory.GetFileSystemEntries(folder).Length == 0, "a folder is left under the trees");
    }
}
