using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Claude Code's own structured output, mapped into Daoris's vocabulary by its adapter (D76 §1, CONV3).
/// </summary>
/// <remarks>
/// The lines are the shapes the binary printed (docs/2026-09-25-stream-json-evidence.md): the probe's
/// two one-word turns, and a tool turn in the SDK's published shape, which the one-word probe did not
/// exercise. Nothing here parses prose; every read is a field of the harness's documented wire.
/// </remarks>
public sealed class ClaudeStreamJsonTests
{
    private static (List<string> Lines, List<SessionEvent> Events, ClaudeStreamJson Mapper) Map(params string[] stdout)
    {
        var mapper = new ClaudeStreamJson();
        var lines = new List<string>();
        var events = new List<SessionEvent>();
        foreach (var line in stdout)
        {
            var mapped = mapper.Read(line);
            lines.AddRange(mapped.Lines);
            events.AddRange(mapped.Events);
        }

        return (lines, events, mapper);
    }

    private const string Init = """{"type":"system","subtype":"init","cwd":"D:/fam/engine","session_id":"c1","tools":["Read"],"model":"m","permissionMode":"acceptEdits","claude_code_version":"2.1.281"}""";
    private const string Status = """{"type":"system","subtype":"status","status":"ok","session_id":"c1"}""";
    private const string Start = """{"type":"stream_event","event":{"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","content":[]}}}""";
    private const string Delta1 = """{"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Capped at "}}}""";
    private const string Delta2 = """{"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"4 per frame."}}}""";
    private const string Whole = """{"type":"assistant","message":{"id":"msg_1","content":[{"type":"text","text":"Capped at 4 per frame."}],"usage":{"input_tokens":2,"cache_creation_input_tokens":17501,"cache_read_input_tokens":15553,"output_tokens":4}},"session_id":"c1","parent_tool_use_id":null}""";
    private const string Result = """{"type":"result","subtype":"success","is_error":false,"result":"Capped at 4 per frame.","num_turns":1,"usage":{"input_tokens":2,"output_tokens":4},"modelUsage":{"some-model":{"contextWindow":1000000,"costUSD":0.14}}}""";

    /// <summary>🔴 The words arrive twice — as deltas, then whole — and must be shown once.</summary>
    [Fact]
    public void Streamed_words_are_the_message_and_the_whole_message_does_not_repeat_them()
    {
        var (lines, events, _) = Map(Init, Status, Start, Delta1, Delta2, Whole, Result);

        var messages = events.Where(e => e.Kind == SessionEventKind.Message).Select(e => e.Text).ToList();
        Assert.Equal(["Capped at ", "4 per frame."], messages);
        // The console reads the whole message once, as a person would read a transcript.
        Assert.Single(lines, line => line.Contains("Capped at 4 per frame."));
    }

    /// <summary>With partial messages off, the whole message is the only copy — and it is kept.</summary>
    [Fact]
    public void A_whole_message_with_no_deltas_before_it_is_the_message()
    {
        var (_, events, _) = Map(Init, Whole, Result);

        Assert.Equal("Capped at 4 per frame.", Assert.Single(events, e => e.Kind == SessionEventKind.Message).Text);
    }

    /// <summary>
    /// The turn ends where the harness says, and context is used against the window the harness names —
    /// read from the wire, never computed, and neither the model's name nor its price is kept (D24, TOOL3).
    /// </summary>
    [Fact]
    public void A_result_ends_the_turn_and_reports_context_against_the_harnesss_own_window()
    {
        var (_, events, mapper) = Map(Init, Start, Delta1, Whole, Result);

        var turn = Assert.Single(events, e => e.Kind == SessionEventKind.Turn);
        Assert.Equal("end_turn", turn.StopReason);

        var usage = Assert.Single(events, e => e.Kind == SessionEventKind.Usage);
        Assert.Equal(2 + 17501 + 15553, usage.Used);
        Assert.Equal(1_000_000, usage.Size);
        Assert.Equal(new AcpUsage(33056, 1_000_000), mapper.Usage);

        Assert.All(events, e => Assert.DoesNotContain("some-model", e.Raw ?? ""));
    }

