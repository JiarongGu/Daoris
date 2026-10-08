using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TRANSCRIPT1: a working session's transcript is on the disk as it is said, not only once its session ends. DEV3d's
/// rehearsal waited out its whole bound for a line its stub had already said, because the transcript held 0 bytes until
/// the session ended (D46 §4: the transcript is the diagnostic a person or a rehearsal opens).
/// </summary>
/// <remarks>
/// No process: a stand-in harness says a line on its stream and then waits, as a harness between steps does, and the test
/// reads the file as any reader beside the driver does, sharing it with the writer (<see cref="StubFile"/>).
/// </remarks>
public sealed class TranscriptAsSaidTests : IDisposable
{
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(10);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-transcript-" + Guid.NewGuid().ToString("N")[..8]);

    public TranscriptAsSaidTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>A harness's stream: it says a line when told and otherwise waits, open until the harness exits.</summary>
    private sealed class Harness : TextReader
    {
        private readonly Channel<string> _lines = Channel.CreateUnbounded<string>();

        public void Say(string line) => _lines.Writer.TryWrite(line);

        public void Exit() => _lines.Writer.TryComplete();

        public override Task<string?> ReadLineAsync() => ReadLineAsync(CancellationToken.None).AsTask();

        public override async ValueTask<string?> ReadLineAsync(CancellationToken ct) =>
            await _lines.Reader.WaitToReadAsync(ct).ConfigureAwait(false) && _lines.Reader.TryRead(out var line) ? line : null;
    }

    private static Task Holds(string transcript, params string[] lines) =>
        Poll.Until(
            () => lines.All(StubFile.Text(transcript).Contains),
            () => $"the transcript held {StubFile.Text(transcript).Length} character(s) while its session worked",
            Within);

    /// <summary>The pipe door: its preamble and both of its harness's streams are on the disk while the harness waits.</summary>
    [Fact]
    public async Task The_pipe_doors_lines_are_on_the_disk_while_its_harness_waits()
    {
        var transcript = Path.Combine(_root, "s1.log");
        var stdout = new Harness();
        var stderr = new Harness();
        var capture = Daoris.Driver.Driver.CaptureAsync(
            stdout, stderr, transcript, "s1", output: null, CancellationToken.None, preamble: "— a fact about this run");

        try
        {
            stdout.Say("reading the quest");
            stderr.Say("a warning of the harness's own");

            await Holds(transcript, "— a fact about this run", "reading the quest", "a warning of the harness's own");
            Assert.False(capture.IsCompleted);
        }
        finally
        {
            stdout.Exit();
            stderr.Exit();
            await capture;
        }

        Assert.Equal(3, StubFile.Lines(transcript).Length);
    }

    /// <summary>The native door's structured stdout: what a line renders is on the disk while the harness waits.</summary>
    [Fact]
    public async Task The_native_doors_rendered_line_is_on_the_disk_while_its_harness_waits()
    {
        var transcript = Path.Combine(_root, "s2.log");
        var stdout = new Harness();
        var stderr = new Harness();
        var capture = Daoris.Driver.Driver.CaptureStructuredAsync(
            stdout, stderr, transcript, "s2", output: null, events: null, new ClaudeStreamJson(), prompt: null,
            CancellationToken.None);

        try
        {
            stdout.Say("""{"type":"assistant","message":{"id":"msg_1","content":[{"type":"text","text":"Reading the quest."}]}}""");

            await Holds(transcript, "Reading the quest.");
            Assert.False(capture.IsCompleted);
        }
        finally
        {
            stdout.Exit();
            stderr.Exit();
            await capture;
        }
    }

    /// <summary>
    /// The writer every door opens, the protocol doors' included: a line written is on the disk while the file is open,
    /// and a line written after it closed is refused as a closed writer's always was.
    /// </summary>
    [Fact]
    public async Task A_line_is_on_the_disk_while_the_transcript_is_open()
    {
        var transcript = Path.Combine(_root, "s3.log");
        var file = TranscriptFile.Open(transcript, append: false);
        try
        {
            file.WriteLine("— the turn began");
            await Holds(transcript, "— the turn began");
        }
        finally
        {
            await file.DisposeAsync();
        }

        Assert.Throws<ObjectDisposedException>(() => file.WriteLine("— too late"));
        Assert.Equal(["— the turn began"], StubFile.Lines(transcript));
    }

    /// <summary>A resumed run's transcript goes on: what it writes follows what the run before it left.</summary>
    [Fact]
    public async Task A_transcript_opened_to_go_on_keeps_what_was_there()
    {
        var transcript = Path.Combine(_root, "s4.log");
        await File.WriteAllTextAsync(transcript, "— the first run\n");

        await using (var file = TranscriptFile.Open(transcript, append: true))
        {
            file.WriteLine("— the run that went on");
            await Holds(transcript, "— the run that went on");
        }

        Assert.Equal(["— the first run", "— the run that went on"], StubFile.Lines(transcript));
    }
}
