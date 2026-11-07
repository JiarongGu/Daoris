using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>
/// Whose words a part of a session's note is: a words part is shown as
/// written, never through a catalogue.
/// </summary>
public static class NoteBy
{
    /// <summary>The agent's own words: its question, its last words, a turn's failure in its words.</summary>
    public const string Agent = "agent";

    /// <summary>The person's words: an answer, a finish's note, a decline's reason, an ask's closing note.</summary>
    public const string Person = "person";

    /// <summary>A program's words passed through: an exception's message, git's.</summary>
    public const string Program = "program";

    /// <summary>An English note kept whole, with no parts.</summary>
    public const string Before = "before";
}

/// <summary>
/// One line of a session's note: a coded part, which the page words from its
/// code and values in the reader's language and whose <see cref="Text"/> is the English its writer wrote; or a words part,
/// someone's words, shown as written.
/// </summary>
/// <remarks>
/// <b>Values are facts, never sentences, and never a path or an account's name</b>: the note travels. A value is
/// text, a whole number, or a list of texts; one read back that is none of them is kept as read, so a newer writer's
/// part passes through this one unchanged. The one exception is a conversation's own lines, its first (<c>opening.*</c>),
/// a wait for its account (<c>waits.*</c>) and a move by the usage curve (<c>chat.moved</c>, <c>chat.moved-from</c>):
/// they are kept only in the conversation's record, which never leaves the machine, so their values name
/// accounts by id.
/// </remarks>
[JsonConverter(typeof(NotePartJson))]
public sealed record NotePart
{
    /// <summary>The part's code, one of <see cref="NoteCodes"/>; null for a words part.</summary>
    public string? Code { get; init; }

    /// <summary>The facts its line carries, in the order written.</summary>
    public IReadOnlyList<KeyValuePair<string, object>> Values { get; init; } = [];

    /// <summary>A coded part's English as its writer writes it: a substring of the note it is built into.</summary>
    public string? Text { get; init; }

    /// <summary>A words part's words.</summary>
    public string? Words { get; init; }

    /// <summary>Whose words, one of <see cref="NoteBy"/>.</summary>
    public string? By { get; init; }

    /// <summary>The value written under this name, or null.</summary>
    public object? Value(string name)
    {
        foreach (var (key, value) in Values)
        {
            if (string.Equals(key, name, StringComparison.Ordinal)) return value;
        }

        return null;
    }

    /// <summary>Someone's words as a part of their own.</summary>
    public static NotePart Said(string words, string by) => new() { Words = words, By = by };

    /// <summary>The parts as the record's JSON spells them, an array.</summary>
    public static void Write(Utf8JsonWriter writer, IReadOnlyList<NotePart> parts)
    {
        writer.WriteStartArray();
        foreach (var part in parts) NotePartJson.WritePart(writer, part);
        writer.WriteEndArray();
    }

    /// <summary>
    /// The parts a record answers, read without trusting the shape: an entry that is neither a coded part nor a words part
    /// is skipped. Null where the record answers none, a record without parts, which is not the same as no parts.
    /// </summary>
    public static IReadOnlyList<NotePart>? Read(JsonElement record, string name = "noteParts")
    {
        return record.ValueKind == JsonValueKind.Object && record.TryGetProperty(name, out var parts) ? ReadParts(parts) : null;
    }

    /// <summary>The parts as a record's array holds them, read without trusting the shape; null where it is no array.</summary>
    public static IReadOnlyList<NotePart>? ReadParts(JsonElement parts) =>
        parts.ValueKind == JsonValueKind.Array ? [.. parts.EnumerateArray().Select(NotePartJson.ReadPart).OfType<NotePart>()] : null;
}

/// <summary>
/// A part as JSON, whichever serializer meets it: the record's own shape, <c>{code, values, text}</c> or <c>{words, by}</c>,
/// so the modules hand a part on to the page as the service answers it.
/// </summary>
public sealed class NotePartJson : JsonConverter<NotePart>
{
    public override NotePart? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return ReadPart(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, NotePart value, JsonSerializerOptions options) => WritePart(writer, value);

