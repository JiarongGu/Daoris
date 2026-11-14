using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// A conversation written as Codex keeps a thread: one rollout file under <c>sessions/</c>, filed by the day it is written and named with the
/// thread's id, which <c>session/load</c> finds by that id (the file <see cref="CodexConversationHome"/> carries). Its first line is the
/// thread's <c>session_meta</c>; each message after it is one <c>response_item</c> per part: the host's words a user message, the agent's
/// words an assistant message and each call a <c>function_call</c>, each result a <c>function_call_output</c>.
/// <para>What its format cannot say is added on each line beside the item, which Codex does not read: <c>seed.message</c>, the message a part
/// belongs to, and <c>seed.error</c> on a result that came back failed, since an output carries no failure. An error result is added after a
/// call no result answered, as Claude Code's writer adds one; <see cref="Read"/> returns it too.</para>
/// </summary>
/// <param name="tree">The full path of the working directory the thread runs in.</param>
/// <param name="at">When it is written: the file's day and name, and each line a millisecond after the one before.</param>
internal sealed class CodexSeedWriter(string tree, DateTimeOffset at) : IConversationSeedWriter
{
    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The result added after a call no result answered.</summary>
    internal const string Unanswered = "(No result was recorded for this call.)";

    /// <inheritdoc/>
    public AgentSupport SupportFor(string doorId) => doorId == "codex-acp" ? AgentSupport.Supported : AgentSupport.Unsupported;

    /// <inheritdoc/>
    public SeededConversation Write(SeedConversation conversation, string doorId, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        if (SupportFor(doorId) != AgentSupport.Supported) throw new NotSupportedException($"'{doorId}' is not a Codex door.");
        if (conversation.Messages.Count == 0) throw new ArgumentException("The conversation has no message.", nameof(conversation));
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var lines = new StringBuilder();
        var count = 0;
        string Stamp() => at.AddMilliseconds(count).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        void Line(string type, JsonObject payload, JsonObject? seed = null)
        {
            var line = new JsonObject { ["timestamp"] = Stamp(), ["type"] = type, ["payload"] = payload };
            if (seed is not null) line["seed"] = seed;
            lines.Append(line.ToJsonString(Compact)).Append('\n');
            count++;
        }

        Line("session_meta", new JsonObject
        {
            ["id"] = sessionId, ["timestamp"] = Stamp(), ["cwd"] = tree, ["originator"] = "daoris", ["cli_version"] = "0.0.0", ["source"] = "cli",
            ["model_provider"] = "openai",
        });

        var n = 0;
        var open = new List<string>();
        void Answer()
        {
            foreach (var id in open)
                Line("response_item", Output(id, Unanswered), new JsonObject { ["message"] = ++n, ["error"] = true });
            open.Clear();
        }

        foreach (var message in conversation.Messages)
        {
            if (message.Role != SeedRole.Tool) Answer();
            n++;
            foreach (var part in message.Parts)
            {
                var seed = new JsonObject { ["message"] = n };
                switch (part)
                {
                    case SeedText text:
                        var user = message.Role == SeedRole.Host;
                        Line("response_item", new JsonObject
                        {
                            ["type"] = "message", ["role"] = user ? "user" : "assistant",
                            ["content"] = new JsonArray(new JsonObject { ["type"] = user ? "input_text" : "output_text", ["text"] = text.Text }),
                        }, seed);
                        break;
                    case SeedToolCall call:
                        Line("response_item", new JsonObject { ["type"] = "function_call", ["name"] = call.Name, ["arguments"] = call.Arguments, ["call_id"] = call.CallId }, seed);
                        open.Add(call.CallId);
                        break;
                    case SeedToolResult result:
                        if (result.IsError) seed["error"] = true;
                        Line("response_item", Output(result.CallId, result.Text), seed);
                        open.Remove(result.CallId);
                        break;
                }
            }
        }

        Answer();
        var day = at.UtcDateTime;
        var name = $"sessions/{day:yyyy}/{day:MM}/{day:dd}/rollout-{day.ToString("yyyy-MM-dd'T'HH-mm-ss", CultureInfo.InvariantCulture)}-{sessionId}.jsonl";
        return new SeededConversation(sessionId, [new ContextFile(name, lines.ToString())]);
    }

    /// <inheritdoc/>
    public SeedConversation Read(SeededConversation seeded)
    {
        ArgumentNullException.ThrowIfNull(seeded);
        var messages = new List<SeedMessage>();
        var parts = new List<SeedPart>();
        (SeedRole Role, string Key)? current = null;
        var unmarked = 0;
        void Close()
        {
            if (current is { } was && parts.Count > 0) messages.Add(new SeedMessage(was.Role, [.. parts]));
            parts.Clear();
        }

        foreach (var file in seeded.Files)
        {
            foreach (var text in file.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                JsonObject line;
                try
                {
                    line = JsonNode.Parse(text) as JsonObject ?? throw new FormatException($"'{file.Name}' holds a line that is not an object.");
                }
                catch (JsonException error)
                {
                    throw new FormatException($"'{file.Name}' is not a Codex rollout: {error.Message}", error);
                }

                if ((string?)line["type"] != "response_item" || line["payload"] is not JsonObject item) continue;
                var (role, part) = Part(item, line["seed"]?["error"] is JsonValue failed && (bool)failed);
                if (part is null) continue;
                // A line this writer wrote names its message; one Codex wrote is grouped with the lines of the same speaker beside it.
                var key = line["seed"]?["message"]?.ToJsonString() ?? (current?.Role == role ? current.Value.Key : "codex-" + unmarked++);
                if (current is not { } now || now.Role != role || now.Key != key)
                {
                    Close();
                    current = (role, key);
                }

                parts.Add(part);
            }
        }

        Close();
        if (messages.Count == 0) throw new FormatException("No message of a Codex rollout was found.");
        return new SeedConversation(messages);
    }

    private static (SeedRole, SeedPart?) Part(JsonObject item, bool failed) => (string?)item["type"] switch
    {
        "message" when (string?)item["role"] is "user" or "assistant" =>
            ((string?)item["role"] == "user" ? SeedRole.Host : SeedRole.Agent,
                new SeedText(string.Concat((item["content"] as JsonArray ?? []).OfType<JsonObject>().Select(c => (string?)c["text"] ?? "")))),
        "function_call" => (SeedRole.Agent, new SeedToolCall((string?)item["call_id"] ?? "", (string?)item["name"] ?? "", (string?)item["arguments"] ?? "{}")),
        "function_call_output" => (SeedRole.Tool, new SeedToolResult((string?)item["call_id"] ?? "", item["output"] switch
        {
            JsonValue value => (string?)value ?? "",
            JsonNode other => other.ToJsonString(Compact),
            null => "",
        }, failed)),
        _ => (SeedRole.Host, null),
    };

    private static JsonObject Output(string id, string text) => new() { ["type"] = "function_call_output", ["call_id"] = id, ["output"] = text };
}