    [Fact]
    public void A_tool_call_and_its_result_are_one_card_with_the_edit_as_a_diff()
    {
        var edit = """{"type":"assistant","message":{"id":"msg_2","content":[{"type":"tool_use","id":"toolu_1","name":"Edit","input":{"file_path":"src/chunk.rs","old_string":"let cap = 0;","new_string":"let cap = 4;"}}]}}""";
        var done = """{"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_1","content":"The file src/chunk.rs has been updated.","is_error":false}]}}""";

        var (lines, events, _) = Map(edit, done);

        var call = events[0];
        Assert.Equal(SessionEventKind.Tool, call.Kind);
        Assert.Equal("toolu_1", call.Id);
        Assert.Equal("Edit src/chunk.rs", call.Title);
        Assert.Equal("edit", call.ToolKind);
        Assert.Equal("in_progress", call.Status);
        Assert.Equal(["src/chunk.rs"], call.Locations);
        var diff = Assert.Single(call.Content!);
        Assert.Equal(("diff", "let cap = 0;", "let cap = 4;"), (diff.Type, diff.OldText, diff.NewText));

        var result = events[1];
        Assert.Equal(("toolu_1", "completed"), (result.Id, result.Status));
        Assert.Equal("The file src/chunk.rs has been updated.", Assert.Single(result.Content!).Text);

        Assert.Contains(lines, line => line.Contains("Edit src/chunk.rs"));
    }

    [Fact]
    public void A_failed_tool_result_is_a_failed_call_and_a_shell_command_is_titled_by_what_it_does()
    {
        var run = """{"type":"assistant","message":{"id":"msg_3","content":[{"type":"tool_use","id":"toolu_2","name":"Bash","input":{"command":"cargo test streaming","description":"Run the streaming tests"}}]}}""";
        var failed = """{"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_2","content":[{"type":"text","text":"error: 1 test failed"}],"is_error":true}]}}""";

        var (_, events, _) = Map(run, failed);

        Assert.Equal(("Run the streaming tests", "execute"), (events[0].Title, events[0].ToolKind));
        Assert.Contains("cargo test streaming", events[0].Input);
        Assert.Equal("failed", events[1].Status);
        Assert.Equal("error: 1 test failed", Assert.Single(events[1].Content!).Text);
    }

    /// <summary>The harness's to-do list is its plan, so it is kept as one.</summary>
    [Fact]
    public void A_todo_list_is_the_plan()
    {
        var todo = """{"type":"assistant","message":{"id":"msg_4","content":[{"type":"tool_use","id":"toolu_3","name":"TodoWrite","input":{"todos":[{"content":"Read the streamer","status":"completed","activeForm":"Reading"},{"content":"Cap it","status":"in_progress","activeForm":"Capping"}]}}]}}""";

        var (_, events, _) = Map(todo);

        var plan = Assert.Single(events, e => e.Kind == SessionEventKind.Plan);
        Assert.Equal(["Read the streamer", "Cap it"], plan.Entries!.Select(entry => entry.Content));
        Assert.Equal("in_progress", plan.Entries![1].Status);
    }

    [Fact]
    public void Thinking_streams_as_a_thought()
    {
        var thinking = """{"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"the cap belongs in the streamer"}}}""";

        var (_, events, _) = Map(Start, thinking);

        Assert.Equal("the cap belongs in the streamer", Assert.Single(events, e => e.Kind == SessionEventKind.Thought).Text);
    }

    /// <summary>
    /// A turn the harness failed says why on the console and ends the turn — the refusal detector reads
    /// the transcript's last lines for "not logged in", so the words must reach it.
    /// </summary>
    [Fact]
    public void A_failed_result_says_why_in_the_transcript_and_ends_the_turn_with_its_subtype()
    {
        var error = """{"type":"result","subtype":"error_during_execution","is_error":true,"result":"Invalid API key · Please run /login"}""";

        var (lines, events, _) = Map(error);

        Assert.Contains(lines, line => line.Contains("Invalid API key · Please run /login"));
        Assert.Equal("error_during_execution", Assert.Single(events, e => e.Kind == SessionEventKind.Turn).StopReason);
    }

    /// <summary>
    /// A turn the person stopped ends as the protocol door calls it, <c>cancelled</c>, whichever part
    /// the interrupt cut — the model writing or a tool running — and never as a failure. The shapes
    /// are the probe's (stream-json evidence, § Stopping a turn).
    /// </summary>
    [Theory]
    [InlineData("aborted_streaming")]
    [InlineData("aborted_tools")]
    public void An_interrupted_turn_ends_cancelled_whichever_part_it_cut(string terminal)
    {
        var interrupted = """{"type":"user","message":{"role":"user","content":[{"type":"text","text":"[Request interrupted by user]"}]},"session_id":"c1"}""";
        var result = $$"""{"type":"result","subtype":"error_during_execution","is_error":true,"stop_reason":null,"terminal_reason":"{{terminal}}","errors":["[ede_diagnostic] result_type=user"]}""";

        var (lines, events, _) = Map(interrupted, result);

        Assert.Equal("cancelled", Assert.Single(events, e => e.Kind == SessionEventKind.Turn).StopReason);
        Assert.Contains("— the turn was stopped", lines);
        // The harness's own marker is the wire's bookkeeping, not something anybody said.
        Assert.DoesNotContain(events, e => e.Kind is SessionEventKind.User or SessionEventKind.Message);
    }

