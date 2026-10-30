using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Who is at work on a session, the window's read (COWORKVIEW1a, the design §4.1): its inputs gathered from memory, the <c>groups</c>
/// slice's entries and the <c>sessions</c> slice's records with each one's health, and composed by the driver's one function
/// (<see cref="AtWork.Compose"/>), which the terminal's verb shares. No service is asked.
/// </summary>
public static class AtWorkRead
{
    /// <param name="lineage">A part's lineage reading, this window's tokens in it; the read's own reads its file.</param>
    public static AtWorkHeld Compose(string session, StateSlice? groups, StateSlice? sessions, Func<AtWorkRecord, LineageReading> lineage)
    {
        var (groupsSnapshot, groupsHealth) = Taken(groups);
        var (sessionsSnapshot, sessionsHealth) = Taken(sessions);
        var records = Records(sessionsSnapshot);
        return AtWork.Compose(session, EntryOf(groupsSnapshot, session), id => records.GetValueOrDefault(id), lineage, groupsHealth, sessionsHealth);
    }

    /// <summary>A slice's snapshot with its health, read together; a slice this Daoris does not hold is one never judged.</summary>
    private static (SliceSnapshot? Snapshot, SliceHealth Health) Taken(StateSlice? slice)
    {
        if (slice is null) return (null, new SliceHealth(false));
        var health = slice.Health;
        var snapshot = slice.Snapshot();
        return (snapshot, new SliceHealth(health.Judged && snapshot.Status == StateSlice.Ready, health.Why));
    }

    /// <summary>The parts of the entry holding <paramref name="session"/>, in the strip's order; null where none holds it.</summary>
    private static IReadOnlyList<string>? EntryOf(SliceSnapshot? groups, string session)
    {
        var entries = groups?.Partitions.FirstOrDefault(each => each.Partition == GroupsStateSlice.Entries)?.Data;
        if (Node(entries) is not JsonObject keyed) return null;
        foreach (var (_, entry) in keyed)
        {
            if (entry?["parts"] is not JsonArray parts) continue;
            List<string> ids = [.. parts.Select(part => Text(part, "session")).OfType<string>()];
            if (ids.Contains(session, StringComparer.Ordinal)) return ids;
        }

        return null;
    }

    /// <summary>Each record's read fields, by its id, from every workspace's partition: never its note, ran or updated.</summary>
    private static Dictionary<string, AtWorkRecord> Records(SliceSnapshot? sessions)
    {
        var records = new Dictionary<string, AtWorkRecord>(StringComparer.Ordinal);
        foreach (var partition in sessions?.Partitions ?? [])
        {
            if (Node(partition.Data) is not JsonObject keyed) continue;
            foreach (var (id, record) in keyed)
            {
                records.TryAdd(id, new AtWorkRecord(id, Text(record, "state")) { Adapter = Text(record, "adapter"), Kind = Text(record, "kind") });
            }
        }

        return records;
    }

    private static JsonNode? Node(object? data) => data as JsonNode ?? (data is null ? null : JsonSerializer.SerializeToNode(data, IpcJson.Options));

    private static string? Text(JsonNode? node, string field) =>
        node?[field] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
}
