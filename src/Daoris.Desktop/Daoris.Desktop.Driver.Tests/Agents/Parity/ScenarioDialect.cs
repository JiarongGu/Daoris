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
        object? Counted(string name, params (string Wire, string Scenario)[] names) =>
            frame.Root.TryGetProperty(name, out var counts) && counts.ValueKind == JsonValueKind.Object
                ? names.Where(each => counts.TryGetProperty(each.Scenario, out _)).ToDictionary(each => each.Wire, each => counts.GetProperty(each.Scenario).GetInt64())
                : null;
        object Assistant(object content) => new
        {
            type = "assistant",
            message = new
            {
                role = "assistant", model = Text("model"), content = new[] { content },
                usage = Counted("ctx", ("input_tokens", "in"), ("output_tokens", "out"), ("cache_read_input_tokens", "read"), ("cache_creation_input_tokens", "write")),
            },
        };
        object Result(bool failed)
        {
            var result = new Dictionary<string, object?>
            {
                ["type"] = "result", ["subtype"] = Text("subtype") ?? (failed ? "error_during_execution" : "success"), ["is_error"] = failed,
                ["result"] = failed ? Text("why") : Text("text"),
            };
            if (frame.Root.TryGetProperty("denied", out var denied))
            {
                result["permission_denials"] = denied.EnumerateArray()
                    .Select(each => new { tool_name = each.GetProperty("name").GetString(), tool_use_id = each.GetProperty("id").GetString() }).ToArray();
            }

            if (Counted("usage", ("input_tokens", "in"), ("output_tokens", "out"), ("cache_read_input_tokens", "read"), ("cache_creation_input_tokens", "write")) is { } usage)
            {
                result["usage"] = usage;
            }

            if (frame.Root.TryGetProperty("byModel", out var byModel))
            {
                result["modelUsage"] = byModel.EnumerateObject().ToDictionary(
                    each => each.Name,
                    each => new
                    {
                        inputTokens = each.Value.GetProperty("in").GetInt64(), outputTokens = each.Value.GetProperty("out").GetInt64(),
                        cacheReadInputTokens = each.Value.GetProperty("read").GetInt64(), cacheCreationInputTokens = each.Value.GetProperty("write").GetInt64(),
                        contextWindow = each.Value.GetProperty("window").GetInt64(),
                    });
            }

            return result;
        }

        object frameOf = t switch
        {
            // The agent's own initialization, or a later status frame that only carries an id.
            "start" when frame.Root.TryGetProperty("announced", out var announced) && announced.ValueKind == JsonValueKind.False =>
                new { type = "system", subtype = "status", session_id = Text("id") },
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
            // The tool's own message, which no model wrote, saying the model it is handed is unknown.
            "refused" => new { type = "assistant", error = "model_not_found", message = new { role = "assistant", model = "<synthetic>", content = Array.Empty<object>() } },
            "end" when Text("kind") is not null => Result(failed: true),
            "end" => Result(failed: false),
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
