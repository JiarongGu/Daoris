using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A look asks git for a checkout's line once (UIFREEZE1): the sweep's list asked it three times a repository, up to three git
/// processes each, and a frozen window's stack held seventeen such asks waiting on .NET's one process-start lock.
/// </summary>
/// <remarks>
/// The fast half: git's guess is a function handed in, so what is counted is how often git would have been asked. The real
/// clean-up's count is <c>SweepLineProcessTests</c>, in the Process half.
/// </remarks>
public sealed class TreeLineLookTests : IDisposable
{
    private readonly string _home = Path.Combine(RepoRoot(), "_fixtures", "line-look", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<string> _asked = [];

    public TreeLineLookTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private TreeContext Context() => new(_home, new LandingPlugins(_home))
    {
        CheckoutLine = (root, _) =>
        {
            lock (_asked) _asked.Add(root);
            return Task.FromResult<string?>("main");
        },
    };

    [Fact]
    public async Task Within_one_look_each_checkout_is_asked_once()
    {
        var context = Context();

        using (context.OneLook())
        {
            for (var each = 0; each < 3; each++)
            {
                Assert.Equal(new Line("main", LineSource.Checkout), await context.LineAsync("one", "engine", "aurora", CancellationToken.None));
                Assert.Equal(new Line("main", LineSource.Checkout), await context.LineAsync("two", "tools", "aurora", CancellationToken.None));
            }
        }

        Assert.Equal(["one", "two"], _asked);
    }

    [Fact]
    public async Task Outside_a_look_and_in_the_next_one_git_is_asked_again()
    {
        var context = Context();

        await context.LineAsync("one", "engine", "aurora", CancellationToken.None);
        await context.LineAsync("one", "engine", "aurora", CancellationToken.None);
        using (context.OneLook()) await context.LineAsync("one", "engine", "aurora", CancellationToken.None);
        using (context.OneLook()) await context.LineAsync("one", "engine", "aurora", CancellationToken.None);

        Assert.Equal(4, _asked.Count);
    }

    [Fact]
    public async Task A_look_inside_a_look_shares_its_answers()
    {
        var context = Context();

        using (context.OneLook())
        {
            await context.LineAsync("one", "engine", "aurora", CancellationToken.None);
            using (context.OneLook()) await context.LineAsync("one", "engine", "aurora", CancellationToken.None);
            await context.LineAsync("one", "engine", "aurora", CancellationToken.None);
        }

        Assert.Single(_asked);
    }

    [Fact]
    public async Task The_persons_line_is_still_read_at_every_call_within_a_look()
    {
        var context = Context();

        using (context.OneLook())
        {
            Assert.Equal(LineSource.Checkout, (await context.LineAsync("one", "engine", "aurora", CancellationToken.None)).Source);
            DriverConfig.Empty.WithLine("engine", "trunk").Save(DaorisHome.Entry(_home, HomeEntry.Driver));
            Assert.Equal(new Line("trunk", LineSource.Repository), await context.LineAsync("one", "engine", "aurora", CancellationToken.None));
        }

        Assert.Single(_asked);
    }

    [Fact]
    public async Task A_look_running_beside_another_does_not_share_its_answers()
    {
        var context = Context();
        using var first = new SemaphoreSlim(0);
        using var second = new SemaphoreSlim(0);

        async Task LookAsync(SemaphoreSlim mine, SemaphoreSlim theirs)
        {
            await Task.Yield();
            using (context.OneLook())
            {
                await context.LineAsync("one", "engine", "aurora", CancellationToken.None);
                theirs.Release();
                await mine.WaitAsync(TimeSpan.FromSeconds(10));
                await context.LineAsync("one", "engine", "aurora", CancellationToken.None);
            }
        }

        await Task.WhenAll(LookAsync(first, second), LookAsync(second, first)).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(2, _asked.Count);
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
