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

    /// <summary>
    /// Each message's words carry the message's id, streamed or whole, so two messages in a row are two
    /// on the page as well (found looking at CONSOLE2 on the protocol door, which had the same gap).
    /// </summary>
    [Fact]
    public void A_messages_words_carry_its_id_streamed_or_whole()
    {
        const string other = """{"type":"assistant","message":{"id":"msg_2","content":[{"type":"text","text":"And then."}]}}""";

        var (_, events, _) = Map(Init, Start, Delta1, Delta2, Whole, other, Result);

        Assert.Equal(["msg_1", "msg_1", "msg_2"],
            events.Where(e => e.Kind == SessionEventKind.Message).Select(e => e.Id));
    }

    /// <summary>
    /// REV3: "something the wire said that this build has no kind for — kept, never dropped" held for
    /// frames and not for a message's BLOCKS: a redacted thought, a server tool or an image yielded no
    /// line and no event. The protocol door keeps its unknowns raw; so does this one now.
    /// </summary>
    [Fact]
    public void A_block_this_build_has_no_kind_for_is_kept_raw_under_its_own_name()
    {
        const string redacted = """
            {"type":"assistant","message":{"id":"msg_9","content":[{"type":"redacted_thinking","data":"opaque"},{"type":"text","text":"Done."}]}}
            """;

        var (_, events, _) = Map(Init, redacted, Result);

        var raw = Assert.Single(events, e => e.Kind == SessionEventKind.Raw);
        Assert.Equal("redacted_thinking", raw.Title);
        Assert.Contains("opaque", raw.Raw);
        Assert.Equal("Done.", Assert.Single(events, e => e.Kind == SessionEventKind.Message).Text);
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

    /// <summary>
    /// CONV5: a turn's tokens are the result's own totals — the probe's tool turn, whose two calls the
    /// result sums. The streamed messages under-count the output (16 and 4 against the result's 80), so
    /// the turn never adds them up itself.
    /// </summary>
    [Fact]
    public void A_turn_carries_the_tokens_its_result_reported_for_the_whole_turn()
    {
        const string first = """{"type":"assistant","message":{"id":"msg_a","content":[{"type":"tool_use","id":"t1","name":"Read","input":{"file_path":"a.md"}}],"usage":{"input_tokens":2,"cache_creation_input_tokens":16605,"cache_read_input_tokens":17228,"output_tokens":16}}}""";
        const string second = """{"type":"assistant","message":{"id":"msg_b","content":[{"type":"text","text":"hello"}],"usage":{"input_tokens":2,"cache_creation_input_tokens":112,"cache_read_input_tokens":33833,"output_tokens":4}}}""";
        const string result = """{"type":"result","subtype":"success","is_error":false,"num_turns":2,"duration_ms":4751,"usage":{"input_tokens":4,"cache_creation_input_tokens":16717,"cache_read_input_tokens":51061,"output_tokens":80,"iterations":[{"input_tokens":2,"output_tokens":4,"cache_read_input_tokens":33833,"cache_creation_input_tokens":112}]},"modelUsage":{"some-model":{"contextWindow":1000000}}}""";

        var (_, events, _) = Map(first, second, result);

        var turn = Assert.Single(events, e => e.Kind == SessionEventKind.Turn);
        Assert.Equal(new TurnTokens(Input: 4, Output: 80, CacheRead: 51061, CacheWrite: 16717), turn.Tokens);
    }

    /// <summary>
    /// USAGECAP1a2 (D179 point 2): a turn's models, for the usage history alone. The model is each top-level answer's
    /// <c>message.model</c>, and the split <c>result.modelUsage</c> by model, taken where it agrees with the turn's total; a helper
    /// model's tokens beside it, outside the total, make the turn not whole. Neither reaches the session's record (D24, TOOL3).
    /// </summary>
    /// <remarks>
    /// 🔴 No frame on this machine holds <c>message.model</c> or <c>modelUsage</c>'s counts: the lines are built from the Messages
    /// API's message shape and the SDK's declared <c>ModelUsage</c> (<c>inputTokens</c>, <c>outputTokens</c>,
    /// <c>cacheReadInputTokens</c>, <c>cacheCreationInputTokens</c>, <c>contextWindow</c>, <c>costUSD</c>), with the probe's tool
    /// turn's totals (stream-json evidence, CONV5). A subagent's answer (<c>parent_tool_use_id</c> set) names no turn's model.
    /// </remarks>
    [Fact]
    public void A_turn_s_models_are_its_answers_model_and_its_model_usage_split_and_neither_reaches_the_record()
    {
        const string first = """{"type":"assistant","message":{"id":"msg_a","model":"claude-opus-4-1","content":[{"type":"tool_use","id":"t1","name":"Read","input":{"file_path":"a.md"}}],"usage":{"input_tokens":2}},"parent_tool_use_id":null}""";
        const string child = """{"type":"assistant","message":{"id":"msg_c","model":"claude-sonnet-4-5","content":[{"type":"text","text":"a subagent"}]},"parent_tool_use_id":"t1"}""";
        const string second = """{"type":"assistant","message":{"id":"msg_b","model":"claude-opus-4-1","content":[{"type":"text","text":"hello"}],"usage":{"input_tokens":2}},"parent_tool_use_id":null}""";
        const string result = """{"type":"result","subtype":"success","is_error":false,"usage":{"input_tokens":4,"cache_creation_input_tokens":16717,"cache_read_input_tokens":51061,"output_tokens":80},"modelUsage":{"claude-opus-4-1":{"inputTokens":4,"outputTokens":80,"cacheReadInputTokens":51061,"cacheCreationInputTokens":16717,"webSearchRequests":0,"costUSD":0.14,"contextWindow":1000000},"claude-haiku-3-5":{"inputTokens":909,"outputTokens":30,"cacheReadInputTokens":0,"cacheCreationInputTokens":0,"costUSD":0.001,"contextWindow":200000}}}""";

        var (_, events, _) = Map(Init, first, child, second, result);

        var turn = Assert.Single(events, e => e.Kind == SessionEventKind.Turn);
        var models = turn.Models!;
        Assert.Equal(("claude-opus-4-1", (bool?)false, TurnModels.FromModelUsage), (models.Model, models.Whole, models.From));
        Assert.Equal(
            [("claude-haiku-3-5", new TurnTokens(909, 30, 0, 0)), ("claude-opus-4-1", new TurnTokens(4, 80, 51061, 16717))],
            models.Split!.Select(each => (each.Model, each.Tokens)));

        // The record keeps the counts and no model's name.
        var line = System.Text.Json.JsonSerializer.Serialize(turn, SessionEventText.Json);
        Assert.Contains("51061", line, StringComparison.Ordinal);
        Assert.DoesNotContain("claude-", line, StringComparison.Ordinal);
        Assert.DoesNotContain("models", line, StringComparison.OrdinalIgnoreCase);
    }

    // ——— MODELROLE1a (D205 §3.2, §3.3): what Claude Code 2.1.309 printed on the owner's install for `--model`
    // (docs/2026-10-23-model-effort-doors-evidence.md), trimmed of fields this reader does not read.

    /// <summary>
    /// <c>--model sonnet</c>, a real turn: the alias resolved, and the answer and <c>modelUsage</c> name the id that ran, the
    /// tool's own side model beside it.
    /// </summary>
    [Fact]
    public void A_measured_turn_on_an_alias_names_the_id_that_ran()
    {
        const string init = """{"type":"system","subtype":"init","session_id":"c2","model":"claude-sonnet-5-5","permissionMode":"acceptEdits","claude_code_version":"2.1.309","per_turn_effort_active":true}""";
        const string answer = """{"type":"assistant","message":{"model":"claude-sonnet-5-5","id":"msg_1","type":"message","role":"assistant","content":[{"type":"text","text":"OK"}],"usage":{"input_tokens":2,"cache_creation_input_tokens":9768,"cache_read_input_tokens":17246,"output_tokens":4}},"parent_tool_use_id":null,"session_id":"c2"}""";
        const string result = """{"type":"result","subtype":"success","is_error":false,"api_error_status":null,"num_turns":1,"result":"OK","stop_reason":"end_turn","terminal_reason":"completed","usage":{"input_tokens":2,"cache_creation_input_tokens":9768,"cache_read_input_tokens":17246,"output_tokens":4},"modelUsage":{"claude-haiku-5-5":{"inputTokens":1179,"outputTokens":16,"cacheReadInputTokens":0,"cacheCreationInputTokens":0,"costUSD":0.0001259,"contextWindow":1000000,"canonicalModel":"claude-haiku-5-5"},"claude-sonnet-5-5":{"inputTokens":2,"outputTokens":4,"cacheReadInputTokens":17246,"cacheCreationInputTokens":9768,"costUSD":0.0408406,"contextWindow":1000000,"canonicalModel":"claude-sonnet-5-5"}},"session_id":"c2"}""";

        var (_, events, _) = Map(init, answer, result);

        var models = Assert.Single(events, e => e.Kind == SessionEventKind.Turn).Models!;
        Assert.Equal(("claude-sonnet-5-5", (bool?)false, TurnModels.FromModelUsage), (models.Model, models.Whole, models.From));
        Assert.Equal(["claude-haiku-5-5", "claude-sonnet-5-5"], models.Split!.Select(each => each.Model));
    }

    /// <summary>
    /// 🔴 A model the account cannot run, as measured: <c>system/init</c> names the asked model unchecked, then the tool's own
    /// message (<c>&lt;synthetic&gt;</c>, <c>error: "model_not_found"</c>, 404) and a failed result with no counts, after
    /// the prompt was handed, with no output and no tool use. The words reach the transcript as the turn's failure, and
    /// <c>&lt;synthetic&gt;</c> is no model that ran.
    /// </summary>
    [Fact]
    public void A_measured_model_refusal_is_the_turn_s_failure_in_the_tool_s_words_and_names_no_model()
    {
        const string words = "There's an issue with the selected model (claude-nonexistent-9). It may not exist or you may not have access to it. Run --model to pick a different model.";
        const string init = """{"type":"system","subtype":"init","session_id":"c3","model":"claude-nonexistent-9","permissionMode":"acceptEdits","claude_code_version":"2.1.309"}""";
        const string refusal = $$"""{"type":"assistant","message":{"id":"3ef8f451","model":"<synthetic>","role":"assistant","stop_reason":"stop_sequence","type":"message","usage":{"input_tokens":0,"output_tokens":0,"cache_creation_input_tokens":0,"cache_read_input_tokens":0},"content":[{"type":"text","text":"{{words}}"}]},"parent_tool_use_id":null,"session_id":"c3","error":"model_not_found","is_api_error_message":true,"api_error_status":404}""";
        const string result = $$"""{"type":"result","subtype":"success","is_error":true,"api_error_status":404,"num_turns":1,"stop_reason":"stop_sequence","terminal_reason":"api_error","usage":{"input_tokens":0,"cache_creation_input_tokens":0,"cache_read_input_tokens":0,"output_tokens":0},"modelUsage":{},"result":"{{words}}","session_id":"c3"}""";

        var (lines, events, _) = Map(init, refusal, result);

        Assert.Contains($"— the turn failed (success): {words}", lines);
        Assert.DoesNotContain(events, e => e.Kind == SessionEventKind.Tool);
        var models = Assert.Single(events, e => e.Kind == SessionEventKind.Turn).Models;
        Assert.NotEqual("<synthetic>", models?.Model);
        Assert.Empty(models!.Reported);
    }

    /// <summary>
    /// USAGECAP1a2: a model switched mid-session splits by turn. The second turn's answers name the new model, and a
    /// <c>modelUsage</c> that ran on from the first turn is read as the second's own less the first's: the process's running
    /// total, which only agrees with the turn's total so. The same turn reported as its own reads the same.
    /// </summary>
    /// <remarks>🔴 Built as the test above: whether <c>modelUsage</c> is the turn's or the process's is not yet recorded.</remarks>
    [Fact]
    public void A_model_switched_mid_session_splits_by_turn_whether_model_usage_runs_on_or_is_the_turn_s_own()
    {
        const string opus = """{"type":"assistant","message":{"id":"msg_1","model":"claude-opus-4-1","content":[{"type":"text","text":"one"}]},"parent_tool_use_id":null}""";
        const string firstResult = """{"type":"result","subtype":"success","is_error":false,"usage":{"input_tokens":4,"cache_creation_input_tokens":16717,"cache_read_input_tokens":51061,"output_tokens":80},"modelUsage":{"claude-opus-4-1":{"inputTokens":4,"outputTokens":80,"cacheReadInputTokens":51061,"cacheCreationInputTokens":16717}}}""";
        const string sonnet = """{"type":"assistant","message":{"id":"msg_2","model":"claude-sonnet-4-5","content":[{"type":"text","text":"two"}]},"parent_tool_use_id":null}""";
        const string runningResult = """{"type":"result","subtype":"success","is_error":false,"usage":{"input_tokens":2,"cache_creation_input_tokens":300,"cache_read_input_tokens":60000,"output_tokens":10},"modelUsage":{"claude-opus-4-1":{"inputTokens":4,"outputTokens":80,"cacheReadInputTokens":51061,"cacheCreationInputTokens":16717},"claude-sonnet-4-5":{"inputTokens":2,"outputTokens":10,"cacheReadInputTokens":60000,"cacheCreationInputTokens":300}}}""";
        const string ownResult = """{"type":"result","subtype":"success","is_error":false,"usage":{"input_tokens":2,"cache_creation_input_tokens":300,"cache_read_input_tokens":60000,"output_tokens":10},"modelUsage":{"claude-sonnet-4-5":{"inputTokens":2,"outputTokens":10,"cacheReadInputTokens":60000,"cacheCreationInputTokens":300}}}""";

        foreach (var (second, from) in new[] { (runningResult, TurnModels.FromModelUsage + TurnModels.Since), (ownResult, TurnModels.FromModelUsage) })
        {
            var (_, events, _) = Map(opus, firstResult, sonnet, second);

            var turns = events.Where(e => e.Kind == SessionEventKind.Turn).Select(e => e.Models!).ToList();
            Assert.Equal([("claude-opus-4-1", (bool?)true), ("claude-sonnet-4-5", true)], turns.Select(each => (each.Model, each.Whole)));
            Assert.Equal(from, turns[1].From);
            Assert.Equal([("claude-sonnet-4-5", new TurnTokens(2, 10, 60000, 300))], turns[1].Split!.Select(each => (each.Model, each.Tokens)));
        }
    }

    /// <summary>Absent is never zero: a result that reported no usage leaves the turn's tokens unknown.</summary>
    [Fact]
    public void A_result_that_reported_no_usage_leaves_the_turns_tokens_unknown()
    {
        var (_, events, _) = Map("""{"type":"result","subtype":"error_during_execution","is_error":true,"result":"Invalid API key"}""");

        Assert.Null(Assert.Single(events, e => e.Kind == SessionEventKind.Turn).Tokens);
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

        // The ending is said by the call's name, never its id (the "?"s the console printed).
        Assert.Equal(["→ Edit src/chunk.rs", "  ✓ Edit src/chunk.rs"], lines);
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
        Assert.Equal(["— the agent refused a control request: no turn to interrupt"], lines);
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

    // 🔴 UNBLOCK5: WRITTEN FROM THE MAKER'S REFERENCE, NOT PRINTED BY A BINARY. The Agent SDK's TypeScript
    // reference, read 2026-10-01, types a denial as `SDKPermissionDeniedMessage` { type: "system", subtype:
    // "permission_denied", tool_name, tool_use_id, agent_id?, decision_reason_type?, decision_reason?, message,
    // uuid, session_id }, emitted by a `-p` run with no permission host since 2.1.223 and best-effort, and the
    // result's `permission_denials` as `SDKPermissionDenial` { tool_name, tool_use_id, tool_input }, "the
    // authoritative record". No turn on this machine has shown either frame yet (D122 §3.10); when one does,
    // these lines are replaced by the ones it printed.
    private const string PushCall = """{"type":"assistant","message":{"id":"msg_7","content":[{"type":"tool_use","id":"toolu_7","name":"Bash","input":{"command":"git push origin main","description":"Push the branch"}}]},"session_id":"c1","parent_tool_use_id":null}""";
    private const string FetchCall = """{"type":"assistant","message":{"id":"msg_8","content":[{"type":"tool_use","id":"toolu_8","name":"WebFetch","input":{"url":"https://example.com/notes"}}]},"session_id":"c1","parent_tool_use_id":null}""";
    private const string PushDenied = """{"type":"system","subtype":"permission_denied","tool_name":"Bash","tool_use_id":"toolu_7","decision_reason_type":"rule","decision_reason":"Permission to use Bash with command git push origin main has been denied.","message":"Permission to use Bash with command git push origin main has been denied.","uuid":"00000000-0000-4000-8000-000000000007","session_id":"c1"}""";
    private const string PushResult = """{"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_7","content":"Permission to use Bash with command git push origin main has been denied.","is_error":true}]},"session_id":"c1","parent_tool_use_id":null}""";
    private const string FetchResult = """{"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_8","content":"Claude requested permissions to use WebFetch, but you haven't granted it yet.","is_error":true}]},"session_id":"c1","parent_tool_use_id":null}""";
    private const string DeniedTurn = """{"type":"result","subtype":"success","is_error":false,"result":"I could not push.","num_turns":3,"stop_reason":"end_turn","permission_denials":[{"tool_name":"Bash","tool_use_id":"toolu_7","tool_input":{"command":"git push origin main"}},{"tool_name":"WebFetch","tool_use_id":"toolu_8","tool_input":{"url":"https://example.com/notes"}}],"session_id":"c1"}""";

    /// <summary>
    /// UNBLOCK5, the mapper's table: the harness's denial is the call's refusal in the record, named by its
    /// tool and what decided it, and the failed result that follows reads <c>refused</c> as the protocol
    /// door's does (HELP4). The console keeps the wire's own word beside the refusal's line.
    /// </summary>
    [Fact]
    public void A_denial_the_harness_reports_is_the_calls_refusal_and_its_failed_result_says_refused()
    {
        var (lines, events, _) = Map(PushCall, PushDenied, PushResult);

        Assert.Equal(3, events.Count);
        var refusal = events[1];
        Assert.Equal((SessionEventKind.Tool, "toolu_7", "refused"), (refusal.Kind, refusal.Id, refusal.Status));
        Assert.Equal(("execute", "Bash", "rule"), (refusal.ToolKind, refusal.ToolName, refusal.RefusedBy));
        // The refusal is a status, never a new title: the card keeps the name its call gave it.
        Assert.Null(refusal.Title);
        Assert.Equal(("toolu_7", "refused"), (events[2].Id, events[2].Status));

        Assert.Equal(["→ Push the branch", "  permission refused: Push the branch (rule)", "  ✗ Push the branch failed"], lines);
    }

    /// <summary>
    /// The result's list is the authoritative record, and the message is best-effort: a denial the message
    /// missed is added from the list, before the turn ends, and one already said is never said twice.
    /// </summary>
    [Fact]
    public void The_results_list_adds_a_denial_the_message_missed_and_never_repeats_one()
    {
        var (lines, events, _) = Map(PushCall, FetchCall, PushDenied, PushResult, FetchResult, DeniedTurn);

        var refused = events.Where(e => e is { Kind: SessionEventKind.Tool, Status: "refused", ToolName: not null }).ToList();
        Assert.Equal(["toolu_7", "toolu_8"], refused.Select(e => e.Id));
        Assert.Equal(("fetch", "WebFetch", (string?)null), (refused[1].ToolKind, refused[1].ToolName, refused[1].RefusedBy));
        // Said before the turn's end, so the refusal is part of the turn it happened in.
        Assert.True(events.IndexOf(refused[1]) < events.FindIndex(e => e.Kind == SessionEventKind.Turn));
        // A result that failed before the list named it is a failure until then; the list turns it.
        Assert.Equal("failed", events.Single(e => e.Id == "toolu_8" && e.Content is not null).Status);
        Assert.Single(lines, line => line == "  permission refused: Fetch https://example.com/notes");
    }

    /// <summary>
    /// A denial of a call the session's record does not hold (a subagent's, whose calls run beside the session,
    /// CONSOLE3c) is said on the console and is no card in the conversation. A frame of another shape than the
    /// reference's (no call id) is an absence, never a throw; a decider that is not a string is unsaid.
    /// </summary>
    [Fact]
    public void A_denial_of_a_call_the_record_does_not_hold_is_said_on_the_console_only()
    {
        const string subagents = """{"type":"system","subtype":"permission_denied","tool_name":"Bash","tool_use_id":"toolu_sub","agent_id":"agent_1","decision_reason_type":"asyncAgent","message":"denied","uuid":"u1","session_id":"c1"}""";
        const string shapeless = """{"type":"system","subtype":"permission_denied","tool_name":"Bash","message":"denied","uuid":"u2","session_id":"c1"}""";
        const string oddDecider = """{"type":"system","subtype":"permission_denied","tool_name":"Bash","tool_use_id":"toolu_7","decision_reason_type":7,"message":"denied","uuid":"u3","session_id":"c1"}""";

        var (lines, events, _) = Map(subagents, shapeless);
        Assert.Empty(events);
        Assert.Equal(["  permission refused: Bash (asyncAgent)"], lines);

        var (_, odd, _) = Map(PushCall, oddDecider);
        var refusal = Assert.Single(odd, e => e.Status == "refused");
        Assert.Null(refusal.RefusedBy);
    }

    /// <summary>
    /// The pipe door's capture, end to end (UNBLOCK5): the harness's denial reaches the machine log as one ask,
    /// named by its tool, its kind and what decided it, and the command and its words never do.
    /// </summary>
    [Fact]
    public async Task The_capture_turns_a_denial_into_one_ask_in_the_machine_log()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-stream-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        try
        {
            var events = new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions));
            using var log = new MachineLog(home, "desktop");
            using var client = new ServiceClient("http://ledger.test", null, new HttpClient());
            using var watch = new SessionLog(log, client, events);
            var stdout = new StringReader(string.Join('\n', Init, PushCall, PushDenied, PushResult, DeniedTurn));

            await HarnessCapture.CaptureStructuredAsync(
                stdout, new StringReader(""), Path.Combine(home, "s1.log"), "s1", output: null, events,
                new ClaudeStreamJson(), prompt: "push it", CancellationToken.None);

            var written = string.Join('\n', Directory.GetFiles(DaorisHome.Entry(home, HomeEntry.Logs)).Select(StubFile.Text));
            var asks = written.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => System.Text.Json.JsonDocument.Parse(line).RootElement)
                .Where(line => line.GetProperty("event").GetString() == "permission.refused")
                .Select(line => line.GetProperty("data"))
                .ToList();
            var ask = Assert.Single(asks);
            Assert.Equal(("s1", "Bash", "execute", "rule"), (
                ask.GetProperty("session").GetString(), ask.GetProperty("tool").GetString(),
                ask.GetProperty("kind").GetString(), ask.GetProperty("by").GetString()));
            Assert.DoesNotContain("git push", written);
            Assert.DoesNotContain("Push the branch", written);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>
    /// TOOL6c (limit-signals evidence §1.1): the native door's <c>rate_limit_event</c> is the account's windows, handed on
    /// apart from the conversation — neither a console line nor an event — and the capture tells its listener, which keeps
    /// it for the walk. A frame with no <c>rate_limit_info</c> object hands on nothing.
    /// </summary>
    [Fact]
    public async Task A_rate_limit_event_is_handed_on_for_the_account_s_windows_and_is_not_the_conversation()
    {
        var info = AccountReadingsTests.Recorded(DateTimeOffset.UtcNow);
        var frame = $$"""{"type":"rate_limit_event","rate_limit_info":{{info}},"uuid":"…","session_id":"c1"}""";

        var mapped = new ClaudeStreamJson().Read(frame);
        Assert.Empty(mapped.Lines);
        Assert.Empty(mapped.Events);
        Assert.Equal("allowed", mapped.Limits!.Value.GetProperty("status").GetString());
        Assert.Null(new ClaudeStreamJson().Read("""{"type":"rate_limit_event","rate_limit_info":"allowed"}""").Limits);
        Assert.Null(new ClaudeStreamJson().Read(Init).Limits);

        var home = Path.Combine(Path.GetTempPath(), "daoris-stream-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        try
        {
            var told = new List<System.Text.Json.JsonElement>();
            var events = new SessionEvents(home);
            await HarnessCapture.CaptureStructuredAsync(
                new StringReader(string.Join('\n', Init, Whole, frame, Result)), new StringReader(""), Path.Combine(home, "s1.log"), "s1",
                output: null, events, new ClaudeStreamJson(), prompt: "take quest #q1", CancellationToken.None, said: told.Add);

            Assert.Equal(0.88, Assert.Single(told).GetProperty("unifiedWindows").GetProperty("five_hour").GetProperty("utilization").GetDouble());
            Assert.DoesNotContain("rate_limit", File.ReadAllText(Path.Combine(home, "s1.log")));
            Assert.DoesNotContain(events.Page("s1").Events, e => e.Kind == SessionEventKind.Raw);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>
    /// AGT3c: what the harness says in its own words is marked apart from its agent's: a line outside its protocol, and a
    /// failed result's words. An agent's text, a turn that ended and a stopped one carry neither.
    /// </summary>
    [Fact]
    public void The_harness_s_own_words_are_marked_apart_from_the_agent_s()
    {
        const string failed = """{"type":"result","subtype":"success","is_error":true,"api_error_status":401,"result":"Failed to authenticate. API Error: 401 API key is invalid."}""";
        const string stopped = """{"type":"result","subtype":"error_during_execution","is_error":true,"result":"Request was aborted.","terminal_reason":"aborted_streaming"}""";
        var mapper = new ClaudeStreamJson();

        Assert.True(mapper.Read("warming up…").Outside);
        Assert.True(mapper.Read("[1, 2]").Outside);
        Assert.Equal("Failed to authenticate. API Error: 401 API key is invalid.", mapper.Read(failed).Failed);
        Assert.Contains("— the turn failed (success): Failed to authenticate. API Error: 401 API key is invalid.", new ClaudeStreamJson().Read(failed).Lines);
        foreach (var line in new[] { Init, Whole, Result, stopped })
        {
            var mapped = new ClaudeStreamJson().Read(line);
            Assert.Equal((false, (string?)null), (mapped.Outside, mapped.Failed));
        }
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

            var used = await HarnessCapture.CaptureStructuredAsync(
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
