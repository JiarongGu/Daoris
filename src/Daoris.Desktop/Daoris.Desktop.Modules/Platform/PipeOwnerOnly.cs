using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The owner's records, which no agent sets: a place's risk and marks, a place's cast, and a step handed to an agent. A press of one that proves a session's key (Ask Daoris's
/// in full access included) goes on the owner's card in every mode, so it runs only on the owner's yes; a process in an agents' job that
/// proves none is refused; the window and the owner's own terminal set it.
/// </summary>
public static class PipeOwnerOnly
{
    /// <summary>The refusal's code for an owner's record pressed from a process in an agents' job.</summary>
    public const string Refused = "owner-only";

    public const string RefusedSentence =
        "This is the owner's to set: a place's risk and marks, who holds a role, and what agents may do, are widened by the owner alone. An agent's press goes on the owner's card through its own session's key. Nothing was changed.";

    /// <summary>Whether <paramref name="request"/> sets one of the owner's records: a place's risk or cast, a step handed to an agent, or a role granting acts to any holder but the rules.</summary>
    public static bool Governs(IpcRequest request)
    {
        if (!string.Equals(request.Module, DriverModule.Name, StringComparison.OrdinalIgnoreCase)) return false;
        return request.Type switch
        {
            "SET_PLACE_RISK" => true,
            "SET_PLACE_CAST" => true,
            "POLICY_SET" => true,
            "WORKFLOW_HOLD" => request.Payload is { ValueKind: JsonValueKind.Object } hold && Text(hold, "act") == CastActs.Hand
                && Text(hold, "holder")?.Trim() is { } holder && (holder == CastHolders.Agent || holder.StartsWith(CastHolders.AgentPrefix, StringComparison.Ordinal)),
            "SET_ROLE" => request.Payload is { } payload && GrantsAgent(payload),
            "SET_ROLES" => request.Payload is { ValueKind: JsonValueKind.Object } batch && batch.TryGetProperty("changes", out var changes)
                && changes.ValueKind == JsonValueKind.Array && changes.EnumerateArray().Any(GrantsAgent),
            _ => false,
        };
    }

    private static string? Text(JsonElement payload, string name) =>
        payload.EnumerateObject().FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)) is
            { Value.ValueKind: JsonValueKind.String } found ? found.Value.GetString() : null;

    /// <summary>
    /// Whether one role change writes an appointed role with any act granted to a holder that is not the person's rules (an agent never
    /// grants agents acts): an agent, a terminal (an agent's session too), or none yet, which one start would make act.
    /// </summary>
    private static bool GrantsAgent(JsonElement change) =>
        change.ValueKind == JsonValueKind.Object && change.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.Object
        && !(role.TryGetProperty("holder", out var holder) && holder.ValueKind == JsonValueKind.String && holder.GetString() == RoleHolders.Rules)
        && role.TryGetProperty("authority", out var acts) && acts.ValueKind == JsonValueKind.Array && acts.GetArrayLength() > 0;
}
