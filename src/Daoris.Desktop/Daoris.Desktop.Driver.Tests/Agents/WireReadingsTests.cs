using Daoris.Desktop.Driver.Tests;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1l: a run's door says what happened as readings on one sink its executor composes. The event file each door keeps is the one
/// it kept before the sink, byte for byte but for each event's own time; every failure is one <see cref="WireReading.Failed"/>; the sink's
/// parts are each told every reading, watched where a console is. AGENTPROVIDER1 s-viii: a subagent's card on the native door wears the
/// protocol door's <c>think</c>.
/// </summary>
public sealed class WireReadingsTests : IDisposable
{
    private const string Init = """{"type":"system","subtype":"init","cwd":"D:/fam/engine","session_id":"c1","tools":["Read"],"model":"m","permissionMode":"acceptEdits","claude_code_version":"2.1.281"}""";
    private const string Whole = """{"type":"assistant","message":{"id":"msg_1","content":[{"type":"text","text":"Capped at 4 per frame."}],"usage":{"input_tokens":2,"output_tokens":4}},"session_id":"c1","parent_tool_use_id":null}""";
    private const string Subagent = """{"type":"assistant","message":{"id":"msg_2","content":[{"type":"tool_use","id":"toolu_2","name":"Task","input":{"description":"Survey the gates","prompt":"Read the gates.","subagent_type":"general-purpose"}}]},"session_id":"c1","parent_tool_use_id":null}""";
    private const string Agent = """{"type":"assistant","message":{"id":"msg_3","content":[{"type":"tool_use","id":"toolu_3","name":"Agent","input":{"description":"Read the area","prompt":"Read it.","subagent_type":"Explore"}}]},"session_id":"c1","parent_tool_use_id":null}""";
    private const string Ended = """{"type":"result","subtype":"success","is_error":false,"result":"Capped at 4 per frame.","num_turns":1,"usage":{"input_tokens":2,"output_tokens":4}}""";
    private const string FailedResult = """{"type":"result","subtype":"error_during_execution","is_error":true,"result":"API Error: 500 overloaded","num_turns":1}""";

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-wire-readings-" + Guid.NewGuid().ToString("N")[..8]);

    public WireReadingsTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>The event file with each event's own time taken out: the one field written as the line is, never the door's.</summary>
    private static string Unstamped(string path) => Regex.Replace(File.ReadAllText(path), "\"at\":\"[^\"]*\"", "\"at\":\"\"", RegexOptions.IgnoreCase);

    [Fact]
    public async Task A_native_fixture_session_keeps_the_event_file_it_kept_before_the_sink()
    {
        string[] lines = [Init, Whole, Subagent, Ended];
        var through = new SessionEvents(Path.Combine(_root, "sink"));
        await HarnessCapture.CaptureStructuredAsync(
            new StringReader(string.Join('\n', lines)), new StringReader(""), Path.Combine(_root, "s1.log"), new RecordReadings("s1", null, through),
            new ClaudeStreamJson(), prompt: "take quest #q1", CancellationToken.None);

        // Before row l the capture kept its opening, then each line's events in the order its mapper read them, straight on the record.
        var before = new SessionEvents(Path.Combine(_root, "before"));
        foreach (var e in HarnessCapture.Opening("take quest #q1", null)) before.Keep("s1", e, null);
        var mapper = new ClaudeStreamJson();
        foreach (var line in lines)
        {
            foreach (var e in mapper.Read(line).Events) before.Keep("s1", e, null);
        }

        Assert.Equal(Unstamped(Path.Combine(_root, "before", "s1.events.jsonl")), Unstamped(Path.Combine(_root, "sink", "s1.events.jsonl")));
    }

