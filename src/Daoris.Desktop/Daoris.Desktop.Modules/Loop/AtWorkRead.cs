using System.Globalization;
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
/// read fields, the rows and quests it looked up, how many quests each session published, each slice's health, and each part's
/// lineage file with the tokens its generations were read by.
/// </summary>
public sealed record AtWorkDependencies(
    string Session, IReadOnlyList<AtWorkEntryPart>? Entry, IReadOnlyDictionary<string, AtWorkRecord?> Records, SliceHealth Groups, SliceHealth Sessions,
    IReadOnlyDictionary<string, AtWorkFile> Files)
{
    public IReadOnlyDictionary<string, AtWorkRow?> Rows { get; init; } = new Dictionary<string, AtWorkRow?>();

    public IReadOnlyDictionary<string, AtWorkQuest?> Quests { get; init; } = new Dictionary<string, AtWorkQuest?>();

    public IReadOnlyDictionary<string, int> Published { get; init; } = new Dictionary<string, int>();

    public SliceHealth QuestsHealth { get; init; }

    /// <summary>
    /// The quests half as the composition read it (COWORKVIEW1b-ii): the rows, the rows left out and the asker, written out. It reads the
    /// whole stores, so it is compared as a result, and a write that moves none of it wakes nothing.
    /// </summary>
    public string? Asked { get; init; }
}

/// <summary>
/// Who is at work on a session, the window's read (COWORKVIEW1a, the design §4.1): its inputs gathered from memory, the <c>groups</c>
/// slice's entries and rows, the <c>sessions</c> slice's records and the <c>quests</c> slice's quests with each one's health, and
/// composed by the driver's one function (<see cref="AtWork.Compose"/>), which the terminal's verb shares. No service is asked. Its
/// dependencies go beside the answer.
/// </summary>
public static class AtWorkRead
{
    /// <param name="lineage">A part's lineage reading, this window's tokens in it.</param>
    /// <param name="pathOf">Where a part's lineage file is, whose stamp is taken before the read; none keeps no file's.</param>
    public static AtWorkHeld Compose(
        string session, StateSlice? groups, StateSlice? sessions, StateSlice? quests, Func<AtWorkRecord, LineageReading> lineage,
        Func<string, string>? pathOf = null)
    {
        var (groupsSnapshot, groupsHealth) = Taken(groups);
        var (sessionsSnapshot, sessionsHealth) = Taken(sessions);
        var (questsSnapshot, questsHealth) = Taken(quests);
        var records = Records(sessionsSnapshot);
        var entry = EntryOf(groupsSnapshot, session);
        var rows = RowsOf(groupsSnapshot);
        var index = QuestIndex.Of(questsSnapshot);
        var asked = new Dictionary<string, AtWorkRecord?>(StringComparer.Ordinal);
        var askedRows = new Dictionary<string, AtWorkRow?>(StringComparer.Ordinal);
        var askedQuests = new Dictionary<string, AtWorkQuest?>(StringComparer.Ordinal);
        var askedPublished = new Dictionary<string, int>(StringComparer.Ordinal);
        var files = new Dictionary<string, AtWorkFile>(StringComparer.Ordinal);
        var routing = new AtWorkRouting(
            entry ?? [], id => askedRows[id] = rows.GetValueOrDefault(id), id => askedQuests[id] = index.ById.GetValueOrDefault(id),
            id => askedPublished[id] = index.Published.GetValueOrDefault(id), questsHealth)
        {
            Asking = Asking(session, entry, questsSnapshot, sessionsSnapshot, rows),
        };
        var held = AtWork.Compose(session, entry?.Select(part => part.Session).ToList(), id => asked[id] = records.GetValueOrDefault(id), record =>
        {
            var file = AtWorkFile.Read(pathOf?.Invoke(record.Id) ?? "", () => lineage(record));
            files[record.Id] = file;
            return file.Reading;
        }, groupsHealth, sessionsHealth, routing);
        return held with
        {
            Dependencies = new AtWorkDependencies(session, entry, asked, groupsHealth, sessionsHealth, files)
            {
                Rows = askedRows, Quests = askedQuests, Published = askedPublished, QuestsHealth = questsHealth,
                Asked = held.Answer is { } answer ? AskedKey(answer.Quests, answer.MoreQuests ?? 0, answer.AskedBy) : null,
            },
        };
    }

    private static readonly JsonSerializerOptions Written = new(JsonSerializerDefaults.Web);

    private static string AskedKey(IReadOnlyList<AtWorkAskedQuest>? quests, int more, AtWorkAsker? by) =>
        JsonSerializer.Serialize(new { quests = quests is { Count: > 0 } ? quests : null, more, by }, Written);

