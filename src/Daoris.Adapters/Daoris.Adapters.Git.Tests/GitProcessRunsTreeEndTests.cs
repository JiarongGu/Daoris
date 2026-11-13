using Daoris.Adapters.Machine;
using Daoris.Ports.Git;

namespace Daoris.Adapters.Git.Tests;

/// <summary>
/// A streamed git read and a byte read whose caller cancels end git and what git started: the adapter never leaves a helper running
/// unowned. The helper is a textconv program (<c>diff.hang.textconv</c> on <c>hang.txt</c>, through git's own shell): it writes
/// <c>begun</c>, waits three seconds and writes <c>marker</c>, and a tree ended by the cancellation never writes the marker.
/// </summary>
public sealed class GitProcessRunsTreeEndTests : IDisposable
{
    private const string Helper = "diff.hang.textconv=echo x > begun && sleep 3 && echo x > marker && cat";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "tree-end", Guid.NewGuid().ToString("N")[..8]);

    private readonly GitProcessRuns _git = new(new MachineProcessHost(new StartPlaces(4, 3), (_, _) => null), new PlainGitPolicy());

    public GitProcessRunsTreeEndTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A straggling handle; the folder is this run's own.
        }
        catch (UnauthorizedAccessException)
        {
            // Likewise.
        }
    }

    [Fact]
    public async Task A_streamed_read_whose_caller_cancels_ends_git_and_what_it_started()
    {
        await InitAsync();
        using var cancel = new CancellationTokenSource();

        var read = _git.ReadAsync(Ask("-c", Helper, "diff", "HEAD~1", "HEAD", "--", "hang.txt"), _ => true, cancel.Token);

        await EndedAsync(read, cancel);
    }

    [Fact]
    public async Task A_byte_read_whose_caller_cancels_ends_git_and_what_it_started()
    {
        await InitAsync();
        using var cancel = new CancellationTokenSource();

        var read = _git.ReadBytesAsync(Ask("-c", Helper, "cat-file", "--batch", "--textconv") with { Input = "HEAD:hang.txt hang.txt\n" }, 1024, cancel.Token);

        await EndedAsync(read, cancel);
    }

    private GitAsk Ask(params string[] arguments) => new(_root, arguments, GitWait.Read, Writes: false);

    private async Task InitAsync()
    {
        await OkAsync("init", "-q");
        File.WriteAllText(Path.Combine(_root, ".gitattributes"), "hang.txt diff=hang\n");
        foreach (var text in new[] { "one\n", "two\n" })
        {
            File.WriteAllText(Path.Combine(_root, "hang.txt"), text);
            await OkAsync("add", "hang.txt", ".gitattributes");
            await OkAsync("-c", "user.name=Fixture", "-c", "user.email=fixture@example.test", "commit", "-q", "-m", text.Trim());
        }
    }

    private async Task OkAsync(params string[] arguments)
    {
        var answer = await _git.RunAsync(Ask(arguments) with { Writes = true }, CancellationToken.None).WaitAsync(Patience);
        Assert.True(answer.Exit == 0, answer.Error);
    }

    private async Task EndedAsync(Task read, CancellationTokenSource cancel)
    {
        Assert.True(SpinWait.SpinUntil(() => File.Exists(Path.Combine(_root, "begun")), Patience), "git's helper never began");
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.WaitAsync(Patience));
        await Task.Delay(TimeSpan.FromSeconds(5));
        Assert.False(File.Exists(Path.Combine(_root, "marker")), "what git started outlived its caller's cancellation");
    }
}
