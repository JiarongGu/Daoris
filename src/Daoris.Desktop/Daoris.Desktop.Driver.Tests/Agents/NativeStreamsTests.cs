using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// CONSOLE3c: what a session on the native door runs beside itself, read off <c>stream-json</c> as the
/// probe recorded it (docs/2026-09-30-console3-native-streams-evidence.md): a task announced on a
/// <c>system</c> line, a subagent's lines marked by <c>parent_tool_use_id</c>, a command's output read
/// from the file the harness names, and each ending in the wire's own word.
/// </summary>
public sealed class NativeStreamsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-native-streams-" + Guid.NewGuid().ToString("N")[..8]);

    public NativeStreamsTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Json(object value) => JsonSerializer.Serialize(value);

    private static string System(string subtype, object fields)
    {
        var node = JsonSerializer.SerializeToNode(fields)!.AsObject();
        node["type"] = "system";
        node["subtype"] = subtype;
        return node.ToJsonString();
    }

    [Fact]
    public async Task A_task_and_a_subagent_are_streams_of_their_own_and_end_in_the_wires_word()
    {
        var file = Path.Combine(_root, "b1.output");
        File.WriteAllText(file, "listening on 4200\nready\n");
        var output = new SessionOutput();
        var transcript = Path.Combine(_root, "s1.log");

        var lines = new[]
        {
            System("task_started", new { task_id = "b1", tool_use_id = "toolu_bg", description = "dev server", is_backgrounded = true, task_type = "local_bash" }),
            Json(new
            {
                type = "user",
                message = new { role = "user", content = new object[] { new { tool_use_id = "toolu_bg", type = "tool_result", content = $"Command running in background with ID: b1. Output is being written to: {file}. You will be notified when it completes. To check interim output, use Read on that file path." } } },
                tool_use_result = new { stdout = "", stderr = "", backgroundTaskId = "b1" },
            }),
            System("task_started", new { task_id = "a1", tool_use_id = "toolu_ag", description = "Read README first line", task_type = "local_agent", subagent_type = "general-purpose" }),
            System("background_tasks_changed", new { tasks = Array.Empty<object>() }),
            Json(new
            {
                type = "assistant",
                parent_tool_use_id = "toolu_ag",
                message = new { id = "msg_child", role = "assistant", content = new object[] { new { type = "text", text = "# console3 room" } } },
            }),
            System("task_notification", new { task_id = "a1", tool_use_id = "toolu_ag", status = "completed", summary = "# console3 room" }),
            System("task_updated", new { task_id = "b1", patch = new { status = "killed" } }),
            System("task_notification", new { task_id = "b1", tool_use_id = "toolu_bg", status = "stopped", output_file = file }),
        };

        await Daoris.Driver.Driver.CaptureStructuredAsync(
            new StringReader(string.Join('\n', lines)), new StringReader(""), transcript, "s1", output, events: null,
            new ClaudeStreamJson(), prompt: null, CancellationToken.None);

        var streams = output.Streams("s1");
        Assert.Equal(
            [("s1/task/b1", SessionStreamKind.Task, "dev server", "stopped"), ("s1/subagent/a1", SessionStreamKind.Subagent, "Read README first line", "completed")],
            streams.Select(stream => (stream.Key, stream.Kind, stream.Name, stream.State)));
        Assert.All(streams, stream => Assert.False(stream.Live));
        // No request stops one task on this door.
        Assert.All(streams, stream => Assert.False(stream.CanStop));

        Assert.Equal(["listening on 4200", "ready"], output.Tail("s1/task/b1").Lines.Select(line => line.Text));
        Assert.Contains("# console3 room", output.Tail("s1/subagent/a1").Lines.Select(line => line.Text));

        var said = output.Tail("s1").Lines.Select(line => line.Text).ToList();
        Assert.Contains("→ background: dev server", said);
        Assert.Contains("→ subagent: Read README first line", said);
        Assert.Contains("  ✓ subagent: Read README first line", said);
        Assert.Contains("  ■ background: dev server stopped", said);
        // The subagent's words are its stream's, never the session's.
        Assert.DoesNotContain("# console3 room", said);
    }

    /// <summary>A stream still open when the session's output ends is ended with it, and says so.</summary>
    [Fact]
    public async Task A_stream_open_at_the_end_ends_with_the_session()
    {
        var output = new SessionOutput();
        var lines = new[]
        {
            System("task_started", new { task_id = "b2", tool_use_id = "toolu_bg2", description = "watcher", task_type = "local_bash" }),
        };

        await Daoris.Driver.Driver.CaptureStructuredAsync(
            new StringReader(string.Join('\n', lines)), new StringReader(""), Path.Combine(_root, "s2.log"), "s2", output,
            events: null, new ClaudeStreamJson(), prompt: null, CancellationToken.None);

        var stream = Assert.Single(output.Streams("s2"));
        Assert.Equal((false, SessionStream.SessionEnded), (stream.Live, stream.State));
        Assert.Contains("  background: watcher ended with its session", output.Tail("s2").Lines.Select(line => line.Text));
    }

    /// <summary>With nowhere to keep streams, every line is the session's, as before CONSOLE3c.</summary>
    [Fact]
    public async Task Without_a_console_a_childs_line_is_the_sessions_as_before()
    {
        var transcript = Path.Combine(_root, "s3.log");
        var child = Json(new
        {
            type = "assistant",
            parent_tool_use_id = "toolu_ag",
            message = new { id = "msg_child", role = "assistant", content = new object[] { new { type = "text", text = "child words" } } },
        });

        await Daoris.Driver.Driver.CaptureStructuredAsync(
            new StringReader(child), new StringReader(""), transcript, "s3", output: null, events: null,
            new ClaudeStreamJson(), prompt: null, CancellationToken.None);

        Assert.Contains("child words", File.ReadAllText(transcript));
    }
}
