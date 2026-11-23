using System.Diagnostics;
using Daoris.Ports.Processes;

namespace Daoris.Adapters.Machine.Tests;

/// <summary>
/// A run whose specification bounds the output waiting unread: the process, not the application, waits when its reader is
/// paused, while its error stream, its exit and its stop go on as they do.
/// </summary>
public sealed class MachineOutputAheadTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private const int Lines = 60000;

    private readonly string _folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "ahead-" + Guid.NewGuid().ToString("N"))).FullName;

    private string Marker => Path.Combine(_folder, "marker.txt");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A process of the test still holds it a moment: the build's folder is ignored and cleaned with it.
        }
    }

    // Writes Lines lines, then a marker file: the marker is there only once every line has left the process.
    private ProcessSpec Floods(int? ahead) =>
        new("cmd.exe", ["/d", "/c", $"(for /l %i in (1,1,{Lines}) do @echo line %i) & echo x>marker.txt"], "Git", AdmissionClasses.Background)
        {
            WorkingFolder = _folder,
            OutputAhead = ahead,
        };

    private static MachineProcessHost Host() => new(new StartPlaces(4, 3), (_, _) => null);

    private static async Task<bool> AppearsAsync(string path, TimeSpan within)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < within)
        {
            if (File.Exists(path)) return true;
            await Task.Delay(100);
        }

        return File.Exists(path);
    }

    [Fact]
    public async Task A_run_with_no_bound_writes_all_it_has_while_nothing_reads_it()
    {
        // The control: it is why the bounded cases below show something.
        await using var run = await Host().StartAsync(Floods(null), CancellationToken.None);

        Assert.True(await AppearsAsync(Marker, Patience));
    }

    [Fact]
    public async Task A_paused_reader_holds_the_process_back_and_a_resumed_one_gets_every_line_in_order_and_the_exit()
    {
        await using var run = await Host().StartAsync(Floods(ahead: 2), CancellationToken.None);

        // Nothing reads: the process fills the pipe and the two pieces waiting, and waits.
        Assert.False(await AppearsAsync(Marker, TimeSpan.FromSeconds(4)));

        var text = new System.Text.StringBuilder();
        using var bound = new CancellationTokenSource(Patience);
        await foreach (var piece in run.Output(bound.Token)) text.Append(piece.Text);
        var end = await run.EndAsync(CancellationToken.None).AsTask().WaitAsync(Patience);

        var lines = text.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(Lines, lines.Length);
        Assert.Equal(Enumerable.Range(1, Lines).Select(i => $"line {i}"), lines);
        Assert.True(File.Exists(Marker));
        Assert.Equal(new ProcessEnd(0, Stopped: false), end);
    }

    [Fact]
    public async Task A_stop_ends_a_run_held_back_and_its_output_ends()
    {
        await using var run = await Host().StartAsync(Floods(ahead: 2), CancellationToken.None);
        Assert.False(await AppearsAsync(Marker, TimeSpan.FromSeconds(2)));

        var reason = await run.StopAsync(CancellationToken.None).AsTask().WaitAsync(Patience);
        var end = await run.EndAsync(CancellationToken.None).AsTask().WaitAsync(Patience);
        using var bound = new CancellationTokenSource(Patience);
        await foreach (var _ in run.Output(bound.Token)) { }

        Assert.Null(reason);
        Assert.True(end.Stopped);
        Assert.False(File.Exists(Marker));
    }

    [Fact]
    public async Task A_slow_reader_gets_both_streams_whole_while_the_output_is_held_back()
    {
        var spec = new ProcessSpec(
            "cmd.exe", ["/d", "/q", "/c", "for /l %i in (1,1,6000) do (@echo out %i&>&2 echo err %i)"], "Git", AdmissionClasses.Background)
        {
            OutputAhead = 1,
        };
        await using var run = await Host().StartAsync(spec, CancellationToken.None);

        var output = new System.Text.StringBuilder();
        var error = new System.Text.StringBuilder();
        var count = 0;
        using var bound = new CancellationTokenSource(Patience);
        await foreach (var piece in run.Output(bound.Token))
        {
            (piece.Stream == ProcessStream.Output ? output : error).Append(piece.Text);
            if (++count % 50 == 0) await Task.Delay(1);
        }

        var end = await run.EndAsync(CancellationToken.None).AsTask().WaitAsync(Patience);
        Assert.Equal(0, end.Exit);
        Assert.Equal(6000, output.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(6000, error.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task A_run_that_ends_while_its_reader_is_paused_loses_no_output_to_its_end()
    {
        // The first piece takes the only room; the tail is written and the process exits while nothing reads.
        var spec = new ProcessSpec("cmd.exe", ["/d", "/q", "/c", "echo first& ping -n 3 127.0.0.1 >nul& echo tail"], "Git", AdmissionClasses.Background)
        {
            OutputAhead = 1,
        };
        await using var run = await Host().StartAsync(spec, CancellationToken.None);

        var clock = Stopwatch.StartNew();
        var end = await run.EndAsync(CancellationToken.None).AsTask().WaitAsync(Patience);
        var ended = clock.Elapsed;
        var text = new System.Text.StringBuilder();
        using var bound = new CancellationTokenSource(Patience);
        await foreach (var piece in run.Output(bound.Token)) text.Append(piece.Text);

        Assert.Equal(0, end.Exit);
        Assert.Contains("tail", text.ToString());
        Assert.True(ended < TimeSpan.FromSeconds(2) + TimeSpan.FromSeconds(3), $"the end waited {ended}");
    }
}
