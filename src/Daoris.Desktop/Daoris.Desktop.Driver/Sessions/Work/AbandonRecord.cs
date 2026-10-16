using System.Globalization;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// This machine's record of what each abandon took and kept (PAUSE1d, D132 point 9, design §4.2):
/// <c>&lt;home&gt;/abandoned.json</c>, written atomically by the driver library at both doors, as <c>landings.json</c> is
/// (D102).
/// </summary>
/// <remarks>
/// <para><b>Under the home, not on the record.</b> The trees, the branches and their tips are this machine's facts, and a
/// quest's record travels (D47 §4). An ask's does not, but one home for both scopes keeps one reader.</para>
///
/// <para><b>No path, and no conversation's words.</b> A tree is its repository and branch; the reason is the person's
/// answer, which every declined quest carries too.</para>
///
/// <para><b>A missing or unreadable file is no record</b>, and the abandon still abandons. An entry stays while its ask or
/// quest has a record here: it is the only trace of a discarded tree, and it is small.</para>
/// </remarks>
public sealed class AbandonRecord(string home)
{
    // One writer at a time in this process: two presses would otherwise read the same file, and the second drop the first.
    private static readonly object Gate = new();

    private static readonly RecordList<AbandonEntry> List = new(Gate, "abandoned", Read, ToJson);

    public string FilePath => DaorisHome.Entry(home, HomeEntry.Abandoned);

    /// <summary>Every entry, oldest first; none when the file is missing or does not read.</summary>
    public IReadOnlyList<AbandonEntry> Entries() => List.Read(FilePath);

    /// <summary>The newest abandon of this ask or quest here, or null: what its page shows as *What went* and *What stayed*.</summary>
    public AbandonEntry? Last(WorkScope scope, string id) =>
        Entries().LastOrDefault(entry => entry.Scope == WorkPausing.Word(scope)
            && string.Equals(entry.Id, id.Trim().TrimStart('#'), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Keep one more entry, dropping those whose ask or quest no longer has a record here.
    /// </summary>
    /// <param name="known">Whether an entry's ask or quest still has a record here; null keeps every entry.</param>
    public void Write(AbandonEntry entry, Func<AbandonEntry, bool>? known = null) =>
        List.Edit(FilePath, all => [.. all.Where(each => known is null || known(each)), entry]);

    /// <summary>
    /// Drop the entries whose ask or quest no longer has a record here (HIST1c, the history-clearing design §2.2), as
    /// <see cref="Write"/> drops them beside a new one: a clear takes the record, and its abandon's trace with it. How many went
    /// comes back; nothing is written when none did.
    /// </summary>
    public int Forget(Func<AbandonEntry, bool> known)
    {
        lock (Gate)
        {
            var all = Entries();
            var kept = all.Where(known).ToList();
            if (kept.Count == all.Count) return 0;
            AtomicFile.WriteText(FilePath, ToJson(kept));
            return all.Count - kept.Count;
        }
    }

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(IReadOnlyList<AbandonEntry> entries) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartArray("abandoned");
        foreach (var entry in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("scope", entry.Scope);
            writer.WriteString("id", entry.Id);
            writer.WriteString("at", Moments.Write(entry.At));
            writer.WriteString("door", entry.Door);
            writer.WriteString("reason", entry.Reason);
            Strings(writer, "declined", entry.Declined);
            if (entry.Closed) writer.WriteBoolean("closed", true);
            writer.WriteStartArray("trees");
            foreach (var tree in entry.Trees)
            {
                writer.WriteStartObject();
                writer.WriteString("repository", tree.Repository);
                writer.WriteString("branch", tree.Branch);
                if (tree.Tip is not null) writer.WriteString("tip", tree.Tip);
                if (tree.Commits is { } commits) writer.WriteNumber("commits", commits);
                if (tree.Uncommitted is { } uncommitted) writer.WriteNumber("uncommitted", uncommitted);
                Strings(writer, "sessions", tree.Sessions);
                if (tree.Alone) writer.WriteBoolean("alone", true);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            Strings(writer, "stopped", entry.Stopped);
            Strings(writer, "archived", entry.Archived);
            writer.WriteStartArray("stayed");
            foreach (var keep in entry.Stayed)
            {
                writer.WriteStartObject();
                writer.WriteString("piece", keep.Piece);
                writer.WriteString("why", keep.Why);
                if (keep.Machine is not null) writer.WriteString("machine", keep.Machine);
                if (keep.Where is not null) writer.WriteString("where", keep.Where);
                if (keep.Changed is { } changed) writer.WriteBoolean("changed", changed);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("declines");
            foreach (var decline in entry.Declines)
            {
                writer.WriteStartObject();
                writer.WriteString("quest", decline.Quest);
                writer.WriteString("answer", decline.Answer);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    private static void Strings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    /// <summary>One entry, or null where it lacks what makes it one: a scope, an id, a door and a reason.</summary>
    private static AbandonEntry? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var scope = Text(element, "scope");
        var id = Text(element, "id");
        var door = Text(element, "door");
        var reason = Text(element, "reason");
        if (scope is not ("ask" or "quest") || id is null || door is null || reason is null) return null;
        var at = Moments.Read(Text(element, "at")) ?? DateTimeOffset.MinValue;
        return new AbandonEntry(scope, id, at, door, reason)
        {
            Declined = Strings(element, "declined"),
            Closed = element.TryGetProperty("closed", out var closed) && closed.ValueKind == JsonValueKind.True,
            Trees = [.. Items(element, "trees").Select(tree => Text(tree, "repository") is { } repository && Text(tree, "branch") is { } branch
                ? new AbandonedTree(repository, branch, Text(tree, "tip"), Int(tree, "commits"), Int(tree, "uncommitted"),
                    Strings(tree, "sessions"), tree.TryGetProperty("alone", out var alone) && alone.ValueKind == JsonValueKind.True)
                : null).OfType<AbandonedTree>()],
            Stopped = Strings(element, "stopped"),
            Archived = Strings(element, "archived"),
            Stayed = [.. Items(element, "stayed").Select(keep => Text(keep, "piece") is { } piece && Text(keep, "why") is { } why
                ? new AbandonKeep(piece, why)
                {
                    Machine = Text(keep, "machine"),
                    Where = Text(keep, "where"),
                    Changed = keep.TryGetProperty("changed", out var changed) && changed.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? changed.GetBoolean()
                        : null,
                }
                : null).OfType<AbandonKeep>()],
            Declines = [.. Items(element, "declines").Select(decline => Text(decline, "quest") is { } quest && Text(decline, "answer") is { } answer
                ? new AbandonDecline(quest, answer)
                : null).OfType<AbandonDecline>()],
        };
    }

    // Empty strings are kept here, as the copy this replaced kept them.
    private static IReadOnlyList<string> Strings(JsonElement element, string name) => Json.Strings(element, name, keepEmpty: true);
}
