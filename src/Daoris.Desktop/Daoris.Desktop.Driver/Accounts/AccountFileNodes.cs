using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// What the four per-account files (<c>cooling.json</c>, <c>names.json</c>, <c>windows.json</c>, <c>reads.json</c>) share when
/// they read and shape their JSON root (DEDUP5, duplication survey §5): each had copied the load, the case-blind key, the child
/// object and the strict moment. The bytes each writes are its own and unchanged; the CLI's twins read the same files.
/// </summary>
internal static class AccountFileNodes
{
    /// <summary>The file's root, and whether it was there and did not read; a missing file is an empty root.</summary>
    public static (JsonObject Root, bool Unreadable) Read(string path)
    {
        if (!File.Exists(path)) return (new JsonObject(), false);
        try
        {
            return JsonNode.Parse(AtomicFile.ReadText(path)) is JsonObject root ? (root, false) : (new JsonObject(), true);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return (new JsonObject(), true);
        }
    }

    /// <summary>The file's root, or an empty one when it is missing or does not read.</summary>
    public static JsonObject Load(string path) => Read(path).Root;

    /// <summary>A name in an object, compared without case, as the readings compare names.</summary>
    public static string? Key(JsonObject held, string name) =>
        held.Select(pair => pair.Key).FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The object under a name, found without case; null when there is no parent, no such name, or it is no object.</summary>
    public static JsonObject? Child(JsonObject? parent, string name) =>
        parent is not null && Key(parent, name) is { } key ? parent[key] as JsonObject : null;

    /// <summary>The object under a name, made and added when it is not there.</summary>
    public static JsonObject Made(JsonObject parent, string name) =>
        Child(parent, name) ?? (JsonObject)(parent[name] = new JsonObject());

    /// <summary>
    /// A moment as ISO 8601 writes it, and only so: a lenient parse reads <i>Oct 3</i> as this year's, which would make a
    /// hand-mangled entry a held window or cooling.
    /// </summary>
    public static DateTimeOffset? Moment(JsonObject entry, string name) => Moments.ReadStrict(Json.Text(entry, name));
}