    /// <summary>
    /// The stores the quests half reads, as the two slices hold them: every quest, and the records that serve or took one or belong to the
    /// entry. The units are built over these by the driver's function, never by a service call.
    /// </summary>
    private static AtWorkAsking Asking(
        string session, IReadOnlyList<AtWorkEntryPart>? entry, SliceSnapshot? quests, SliceSnapshot? sessions, IReadOnlyDictionary<string, AtWorkRow> rows)
    {
        var members = (entry?.Select(part => part.Session) ?? []).Append(session).ToHashSet(StringComparer.Ordinal);
        var views = new Dictionary<string, QuestView>(StringComparer.Ordinal);
        foreach (var partition in quests?.Partitions ?? [])
        {
            if (Node(partition.Data) is not JsonObject keyed) continue;
            foreach (var (id, quest) in keyed)
            {
                if (Text(quest, "status") is not { } status || views.ContainsKey(id)) continue;
                views[id] = new QuestView(id, Text(quest, "from") ?? "", Text(quest, "to") ?? "", Text(quest, "title") ?? "", "", status)
                {
                    Short = Text(quest, "short"),
                    Parent = Text(quest, "parent"),
                    Awaits = Text(quest, "awaits"),
                    PublishedBy = Text(quest, "publishedBy"),
                    Holder = Text(quest, "holder"),
                    Here = Text(quest, "here"),
                    Held = quest?["held"] is JsonValue held && held.TryGetValue<bool>(out var value) && value,
                    Lanes = quest?["lanes"] is JsonArray { Count: > 0 } lanes ? [.. lanes.Select(lane => lane?.GetValue<string>()).OfType<string>()] : [],
                };
            }
        }

        var records = new Dictionary<string, SessionRecord>(StringComparer.Ordinal);
        foreach (var partition in sessions?.Partitions ?? [])
        {
            if (Node(partition.Data) is not JsonObject keyed) continue;
            foreach (var (id, record) in keyed)
            {
                var (serves, took) = (Text(record, "quest"), Text(record, "tookQuest"));
                if (records.ContainsKey(id) || (serves is null && took is null && !members.Contains(id))) continue;
                records[id] = new SessionRecord(id, Text(record, "repository") ?? "", Text(record, "state") ?? "")
                {
                    Quest = serves,
                    TookQuest = took,
                    Kind = Text(record, "kind") ?? "driven",
                    Adapter = Text(record, "adapter"),
                    Created = DateTimeOffset.TryParse(Text(record, "created"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created) ? created : default,
                };
            }
        }

        return AtWorkQuests.Reading([.. views.Values], [.. records.Values], id => rows.GetValueOrDefault(id));
    }

    /// <summary>Whether the quests half, read again from the slices now, is what <paramref name="read"/> answered.</summary>
    private static bool AskedStands(AtWorkDependencies read, SliceSnapshot? quests, SliceSnapshot? sessions, IReadOnlyDictionary<string, AtWorkRow> rows)
    {
        if (read.Asked is null || read.Records.GetValueOrDefault(read.Session) is not { } attended) return true;
        var ids = (read.Entry?.Select(part => part.Session) ?? []).Prepend(read.Session).Distinct(StringComparer.Ordinal).ToList();
        var now = Asking(read.Session, read.Entry, quests, sessions, rows)(read.Session, ids, read.Entry ?? [], attended);
        return AskedKey(now.Quests, now.More, now.By) == read.Asked;
    }

    /// <summary>
    /// What <paramref name="read"/>'s slices hold now, as a composition would read it: whether each part's read fields, the entry, the
    /// rows and quests it looked up and each slice's health stand.
    /// </summary>
    public static bool SlicesStand(AtWorkDependencies read, StateSlice? groups, StateSlice? sessions, StateSlice? quests)
    {
        var (groupsSnapshot, groupsHealth) = Taken(groups);
        var (sessionsSnapshot, sessionsHealth) = Taken(sessions);
        var (questsSnapshot, questsHealth) = Taken(quests);
        if (groupsHealth != read.Groups || sessionsHealth != read.Sessions || questsHealth != read.QuestsHealth) return false;
        var entry = EntryOf(groupsSnapshot, read.Session);
        if (entry is null != read.Entry is null || (entry is not null && !entry.SequenceEqual(read.Entry!))) return false;
        var records = Records(sessionsSnapshot);
        if (!read.Records.All(each => Equals(records.GetValueOrDefault(each.Key), each.Value))) return false;
        var rows = RowsOf(groupsSnapshot);
        if (!read.Rows.All(each => Equals(rows.GetValueOrDefault(each.Key), each.Value))) return false;
        var index = QuestIndex.Of(questsSnapshot);
        return read.Quests.All(each => Equals(index.ById.GetValueOrDefault(each.Key), each.Value))
            && read.Published.All(each => index.Published.GetValueOrDefault(each.Key) == each.Value)
            && AskedStands(read, questsSnapshot, sessionsSnapshot, rows);
    }

    /// <summary>The quests by id, and how many each session published.</summary>
    private sealed record QuestIndex(Dictionary<string, AtWorkQuest> ById, Dictionary<string, int> Published)
    {
        public static QuestIndex Of(SliceSnapshot? quests)
        {
            var byId = new Dictionary<string, AtWorkQuest>(StringComparer.Ordinal);
            var published = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var partition in quests?.Partitions ?? [])
            {
                if (Node(partition.Data) is not JsonObject keyed) continue;
                foreach (var (id, quest) in keyed)
                {
                    if (Text(quest, "status") is not { } status || !byId.TryAdd(id, Quest(id, status, quest!))) continue;
                    if (Text(quest, "publishedBy") is { } by) published[by] = published.GetValueOrDefault(by) + 1;
                }
            }

            return new QuestIndex(byId, published);
        }

        private static AtWorkQuest Quest(string id, string status, JsonNode quest) => new(id, status)
        {
            To = Text(quest, "to"),
            From = Text(quest, "from"),
            Awaits = Text(quest, "awaits"),
            PublishedBy = Text(quest, "publishedBy"),
            Parent = Text(quest, "parent"),
            Lanes = quest["lanes"] is JsonArray { Count: > 0 } lanes ? string.Join('+', lanes.Select(lane => lane?.GetValue<string>()).OfType<string>()) : null,
            Holder = Text(quest, "holder"),
            Here = Text(quest, "here"),
            Held = quest["held"] is JsonValue held && held.TryGetValue<bool>(out var value) && value,
        };
    }

