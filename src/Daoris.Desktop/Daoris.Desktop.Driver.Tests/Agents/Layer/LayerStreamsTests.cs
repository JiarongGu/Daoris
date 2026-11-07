using System.Text.Json;
using Daoris.Driver;
using Lyntai.Agents;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a layer run's session runs beside itself (<see cref="LayerStreams"/>): each event a child said, by the call that spawned it, a line of
/// that child's own stream as the native reader says the frame it stands for (<see cref="ClaudeStreams"/>), and every other event the
/// session's own. On events the tests make, beside those frames.
/// </summary>
public sealed class LayerStreamsTests
{
    private static readonly WireReading.ChildSeen Seen = new("a1", ChildSignal.Stream) { Call = "toolu_ag", Task = "Read README first line" };

    private static string Frame(object value) => JsonSerializer.Serialize(value);

    private static LayerStreams Over(IStreamsReader beside) => new(
        beside,
        () => new LayerReadings("native", TimeProvider.System, TimeSpan.FromHours(1), steps: ClaudeDrivenLayer.Registration("claude").Steps));

    [Fact]
    public void A_child_s_calls_and_words_are_its_stream_s_lines_as_the_native_reader_says_its_frames()
    {
        var (native, layer) = (new Recorded(), new Recorded());
        var beside = new ClaudeStreams(native, native.Said);
        var streams = Over(new ClaudeStreams(layer, layer.Said));
        beside.Read(Seen);
        streams.Read(Seen);

        Assert.True(beside.Take(NativeFrame.Of(Frame(new
        {
            type = "assistant", parent_tool_use_id = "toolu_ag",
            message = new { id = "m1", role = "assistant", content = new object[] { new { type = "tool_use", id = "toolu_read", name = "Read", input = new { file_path = "README.md" } } } },
        }))));
        Assert.True(beside.Take(NativeFrame.Of(Frame(new
        {
            type = "user", parent_tool_use_id = "toolu_ag",
            message = new { role = "user", content = new object[] { new { tool_use_id = "toolu_read", type = "tool_result", content = "# console3 room" } } },
        }))));
        Assert.True(beside.Take(NativeFrame.Of(Frame(new
        {
            type = "assistant", parent_tool_use_id = "toolu_ag",
            message = new { id = "m2", role = "assistant", content = new object[] { new { type = "text", text = "first\nsecond" } } },
        }))));
        Assert.True(streams.Take(new ToolCall("Read", """{"file_path":"README.md"}""", "toolu_read") { ParentCallId = "toolu_ag" }));
        Assert.True(streams.Take(new ToolResult("toolu_read", "# console3 room", IsError: false) { ParentCallId = "toolu_ag" }));
        Assert.True(streams.Take(new TextDelta("first\nsecond") { ParentCallId = "toolu_ag" }));

        Assert.Equal(native.Seen, layer.Seen);
        Assert.Contains(("line", "subagent/a1", "first"), layer.Seen);
        Assert.Contains(("line", "subagent/a1", "→ Read README.md"), layer.Seen);
    }

    [Fact]
    public void The_session_s_own_events_are_no_stream_s_and_a_child_nobody_announced_still_has_its_own()
    {
        var layer = new Recorded();
        var streams = Over(new ClaudeStreams(layer, layer.Said));

        Assert.False(streams.Take(new TextDelta("mine")));
        Assert.False(streams.Take(new ToolCall("Read", "{}", "c1")));
        Assert.False(streams.Take(new AgentTaskStarted("a1", "local_agent", null, "toolu_x")));
        Assert.True(streams.Take(new TextDelta("unannounced") { ParentCallId = "toolu_x" }));

        Assert.Equal(
            [("open", "subagent/toolu_x", "subagent"), ("said", "", "→ subagent: subagent"), ("line", "subagent/toolu_x", "unannounced")],
            layer.Seen);
    }

    /// <summary>The streams and the session's console lines, in the order they were said.</summary>
    private sealed class Recorded : ISessionStreams
    {
        public List<(string What, string Stream, string Text)> Seen { get; } = [];

        public void Said(string text) => Seen.Add(("said", "", text));

        public void Open(SessionStream stream) => Seen.Add(("open", stream.Id, stream.Name));

        public void Line(string streamId, string text) => Seen.Add(("line", streamId, text));

        public void End(string streamId, string? state) => Seen.Add(("end", streamId, state ?? ""));
    }
}
