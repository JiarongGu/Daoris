using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The Daoris path's reader of the tests' scenario vocabulary (the agent-lyntai design §6.3, 1b-i): each scenario line said in Claude Code's
/// <c>stream-json</c> and read by today's native reader of it, so the Daoris path is today's capture and reader, never one written to
/// match the layer's. A line that is no scenario frame is handed on as written.
/// </summary>
internal sealed class ScenarioDialect : INativeDialect
{
    public IStreamMapper Mapper() => new Translated(((INativeDialect)TestDoors.StreamJson).Mapper());

    /// <summary>A scenario line as Claude Code's stream says the same thing.</summary>
    internal static string Claude(string line)
    {
        var frame = NativeFrame.Of(line);
        if (!frame.IsObject || frame.Text("t") is not { } t) return line;
        string? Text(string name) => frame.Text(name);
        object Assistant(object content) => new { type = "assistant", message = new { role = "assistant", content = new[] { content } } };
        object frameOf = t switch
        {
            "start" => new { type = "system", subtype = "init", session_id = Text("id"), cwd = ".", tools = Array.Empty<string>(), model = "m" },
            "text" => Assistant(new { type = "text", text = Text("text") }),
            "think" => Assistant(new { type = "thinking", thinking = Text("text") }),
            "call" => Assistant(new { type = "tool_use", id = Text("id"), name = Text("name"), input = JsonDocument.Parse(Text("args") ?? "{}").RootElement }),
            "result" => new
            {
                type = "user",
                message = new
                {
                    role = "user",
                    content = new[]
                    {
                        new { type = "tool_result", tool_use_id = Text("id"), content = Text("text") ?? "", is_error = frame.Root.TryGetProperty("error", out var error) && error.GetBoolean() },
                    },
                },
            },
            "end" when Text("kind") is not null =>
                new { type = "result", subtype = Text("subtype") ?? "error_during_execution", is_error = true, result = Text("why") },
            "end" => new { type = "result", subtype = Text("subtype") ?? "success", is_error = false, result = Text("text") },
            _ => line,
        };
        return frameOf is string same ? same : JsonSerializer.Serialize(frameOf);
    }

    private sealed class Translated(IStreamMapper claude) : IStreamMapper
    {
        public StreamMapped Read(NativeFrame frame) => claude.Read(NativeFrame.Of(Claude(frame.Line)));

        public AcpUsage? Usage => claude.Usage;

        public string? Conversation => claude.Conversation;

        public IStreamsReader? Beside(ISessionStreams streams, Action<string> say) => claude.Beside(streams, say);
    }
}
