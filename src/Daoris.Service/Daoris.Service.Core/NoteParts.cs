using System.Globalization;
using System.Text.Json;
using static Daoris.Knowledge.JsonFields;

namespace Daoris.Knowledge;

/// <summary>A code the ledger words a line of a session's note by (LANG1a, D142 points 3–4; the language design §4).</summary>
/// <param name="Code">The code, as the record and the page spell it.</param>
/// <param name="Values">The facts its line carries, each a placeholder in its entry in both catalogues.</param>
public sealed record LedgerNoteCode(string Code, IReadOnlyList<string> Values)
{
    /// <summary>The page's key for this code.</summary>
    public string CatalogueKey => $"note.{Code}";
}

/// <summary>
/// Every code the ledger writes a line of a session's note by (LANG1a, the language design §4, the service's own lines). A
/// twin (<c>.claude/knowledge/twins.md</c>): the driver's <c>NoteCodes</c> declares the driver's, the page's <c>note.json</c> in
/// both languages words each, and the web's <c>locales/note.test.ts</c> parses these declarations, one per line as written
/// here, to hold the catalogue to them. <c>LedgerNoteCodesTests</c> reads both catalogues, so a new line fails until both
/// languages word it.
/// </summary>
public static class LedgerNoteCodes
{
    public static readonly LedgerNoteCode Answered = new("ledger.answered", []);
    public static readonly LedgerNoteCode Parked = new("ledger.parked", []);
    public static readonly LedgerNoteCode WentOn = new("ledger.went-on", ["at"]);

    /// <summary>Every code above, read off the declarations so none escapes the catalogue test.</summary>
    public static IReadOnlyList<LedgerNoteCode> All { get; } =
    [
        .. typeof(LedgerNoteCodes).GetFields()
            .Where(field => field.FieldType == typeof(LedgerNoteCode))
            .Select(field => (LedgerNoteCode)field.GetValue(null)!),
    ];
}

/// <summary>
/// One line the ledger writes on a session's note (LANG1a): its own coded line, its English the text, or someone's words.
/// </summary>
public sealed record NoteLine
{
    public string? Code { get; private init; }

    public IReadOnlyList<(string Name, string Value)> Values { get; private init; } = [];

    public string? Text { get; private init; }

    public string? Words { get; private init; }

    public string? By { get; private init; }

    /// <exception cref="ArgumentException">A value the code does not declare: its entry could not say it.</exception>
    public static NoteLine Coded(LedgerNoteCode code, string text, params (string Name, string Value)[] values)
    {
        foreach (var (name, _) in values)
        {
            if (!code.Values.Contains(name))
            {
                throw new ArgumentException($"`{code.Code}` declares no value `{name}`, so no entry of it can say it", nameof(values));
            }
        }

        return new NoteLine { Code = code.Code, Values = values, Text = text };
    }

