using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The streams beside and the run's own ending read a child's and a background task's start and end from their readings alone
/// (AGENTPROVIDER1l): every recorded Claude Code stream through the native capture, with a console and without one, says its transcript, its
/// console, its streams and its ending byte for byte as the capture said them when each reader read those off the frame
/// (<c>fixtures/native/recorded-words.txt</c>, written by that capture).
/// </summary>
public sealed class RecordedStreamWordsTests : IDisposable
{
    private static readonly string Fixtures = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures");

    private readonly string _root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "recorded-words", Guid.NewGuid().ToString("N")[..8]);

    public RecordedStreamWordsTests() => Directory.CreateDirectory(_root);

    public void Dispose() => GitSeed.Discard(_root);

    [Fact]
    public async Task Every_recorded_stream_s_words_are_said_byte_for_byte_as_before()
    {
        var said = new StringBuilder();
        var recorded = Directory.EnumerateFiles(Fixtures, "claude-*.stream.jsonl", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
        foreach (var fixture in recorded)
        {
            var name = Path.GetRelativePath(Fixtures, fixture).Replace('\\', '/');
            foreach (var console in new[] { true, false })
            {
                said.Append($"== {name}, {(console ? "with a console" : "with none")}\n");
                await WordsAsync(said, File.ReadAllText(fixture), console);
            }
        }

        Assert.Equal(3, recorded.Count);
        Assert.Equal(File.ReadAllText(Path.Combine(Fixtures, "native", "recorded-words.txt")).ReplaceLineEndings("\n"), said.ToString());
    }

    /// <summary>What one capture of <paramref name="lines"/> said: its transcript, its console and streams where one is kept, and its ending.</summary>
    private async Task WordsAsync(StringBuilder said, string lines, bool console)
    {
        var output = console ? new SessionOutput() : null;
        var own = new HarnessWords();
        var transcript = Path.Combine(_root, Guid.NewGuid().ToString("N")[..8] + ".log");
        await HarnessCapture.CaptureStructuredAsync(
            new StringReader(lines), new StringReader(""), transcript, new RecordReadings("s1", output, null), new ClaudeStreamJson(), prompt: null,
            CancellationToken.None, own: own);

        foreach (var line in File.ReadAllLines(transcript)) said.Append($"transcript: {line}\n");
        if (output is not null)
        {
            foreach (var line in output.Tail("s1").Lines) said.Append($"console: {line.Text}\n");
            foreach (var stream in output.Streams("s1"))
            {
                said.Append($"stream: {stream.Key} {stream.Kind} '{stream.Name}' {stream.State} live={stream.Live} stops={stream.CanStop}\n");
                foreach (var line in output.Tail(stream.Key).Lines) said.Append($"  {line.Text}\n");
            }
        }

        var ending = own.Ended(0);
        foreach (var line in ending.Lines) said.Append($"own: {line}\n");
        said.Append($"ending: failure={ending.Failure} cut=[{string.Join(", ", ending.BackgroundCut)}] refused={ending.ModelRefused}\n");
    }
}
