using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// An agent's hold of a decision, kept on it so its card says the tier that actually ran and what held its turn read-only: the role and
/// the revision it is kept under, the choice's agent, model and effort, the tier that ran, the posture, its turn's session and its tokens.
/// </summary>
public sealed record SupervisorAgentHolder(
    string Role, long Revision, string Agent, string? Model, string? Effort, string? Ran, string Posture, string Session)
{
    public long? Tokens { get; init; }
}

/// <summary>A decision file's <c>holder</c>, written and read in one place for every kind: a terminal's <c>{ kind, parent }</c>, an agent's beside it.</summary>
public static class SupervisorHolders
{
    /// <summary>The holder of a decision an agent's turn made.</summary>
    public static SupervisorHolder OfAgent(SupervisorAgentHolder agent) => new(RoleHolders.Agent, null) { Agent = agent };

    public static void Write(Utf8JsonWriter writer, SupervisorHolder holder)
    {
        writer.WriteStartObject("holder");
        writer.WriteString("kind", holder.Kind);
        if (holder.Parent is not null) writer.WriteString("parent", holder.Parent);
        if (holder.Agent is { } agent)
        {
            writer.WriteString("role", agent.Role);
            writer.WriteNumber("revision", agent.Revision);
            writer.WriteString("agent", agent.Agent);
            if (agent.Model is not null) writer.WriteString("model", agent.Model);
            if (agent.Effort is not null) writer.WriteString("effort", agent.Effort);
            if (agent.Ran is not null) writer.WriteString("ran", agent.Ran);
            writer.WriteString("posture", agent.Posture);
            writer.WriteString("session", agent.Session);
            if (agent.Tokens is { } tokens) writer.WriteNumber("tokens", tokens);
        }

        writer.WriteEndObject();
    }

    public static JsonObject Node(SupervisorHolder holder)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            Write(writer, holder);
            writer.WriteEndObject();
        }

        return (JsonNode.Parse(stream.ToArray())!["holder"]!.DeepClone() as JsonObject)!;
    }

    /// <summary>The <c>holder</c> of <paramref name="root"/>; none named reads as a holder of no kind.</summary>
    public static SupervisorHolder Read(JsonElement root)
    {
        if (!root.TryGetProperty("holder", out var held) || held.ValueKind != JsonValueKind.Object) return new SupervisorHolder("", null);
        var holder = new SupervisorHolder(Json.Text(held, "kind") ?? "", Json.Text(held, "parent"));
        if (Json.Text(held, "role") is not { } role || Json.Text(held, "agent") is not { } agent) return holder;
        return holder with
        {
            Agent = new SupervisorAgentHolder(
                role, held.TryGetProperty("revision", out var revision) && revision.TryGetInt64(out var number) ? number : 0, agent,
                Json.Text(held, "model"), Json.Text(held, "effort"), Json.Text(held, "ran"), Json.Text(held, "posture") ?? "", Json.Text(held, "session") ?? "")
            {
                Tokens = held.TryGetProperty("tokens", out var tokens) && tokens.TryGetInt64(out var spent) ? spent : null,
            },
        };
    }
}
