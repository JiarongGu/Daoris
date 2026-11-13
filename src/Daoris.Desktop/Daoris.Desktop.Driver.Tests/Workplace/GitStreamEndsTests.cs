using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A streamed git read whose caller cancels ends git and what it started: the review's reads
/// (<see cref="WorkingTree.ReadGitAsync"/>) and a blob's preview (<see cref="WorkingTree.GitBytesAsync"/>) end git with the
/// cancellation, never leaving it running unowned. <see cref="GitBoundsTests"/> holds the runner's half with a stand-in.
/// </summary>
/// <remarks>
/// The hang is a textconv helper (<c>diff.hang.textconv</c>, set on <c>hang.txt</c> by the repository's attributes), which git
/// runs through its shell for a diff of the file and for <c>cat-file --textconv</c>: it writes <c>begun</c>, waits three seconds and
/// writes <c>marker</c>, and a tree ended by the cancellation never writes the marker. An alias would stand in for no command the
/// scheduler lets run. Fixtures live under the repository's gitignored <c>_fixtures/</c>.
/// 🔴 Nothing here reaches a network.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitStreamEndsTests : IDisposable
{
    private const string Helper = "diff.hang.textconv=echo x > begun && sleep 3 && echo x > marker && cat";

    private static readonly string[] Diff = ["-c", Helper, "diff", "HEAD~1", "HEAD", "--", "hang.txt"];

    private static readonly string[] Blob = ["-c", Helper, "cat-file", "--batch", "--textconv"];

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

        var read = WorkingTree.ReadGitAsync(_root, Diff, _ => true, cancel.Token);
        await EndedAsync(read, cancel);
    }

    [Fact]
    public async Task A_blob_read_whose_caller_cancels_ends_git_and_what_it_started()
    {
        await InitAsync();
        using var cancel = new CancellationTokenSource();

        var read = WorkingTree.GitBytesAsync(_root, Blob, 1024, "HEAD:hang.txt hang.txt\n", cancel.Token);
        await EndedAsync(read, cancel);
    }

    private async Task InitAsync()
    {
        await GitOkAsync("init", "-q");
        File.WriteAllText(Path.Combine(_root, ".git", "info", "attributes"), "hang.txt diff=hang\n");
        foreach (var text in new[] { "one\n", "two\n" })
        {
            File.WriteAllText(Path.Combine(_root, "hang.txt"), text);
            await GitOkAsync("add", "hang.txt");
            await GitOkAsync("-c", "user.name=Fixture", "-c", "user.email=fixture@example.test", "commit", "-q", "-m", text.Trim());
        }
    }

    private async Task GitOkAsync(params string[] arguments)
    {
        var (code, _, error) = await WorkingTree.GitAsync(_root, arguments, CancellationToken.None);
        Assert.True(code == 0, error);
    }

    private async Task EndedAsync(Task read, CancellationTokenSource cancel)
    {
        Assert.True(SpinWait.SpinUntil(() => File.Exists(Path.Combine(_root, "begun")), Poll.Patience), "git's helper never began; the read " + (read.IsCompleted ? "ended: " + (read is Task<(byte[] Bytes, int Count)?> bytes ? bytes.Result?.Count.ToString() ?? "null" : read.Status.ToString()) : "was still running"));
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.WaitAsync(Poll.Patience));
        await Task.Delay(TimeSpan.FromSeconds(5));
        Assert.False(File.Exists(Path.Combine(_root, "marker")), "what git started outlived its caller's cancellation");
    }

    private static string RepoRoot() => GitSeed.RepoRoot();
}
