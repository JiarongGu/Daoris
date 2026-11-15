using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// A conversation written as dsh keeps a session (format v4, read from its open-source packages and resumed on the real program): a
/// directory <c>sessions/--&lt;cwd&gt;--/&lt;id&gt;/</c> under its home holding <c>session.v4.jsonl</c>, a header line (<c>version</c>, <c>id</c>,
/// <c>createdAt</c>, the tree as <c>cwd</c>, <c>isSeeded</c> false) and one event per line, each with a gap-free <c>seq</c> and a <c>time</c>.
/// The log is the text this writer returns; <see cref="DshZstdFrames"/> makes the file dsh's default compression reads.
/// <para>A turn opens on the host's words (<c>turn/start</c>, <c>step/start</c>, a <c>user/message</c> each) and a step is one agent message
/// (an <c>assistant/message</c> carrying the stream that rebuilds its blocks, a <c>tool/call</c> per call) with the results that answer it
/// (<c>tool/result</c>); a turn that ends on the agent's words is closed, and one that ends on the host's or on results is left open, which
/// dsh closes itself when it resumes the session. A call no result answered is answered by an error result, as the other writers do, and
/// <see cref="Read"/> returns it too. The messages' sources name no model, since none made them.</para>
/// </summary>
/// <param name="tree">The full path of the working directory the session runs in, which dsh files it under and resumes it for.</param>
/// <param name="at">When it is written: the session's creation and each event a millisecond after the one before.</param>
internal sealed class DshSeedWriter(string tree, DateTimeOffset at) : IConversationSeedWriter
{
    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private const string Provider = "daoris-seed";

    /// <summary>The result added after a call no result answered.</summary>
    internal const string Unanswered = "(No result was recorded for this call.)";

    /// <inheritdoc/>
    public AgentSupport SupportFor(string doorId) => doorId == "dsh" ? AgentSupport.Supported : AgentSupport.Unsupported;

    /// <summary>The directory name dsh files a tree's sessions under: separators one dash, anything but letters, digits and <c>._-</c> its code.</summary>
    internal static string ProjectKey(string cwd)
    {
        var readable = new StringBuilder();
        var run = false;
        foreach (var ch in cwd)
        {
            if (ch is '/' or '\\' or ':')
            {
                if (!run) readable.Append('-');
                run = true;
            }
            else
            {
                readable.Append(ch is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '_' or '-' ? ch.ToString() : $"~{(int)ch:X4}");
                run = false;
            }
        }

        var text = readable.ToString().TrimStart('-');
        return $"--{(text.Length == 0 ? "root" : text[..Math.Min(text.Length, 251)])}--";
    }

    /// <summary>The file as dsh reads it: the name with its compression and the log in frames.</summary>
    internal static PhysicalSeedFile Physical(ContextFile file) => new(file.Name + ".zstd", DshZstdFrames.OfLog(file.Text));

