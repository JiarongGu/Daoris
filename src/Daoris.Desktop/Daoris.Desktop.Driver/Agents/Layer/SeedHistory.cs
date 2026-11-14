using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// A session's recorded events as a conversation in no agent's format, what a seed writer writes in its own: the person's and Daoris's
/// words as the host's, the agent's words and its tool calls as the agent's, each call's last result as the tool's right after the
/// message that made it. Thoughts, notes, plans and readings are no turn of the conversation and stay out.
/// </summary>
internal static class SeedHistory
{
    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The conversation <paramref name="events"/> hold, oldest first.</summary>
    public static SeedConversation Of(IReadOnlyList<SessionEvent> events)
    {
        // A tool call's updates share its id: its last name, input, output and status are the call's.
        var calls = new Dictionary<string, (string Name, string? Input, string? Output, string? Status)>(StringComparer.Ordinal);
        foreach (var e in events.Where(e => e.Kind == SessionEventKind.Tool && e.Id is { Length: > 0 }))
        {
            calls.TryGetValue(e.Id!, out var was);
            calls[e.Id!] = (e.ToolName ?? e.Title ?? e.ToolKind ?? was.Name ?? "tool", e.Input ?? was.Input, e.Output ?? was.Output, e.Status ?? was.Status);
        }

        var messages = new List<SeedMessage>();
        var parts = new List<SeedPart>();
        var made = new List<string>();
        var text = new System.Text.StringBuilder();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Words()
        {
            if (text.Length > 0) parts.Add(new SeedText(text.ToString()));
            text.Clear();
        }

        void Close()
        {
            Words();
            if (parts.Count > 0) messages.Add(new SeedMessage(SeedRole.Agent, [.. parts]));
            parts.Clear();
            foreach (var id in made)
            {
                var call = calls[id];
                var failed = call.Status is "failed" or "refused";
                messages.Add(new SeedMessage(SeedRole.Tool, [new SeedToolResult(id, call.Output is { Length: > 0 } output ? output : $"({call.Status ?? "no result"})", failed)]));
            }

            made.Clear();
        }

        foreach (var e in events)
        {
            switch (e.Kind)
            {
                // Words shown while they wait come again where the session took them, which is the turn's ask.
                case SessionEventKind.User when e.Reaches is null && !string.IsNullOrWhiteSpace(e.Text):
                    Close();
                    messages.Add(SeedMessage.Host(e.Text!));
                    break;
                case SessionEventKind.Message when !string.IsNullOrEmpty(e.Text):
                    if (made.Count > 0) Close();
                    text.Append(e.Text);
                    break;
                case SessionEventKind.Tool when e.Id is { Length: > 0 } id && seen.Add(id):
                    Words();
                    var call = calls[id];
                    parts.Add(new SeedToolCall(id, call.Name, Arguments(call.Input)));
                    made.Add(id);
                    break;
            }
        }

        Close();
        return new SeedConversation(messages);
    }

    /// <summary>A call's input as a JSON object, which every agent's format takes: the input itself where it is one, else held under <c>input</c>.</summary>
    internal static string Arguments(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "{}";
        try
        {
            if (JsonNode.Parse(input) is JsonObject) return input;
        }
        catch (JsonException)
        {
        }

        return new JsonObject { ["input"] = input }.ToJsonString(Compact);
    }
}
