using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The places a conversation spoke under as one JSON object: its focus, the one chosen and not yet applied, each place it held and
/// each message that changed it. The conversation's facts keep it in one column, and the home's migration reads an older
/// file's same properties through it.
/// </summary>
internal static class ConversationPlaces
{
    /// <summary><paramref name="kept"/>'s places as an object, or null where it holds none.</summary>
    public static string? Write(HelpKept kept)
    {
        if (kept.Focus is null && kept.FocusNext is null && kept.Foci is not { Count: > 0 } && kept.Marks.Count == 0) return null;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            if (kept.Focus is { } focus)
            {
                writer.WritePropertyName("focus");
                focus.Write(writer);
            }

            if (kept.FocusNext is { } next)
            {
                writer.WritePropertyName("focusNext");
                next.Write(writer);
            }

            if (kept.Foci is { Count: > 0 } foci)
            {
                writer.WriteStartArray("foci");
                foreach (var place in foci) place.Write(writer);
                writer.WriteEndArray();
            }

            if (kept.Marks.Count > 0)
            {
                writer.WriteStartArray("marks");
                foreach (var mark in kept.Marks) mark.Write(writer);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary><paramref name="kept"/> with the places <paramref name="root"/> spells; text that does not read leaves it as it is.</summary>
    public static HelpKept Read(HelpKept kept, string? places)
    {
        if (string.IsNullOrEmpty(places)) return kept;
        try
        {
            using var document = JsonDocument.Parse(places);
            return Read(kept, document.RootElement);
        }
        catch (JsonException)
        {
            return kept;
        }
    }

    public static HelpKept Read(HelpKept kept, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return kept;
        return kept with
        {
            Focus = Place(root, "focus"),
            FocusNext = Place(root, "focusNext"),
            Foci = root.TryGetProperty("foci", out var foci) && foci.ValueKind == JsonValueKind.Array
                ? foci.EnumerateArray().Select(HelpFocus.Read).OfType<HelpFocus>().Distinct().ToList() is { Count: > 0 } held ? held : null
                : null,
            Marks = root.TryGetProperty("marks", out var marks) && marks.ValueKind == JsonValueKind.Array
                ? [.. marks.EnumerateArray().Select(HelpFocusMark.Read).OfType<HelpFocusMark>()]
                : [],
        };
    }

    private static HelpFocus? Place(JsonElement element, string name) =>
        element.TryGetProperty(name, out var place) ? HelpFocus.Read(place) : null;
}