    /// <summary>
    /// The harness's answer to a control request is the driver's business: it is neither the
    /// conversation nor an update this build does not know.
    /// </summary>
    [Fact]
    public void A_control_response_is_the_drivers_answer_and_stays_out_of_the_record()
    {
        var answered = """{"type":"control_response","response":{"subtype":"success","request_id":"r1","response":{"still_queued":[]}}}""";
        var refused = """{"type":"control_response","response":{"subtype":"error","request_id":"r2","error":"no turn to interrupt"}}""";

        var (lines, events, _) = Map(answered, refused);

        Assert.Empty(events);
        Assert.Equal(["— the harness refused a control request: no turn to interrupt"], lines);
    }

    /// <summary>The native door's stop, as the one line the binary answered in the probe.</summary>
    [Fact]
    public void The_adapter_frames_an_interrupt_as_the_control_request_the_harness_answers()
    {
        var framed = new ClaudeCodeAdapter().FrameInterrupt();

        Assert.NotNull(framed);
        using var json = System.Text.Json.JsonDocument.Parse(framed!);
        Assert.Equal("control_request", json.RootElement.GetProperty("type").GetString());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("request_id").GetString()));
        Assert.Equal("interrupt", json.RootElement.GetProperty("request").GetProperty("subtype").GetString());
        Assert.DoesNotContain('\n', framed);
    }

    /// <summary>What is not a frame is shown as itself, and a type this build does not know is kept raw.</summary>
    [Fact]
    public void A_line_that_is_not_a_frame_is_shown_and_an_unknown_type_is_kept_raw()
    {
        var (lines, events, _) = Map("warming up…", """{"type":"telemetry","what":"x"}""");

        Assert.Contains("warming up…", lines);
        var raw = Assert.Single(events);
        Assert.Equal((SessionEventKind.Raw, "telemetry"), (raw.Kind, raw.Title));
    }

    /// <summary>
    /// The native door's capture end to end, from the harness's stdout: the transcript is text a person
    /// reads (never the JSON), the record opens with what was asked and holds what the wire said, and
    /// the context the harness reported comes back for the usage record (TOOL3).
    /// </summary>
    [Fact]
    public async Task The_capture_keeps_a_readable_transcript_and_the_conversation_beside_it()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-stream-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        try
        {
            var transcript = Path.Combine(home, "s1.log");
            var events = new SessionEvents(home);
            var stdout = new StringReader(string.Join('\n', Init, Status, Start, Delta1, Delta2, Whole, Result));

            var used = await Daoris.Driver.Driver.CaptureStructuredAsync(
                stdout, new StringReader("a warning on stderr"), transcript, "s1", output: null, events,
                new ClaudeStreamJson(), prompt: "take quest #q1", CancellationToken.None);

            var text = File.ReadAllText(transcript);
            Assert.Contains("Capped at 4 per frame.", text);
            Assert.Contains("a warning on stderr", text);
            Assert.DoesNotContain("\"type\"", text);

            var kinds = events.Page("s1").Events.Select(e => e.Kind).ToList();
            Assert.Equal(SessionEventKind.User, kinds[0]);
            Assert.Contains(SessionEventKind.Message, kinds);
            Assert.Equal(SessionEventKind.Turn, kinds[^1]);
            Assert.Equal(new AcpUsage(33056, 1_000_000), used);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void The_adapter_frames_a_persons_message_as_a_user_line_the_harness_reads()
    {
        var framed = new ClaudeCodeAdapter().FrameMessage("cap it — \"4\" per frame");

        using var json = System.Text.Json.JsonDocument.Parse(framed);
        Assert.Equal("user", json.RootElement.GetProperty("type").GetString());
        var content = json.RootElement.GetProperty("message").GetProperty("content")[0];
        Assert.Equal(("text", "cap it — \"4\" per frame"), (content.GetProperty("type").GetString(), content.GetProperty("text").GetString()));
        Assert.DoesNotContain('\n', framed);
    }
}
