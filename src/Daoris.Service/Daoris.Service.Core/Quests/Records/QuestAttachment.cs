namespace Daoris.Knowledge;

/// <summary>A file a quest carries, as its record names it — never where it lies on a disk.</summary>
/// <param name="Name">The file's own name, made safe to keep: what a reader and a session see.</param>
/// <param name="Sha256">Its content's hash, lowercase hex — the file's identity, and how it is found.</param>
/// <param name="Bytes">Its size.</param>
public sealed record QuestAttachment(string Name, string Sha256, long Bytes)
{
    /// <summary>Its one shape in JSON, the same in a store's column and on the wire.</summary>
    public void Write(System.Text.Json.Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("name", Name);
        writer.WriteString("sha256", Sha256);
        writer.WriteNumber("bytes", Bytes);
        writer.WriteEndObject();
    }

    /// <summary>
    /// One read back out of a store's own column, which only this service writes. The wire's reader
    /// judges what it reads instead (<see cref="QuestWire"/>), because a remote's JSON is not ours.
    /// </summary>
    public static QuestAttachment Stored(System.Text.Json.JsonElement item) => new(
        item.GetProperty("name").GetString() ?? "",
        item.GetProperty("sha256").GetString() ?? "",
        item.GetProperty("bytes").GetInt64());
}