    [Fact]
    public void A_protocol_session_s_events_reach_the_event_file_as_a_direct_keep_wrote_them()
    {
        SessionEvent[] said =
        [
            new() { Kind = SessionEventKind.User, Origin = "target", Text = "take quest #q1" },
            new() { Kind = SessionEventKind.Note, Text = "started on `acp-stub`" },
            new() { Kind = SessionEventKind.Tool, Id = "c1", Title = "Read the area", ToolKind = "read", Status = "completed" },
        ];
        var output = new SessionOutput();
        var through = new SessionEvents(Path.Combine(_root, "sink"));
        var sink = new RecordReadings("s1", output, through);
        foreach (var e in said) sink.Read(new WireReading.Said(e));
        sink.Read(new WireReading.Shown("— the turn ended: end_turn"));

        var before = new SessionEvents(Path.Combine(_root, "before"));
        foreach (var e in said) before.Keep("s1", e, null);

        Assert.Equal(Unstamped(Path.Combine(_root, "before", "s1.events.jsonl")), Unstamped(Path.Combine(_root, "sink", "s1.events.jsonl")));
        Assert.Equal(["— the turn ended: end_turn"], output.Tail("s1").Lines.Select(line => line.Text));
    }

    [Fact]
    public async Task A_failed_native_result_is_one_Failed_reading_with_its_own_words()
    {
        var readings = new List<WireReading>();
        await HarnessCapture.CaptureStructuredAsync(
            new StringReader(string.Join('\n', Init, FailedResult)), new StringReader(""), Path.Combine(_root, "s2.log"),
            ReadingSinks.On<WireReading>(readings.Add), new ClaudeStreamJson(), prompt: null, CancellationToken.None, own: new HarnessWords());

        var failed = Assert.Single(readings.OfType<WireReading.Failed>());
        Assert.Equal(new DoorFailure("pipe", null, null, null, "API Error: 500 overloaded", DoorWhere.Result), failed.Failure);
        // The conversation the stream named is one reading too, before anything ended.
        Assert.Equal("c1", Assert.Single(readings.OfType<WireReading.ConversationNamed>()).Conversation);
    }

    [Fact]
    public async Task A_clean_native_run_says_no_failure()
    {
        var readings = new List<WireReading>();
        await HarnessCapture.CaptureStructuredAsync(
            new StringReader(string.Join('\n', Init, Whole, Ended)), new StringReader(""), Path.Combine(_root, "s3.log"),
            ReadingSinks.On<WireReading>(readings.Add), new ClaudeStreamJson(), prompt: null, CancellationToken.None);

        Assert.Empty(readings.OfType<WireReading.Failed>());
    }

    [Fact]
    public void A_protocol_failure_without_a_refusal_carries_its_words_and_where_it_was_met()
    {
        var failure = DoorFailures.OfProtocol(new DriverException("the agent went away"), DoorWhere.Open);

        Assert.Equal(new DoorFailure("protocol", null, null, null, "the agent went away", DoorWhere.Open), failure);
    }

    [Fact]
    public void Each_part_is_told_every_reading_in_order_and_the_sink_is_watched_where_a_part_is()
    {
        var told = new List<string>();
        var sink = ReadingSinks.Of(
            ReadingSinks.On<WireReading.ConversationNamed>(named => told.Add("binding:" + named.Conversation)),
            null,
            ReadingSinks.On<WireReading>(reading => told.Add("all:" + reading.GetType().Name)));

        sink.Read(new WireReading.ConversationNamed("c1"));
        sink.Read(new WireReading.Shown("a line"));

        Assert.Equal(["binding:c1", "all:ConversationNamed", "all:Shown"], told);
        Assert.False(sink.Watched);
        Assert.False(ReadingSinks.None.Watched);
        Assert.True(ReadingSinks.Of(sink, new RecordReadings("s1", new SessionOutput(), null)).Watched);
        Assert.Null(ReadingStreams.Of(sink));
    }

