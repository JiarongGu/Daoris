using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One reader of Claude Code's <c>stream-json</c> (AGENTPROVIDER1l): the capture reads each line into one <see cref="NativeFrame"/>, and the
/// children it says (<see cref="NativeChildren"/>), the streams beside and the dialect's mapper each take that frame, never the text to parse
/// again.
/// </summary>
public sealed class NativeFrameTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "native-frame", Guid.NewGuid().ToString("N")[..8]);

    public NativeFrameTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public async Task Each_line_is_read_once_and_every_reader_takes_the_same_frame()
    {
        var readers = new Readers();
        const string lines = """
            {"type":"system","subtype":"init","session_id":"c1"}
            warming up
            {"type":"assistant","message":{"content":[]}}
            """;

        await HarnessCapture.CaptureStructuredAsync(
            new StringReader(lines), new StringReader(""), Path.Combine(_home, "s1.log"), new WatchedSink(), readers, prompt: null,
            CancellationToken.None);

        Assert.Equal(3, readers.Taken.Count);
        Assert.Equal(readers.Taken, readers.Read);
        Assert.Equal(
            ["""{"type":"system","subtype":"init","session_id":"c1"}""", "warming up", """{"type":"assistant","message":{"content":[]}}"""],
            readers.Read.Select(frame => frame.Line));
        Assert.Equal([true, false, true], readers.Read.Select(frame => frame.IsObject));
        Assert.Equal(["system", null, "assistant"], readers.Read.Select(frame => frame.Type));
    }

    [Fact]
    public void A_line_that_is_no_json_keeps_its_text_alone()
    {
        var frame = NativeFrame.Of("[1, 2]");

        Assert.True(frame.Parsed);
        Assert.False(frame.IsObject);
        Assert.Null(frame.Type);
        Assert.False(NativeFrame.Of("not json {").Parsed);
        Assert.Equal("not json {", NativeFrame.Of("not json {").Line);
    }

    /// <summary>A sink someone watches, so the streams beside are asked.</summary>
    private sealed class WatchedSink : IReadingSink
    {
        public bool Watched => true;

        public void Read(WireReading reading)
        {
        }
    }

    private sealed class Readers : IStreamMapper, IStreamsReader
    {
        public List<NativeFrame> Taken { get; } = [];
        public List<NativeFrame> Read { get; } = [];
        public AcpUsage? Usage => null;

        StreamMapped IStreamMapper.Read(NativeFrame frame)
        {
            Read.Add(frame);
            return StreamMapped.Nothing;
        }

        public IStreamsReader Beside(ISessionStreams streams, Action<string> say) => this;

        public bool Take(NativeFrame frame)
        {
            Taken.Add(frame);
            return false;
        }

        public Task EndAllAsync() => Task.CompletedTask;
    }
}
