using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Knowledge;

/// <summary>
/// Codex's call key: its turn metadata names the thread, its parent, the spawn kind and the root's session, as JSON text or an object.
/// Measured on Codex 0.160.0. It also names the workspace's absolute path and commit: neither is read (D46, D47).
/// </summary>
public sealed class CodexCallKeys : ICallKeys
{
    /// <summary>Codex, by the name the driver gives its agent.</summary>
    public const string Agent = "codex";

    /// <summary>Codex's turn metadata.</summary>
    public const string Turn = "x-codex-turn-metadata";

    public string Door => Agent;

    public string Key => Turn;

    public CallLineage? Read(JsonNode value)
    {
        if (Metadata(value) is not { } turn) return new CallLineage(Agent) { Malformed = "turn" };

        var malformed = new List<string>();
        string? Field(string name, string key)
        {
            var id = CallLineage.Id(turn[key]);
            if (id is null && turn[key] is { } sent && sent.GetValueKind() != JsonValueKind.Null) malformed.Add(name);
            return id;
        }

        var lineage = new CallLineage(Agent)
        {
            Thread = Field("thread", "thread_id"),
            Parent = Field("parent", "parent_thread_id"),
            Kind = Field("kind", "subagent_kind"),
            Source = Field("source", "thread_source"),
            Root = Field("root", "session_id"),
        };
        if (malformed.Count > 0) return lineage with { Malformed = string.Join(',', malformed) };
        return lineage.Thread is null && lineage.Root is null ? null : lineage;
    }

    /// <summary>Codex's metadata, an object or JSON text holding one; anything else is none.</summary>
    private static JsonObject? Metadata(JsonNode value)
    {
        if (value is JsonObject turn) return turn;
        if (value is not JsonValue text || !text.TryGetValue<string>(out var json)) return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