    internal static void WritePart(Utf8JsonWriter writer, NotePart part)
    {
        writer.WriteStartObject();
        if (part.Code is { } code)
        {
            writer.WriteString("code", code);
            writer.WriteStartObject("values");
            foreach (var (name, value) in part.Values)
            {
                writer.WritePropertyName(name);
                WriteValue(writer, value);
            }

            writer.WriteEndObject();
            if (part.Text is not null) writer.WriteString("text", part.Text);
        }
        else
        {
            writer.WriteString("words", part.Words ?? "");
            writer.WriteString("by", part.By ?? NoteBy.Before);
        }

        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case string text:
                writer.WriteStringValue(text);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case IEnumerable<string> texts:
                writer.WriteStartArray();
                foreach (var text in texts) writer.WriteStringValue(text);
                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    internal static NotePart? ReadPart(JsonElement part)
    {
        if (part.ValueKind != JsonValueKind.Object) return null;
        if (Json.Text(part, "code") is { Length: > 0 } code)
        {
            var values = new List<KeyValuePair<string, object>>();
            if (part.TryGetProperty("values", out var given) && given.ValueKind == JsonValueKind.Object)
            {
                foreach (var value in given.EnumerateObject())
                {
                    values.Add(new(value.Name, ReadValue(value.Value)));
                }
            }

            return new NotePart { Code = code, Values = values, Text = Json.Text(part, "text") };
        }

        return Json.Text(part, "words") is { } words ? NotePart.Said(words, Json.Text(part, "by") ?? NoteBy.Before) : null;
    }

    private static object ReadValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number when value.TryGetInt64(out var number) => number,
        JsonValueKind.Array when value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String) =>
            (IReadOnlyList<string>)[.. value.EnumerateArray().Select(item => item.GetString()!)],
        _ => value.Clone(),
    };
}

/// <summary>
/// A note as its writer composes it: its English, <see cref="Note"/>, byte for byte as the record
/// keeps it, and its parts, each coded part's text a substring of it by construction.
/// </summary>
/// <remarks>
/// Composed by joining: <see cref="Then(string, Noted)"/> adds glue and a line, <see cref="Then(string)"/> glue alone, and
/// <see cref="Also"/> a part whose text the English already holds, where a writer's English interleaves two lines (an exit
/// inside a declined sentence, a program's words inside a tree's).
/// </remarks>
public sealed record Noted(string Note, IReadOnlyList<NotePart> Parts)
{
    /// <summary>Someone's words, as a part of their own; nothing for no words.</summary>
    public static Noted Said(string words, string by) => new(words, words.Length == 0 ? [] : [NotePart.Said(words, by)]);

    /// <summary>
    /// A record's note as parts: its own parts, or its note whole as one <see cref="NoteBy.Before"/>
    /// part where it has none, or nothing.
    /// </summary>
    public static Noted From(string? note, IReadOnlyList<NotePart>? parts) =>
        parts is { Count: > 0 } ? new(note ?? "", parts)
        : string.IsNullOrEmpty(note) ? new("", [])
        : Said(note, NoteBy.Before);

    /// <summary>This, the glue, then the line.</summary>
    public Noted Then(string glue, Noted next) => new(Note + glue + next.Note, [.. Parts, .. next.Parts]);

    /// <summary>This, then glue that is no line of its own.</summary>
    public Noted Then(string glue) => new(Note + glue, Parts);

    /// <summary>A part whose text this English already holds.</summary>
    public Noted Also(NotePart part) => new(Note, [.. Parts, part]);

    /// <summary>
    /// This without someone's words that a coded line will say as facts: the last words part holding exactly
    /// <paramref name="words"/> by <paramref name="by"/>, and those words and the space before them out of the English. This,
    /// unchanged, where no such part is or the English does not hold them.
    /// </summary>
    public Noted Unsaid(string words, string by)
    {
        var at = -1;
        for (var part = Parts.Count - 1; part >= 0 && at < 0; part--)
        {
            if (Parts[part] is { Code: null } said && said.Words == words && said.By == by) at = part;
        }

        var start = at < 0 || words.Length == 0 ? -1 : Note.LastIndexOf(words, StringComparison.Ordinal);
        if (start < 0) return this;

        var from = start;
        while (from > 0 && char.IsWhiteSpace(Note[from - 1])) from--;
        return new(Note[..from] + Note[(start + words.Length)..], [.. Parts.Where((_, part) => part != at)]);
    }
}
