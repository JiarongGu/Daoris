using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's message that carried a change of focus, as the conversation's file keeps it: the message by id and time, the place
/// before and after, and whether the driver widened a place no longer here. What the transcript draws its divider from, so a reload,
/// another window and a later reading draw the same; a conversation's first focus and a message that changed nothing keep none.
/// </summary>
public sealed record HelpFocusMark(string? Word, DateTimeOffset? At, HelpFocus From, HelpFocus To, bool Widened)
{
    /// <summary>The mark a JSON object spells, or null for one with no place before or after.</summary>
    public static HelpFocusMark? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty("from", out var from) || HelpFocus.Read(from) is not { } before) return null;
        if (!element.TryGetProperty("to", out var to) || HelpFocus.Read(to) is not { } after) return null;
        var widened = element.TryGetProperty("widened", out var flag) && flag.ValueKind == JsonValueKind.True;
        return new(Json.NonEmpty(element, "word"), Moments.Read(element, "at"), before, after, widened);
    }

    /// <summary>The mark as one JSON object, its absent message left out.</summary>
    public void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        if (Word is { } word) writer.WriteString("word", word);
        if (At is { } at) writer.WriteString("at", Moments.Write(at));
        writer.WritePropertyName("from");
        From.Write(writer);
        writer.WritePropertyName("to");
        To.Write(writer);
        if (Widened) writer.WriteBoolean("widened", true);
        writer.WriteEndObject();
    }

    /// <summary>The mark as <c>HELP_CONVERSATIONS</c> answers it: <c>{ word, at, from, to, widened }</c>.</summary>
    public IReadOnlyDictionary<string, object?> Wire() => new Dictionary<string, object?>
    {
        ["word"] = Word, ["at"] = At, ["from"] = From.Wire(), ["to"] = To.Wire(), ["widened"] = Widened,
    };
}