    /// <inheritdoc/>
    public SeededConversation Write(SeedConversation conversation, string doorId, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        if (SupportFor(doorId) != AgentSupport.Supported) throw new NotSupportedException($"'{doorId}' is not dsh's door.");
        if (conversation.Messages.Count == 0) throw new ArgumentException("The conversation has no message.", nameof(conversation));
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var start = at.ToUnixTimeMilliseconds();
        var log = new StringBuilder();
        log.Append(new JsonObject
        {
            ["type"] = "session", ["version"] = 4, ["id"] = sessionId, ["createdAt"] = start, ["cwd"] = tree, ["isSeeded"] = false, ["delegationDepth"] = 0,
        }.ToJsonString(Compact)).Append('\n');

        var seq = 0;
        void Event(string type, JsonObject data, bool surface = false)
        {
            var line = new JsonObject { ["type"] = type, ["seq"] = seq, ["time"] = start + seq + 1, ["data"] = data };
            if (surface) line["surfaceOp"] = "append";
            log.Append(line.ToJsonString(Compact)).Append('\n');
            seq++;
        }

        string Id(int n) => new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{sessionId}/{n}"))[..16]).ToString();
        long Time() => start + seq + 1;

        int turn = 0, step = 0, ids = 0;
        bool turnOpen = false, stepOpen = false, spoke = false;
        var open = new List<string>();

        void Answer()
        {
            foreach (var call in open) Result(call, Unanswered, true);
            open.Clear();
        }

        void Result(string call, string text, bool failed)
        {
            var message = new JsonObject
            {
                ["id"] = Id(ids++), ["role"] = "tool", ["content"] = Text(text), ["source"] = new JsonObject { ["kind"] = "tool", ["callId"] = call },
                ["toolCallId"] = call,
            };
            if (failed) message["isError"] = true;
            Event("tool/result", new JsonObject { ["turn"] = turn, ["step"] = step, ["message"] = message }, surface: true);
        }

        void CloseStep()
        {
            Answer();
            if (stepOpen) Event("step/end", new JsonObject { ["turn"] = turn, ["step"] = step });
            stepOpen = false;
            spoke = false;
        }

        void CloseTurn()
        {
            CloseStep();
            if (turnOpen) Event("turn/end", new JsonObject { ["turn"] = turn, ["reason"] = new JsonObject { ["kind"] = "completed" } });
            turnOpen = false;
        }

        void OpenStep()
        {
            if (!turnOpen)
            {
                turn++;
                step = 0;
                Event("turn/start", new JsonObject { ["turn"] = turn });
                turnOpen = true;
            }

            step++;
            Event("step/start", new JsonObject { ["turn"] = turn, ["step"] = step });
            stepOpen = true;
        }

        foreach (var message in conversation.Messages)
        {
            if (message.Role == SeedRole.Host)
            {
                if (stepOpen && spoke) CloseTurn();
                else if (!stepOpen && turnOpen) CloseTurn();
                if (!stepOpen) OpenStep();
                foreach (var text in message.Parts.OfType<SeedText>())
                {
                    Event("user/message", new JsonObject
                    {
                        ["content"] = Text(text.Text), ["source"] = new JsonObject { ["kind"] = "user" }, ["role"] = "user", ["id"] = Id(ids++),
                    }, surface: true);
                }
            }
            else if (message.Role == SeedRole.Agent)
            {
                if (stepOpen && spoke) CloseStep();
                if (!stepOpen) OpenStep();
                var blocks = new JsonArray();
                var stream = new JsonArray();
                var calls = new List<SeedToolCall>();
                void Chunk(JsonObject chunk) => stream.Add(new JsonObject { ["type"] = "chunk", ["time"] = Time(), ["chunk"] = chunk });
                foreach (var part in message.Parts)
                {
                    var index = blocks.Count;
                    if (part is SeedText { Text.Length: > 0 } text)
                    {
                        var block = new JsonObject { ["type"] = "text", ["text"] = text.Text };
                        blocks.Add(block.DeepClone());
                        Chunk(new JsonObject { ["type"] = "block-start", ["index"] = index, ["blockType"] = "text" });
                        stream.Add(new JsonObject
                        {
                            ["type"] = "text-chunks", ["time0"] = Time(), ["index"] = index, ["dt"] = new JsonArray(), ["texts"] = new JsonArray(text.Text),
                        });
                        Chunk(new JsonObject { ["type"] = "block-end", ["index"] = index, ["block"] = block });
                    }
                    else if (part is SeedToolCall call)
                    {
                        var block = new JsonObject { ["type"] = "tool-call", ["id"] = call.CallId, ["name"] = call.Name, ["arguments"] = call.Arguments };
                        blocks.Add(block.DeepClone());
                        calls.Add(call);
                        Chunk(new JsonObject { ["type"] = "block-start", ["index"] = index, ["blockType"] = "tool-call" });
                        stream.Add(new JsonObject
                        {
                            ["type"] = "tool-call-chunks", ["time0"] = Time(), ["index"] = index, ["dt"] = new JsonArray(), ["id"] = call.CallId,
                            ["name"] = call.Name, ["args"] = new JsonArray(call.Arguments),
                        });
                        Chunk(new JsonObject { ["type"] = "block-end", ["index"] = index, ["block"] = block });
                    }
                }

                Chunk(new JsonObject { ["type"] = "finish", ["reason"] = new JsonObject { ["kind"] = calls.Count > 0 ? "tool-calls" : "stop" } });
                Event("assistant/message", new JsonObject
                {
                    ["turn"] = turn, ["step"] = step,
                    ["message"] = new JsonObject
                    {
                        ["role"] = "assistant", ["id"] = Id(ids++), ["content"] = blocks,
                        ["source"] = new JsonObject { ["kind"] = "model", ["provider"] = Provider, ["model"] = "seed" },
                    },
                    ["stream"] = stream,
                }, surface: true);
                foreach (var call in calls)
                {
                    Event("tool/call", new JsonObject { ["turn"] = turn, ["step"] = step, ["callId"] = call.CallId, ["name"] = call.Name, ["arguments"] = call.Arguments });
                    open.Add(call.CallId);
                }

                spoke = true;
            }
            else
            {
                if (!stepOpen) OpenStep();
                foreach (var result in message.Parts.OfType<SeedToolResult>())
                {
                    Result(result.CallId, result.Text, result.IsError);
                    open.Remove(result.CallId);
                }
            }
        }

        Answer();
        if (stepOpen && spoke && conversation.Messages[^1].Role == SeedRole.Agent) CloseTurn();
        var name = $"sessions/{ProjectKey(tree)}/{sessionId}/session.v4.jsonl";
        return new SeededConversation(sessionId, [new ContextFile(name, log.ToString())]);
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
                    throw new FormatException($"'{file.Name}' is not a dsh session log: {error.Message}", error);
                }

                if (line["data"] is not JsonObject data) continue;
                switch ((string?)line["type"])
                {
                    case "user/message":
                        messages.Add(new SeedMessage(SeedRole.Host, [new SeedText(Joined(data["content"]))]));
                        break;
                    case "assistant/message" when data["message"] is JsonObject { } said && said["content"] is JsonArray blocks:
                        var parts = new List<SeedPart>();
                        foreach (var block in blocks.OfType<JsonObject>())
                        {
                            if ((string?)block["type"] == "text") parts.Add(new SeedText((string?)block["text"] ?? ""));
                            else if ((string?)block["type"] == "tool-call") parts.Add(new SeedToolCall((string?)block["id"] ?? "", (string?)block["name"] ?? "", (string?)block["arguments"] ?? "{}"));
                        }

                        if (parts.Count > 0) messages.Add(new SeedMessage(SeedRole.Agent, [.. parts]));
                        break;
                    case "tool/result" when data["message"] is JsonObject result:
                        messages.Add(new SeedMessage(SeedRole.Tool, [new SeedToolResult(
                            (string?)result["toolCallId"] ?? "", Joined(result["content"]), result["isError"] is JsonValue failed && (bool)failed)]));
                        break;
                }
            }
        }

        if (messages.Count == 0) throw new FormatException("No message of a dsh session log was found.");
        return new SeedConversation(messages);
    }

    private static JsonArray Text(string text) => new(new JsonObject { ["type"] = "text", ["text"] = text });

    private static string Joined(JsonNode? content) =>
        string.Concat((content as JsonArray ?? []).OfType<JsonObject>().Where(b => (string?)b["type"] == "text").Select(b => (string?)b["text"] ?? ""));
}
