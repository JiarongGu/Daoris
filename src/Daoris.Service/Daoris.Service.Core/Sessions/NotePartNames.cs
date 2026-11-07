using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Knowledge;

/// <summary>
/// A session note's parts under the names this build reads: a part whose values name a door <c>adapter</c> has it renamed
/// <c>door</c>, and the reason <c>adapter</c> (the person changed the door since) is the reason <c>door</c>. The store moves its own
/// notes as it opens, because a shared host opens the same schema and runs no desktop migration; a note that is not JSON is left as kept.
/// </summary>
internal static class NotePartNames
{
    public static async Task RenameAsync(ConnectionGate db, CancellationToken ct)
    {
        var kept = new List<(string Id, string Parts)>();
        await using (var read = db.Command())
        {
            read.CommandText = "SELECT id, note_parts FROM sessions WHERE note_parts LIKE '%adapter%'";
            await using var reader = await read.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                if (Renamed(reader.GetString(1)) is { } parts) kept.Add((reader.GetString(0), parts));
            }
        }

        foreach (var (id, parts) in kept)
        {
            await using var write = db.Command();
            write.CommandText = "UPDATE sessions SET note_parts = $parts WHERE id = $id";
            write.Parameters.AddWithValue("$parts", parts);
            write.Parameters.AddWithValue("$id", id);
            await write.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>The parts as this build reads them; null where nothing in them is spelt the older way.</summary>
    internal static string? Renamed(string parts)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(parts);
        }
        catch (JsonException)
        {
            return null;
        }

        var changed = false;
        foreach (var values in (node as JsonArray ?? []).OfType<JsonObject>().Select(part => part["values"]).OfType<JsonObject>())
        {
            if (values["adapter"] is { } door && values["door"] is null)
            {
                values.Remove("adapter");
                values["door"] = door;
                changed = true;
            }

            if (values["why"] is JsonValue why && why.TryGetValue<string>(out var code) && code == "adapter")
            {
                values["why"] = "door";
                changed = true;
            }
        }

        return changed ? node!.ToJsonString() : null;
    }
}