    /// <summary>A slice's snapshot with its health, read together; a slice this Daoris does not hold is one never judged.</summary>
    private static (SliceSnapshot? Snapshot, SliceHealth Health) Taken(StateSlice? slice)
    {
        if (slice is null) return (null, new SliceHealth(false));
        var health = slice.Health;
        return (slice.Snapshot(), new SliceHealth(health.Judged, health.Why));
    }

    /// <summary>The parts of the entry holding <paramref name="session"/>, in the strip's order; null where none holds it.</summary>
    private static IReadOnlyList<AtWorkEntryPart>? EntryOf(SliceSnapshot? groups, string session)
    {
        var entries = groups?.Partitions.FirstOrDefault(each => each.Partition == GroupsStateSlice.Entries)?.Data;
        if (Node(entries) is not JsonObject keyed) return null;
        foreach (var (_, entry) in keyed)
        {
            if (entry?["parts"] is not JsonArray parts) continue;
            List<AtWorkEntryPart> read =
            [
                .. parts.Select(part => Text(part, "session") is { } id
                    ? new AtWorkEntryPart(id, Text(part, "quest"), Text(part, "shown") ?? "", Text(part, "began") ?? "")
                    {
                        After = Text(part, "after"),
                        AskedBy = Text(part, "askedBy"),
                    }
                    : null).OfType<AtWorkEntryPart>(),
            ];
            if (read.Any(part => part.Session == session)) return read;
        }

        return null;
    }

    /// <summary>Each session's row, the two fields the strip words a wait by.</summary>
    private static Dictionary<string, AtWorkRow> RowsOf(SliceSnapshot? groups)
    {
        var rows = new Dictionary<string, AtWorkRow>(StringComparer.Ordinal);
        var data = groups?.Partitions.FirstOrDefault(each => each.Partition == GroupsStateSlice.Rows)?.Data;
        if (Node(data) is not JsonObject keyed) return rows;
        foreach (var (id, row) in keyed) rows[id] = new AtWorkRow(Text(row, "awaits"), Text(row, "awaitsOf"));
        return rows;
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
                records.TryAdd(id, new AtWorkRecord(id, Text(record, "state"))
                {
                    Adapter = Text(record, "adapter"),
                    Kind = Text(record, "kind"),
                    TookQuest = Text(record, "tookQuest"),
                    Repository = Text(record, "repository"),
                    Tree = Text(record, "tree"),
                });
            }
        }

        return records;
    }

    private static JsonNode? Node(object? data) => data as JsonNode ?? (data is null ? null : JsonSerializer.SerializeToNode(data, IpcJson.Options));

    private static string? Text(JsonNode? node, string field) =>
        node?[field] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
}
