using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// A conversation written as Claude Code keeps one: a JSON line per message in <c>projects/&lt;tree's folder&gt;/&lt;id&gt;.jsonl</c> under
/// its home, each chained to the one before by its <c>parentUuid</c>, which both doors' <c>--resume</c> and <c>session/load</c> read there
/// (the folder <see cref="ClaudeConversationCarry"/> copies). The host's words and the tool results are user lines, the agent's words and calls
/// one assistant line, its model <c>&lt;synthetic&gt;</c> as Claude Code marks a message no model made.
/// <para>What its format needs added, which <see cref="Read"/> returns too: a host line first where the history opens on the agent's turn, since
/// the model's first message is the user's, and an error result after a call no result answered, since a call left open is refused.</para>
/// </summary>
/// <param name="tree">The full path of the working directory the conversation runs in, which names its folder.</param>
/// <param name="at">When it is written: each line is stamped a millisecond after the one before.</param>
internal sealed class ClaudeSeedWriter(string tree, DateTimeOffset at) : IConversationSeedWriter
{
    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The words of the host line added where the history opens on the agent's turn.</summary>
    internal const string Opening = "(The conversation so far.)";

    /// <summary>The result added after a call no result answered.</summary>
    internal const string Unanswered = "(No result was recorded for this call.)";

    /// <inheritdoc/>
    public AgentSupport SupportFor(string doorId) =>
        doorId is "claude-code" or "claude-code-acp" ? AgentSupport.Supported : AgentSupport.Unsupported;

    /// <inheritdoc/>
    public SeededConversation Write(SeedConversation conversation, string doorId, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        if (SupportFor(doorId) != AgentSupport.Supported) throw new NotSupportedException($"'{doorId}' is not a Claude Code door.");
        if (conversation.Messages.Count == 0) throw new ArgumentException("The conversation has no message.", nameof(conversation));
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var lines = new StringBuilder();
        string? parent = null;
        var count = 0;
        var open = new List<string>();
        void Line(string type, JsonObject message)
        {
            var uuid = Uuid(sessionId, count);
            var line = new JsonObject
            {
                ["parentUuid"] = parent, ["isSidechain"] = false, ["userType"] = "external", ["cwd"] = tree, ["sessionId"] = sessionId,
                ["type"] = type, ["message"] = message, ["uuid"] = uuid,
                ["timestamp"] = at.AddMilliseconds(count).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            };
            lines.Append(line.ToJsonString(Compact)).Append('\n');
            parent = uuid;
            count++;
        }

        void Answer()
        {
            foreach (var id in open) Line("user", User(new JsonArray(Result(id, Unanswered, error: true))));
            open.Clear();
        }

        if (conversation.Messages[0].Role != SeedRole.Host) Line("user", User(Opening));
        var n = 0;
        foreach (var message in conversation.Messages)
        {
            n++;
            if (message.Role != SeedRole.Tool) Answer();
            switch (message.Role)
            {
                case SeedRole.Host:
                    var words = message.Parts.OfType<SeedText>().Select(p => p.Text).ToList();
                    Line("user", words.Count == 1 ? User(words[0]) : User(new JsonArray([.. words.Select(w => (JsonNode)new JsonObject { ["type"] = "text", ["text"] = w })])));
                    break;
                case SeedRole.Agent:
                    var content = new JsonArray();
                    foreach (var part in message.Parts)
                    {
                        if (part is SeedText text) content.Add(new JsonObject { ["type"] = "text", ["text"] = text.Text });
                        else if (part is SeedToolCall call)
                        {
                            content.Add(new JsonObject { ["type"] = "tool_use", ["id"] = call.CallId, ["name"] = call.Name, ["input"] = Input(call.Arguments) });
                            open.Add(call.CallId);
                        }
                    }

                    Line("assistant", new JsonObject
                    {
                        ["id"] = $"msg_seed_{n}", ["type"] = "message", ["role"] = "assistant", ["model"] = "<synthetic>", ["content"] = content,
                        ["stop_reason"] = open.Count > 0 ? "tool_use" : "end_turn", ["stop_sequence"] = null,
                        ["usage"] = new JsonObject { ["input_tokens"] = 0, ["output_tokens"] = 0 },
                    });
                    break;
                case SeedRole.Tool:
                    var results = new JsonArray();
                    foreach (var result in message.Parts.OfType<SeedToolResult>())
                    {
                        results.Add(Result(result.CallId, result.Text, result.IsError));
                        open.Remove(result.CallId);
                    }

                    Line("user", User(results));
                    break;
            }
        }

        Answer();
        var folder = ClaudeConversationHome.Home.Projects!;
        return new SeededConversation(sessionId, [new ContextFile($"{folder.Folder}/{folder.Flatten(tree)}/{sessionId}.jsonl", lines.ToString())]);
    }

