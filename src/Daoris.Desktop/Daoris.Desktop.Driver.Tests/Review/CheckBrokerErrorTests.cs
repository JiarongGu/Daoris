using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check Daoris could not run is its own error, held on the check and never said to the session; a log is opened to share with its reader;
/// and one broker holds the machine's checks, so two Daoris processes never run one ask on one log.
/// </summary>
public sealed class CheckBrokerErrorTests : IDisposable
{
    private static Task<string?> Taken(string _, CancellationToken ct) => Task.FromResult<string?>("Taken");
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-12T10:00:00Z");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "check-error-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _tree;

    public CheckBrokerErrorTests()
    {
        _tree = Path.Combine(_home, "tree");
        Directory.CreateDirectory(_tree);
    }

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static CheckAsk Ask(string id, int minute = 0) => new(id, "s1", "tests", null, CheckAsk.ByAgent, At.AddMinutes(minute));

    private static Task<string?> Said(List<string> words, string session, string text, string key, CancellationToken ct)
    {
        words.Add(text);
        return Task.FromResult<string?>("handed");
    }

    [Fact]
    public async Task A_check_whose_log_cannot_be_opened_is_daoriss_error_and_says_nothing_to_the_session()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        Directory.CreateDirectory(files.LogOf("a1"));
        var broker = new CheckBroker(_home, _ => _tree, (_, _, _, _) => Task.FromResult(0));

        var state = await broker.RunNextAsync(CancellationToken.None);

        Assert.Equal(CheckStates.Errored, state!.State);
        Assert.False(string.IsNullOrEmpty(state.Error));
        Assert.Null(state.Tail);
        var words = new List<string>();
        Assert.Empty(await CheckHanding.HandAsync(files, (s, t, k, c) => Said(words, s, t, k, c), CancellationToken.None, Taken));
        Assert.Empty(words);
        Assert.Empty(CheckWaits.Open("s1", files.Asks(), files.ReadState).Where(ask => files.ReadState(ask.Id)?.Handed is not null));
    }

    [Fact]
    public async Task A_wrapper_that_could_not_run_is_errored_and_not_a_gate_failure()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        var broker = new CheckBroker(_home, _ => _tree, async (_, _, output, _) =>
        {
            await output.WriteLineAsync("run-heavy: no declared gate could be run");
            return 2;
        });

        var state = await broker.RunNextAsync(CancellationToken.None);

        Assert.Equal(CheckStates.Errored, state!.State);
        Assert.Contains("no declared gate", state.Error);
        Assert.Null(CheckQueue.Next(files.Asks(), files.ReadState));
        Assert.Equal("a1", CheckQueue.Next(files.Asks(), files.ReadState, state.At + CheckQueue.RetryAfter)?.Id);
    }

    [Fact]
    public async Task A_gate_that_ran_and_exited_one_is_said_to_the_session_as_today()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        var broker = new CheckBroker(_home, _ => _tree, async (_, _, output, _) =>
        {
            await output.WriteLineAsync("boom");
            return 1;
        });

        Assert.Equal(CheckStates.Failed, (await broker.RunNextAsync(CancellationToken.None))!.State);
        var words = new List<string>();
        Assert.Equal(["a1"], await CheckHanding.HandAsync(files, (s, t, k, c) => Said(words, s, t, k, c), CancellationToken.None, Taken));
        Assert.Contains("it failed (exit 1)", Assert.Single(words));
    }

    [Fact]
    public async Task A_run_that_could_not_start_is_daoriss_error_and_is_asked_again_once_its_delay_passes()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        var broker = new CheckBroker(_home, _ => _tree, (_, _, _, _) => throw new DriverException("no job could be made"));

        var state = await broker.RunNextAsync(CancellationToken.None);

        Assert.Equal(CheckStates.Errored, state!.State);
        Assert.Contains("no job could be made", state.Error);
        var asks = files.Asks();
        Assert.Null(CheckQueue.Next(asks, files.ReadState));
        Assert.Equal("a1", CheckQueue.Next(asks, files.ReadState, DateTimeOffset.UtcNow + CheckQueue.RetryAfter + TimeSpan.FromSeconds(1))?.Id);
    }

    [Fact]
    public async Task A_log_is_read_while_it_is_written_and_written_while_it_is_read()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        string? seen = null;
        var broker = new CheckBroker(_home, _ => _tree, async (_, _, output, _) =>
        {
            await output.WriteLineAsync("first");
            seen = files.ReadLog("a1");
            using var reader = files.OpenLogReader("a1");
            await output.WriteLineAsync("second");
            return 0;
        });

        var state = await broker.RunNextAsync(CancellationToken.None);

        Assert.Equal(CheckStates.Reached, state!.State);
        Assert.Contains("first", seen);
        Assert.Contains("second", state.Tail);
    }

    [Fact]
    public async Task A_second_broker_never_runs_the_ask_the_first_holds()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        files.WriteAsk(Ask("a2", minute: 1));
        CheckState? second = null;
        var other = new CheckBroker(_home, _ => _tree, (_, _, _, _) => Task.FromResult(0));
        var first = new CheckBroker(_home, _ => _tree, async (_, _, output, _) =>
        {
            await output.WriteLineAsync("running");
            second = await other.RunNextAsync(CancellationToken.None);
            return 0;
        });

        var state = await first.RunNextAsync(CancellationToken.None);

        Assert.Null(second);
        Assert.Equal(CheckStates.Reached, state!.State);
        Assert.Equal(CheckStates.Reached, files.ReadState("a1")!.State);
    }
}
