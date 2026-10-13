using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Where the loop follows lines (WSSETUP5, D124 §3.1): a look follows the lines Daoris moved since each was last followed,
/// before it reads the registry, and a watch follows every line once as it starts, saying what must be fixed in a look's
/// report. Held over the stand-in ledger with a root that is a linked worktree, which is refused before git is asked, so
/// nothing here starts a process.
/// </summary>
public sealed class RegistrationFollowLoopTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-follow-loop-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(TimeSpan.FromSeconds(60));

    private string Tree => Path.Combine(_home, "trees", "engine-1");

    public RegistrationFollowLoopTests()
    {
        Directory.CreateDirectory(Tree);
        // A linked worktree marks itself: its `.git` is a file naming the main repository's `.git/worktrees/<name>`.
        File.WriteAllText(Path.Combine(Tree, ".git"), "gitdir: /checkouts/engine/.git/worktrees/engine-1\n");
        _ledger.Register("engine", Tree);
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private Daoris.Driver.Driver Driver() => new(
        _ledger.Client(), DriverConfig.Empty, AdapterSet.Built(), _home,
        harnesses: new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")));

    [Fact]
    public async Task A_look_follows_a_line_daoris_moved_once_and_says_what_must_be_fixed()
    {
        RegistryFollowing.Moved(_home, "engine", DateTimeOffset.UtcNow.AddSeconds(-1));

        var look = await Driver().TickAsync(_closing.Token).WaitAsync(Bound);
        var next = await Driver().TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.Contains(look.Events, line => line.StartsWith("registry  engine: `engine`'s root here is a linked worktree", StringComparison.Ordinal));
        Assert.DoesNotContain(next.Events, line => line.StartsWith("registry", StringComparison.Ordinal));
        Assert.Equal(RegistryOutcome.Worktree, RegistryFollowing.Read(_home)["engine"].Outcome);
    }

    [Fact]
    public async Task A_look_with_no_line_moved_follows_nothing()
    {
        var look = await Driver().TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.DoesNotContain(look.Events, line => line.StartsWith("registry", StringComparison.Ordinal));
        Assert.Empty(RegistryFollowing.Read(_home));
    }

    [Fact]
    public async Task A_watch_follows_every_line_once_as_it_starts_and_says_it_in_a_looks_report()
    {
        var config = Path.Combine(_home, "driver.json");
        File.WriteAllText(config, """{ "drivable": [], "pollSeconds": 1 }""");
        var watch = new DriverWatch(
            _ledger.Client(), config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")));
        var said = new List<string>();
        var looks = 0;
        var watching = watch.RunAsync(
            (report, _) =>
            {
                lock (said) said.AddRange(report.Events.Where(line => line.StartsWith("registry", StringComparison.Ordinal)));
                Interlocked.Increment(ref looks);
                return Task.CompletedTask;
            },
            onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => { lock (said) return said.Count > 0; }, () => $"{looks} look(s), nothing said of the registry", Bound);
            var at = Volatile.Read(ref looks);
            watch.Nudge();
            await Poll.Until(() => Volatile.Read(ref looks) >= at + 2, () => $"{looks} look(s)", Bound);

            lock (said) Assert.Single(said);
            Assert.StartsWith("registry  engine: `engine`'s root here is a linked worktree", said[0], StringComparison.Ordinal);
        }
        finally
        {
            await _closing.CancelAsync();
            try { await watching.WaitAsync(Bound); } catch (OperationCanceledException) { }
        }
    }
}
