using System.Text;
using Daoris.Ports.Processes;
using Xunit;

namespace Daoris.Testing;

/// <summary>
/// The cases every <see cref="IProcessHost"/> and its <see cref="IProcessLiveness"/> must pass, written once against the ports: a
/// subclass hands three programs, one that writes a text, one that exits with a code and one that runs until it is stopped, which
/// the scripted fake answers from its rows and the machine runs.
/// </summary>
public abstract class ProcessHostContract
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    protected abstract IProcessHost Host { get; }

    protected abstract IProcessLiveness Liveness { get; }

    /// <summary>A program that writes <paramref name="text"/> and exits 0.</summary>
    protected abstract ProcessSpec Says(string text);

    /// <summary>A program that exits with <paramref name="code"/>.</summary>
    protected abstract ProcessSpec Exits(int code);

    /// <summary>A program that runs until it is stopped.</summary>
    protected abstract ProcessSpec Waits();

    private static async Task<string> ReadAsync(IChildRun run)
    {
        using var bound = new CancellationTokenSource(Patience);
        var text = new StringBuilder();
        await foreach (var piece in run.Output(bound.Token)) text.Append(piece.Text);
        return text.ToString();
    }

    [Fact]
    public async Task A_run_answers_what_it_wrote_and_its_exit()
    {
        await using var run = await Host.StartAsync(Says("contract-hello"), CancellationToken.None);

        var text = await ReadAsync(run);
        var end = await run.EndAsync(CancellationToken.None).AsTask().WaitAsync(Patience);

        Assert.Contains("contract-hello", text);
        Assert.Equal(new ProcessEnd(0, Stopped: false), end);
    }

    [Fact]
    public async Task A_run_ends_with_its_programs_exit_code()
    {
        await using var run = await Host.StartAsync(Exits(3), CancellationToken.None);

        var end = await run.EndAsync(CancellationToken.None).AsTask().WaitAsync(Patience);

        Assert.Equal(3, end.Exit);
    }

    [Fact]
    public async Task A_stopped_run_ends_stopped_and_runs_no_longer()
    {
        await using var run = await Host.StartAsync(Waits(), CancellationToken.None);
        Assert.True(Liveness.Runs(run.Id, run.Created));

        await run.StopAsync(CancellationToken.None);
        var end = await run.EndAsync(CancellationToken.None).AsTask().WaitAsync(Patience);

        Assert.True(end.Stopped);
        Assert.False(Liveness.Runs(run.Id, run.Created));
    }

    [Fact]
    public async Task A_stop_of_a_run_that_ends_answers_no_reason()
    {
        await using var run = await Host.StartAsync(Waits(), CancellationToken.None);

        var reason = await run.StopAsync(CancellationToken.None).AsTask().WaitAsync(Patience);

        Assert.Null(reason);
    }

    [Fact]
    public async Task A_run_asked_for_raw_output_answers_its_bytes_and_no_text()
    {
        await using var run = await Host.StartAsync(Says("contract-raw") with { RawOutput = true }, CancellationToken.None);

        using var bound = new CancellationTokenSource(Patience);
        var bytes = new MemoryStream();
        await foreach (var piece in run.Bytes(bound.Token)) bytes.Write(piece.Span);
        var text = await ReadAsync(run);
        await run.EndAsync(CancellationToken.None).AsTask().WaitAsync(Patience);

        Assert.Contains("contract-raw", Encoding.UTF8.GetString(bytes.ToArray()));
        Assert.DoesNotContain("contract-raw", text);
    }

    [Fact]
    public async Task A_run_whose_input_is_closed_ends_as_it_would()
    {
        await using var run = await Host.StartAsync(Says("contract-input") with { Input = true }, CancellationToken.None);

        await run.CloseInputAsync();
        var end = await run.EndAsync(CancellationToken.None).AsTask().WaitAsync(Patience);

        Assert.Equal(0, end.Exit);
    }

    [Fact]
    public async Task A_process_of_the_id_made_at_another_time_is_not_the_run()
    {
        await using var run = await Host.StartAsync(Waits(), CancellationToken.None);

        Assert.False(Liveness.Runs(run.Id, run.Created.AddHours(1)));
    }

    [Fact]
    public async Task A_start_with_a_cancelled_token_starts_nothing()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Host.StartAsync(Says("never"), cancelled.Token));
    }
}
