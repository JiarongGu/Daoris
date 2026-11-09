using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A structured capture drains both of the harness's pipes to their ends, whatever a reading's keeping throws.</summary>
public sealed class HarnessCaptureDrainTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-capture-drain-" + Guid.NewGuid().ToString("N")[..8]);

    public HarnessCaptureDrainTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class Refusing : IReadingSink
    {
        public bool Watched => false;

        public void Read(WireReading reading)
        {
            if (reading is WireReading.Said) throw new IOException("the record refused the event");
        }
    }

    /// <summary>A stderr that answers its end only after a moment, as a harness still writing does.</summary>
    private sealed class Slow : StringReader
    {
        public Slow() : base("") { }

        public bool Ended { get; private set; }

        public override async ValueTask<string?> ReadLineAsync(CancellationToken ct)
        {
            await Task.Delay(300, ct);
            Ended = true;
            return null;
        }
    }

    private static string Said(string text) => JsonSerializer.Serialize(new
    {
        type = "assistant",
        message = new { id = "m-" + text, role = "assistant", content = new object[] { new { type = "text", text } } },
    });

    [Fact]
    public async Task A_reading_the_sink_refuses_leaves_stdout_drained_and_stderr_ended_before_the_failure_is_thrown()
    {
        var stdout = new StringReader(string.Join('\n', Said("one"), Said("two"), Said("three")));
        var stderr = new Slow();

        await Assert.ThrowsAsync<IOException>(() => HarnessCapture.CaptureStructuredAsync(
            stdout, stderr, Path.Combine(_root, "s1.log"), new Refusing(), new ClaudeStreamJson(), prompt: null, CancellationToken.None));

        Assert.Null(stdout.ReadLine());
        Assert.True(stderr.Ended);
    }

    [Fact]
    public async Task An_opening_the_sink_refuses_leaves_both_pipes_drained_before_the_failure_is_thrown()
    {
        var stdout = new StringReader(string.Join('\n', Said("one"), Said("two")));
        var stderr = new Slow();

        await Assert.ThrowsAsync<IOException>(() => HarnessCapture.CaptureStructuredAsync(
            stdout, stderr, Path.Combine(_root, "s2.log"), new Refusing(), new ClaudeStreamJson(), prompt: "go", CancellationToken.None));

        Assert.Null(stdout.ReadLine());
        Assert.True(stderr.Ended);
    }

    [Fact]
    public async Task A_preamble_the_sink_refuses_leaves_both_pipes_drained_before_the_failure_is_thrown()
    {
        var stdout = new StringReader("one\ntwo");
        var stderr = new Slow();

        await Assert.ThrowsAsync<IOException>(() => HarnessCapture.CaptureAsync(
            stdout, stderr, Path.Combine(_root, "p1.log"), new RefusingLines(), CancellationToken.None, preamble: "set up"));

        Assert.Null(stdout.ReadLine());
        Assert.True(stderr.Ended);
    }

    private sealed class RefusingLines : IReadingSink
    {
        public bool Watched => false;

        public void Read(WireReading reading)
        {
            if (reading is WireReading.Shown) throw new IOException("the console refused the line");
        }
    }

    [Fact]
    public async Task A_line_the_sink_refuses_leaves_the_pipe_read_to_its_end_before_the_failure_is_thrown()
    {
        var stderr = new StringReader("one\ntwo\nthree");
        var told = new List<string>();

        await Assert.ThrowsAsync<IOException>(() => HarnessCapture.PumpAsync(
            stderr, TextWriter.Null, new RefusingLines(), CancellationToken.None, also: told.Add));

        Assert.Null(stderr.ReadLine());
        Assert.Equal(["one", "two", "three"], told);
    }
}