    /// <inheritdoc/>
    public SeedConversation Read(SeededConversation seeded)
    {
        ArgumentNullException.ThrowIfNull(seeded);
        var messages = new List<SeedMessage>();
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
                    throw new FormatException($"'{file.Name}' is not Claude Code's conversation: {error.Message}", error);
                }

                if (line["message"] is not JsonObject message) continue;
                var type = (string?)line["type"];
                if (type == "assistant" && message["content"] is JsonArray said)
                {
                    List<SeedPart> parts = [.. said.OfType<JsonObject>().Select(Part).OfType<SeedPart>()];
                    if (parts.Count > 0) messages.Add(new SeedMessage(SeedRole.Agent, parts));
                }
                else if (type == "user")
                {
                    if (message["content"] is JsonValue words) messages.Add(SeedMessage.Host((string)words!));
                    else if (message["content"] is JsonArray blocks)
                    {
                        var results = blocks.OfType<JsonObject>().Where(b => (string?)b["type"] == "tool_result").Select(ResultOf).ToList<SeedPart>();
                        if (results.Count > 0) messages.Add(new SeedMessage(SeedRole.Tool, results));
                        else messages.Add(new SeedMessage(SeedRole.Host, [.. blocks.OfType<JsonObject>().Select(b => (SeedPart)new SeedText((string?)b["text"] ?? ""))]));
                    }
                }
            }
        }

        if (messages.Count == 0) throw new FormatException("No message of Claude Code's conversation was found.");
        return new SeedConversation(messages);
    }

    private static SeedPart? Part(JsonObject block) => (string?)block["type"] switch
    {
        "text" => new SeedText((string?)block["text"] ?? ""),
        "tool_use" => new SeedToolCall((string?)block["id"] ?? "", (string?)block["name"] ?? "", block["input"]?.ToJsonString(Compact) ?? "{}"),
        _ => null,
    };

    private static SeedToolResult ResultOf(JsonObject block)
    {
        var content = block["content"] switch
        {
            JsonValue value => (string?)value ?? "",
            JsonArray parts => string.Concat(parts.OfType<JsonObject>().Select(p => (string?)p["text"] ?? "")),
            _ => "",
        };
        return new SeedToolResult((string?)block["tool_use_id"] ?? "", content, block["is_error"] is JsonValue error && (bool)error);
    }

    private static JsonObject User(JsonNode content) => new() { ["role"] = "user", ["content"] = content };

    private static JsonObject User(string words) => User(JsonValue.Create(words)!);

    private static JsonObject Result(string id, string text, bool error)
    {
        var result = new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = id, ["content"] = text };
        if (error) result["is_error"] = true;
        return result;
    }

    /// <summary>A call's arguments as the object Claude Code's <c>input</c> holds, held under <c>input</c> where they are no object.</summary>
    private static JsonNode Input(string arguments)
    {
        try
        {
            if (JsonNode.Parse(arguments) is JsonObject input) return input;
        }
        catch (JsonException)
        {
        }

        return new JsonObject { ["input"] = arguments };
    }

    /// <summary>A line's id, the same for the same session and place, so two writes of one conversation are the same files.</summary>
    private static string Uuid(string session, int place) =>
        new Guid(MD5.HashData(Encoding.UTF8.GetBytes(session + "/" + place.ToString(CultureInfo.InvariantCulture)))).ToString("D");
}
