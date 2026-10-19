using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A streamed git read whose caller cancels ends git and what it started (PARALLEL1e, D181 point 4): the review's reads
/// (<see cref="WorkingTree.ReadGitAsync"/>) and a blob's preview (<see cref="WorkingTree.GitBytesAsync"/>) threw the
/// cancellation and left git running unowned. <see cref="GitBoundsTests"/> holds the runner's half with a stand-in.
/// </summary>
/// <remarks>
/// git runs an alias through its shell, which writes <c>begun</c>, waits three seconds and writes <c>marker</c>: a tree
/// ended by the cancellation never writes the marker. Fixtures live under the repository's gitignored <c>_fixtures/</c>.
/// 🔴 Nothing here reaches a network.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitStreamEndsTests : IDisposable
{
    private static readonly string[] Hang = ["-c", "alias.hang=!echo x > begun && sleep 3 && echo x > marker", "hang"];

    private readonly string _root;

    public GitStreamEndsTests()
    {
        _root = Path.Combine(RepoRoot(), "_fixtures", "git-stream-ends", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            GitSeed.Remove(_root);
        }
        catch (IOException) { /* a straggling handle; the next run's folder is its own */ }
        catch (UnauthorizedAccessException) { /* likewise */ }
    }

    [Fact]
    public async Task A_review_read_whose_caller_cancels_ends_git_and_what_it_started()
    {
        await InitAsync();
        using var cancel = new CancellationTokenSource();

        var read = WorkingTree.ReadGitAsync(_root, Hang, _ => true, cancel.Token);
        await EndedAsync(read, cancel);
    }

    [Fact]
    public async Task A_blob_read_whose_caller_cancels_ends_git_and_what_it_started()
    {
        await InitAsync();
        using var cancel = new CancellationTokenSource();

        var read = WorkingTree.GitBytesAsync(_root, Hang, 1024, cancel.Token);
        await EndedAsync(read, cancel);
    }

    private async Task InitAsync()
    {
        var (code, _, error) = await WorkingTree.GitAsync(_root, ["init", "-q"], CancellationToken.None);
        Assert.True(code == 0, error);
    }

    private async Task EndedAsync(Task read, CancellationTokenSource cancel)
    {
        Assert.True(SpinWait.SpinUntil(() => File.Exists(Path.Combine(_root, "begun")), Poll.Patience), "git's alias never began");
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.WaitAsync(Poll.Patience));
        await Task.Delay(TimeSpan.FromSeconds(5));
        Assert.False(File.Exists(Path.Combine(_root, "marker")), "what git started outlived its caller's cancellation");
    }

    // A worktree's own `.git` is a file: the scratch stays in the checkout the run is in (SCRATCHLEAK1).
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !Directory.Exists(Path.Combine(directory.FullName, ".git"))
               && !File.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