    /// <summary>Someone's words, as a part of their own: <c>agent</c>, <c>person</c>, <c>program</c> or <c>before</c>.</summary>
    public static NoteLine Said(string words, string by) => new() { Words = words, By = by };

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        if (Code is not null)
        {
            writer.WriteString("code", Code);
            writer.WriteStartObject("values");
            foreach (var (name, value) in Values) writer.WriteString(name, value);
            writer.WriteEndObject();
            writer.WriteString("text", Text);
        }
        else
        {
            writer.WriteString("words", Words);
            writer.WriteString("by", By);
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// A session note's parts as the store keeps them (LANG1a, D142 point 2; the language design §3): an ordered JSON list beside
/// the note, each a coded part (<c>code</c>, <c>values</c>, <c>text</c>) or a words part (<c>words</c>, <c>by</c>). Kept as text,
/// read and written by hand for the AOT reason every shape in Core is.
/// </summary>
/// <remarks>
/// <para><b>The note stays.</b> Each writer keeps composing its English exactly as before; the parts ride beside it, and a
/// reader that knows nothing of them reads the note as it always did.</para>
///
/// <para><b>Adding to a note</b> reads the record's parts and adds after them; a record with a note and no parts is carried
/// as one <c>before</c> part (<see cref="After"/>). <b>A move that writes a note without parts clears them</b>, the store's
/// rule (<see cref="SessionStore.SetStateAsync"/>).</para>
/// </remarks>
public static class NoteParts
{
    /// <summary>
    /// Parts as a door or a feed gives them, as the store keeps them: only what a part is, in order; an entry that is
    /// neither a coded part nor a words part is dropped. Null for no list, or one that kept nothing.
    /// </summary>
    public static string? Normalize(JsonElement? given) =>
        given is { ValueKind: JsonValueKind.Array } list ? Rewritten(list, clean: null) : null;

    /// <summary>
    /// The parts as they may leave this machine (D47 §4, REV3): every string in them, text, words and values alike, cleaned
    /// as the note is (<see cref="SessionNote.ForAnotherMachine"/>). A code and whose words a part is are identifiers, and stay.
    /// </summary>
    public static string? ForAnotherMachine(string? parts, Session session)
    {
        if (Parse(parts) is not { } list) return parts;
        return Rewritten(list, text => SessionNote.ForAnotherMachine(text, session) ?? text);
    }

    /// <summary>The parts as an element for an answer's field, or null.</summary>
    public static JsonElement? Element(string? parts) => Parse(parts);

    /// <summary>
    /// The record's parts, then these lines (the language design §3): its own parts where it has them, its note whole as one
    /// <c>before</c> part where it has a note and none, else nothing before them.
    /// </summary>
    /// <param name="thenParts">The parts of a note that follows the lines, as a move passed them, or null.</param>
    /// <param name="thenNote">That note, carried whole where it came with no parts.</param>
    public static string After(
        string? parts, string? note, IEnumerable<NoteLine> lines, string? thenParts = null, string? thenNote = null) => Written(writer =>
    {
        writer.WriteStartArray();
        Carried(writer, parts, note);
        foreach (var line in lines) line.Write(writer);
        Carried(writer, thenParts, thenNote);
        writer.WriteEndArray();
    });

    /// <summary>A note's own parts where it has them, else the note whole as one <c>before</c> part, else nothing.</summary>
    private static void Carried(Utf8JsonWriter writer, string? parts, string? note)
    {
        if (Parse(parts) is { } list && list.GetArrayLength() > 0)
        {
            foreach (var part in list.EnumerateArray()) part.WriteTo(writer);
        }
        else if (!string.IsNullOrWhiteSpace(note))
        {
            NoteLine.Said(note, "before").Write(writer);
        }
    }

    /// <summary>A moment as a value: ISO 8601 in UTC, to the second, which the page formats in the reader's language and zone.</summary>
    public static string Moment(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static JsonElement? Parse(string? parts)
    {
        if (string.IsNullOrWhiteSpace(parts)) return null;
        try
        {
            using var document = JsonDocument.Parse(parts);
            return document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Rewritten(JsonElement list, Func<string, string>? clean)
    {
        var kept = 0;
        var text = Written(writer =>
        {
            writer.WriteStartArray();
            foreach (var part in list.EnumerateArray())
            {
                if (WritePart(writer, part, clean)) kept++;
            }

            writer.WriteEndArray();
        });
        return kept == 0 ? null : text;
    }

    private static bool WritePart(Utf8JsonWriter writer, JsonElement part, Func<string, string>? clean)
    {
        if (part.ValueKind != JsonValueKind.Object) return false;
        if (Text(part, "code") is { Length: > 0 } code)
        {
            writer.WriteStartObject();
            writer.WriteString("code", code);
            writer.WriteStartObject("values");
            if (part.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Object)
            {
                foreach (var value in values.EnumerateObject())
                {
                    writer.WritePropertyName(value.Name);
                    WriteValue(writer, value.Value, clean);
                }
            }

            writer.WriteEndObject();
            if (Text(part, "text") is { } said) writer.WriteString("text", clean is null ? said : clean(said));
            writer.WriteEndObject();
            return true;
        }

        if (Text(part, "words") is not { } words) return false;
        writer.WriteStartObject();
        writer.WriteString("words", clean is null ? words : clean(words));
        // Whose words, as the writer said: a newer writer's word passes through, and none is an English note from before.
        writer.WriteString("by", Text(part, "by") is { Length: > 0 } by ? by : "before");
        writer.WriteEndObject();
        return true;
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonElement value, Func<string, string>? clean)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var text = value.GetString()!;
                writer.WriteStringValue(clean is null ? text : clean(text));
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteValue(writer, item, clean);
                writer.WriteEndArray();
                break;
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var field in value.EnumerateObject())
                {
                    writer.WritePropertyName(field.Name);
                    WriteValue(writer, field.Value, clean);
                }

                writer.WriteEndObject();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}
