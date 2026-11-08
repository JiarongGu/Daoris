using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The executor's loop on a checkout whose git folder is bare files, so no process starts: an attempt it cannot run is answered
/// <i>unknown</i> and the loop goes on, and a stop between taking the slots and leaving the queue is not run as well.
/// </summary>
public sealed class HeavyExecutorFaultTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "heavy-fault", Guid.NewGuid().ToString("N")[..8]);
    private HeavyExecutor? _executor;
    private string Home => Path.Combine(_folder, "home");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(Home);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_executor is not null) await _executor.DisposeAsync();
        GitSeed.Discard(_folder);
    }

    private string Checkout(string name)
    {
        var checkout = Path.Combine(_folder, name);
        Directory.CreateDirectory(Path.Combine(checkout, ".git"));
        return checkout;
    }

    private HeavyExecutor Executor() => _executor = new HeavyExecutor(Home, new VerifyWorld { Poll = TimeSpan.FromMilliseconds(50) });

    private async Task<VerifyResult> SettledAsync(string run, string invocation)
    {
        var path = VerifyFiles.ResultOf(Home, run, invocation);
        await Poll.Until(() => File.Exists(path), () => $"no result at {path}");
        return VerifyResult.Read(path) ?? throw new InvalidOperationException($"the result at {path} does not read");
    }

    [Fact]
    public async Task A_bound_the_clock_cannot_count_answers_unknown_and_the_next_attempt_still_runs()
    {
        var executor = Executor();
        var checkout = Checkout("engine");
        await executor.SendAsync(new VerifySend("r1", "i1", "engine", checkout, "c1", null) { Bound = TimeSpan.FromDays(60) });

        var first = await SettledAsync("r1", "i1");
        Assert.Equal(VerifyVerdicts.Unknown, first.Verdict);

        // The loop is still there: a later attempt is taken and answered, here for a checkout git's files do not name.
        await executor.SendAsync(new VerifySend("r2", "i2", "game", Path.Combine(_folder, "elsewhere"), "c2", null));
        Assert.Equal(VerifyVerdicts.Unknown, (await SettledAsync("r2", "i2")).Verdict);
        Assert.False(executor.Holds("i1"));
    }

    [Fact]
    public async Task A_callback_that_throws_when_a_result_is_told_does_not_end_the_loop()
    {
        var told = 0;
        var executor = _executor = new HeavyExecutor(Home, new VerifyWorld
        {
            Poll = TimeSpan.FromMilliseconds(50),
            Answered = (_, _) => { if (Interlocked.Increment(ref told) == 1) throw new InvalidOperationException("the runtime's ask failed"); },
        });
        var checkout = Checkout("engine");
        await executor.SendAsync(new VerifySend("r1", "i1", "engine", checkout, "c1", null) { Bound = TimeSpan.FromDays(60) });
        await SettledAsync("r1", "i1");

        await executor.SendAsync(new VerifySend("r2", "i2", "engine", checkout, "c2", null) { Bound = TimeSpan.FromDays(60) });
        Assert.Equal(VerifyVerdicts.Unknown, (await SettledAsync("r2", "i2")).Verdict);
        Assert.True(executor.Answered("i2"));
    }

    [Fact]
    public async Task A_stop_between_the_slots_and_the_queue_is_not_run_as_well()
    {
        var executor = Executor();
        var checkout = Checkout("engine");
        var armed = true;
        executor.SlotsTaken = () =>
        {
            if (!armed) return;
            armed = false;
            executor.Stop("i1");
        };

        await executor.SendAsync(new VerifySend("r1", "i1", "engine", checkout, "c1", null));
        var stopped = await SettledAsync("r1", "i1");
        Assert.Equal(VerifyResult.Stopped, stopped.Code);

        // The slots were let go: the next attempt takes them and is answered, and the stopped one's answer stands.
        await executor.SendAsync(new VerifySend("r2", "i2", "engine", checkout, "c2", null) { Bound = TimeSpan.FromDays(60) });
        await SettledAsync("r2", "i2");
        Assert.Equal(VerifyResult.Stopped, (await SettledAsync("r1", "i1")).Code);
    }
}
