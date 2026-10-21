using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What the look judged it may drop of a tree it sets aside, beside where the folder goes (KEEP1a, D195 point 3; the cleanup design §2.3
/// and §5, Codex 2.4): <c>trees/.tidied/&lt;ws&gt;/&lt;repo&gt;/&lt;name&gt;-&lt;moment&gt;.slim.json</c>, written by the look before the move
/// (<see cref="SlimStates.Judged"/>) and after it (<see cref="SlimStates.Moved"/>), then by the slim as each path goes. It lives and goes
/// with its folder; a file, so the folders' enumerations pass it over.
/// </summary>
/// <remarks>
/// Only a manifest at <see cref="SlimStates.Moved"/> is ever slimmed: one left at <see cref="SlimStates.Judged"/>, where the process
/// stopped between the move and that write, is unknown, so its folder is kept whole for its days. An older build reads no manifest: its
/// purge keeps a slimmed folder as written, which keeps more than it should, never less (<c>compatible-builds.md</c>). A manifest of
/// another schema is never read as this one.
/// </remarks>
/// <param name="Judged">When the look judged it, before the move.</param>
/// <param name="Moved">When the move ended and git let go of the tree; null before.</param>
/// <param name="Paths">Each package folder judged, relative to the tree's top in git's spelling.</param>
internal sealed record SlimManifest(
    string State, DateTimeOffset Judged, DateTimeOffset? Moved, string Repository, string Branch, IReadOnlyList<SlimPath> Paths)
{
    /// <summary>The shape this build writes and reads.</summary>
    internal const int Schema = 1;

    /// <summary>What follows the folder's name in its manifest's.</summary>
    internal const string Suffix = ".slim.json";

    private const string Moment = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>Where the manifest of <paramref name="folder"/> is: beside it, its name with <see cref="Suffix"/> after it.</summary>
    internal static string PathFor(string folder) => Path.TrimEndingDirectorySeparator(folder) + Suffix;

    /// <summary>The folder a manifest is of; null where its name is no manifest's.</summary>
    internal static string? FolderOf(string manifest) =>
        manifest.EndsWith(Suffix, StringComparison.Ordinal) ? manifest[..^Suffix.Length] : null;

    /// <summary>The manifest at <paramref name="path"/>: none where there is no file; else it, or why it does not read as one.</summary>
    internal static (SlimManifest? Manifest, string? Problem) Read(string path)
    {
        string text;
        try
        {
            if (!File.Exists(path)) return (null, null);
            text = File.ReadAllText(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (null, error.Message);
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("schema", out var schema) || !schema.TryGetInt32(out var stamped) || stamped != Schema
                || Text(root, "state") is not { } state || At(root, "judged") is not { } judged
                || Text(root, "repository") is not { } repository || Text(root, "branch") is not { } branch
                || !root.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Array)
            {
                return (null, "it is no manifest of this build's shape");
            }

            var read = new List<SlimPath>();
            foreach (var each in paths.EnumerateArray())
            {
                if (each.ValueKind != JsonValueKind.Object || Text(each, "path") is not { } relative || Text(each, "state") is not { } at)
                {
                    return (null, "a path in it does not read");
                }

                read.Add(new(relative, at, Text(each, "why"), At(each, "tried"), At(each, "ended")));
            }

            return (new(state, judged, At(root, "moved"), repository, branch, read), null);
        }
        catch (JsonException error)
        {
            return (null, error.Message);
        }

        static string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        static DateTimeOffset? At(JsonElement element, string name) =>
            Text(element, name) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
                ? at
                : null;
    }

    /// <summary>Written whole, beside then renamed (<see cref="AtomicFile"/>).</summary>
    internal void Write(string path) => AtomicFile.WriteText(path, JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("schema", Schema);
        writer.WriteString("state", State);
        writer.WriteString("judged", Said(Judged));
        WriteMoment(writer, "moved", Moved);
        writer.WriteString("repository", Repository);
        writer.WriteString("branch", Branch);
        writer.WriteStartArray("paths");
        foreach (var each in Paths)
        {
            writer.WriteStartObject();
            writer.WriteString("path", each.Path);
            writer.WriteString("state", each.State);
            if (each.Why is { } why) writer.WriteString("why", why);
            else writer.WriteNull("why");
            WriteMoment(writer, "tried", each.Tried);
            WriteMoment(writer, "ended", each.Ended);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }));

    private static string Said(DateTimeOffset at) => at.UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture);

    private static void WriteMoment(Utf8JsonWriter writer, string name, DateTimeOffset? at)
    {
        if (at is { } when) writer.WriteString(name, Said(when));
        else writer.WriteNull(name);
    }

    /// <summary>The paths the slim removed, in the manifest's order.</summary>
    internal IReadOnlyList<string> Removed => [.. Paths.Where(path => path.State == SlimStates.Removed).Select(path => path.Path)];
}

/// <summary>One package folder in a manifest, and its state (the cleanup design §5).</summary>
/// <param name="Path">Relative to the tree's top, in git's spelling: <c>node_modules</c>, <c>web/node_modules</c>.</param>
/// <param name="Why">Kept: why, a <see cref="SlimKept"/> code; removing: the last failure's words.</param>
/// <param name="Tried">When a removal last failed for the same reason as the try before it: it waits a day from here (§3.1).</param>
/// <param name="Ended">Removed: when its removal ended, against which the purge reads the folder above it (Fable F1.1).</param>
internal sealed record SlimPath(string Path, string State, string? Why = null, DateTimeOffset? Tried = null, DateTimeOffset? Ended = null)
{
    /// <summary>Whether its end is written: removed, or kept.</summary>
    internal bool Final => State is SlimStates.Removed or SlimStates.Kept;
}

/// <summary>A manifest's states, and each path's (the cleanup design §2.3).</summary>
public static class SlimStates
{
    /// <summary>The manifest: written before the move. Unknown on its own, so its folder is kept whole.</summary>
    public const string Judged = "judged";

    /// <summary>The manifest: the move ended and git let go of the tree. Only such a manifest is slimmed.</summary>
    public const string Moved = "moved";

    /// <summary>A path: waiting for the slim.</summary>
    public const string Pending = "pending";

    /// <summary>A path: its removal began and has not ended; the purge's read passes over it.</summary>
    public const string Removing = "removing";

    /// <summary>A path: removed, with when that ended.</summary>
    public const string Removed = "removed";

    /// <summary>A path: kept for its folder's days, with why.</summary>
    public const string Kept = "kept";
}

/// <summary>Why the slim kept a path (the cleanup design §5): a manifest's <c>why</c>, and the machine log's <c>tidied.slimmed</c> count.</summary>
public static class SlimKept
{
    /// <summary>Something in its folder was written after the move: the folder is kept whole for its days (step 1).</summary>
    public const string Written = "written";

    /// <summary>A link lies on the way from the trees home to it, it included, or its folder holds a link outside a build folder (step 2).</summary>
    public const string Linked = "linked";

    /// <summary>Its path is rooted, climbs with <c>..</c>, does not lie under its folder by whole segments, or names no package folder (step 2).</summary>
    public const string Refused = "refused";

    /// <summary>It is no longer there, or no longer a folder.</summary>
    public const string Gone = "gone";
}
