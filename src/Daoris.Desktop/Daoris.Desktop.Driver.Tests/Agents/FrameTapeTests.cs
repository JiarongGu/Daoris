using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The frames of a sitting kept as the harness and Daoris said them: each line of a door's pipe, one tape, whichever harness is on it,
/// flushed as it is said so a process cut short leaves what it had said, and never a secret a frame carries.
/// </summary>
public sealed class FrameTapeTests : IDisposable
{
    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "_frame-tape-" + Guid.NewGuid().ToString("N")[..8]);

    public FrameTapeTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Tape => FrameTape.PathOf(Path.Combine(_root, "s1.log"));

    private IReadOnlyList<JsonElement> Kept() =>
        [.. File.ReadAllLines(Tape).Select(line => JsonDocument.Parse(line).RootElement.Clone())];

    /// <summary>The tape sits beside the transcript, under the session's id, and counts as that session's history.</summary>
    [Fact]
    public void The_tape_is_beside_the_transcript_under_the_sessions_name()
    {
        Assert.Equal(Path.Combine(_root, "s1.frames.jsonl"), Tape);
        Assert.Contains(".frames.jsonl", SessionHomeFiles.SessionSuffixes);
    }

    /// <summary>What is read and what is written are kept in the order they went, each as the frame it is, flushed as it goes.</summary>
    [Fact]
    public async Task Each_line_read_and_written_is_kept_in_order_as_it_goes()
    {
        using var tape = new FrameTape(Tape, _ => { });
        var heard = tape.Hearing(new StringReader("""{"jsonrpc":"2.0","id":1,"result":{}}""" + "\nnot a frame\n"));
        var said = new StringWriter();
        var speaking = tape.Speaking(said);

        await speaking.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"session/prompt","params":{}}""");
        Assert.Equal("""{"jsonrpc":"2.0","id":1,"result":{}}""", await heard.ReadLineAsync(CancellationToken.None));
        Assert.Equal("not a frame", await heard.ReadLineAsync(CancellationToken.None));
        Assert.Null(await heard.ReadLineAsync(CancellationToken.None));

        // Read while the tape is still open: each line is flushed as it is kept.
        var kept = Kept();
        Assert.Equal(["out", "in", "in"], kept.Select(each => each.GetProperty("dir").GetString()));
        Assert.Equal("session/prompt", kept[0].GetProperty("frame").GetProperty("method").GetString());
        Assert.Equal(1, kept[1].GetProperty("frame").GetProperty("id").GetInt32());
        Assert.Equal("not a frame", kept[2].GetProperty("raw").GetString());
        Assert.Equal("""{"jsonrpc":"2.0","id":2,"method":"session/prompt","params":{}}""" + Environment.NewLine, said.ToString());
    }

    /// <summary>A key a frame carries is never kept: the values of an environment, headers and credentials are left out where they sit.</summary>
    [Fact]
    public async Task A_secret_a_frame_carries_is_left_out_where_it_sits()
    {
        using var tape = new FrameTape(Tape, _ => { });
        var speaking = tape.Speaking(new StringWriter());

        await speaking.WriteLineAsync(
            """{"id":1,"method":"session/new","params":{"cwd":"x","mcpServers":[{"name":"c","env":[{"name":"K","value":"sk-secret"}],"headers":[{"name":"Authorization","value":"Bearer sk-secret"}],"url":"http://127.0.0.1:1/mcp"}]}}""");

        var text = File.ReadAllText(Tape);
        Assert.DoesNotContain("sk-secret", text);
        Assert.Contains("http://127.0.0.1:1/mcp", text);
    }

    /// <summary>A tape past its bound says so once and keeps no more, and a tape that cannot be written costs the run nothing.</summary>
    [Fact]
    public async Task A_tape_past_its_bound_stops_and_says_so_once()
    {
        var said = new List<string>();
        using var tape = new FrameTape(Tape, said.Add, cap: 200);
        var heard = tape.Hearing(new StringReader(string.Join('\n', Enumerable.Range(0, 20).Select(n => $$"""{"n":{{n}}}""")) + "\n"));
        while (await heard.ReadLineAsync(CancellationToken.None) is not null)
        {
        }

        var kept = Kept();
        Assert.True(kept.Count is > 1 and < 20);
        Assert.Equal("cap", kept[^1].GetProperty("stopped").GetString());
        Assert.Single(said);

        using var unwritable = new FrameTape(_root, said.Add);
        var read = unwritable.Hearing(new StringReader("{}\n"));
        Assert.Equal("{}", await read.ReadLineAsync(CancellationToken.None));
        Assert.Equal(2, said.Count);
    }
}
