using System.Text.Json;
using static Daoris.Knowledge.JsonFields;

namespace Daoris.Knowledge;

/// <summary>
/// Session records on the wire (D47 §6, SYNC4) — written once, here, and read by both sides: the
/// remote's two session doors and the machine's client that speaks to them.
/// </summary>
/// <remarks>
/// <para>What travels is a fact about work: which quest, which repository, which tool, how it ended,
/// whether an account's limit cut it off (never whose), what landed. What never travels has no field here at all — the TRANSCRIPT and the TREE are paths on
/// a machine's disk, and the PROFILE is the name of the account it ran as (D47 §4, D49 §4, D51). Absent,
/// not policed: a record read from a store that holds them is copied field by field, and none of the
/// three is a field.</para>
///
/// <para>Hand-written rather than reflected, for the reason every JSON shape in Core is: nothing here may
/// quietly stop working under AOT.</para>
/// </remarks>
public static class SessionWire
{
    /// <summary>A feed of this machine's own records, as the remote's feed door reads it.</summary>
    public static string Feed(IReadOnlyList<FedSessionRecord> records) => Written(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartArray("records");
        foreach (var record in records)
        {
            writer.WriteStartObject();
            Write(writer, record.Id, origin: null, record.Quest, record.Repository, record.Adapter, record.State,
                record.Note, record.Evidence, record.Created, record.Updated, record.Kind, record.HarnessVersion,
                record.Limit, record.NoteParts);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>A feed read back at the door — null when it is not one. Each record is judged by <see cref="SessionFeed"/>.</summary>
    public static IReadOnlyList<FedSessionRecord>? ReadFeed(string json)
    {
        if (ParseObject(json) is not { } root) return null;
        var records = new List<FedSessionRecord>();
        foreach (var item in Items(root, "records"))
        {
            if (item.ValueKind != JsonValueKind.Object || Time(item, "created") is not { } created
                || Time(item, "updated") is not { } updated)
            {
                return null;
            }

            records.Add(new FedSessionRecord(
                Text(item, "id"), Text(item, "quest"), Text(item, "repository"), Text(item, "adapter"),
                Text(item, "state"), Text(item, "note"), Text(item, "evidence"), created, updated,
                Text(item, "kind"), Text(item, "harnessVersion"), Said(item, "limit"), Parts(item)));
        }

        return records;
    }

    /// <summary>A page of the team's records the remote holds — each keyed `origin/id`, carrying its origin.</summary>
    public static string Page(SessionFetch page) => Written(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("through", page.Through);
        writer.WriteBoolean("more", page.More);
        writer.WriteStartArray("records");
        foreach (var session in page.Records)
        {
            writer.WriteStartObject();
            Write(writer, session.Id, session.Origin, session.Quest, session.Repository, session.Adapter,
                session.StateName, session.Note, session.Evidence, session.Created, session.Updated,
                session.Kind.ToString(), session.HarnessVersion, session.Limit, session.NoteParts);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>A page read back on a machine, or null when it is not one — or when any record in it is not whole.</summary>
    public static SessionFetch? ReadPage(string json)
    {
        if (ParseObject(json) is not { } root) return null;
        var records = new List<Session>();
        foreach (var item in Items(root, "records"))
        {
            if (item.ValueKind != JsonValueKind.Object
                || Text(item, "id") is not { Length: > 0 } id || Text(item, "origin") is not { Length: > 0 } origin
                || Text(item, "repository") is not { Length: > 0 } repository
                || !Session.TryParse(Text(item, "state") ?? "", out var state)
                || Time(item, "created") is not { } created || Time(item, "updated") is not { } updated)
            {
                return null;
            }

            records.Add(new Session(
                id, Text(item, "quest"), repository, Text(item, "adapter") ?? "unknown", state,
                Text(item, "note"), Text(item, "evidence"), Transcript: null, created, updated,
                Kind: Enum.TryParse<SessionKind>(Text(item, "kind"), ignoreCase: true, out var kind) ? kind : SessionKind.Driven,
                HarnessVersion: Text(item, "harnessVersion"))
            {
                Origin = origin,
                Limit = Said(item, "limit"),
                NoteParts = Parts(item),
            });
        }

        return new SessionFetch(
            records,
            root.TryGetProperty("through", out var through) && through.ValueKind == JsonValueKind.Number ? through.GetInt64() : 0,
            root.TryGetProperty("more", out var more) && more.ValueKind == JsonValueKind.True);
    }

    private static void Write(
        Utf8JsonWriter writer, string? id, string? origin, string? quest, string? repository, string? adapter,
        string? state, string? note, string? evidence, DateTimeOffset created, DateTimeOffset updated,
        string? kind, string? harnessVersion, bool limit, string? noteParts)
    {
        writer.WriteString("id", id);
        if (origin is not null) writer.WriteString("origin", origin);
        // A chat serves no quest (D49 §3): omitted rather than sent blank, which would read as an id
        // that failed to parse.
        if (quest is not null) writer.WriteString("quest", quest);
        writer.WriteString("repository", repository);
        writer.WriteString("adapter", adapter);
        writer.WriteString("state", state);
        if (note is not null) writer.WriteString("note", note);
        // LANG1a: the note's parts beside it, as kept; absent for a record from before parts, which a reader from before ignores.
        if (NoteParts.Element(noteParts) is { } parts)
        {
            writer.WritePropertyName("noteParts");
            parts.WriteTo(writer);
        }

        if (evidence is not null) writer.WriteString("evidence", evidence);
        writer.WriteString("created", created.ToString("O"));
        writer.WriteString("updated", updated.ToString("O"));
        if (kind is not null) writer.WriteString("kind", kind);
        if (harnessVersion is not null) writer.WriteString("harnessVersion", harnessVersion);
        // TOOL4c: said only where true, so a record from before the field and one it never applied to read
        // alike on both sides, as false. It names no account, which is why it has a field at all.
        if (limit) writer.WriteBoolean("limit", true);
    }

    /// <summary>A record's note parts as the wire carried them, kept only as what a part is (LANG1a); null for none.</summary>
    private static string? Parts(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty("noteParts", out var parts) ? NoteParts.Normalize(parts) : null;

    /// <summary>Whether a flag is said, true: absent, false or of another kind is not.</summary>
    private static bool Said(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.True;

    private static DateTimeOffset? Time(JsonElement element, string name) =>
        Text(element, name) is { } text && DateTimeOffset.TryParse(text, out var at) ? at : null;
}
