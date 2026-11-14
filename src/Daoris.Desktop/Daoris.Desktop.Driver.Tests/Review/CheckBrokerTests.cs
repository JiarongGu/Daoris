using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The check broker: asks kept under the home, queued by who made them, one run at a time through the heavy run, each ending in a
/// verdict with the log's tail.
/// </summary>
public sealed class CheckBrokerTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-12T10:00:00Z");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "check-broker-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _tree;

    public CheckBrokerTests()
    {
        _tree = Path.Combine(_home, "tree");
        Directory.CreateDirectory(_tree);
    }

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static CheckAsk Ask(string id, int minute = 0, string session = "s1", string? gate = "tests") =>
        new(id, session, gate, null, At.AddMinutes(minute));

    [Fact]
    public void The_oldest_check_goes_first_and_nothing_starts_while_one_runs()
    {
        var asks = new[] { Ask("a1", minute: 0), Ask("a2", minute: 5), Ask("a3", minute: 1) };

        Assert.Equal("a1", CheckQueue.Next(asks, _ => null)?.Id);
        Assert.Null(CheckQueue.Next(asks, id => id == "a1" ? new CheckState("a1", CheckStates.Running, At) : null));
        Assert.Equal(0, CheckQueue.Ahead(asks[0], asks, _ => null));
        Assert.Equal(2, CheckQueue.Ahead(asks[1], asks, _ => null));
        Assert.Equal(1, CheckQueue.Ahead(asks[2], asks, id => id == "a1" ? new CheckState("a1", CheckStates.Running, At) : null));
    }

    [Fact]
    public void A_check_that_finished_or_was_handed_no_longer_waits()
    {
        var asks = new[] { Ask("a1"), Ask("a2") };

        Assert.Equal("a2", CheckQueue.Next(asks, id => id == "a1" ? new CheckState("a1", CheckStates.Reached, At) : null)?.Id);
    }

    [Fact]
    public void The_files_keep_an_ask_its_state_and_its_log_under_the_home()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1", gate: null) with { Command = ["dotnet", "test"] });
        files.WriteState(new CheckState("a1", CheckStates.Failed, At) { Exit = 1, Tail = "boom", Tip = "abc123" });

        var ask = Assert.Single(files.Asks());
        Assert.Equal(("a1", "s1", null), (ask.Id, ask.Session, ask.Gate));
        Assert.Equal(["dotnet", "test"], ask.Command);
        var state = files.ReadState("a1");
        Assert.Equal((CheckStates.Failed, 1, "boom", "abc123"), (state!.State, state.Exit, state.Tail, state.Tip));
        Assert.Null(files.ReadState("zz"));
    }

    [Fact]
    public async Task The_broker_runs_the_next_ask_in_the_askers_tree_and_keeps_a_pass_with_its_tail()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        HeavyRunAsk? ran = null;
        string? where = null;
        var broker = new CheckBroker(_home, _ => _tree, async (ask, world, output, ct) =>
        {
            ran = ask;
            where = world.Directory;
            Assert.Equal(CheckStates.Running, files.ReadState("a1")!.State);
            await output.WriteLineAsync("all 12 passed");
            return 0;
        });

        var state = await broker.RunNextAsync(CancellationToken.None);

        Assert.Equal(("tests", _tree), (ran!.Gate, where));
        Assert.Equal((CheckStates.Reached, 0), (state!.State, state.Exit));
        Assert.Contains("all 12 passed", state.Tail);
        Assert.Equal(CheckStates.Reached, files.ReadState("a1")!.State);
        Assert.Null(await broker.RunNextAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_red_check_is_failed_and_keeps_only_the_log_tail()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        var broker = new CheckBroker(_home, _ => _tree, async (_, _, output, _) =>
        {
            for (var line = 0; line < 400; line++) await output.WriteLineAsync($"line {line}");
            return 1;
        });

        var state = await broker.RunNextAsync(CancellationToken.None);

        Assert.Equal((CheckStates.Failed, 1), (state!.State, state.Exit));
        Assert.Contains("line 399", state.Tail);
        Assert.DoesNotContain("line 0\n", state.Tail);
        Assert.True(state.Tail!.Length <= CheckBroker.TailBound);
    }

    [Fact]
    public async Task An_asker_with_no_tree_is_an_error_and_a_stopped_run_is_stopped()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        var none = new CheckBroker(_home, _ => null, (_, _, _, _) => Task.FromResult(0));
        var missing = await none.RunNextAsync(CancellationToken.None);
        Assert.Equal(CheckStates.Errored, missing!.State);
        Assert.Contains("no tree", missing.Error);
        Assert.Null(missing.Exit);
        Assert.Equal("a1", CheckQueue.Next(files.Asks(), files.ReadState, missing.At + CheckQueue.RetryAfter)?.Id);

        files.WriteAsk(Ask("a2", minute: 1));
        using var cancel = new CancellationTokenSource();
        var broker = new CheckBroker(_home, _ => _tree, (_, _, _, token) =>
        {
            cancel.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(0);
        });
        Assert.Equal(CheckStates.Stopped, (await broker.RunNextAsync(cancel.Token))!.State);
        Assert.Equal(CheckStates.Stopped, files.ReadState("a2")!.State);
    }
}