    [Fact]
    public void A_stream_beside_the_run_reaches_its_console_through_its_readings()
    {
        var output = new SessionOutput();
        var streams = ReadingStreams.Of(new RecordReadings("s1", output, null));

        Assert.NotNull(streams);
        streams.Open(new SessionStream("task", SessionStreamKind.Task, "Task", true));
        streams.Line("task", "first");
        streams.End("task", "completed");
        Assert.Single(output.Streams("s1"));
        Assert.Equal(["first"], output.Tail(SessionOutput.Key("s1", "task")).Lines.Select(line => line.Text));
        Assert.Empty(output.Tail("s1").Lines);
    }

    [Fact]
    public void Each_reading_a_run_says_carries_its_run_its_process_and_its_place_in_the_run_s_order()
    {
        var heard = new List<WireReading>();
        var (generation, process) = (1, (string?)"0123456789abcdef");
        IReadingSink sink = new StampedSink(ReadingSinks.On<WireReading>(heard.Add), () => "s1", () => generation, () => process);

        sink.Read(new WireReading.Shown("one"));
        (generation, process) = (2, "fedcba9876543210");
        sink.Read(new WireReading.Shown("two"));

        Assert.Equal(
            [("s1", 1, "0123456789abcdef", 1L), ("s1", 2, "fedcba9876543210", 2L)],
            heard.Select(reading => (reading.Run, reading.Generation, reading.Process, reading.Sequence)));
        Assert.Equal(["one", "two"], heard.Cast<WireReading.Shown>().Select(shown => shown.Text));
    }

    private static readonly AgentError Limited = new(
        AgentErrorKind.Limit, DoorFailures.OfResult("You've hit your limit"), "own:words:limit",
        new LimitSeen("limit", null, DateTimeOffset.UnixEpoch.AddHours(5), Stated: true, AssumedZone: false));

    private static readonly WireReading TurnEnded = new WireReading.Said(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "success" });

    /// <summary>
    /// The native door's failed result, read as a limit, is the limit its turn ended on, told once at that end; nothing else the sink hears is
    /// (D210 1c, TOOL4n).
    /// </summary>
    [Fact]
    public void The_native_door_s_failed_result_read_as_a_limit_is_its_turn_s_limit_told_once_and_a_protocol_failure_is_none()
    {
        var met = new List<AgentError>();
        var part = new NativeLimits(failure => failure.Door == "pipe" ? Limited : Limited with { Kind = AgentErrorKind.Fatal }, met.Add);

        part.Read(new WireReading.Failed(DoorFailures.OfResult("You've hit your limit")));
        part.Read(new WireReading.Failed(DoorFailures.OfPrompt(new AcpRefusal("overloaded", errorKind: "overloaded", code: -32603, words: "overloaded"))));
        part.Read(new WireReading.Failed(new DoorFailure("pipe", null, null, null, "You've hit your limit", DoorWhere.Harness)));
        part.Read(new WireReading.Failed(DoorFailures.OfResult("replayed")) { Replayed = true });
        part.Read(new WireReading.Shown("You've hit your limit"));
        Assert.Empty(met);

        part.Read(TurnEnded);

        Assert.Same(Limited, Assert.Single(met));
        Assert.Same(Limited, part.Ended);
    }

    [Fact]
    public void A_pipe_result_the_reader_does_not_read_as_a_limit_is_no_limit()
    {
        var part = new NativeLimits(_ => Limited with { Kind = AgentErrorKind.Transient }, _ => throw new InvalidOperationException("met"));

        part.Read(new WireReading.Failed(DoorFailures.OfResult("API Error: 500 overloaded")));
        part.Read(TurnEnded);

        Assert.Null(part.Ended);
    }

    [Theory]
    [InlineData(Subagent, "Agent: Survey the gates")]
    [InlineData(Agent, "Agent")]
    public void A_subagent_s_card_on_the_native_door_wears_think(string line, string title)
    {
        var mapper = new ClaudeStreamJson();
        mapper.Read(Init);

        var card = Assert.Single(mapper.Read(line).Events, e => e.Kind == SessionEventKind.Tool);
        Assert.Equal("think", card.ToolKind);
        Assert.Equal(title, card.Title);
    }
}
