using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The lineage as Participants' part of a run's sink (AGENTPROVIDER1l, the agent-provider design §11 row l): each door says a child's start
/// and end once, as a reading stamped with its process's token, and the part writes the session's lineage from those readings alone.
/// </summary>
public sealed class LineageReadingsTests : IDisposable
{
    private const string Proc = "0123456789abcdef";

    private static readonly string Fixtures =
        Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "lineage");

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "lineage-readings", Guid.NewGuid().ToString("N")[..8]);

    public LineageReadingsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    /// <summary>
    /// The real family's stream (<c>claude-family.stream.jsonl</c>) through the native capture, and a protocol door's announcements through its
    /// streams beside: each session's lineage as the driver wrote it before the readings, byte for byte but its moments.
    /// </summary>
    [Fact]
    public async Task A_fixture_session_s_lineage_is_written_byte_for_byte_as_before_on_each_door()
    {
        await Capture(File.ReadAllText(Path.Combine(Fixtures, "claude-family.stream.jsonl")), Run("s1"));

        var streams = new AcpStreams(
            new SessionStreams(new SessionOutput(), "d-1"), new AcpConsole(_ => { }, AcpConsole.Quiet), _ => { }, AcpConsole.Quiet,
            children: Run("s2"));
        Take(streams, "s-1", Spawned("a1", "Reader", "Read the README.\nthen the rest"));
        Take(streams, "a1", Spawned("a2", "Nested", null));
        Take(streams, "s-1", Spawned("a1", "again", "again"));
        Take(streams, "s-1", Ended("a2", "completed"));
        Take(streams, "s-1", Ended("a1", null));

        Assert.Equal(Lines(File.ReadAllLines(Path.Combine(Fixtures, "claude-family.driver.jsonl"))), Written("s1"));
        Assert.Equal(
            Lines(
                """{"at":"1970-01-01T00:00:00.000Z","by":"announced","event":"started","agent":"a1","depth":1,"name":"Reader","task":"Read the README.","proc":"0123456789abcdef"}""",
                """{"at":"1970-01-01T00:00:00.000Z","by":"announced","event":"started","agent":"a2","parent":"a1","name":"Nested","proc":"0123456789abcdef"}""",
                """{"at":"1970-01-01T00:00:00.000Z","by":"announced","event":"ended","agent":"a2","state":"completed","proc":"0123456789abcdef"}""",
                """{"at":"1970-01-01T00:00:00.000Z","by":"announced","event":"ended","agent":"a1","proc":"0123456789abcdef"}"""),
            Written("s2"));
    }

    /// <summary>
    /// The design's proof of row l: one frame may give several readings, never one fact twice, nor one lineage line twice. The wire repeating a
    /// child's start or end, a background command's task, an end with no start and a line that is no frame are each read as nothing more.
    /// </summary>
    [Fact]
    public async Task Each_child_s_start_and_end_is_read_once_and_written_once()
    {
        var heard = new List<WireReading>();
        const string started = """{"type":"system","subtype":"task_started","task_id":"a1","tool_use_id":"t1","description":"Read it.","spawn_depth":1,"task_type":"local_agent"}""";
        const string ended = """{"type":"system","subtype":"task_notification","task_id":"a1","status":"completed"}""";

        await Capture(
            string.Join('\n',
                started, started,
                """{"type":"system","subtype":"task_started","task_id":"b1","description":"npm test","task_type":"local_bash"}""",
                """{"type":"system","subtype":"task_notification","task_id":"b1","status":"completed"}""",
                """{"type":"system","subtype":"task_notification","task_id":"a9","status":"completed"}""",
                ended, ended, "not json"),
            Run("s1", ReadingSinks.On<WireReading>(heard.Add)));

        var children = heard.Where(reading => reading is WireReading.ChildSeen or WireReading.ChildEnded).ToList();
        Assert.Equal(
            [
                new WireReading.ChildSeen("a1", ChildSignal.Stream) { Call = "t1", Depth = 1, Task = "Read it." },
                new WireReading.ChildEnded("a1", ChildSignal.Stream, "completed"),
            ],
            children.Select(reading => reading with { Run = null, Generation = 0, Sequence = 0, Process = null }));
        Assert.All(children, reading => Assert.Equal(Proc, reading.Process));
        Assert.Equal(
            Lines(
                """{"at":"1970-01-01T00:00:00.000Z","by":"stream","event":"started","agent":"a1","depth":1,"description":"Read it.","proc":"0123456789abcdef"}""",
                """{"at":"1970-01-01T00:00:00.000Z","by":"stream","event":"ended","agent":"a1","status":"completed","proc":"0123456789abcdef"}"""),
            Written("s1"));
    }

    /// <summary>
    /// The part writes what the reading says and nothing of its own: the label bounded, the process named where the run stamped a token, a
    /// replay no new child, and every other reading passed over.
    /// </summary>
    [Fact]
    public void The_part_writes_a_child_s_line_from_its_reading_alone()
    {
        var part = new LineageReadings(_home, "s1");

        part.Read(new WireReading.Shown("a line"));
        part.Read(new WireReading.ChildSeen("a0", ChildSignal.Announced) { Depth = 1, Replayed = true });
        part.Read(new WireReading.ChildEnded("a0", ChildSignal.Announced, "completed") { Replayed = true, Process = Proc });
        Assert.False(File.Exists(ChildLineage.PathOf(_home, "s1")));

        part.Read(new WireReading.ChildSeen("a1", ChildSignal.Announced) { Name = " " + new string('n', 130), Task = "  \nthe rest" });
        part.Read(new WireReading.ChildSeen("../a2", ChildSignal.Announced) { Process = Proc });
        part.Read(new WireReading.ChildEnded("a1", ChildSignal.Announced, "no word/here") { Process = "not a token" });

        Assert.Equal(
            Lines(
                $$"""{"at":"1970-01-01T00:00:00.000Z","by":"announced","event":"started","agent":"a1","name":"{{new string('n', 120)}}"}""",
                """{"at":"1970-01-01T00:00:00.000Z","by":"announced","event":"ended","agent":"a1"}"""),
            Written("s1"));
    }

    /// <summary>The run's sink as a driven executor composes it, stamped as the run stamps it: this session's, its first process's.</summary>
    private IReadingSink Run(string session, IReadingSink? beside = null) =>
        new StampedSink(ReadingSinks.Of(beside, new LineageReadings(_home, session)), () => session, () => 1, () => Proc);

    private Task Capture(string lines, IReadingSink sink) => HarnessCapture.CaptureStructuredAsync(
        new StringReader(lines), new StringReader(""), Path.Combine(_home, "capture.log"), sink, new ClaudeStreamJson(), prompt: null,
        CancellationToken.None);

    private static string Lines(params string[] lines) => string.Concat(lines.Where(line => line.Length > 0).Select(line => line + "\n"));

    /// <summary>The session's lineage file as its bytes say, every moment read as the epoch's.</summary>
    private string Written(string session) =>
        Regex.Replace(Encoding.UTF8.GetString(File.ReadAllBytes(ChildLineage.PathOf(_home, session))), "\"at\":\"[^\"]*\"", "\"at\":\"1970-01-01T00:00:00.000Z\"");

    private static void Take(AcpStreams streams, string? from, string update)
    {
        using var parsed = JsonDocument.Parse(update);
        Assert.True(streams.Take(from, "s-1", parsed.RootElement.Clone()));
    }

    private static string Spawned(string id, string? name, string? task) => JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["sessionUpdate"] = "subagent_spawned", ["subagentSessionId"] = id, ["name"] = name, ["task"] = task,
    });

    private static string Ended(string id, string? state) => state is null
        ? JsonSerializer.Serialize(new { sessionUpdate = "subagent_state_update", subagentSessionId = id })
        : JsonSerializer.Serialize(new { sessionUpdate = "subagent_state_update", subagentSessionId = id, state });
}
