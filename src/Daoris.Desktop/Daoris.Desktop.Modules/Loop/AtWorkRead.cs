using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>One lineage file as a composition read it: its size and write time taken before the read, and what the read found.</summary>
public sealed record AtWorkFile(string Path, long Size, long Ticks, LineageReading Reading)
{
    public static AtWorkFile Read(string path, Func<LineageReading> read)
    {
        var (size, ticks) = Stamp(path);
        return new AtWorkFile(path, size, ticks, read());
    }

    /// <summary>A file's size and last write, or -1 where there is none.</summary>
    public static (long Size, long Ticks) Stamp(string path)
    {
        if (path.Length == 0) return (-1, 0);
        var info = new FileInfo(path);
        return info.Exists ? (info.Length, info.LastWriteTimeUtc.Ticks) : (-1, 0);
    }
}

/// <summary>
/// Everything a composition read, with the version it read (the design §4.1, <i>its dependencies</i>): the entry's parts, each part's
/// read fields, each slice's health, and each part's lineage file with the tokens its generations were read by.
/// </summary>
public sealed record AtWorkDependencies(
    string Session, IReadOnlyList<string>? Entry, IReadOnlyDictionary<string, AtWorkRecord?> Records, SliceHealth Groups, SliceHealth Sessions,
    IReadOnlyDictionary<string, AtWorkFile> Files);

/// <summary>
/// Who is at work on a session, the window's read (COWORKVIEW1a, the design §4.1): its inputs gathered from memory, the <c>groups</c>
/// slice's entries and the <c>sessions</c> slice's records with each one's health, and composed by the driver's one function
/// (<see cref="AtWork.Compose"/>), which the terminal's verb shares. No service is asked. Its dependencies go beside the answer.
/// </summary>
public static class AtWorkRead
{
    /// <param name="lineage">A part's lineage reading, this window's tokens in it.</param>
    /// <param name="pathOf">Where a part's lineage file is, whose stamp is taken before the read; none keeps no file's.</param>
    public static AtWorkHeld Compose(
        string session, StateSlice? groups, StateSlice? sessions, Func<AtWorkRecord, LineageReading> lineage, Func<string, string>? pathOf = null)
    {
        var (groupsSnapshot, groupsHealth) = Taken(groups);
        var (sessionsSnapshot, sessionsHealth) = Taken(sessions);
        var records = Records(sessionsSnapshot);
        var entry = EntryOf(groupsSnapshot, session);
        var asked = new Dictionary<string, AtWorkRecord?>(StringComparer.Ordinal);
        var files = new Dictionary<string, AtWorkFile>(StringComparer.Ordinal);
        var held = AtWork.Compose(session, entry, id => asked[id] = records.GetValueOrDefault(id), record =>
        {
            var file = AtWorkFile.Read(pathOf?.Invoke(record.Id) ?? "", () => lineage(record));
            files[record.Id] = file;
            return file.Reading;
        }, groupsHealth, sessionsHealth);
        return held with { Dependencies = new AtWorkDependencies(session, entry, asked, groupsHealth, sessionsHealth, files) };
    }

    /// <summary>
    /// What <paramref name="read"/>'s slices hold now, as a composition would read it: whether each part's read fields, the entry and
    /// each slice's health stand.
    /// </summary>
    public static bool SlicesStand(AtWorkDependencies read, StateSlice? groups, StateSlice? sessions)
    {
        var (groupsSnapshot, groupsHealth) = Taken(groups);
        var (sessionsSnapshot, sessionsHealth) = Taken(sessions);
        if (groupsHealth != read.Groups || sessionsHealth != read.Sessions) return false;
        var entry = EntryOf(groupsSnapshot, read.Session);
        if (entry is null != read.Entry is null || (entry is not null && !entry.SequenceEqual(read.Entry!, StringComparer.Ordinal))) return false;
        var records = Records(sessionsSnapshot);
        return read.Records.All(each => Equals(records.GetValueOrDefault(each.Key), each.Value));
    }

    /// <summary>A slice's snapshot with its health, read together; a slice this Daoris does not hold is one never judged.</summary>
    private static (SliceSnapshot? Snapshot, SliceHealth Health) Taken(StateSlice? slice)
    {
        if (slice is null) return (null, new SliceHealth(false));
        var health = slice.Health;
        return (slice.Snapshot(), new SliceHealth(health.Judged, health.Why));
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
