using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Words a person or a plugin writes into configuration: a JSON string as written, or an object of language tags to text with at
/// least one entry. It needs no language; a page shows it in the viewer's language, else the viewer's base language (<c>zh-TW</c> → <c>zh</c>),
/// else as written (the string, or the first entry). The one reader of the shape on this side; the page's is <c>locales/authored.ts</c>.
/// </summary>
[JsonConverter(typeof(AuthoredTextConverter))]
public sealed class AuthoredText : IEquatable<AuthoredText>
{
    private static readonly Regex Tag = new("^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonWriterOptions Compacted = new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly string? _plain;

    private readonly IReadOnlyList<KeyValuePair<string, string>> _entries;

    private AuthoredText(string? plain, IReadOnlyList<KeyValuePair<string, string>> entries)
    {
        _plain = plain;
        _entries = entries;
    }

    /// <summary>One text as the author wrote it, in no language; written as a plain string.</summary>
    public static AuthoredText Of(string text) => new(text, []);

    /// <summary>
    /// Texts by language tag, in the order given, or null where there are none or one is blank or its tag is not a language tag. A later
    /// entry of a tag already given replaces the earlier text and keeps its place.
    /// </summary>
    public static AuthoredText? FromTags(IEnumerable<KeyValuePair<string, string>> entries)
    {
        var kept = new List<KeyValuePair<string, string>>();
        foreach (var (tag, text) in entries)
        {
            if (!Tag.IsMatch(tag) || string.IsNullOrWhiteSpace(text)) return null;
            var at = kept.FindIndex(each => string.Equals(each.Key, tag, StringComparison.OrdinalIgnoreCase));
            if (at >= 0) kept[at] = new(kept[at].Key, text);
            else kept.Add(new(tag, text));
        }

        return kept.Count == 0 ? null : new(null, kept);
    }

    /// <summary>
    /// The words <paramref name="element"/> holds, or null where it is no authored text: not a string or an object of tags to non-blank
    /// strings, or any text longer than <paramref name="most"/> characters (0 sets no bound), or, for <paramref name="oneLine"/>, holding a break.
    /// </summary>
    public static AuthoredText? Read(JsonElement element, int most = 0, bool oneLine = false)
    {
        bool Fits(string text) => !string.IsNullOrWhiteSpace(text) && (most == 0 || text.Length <= most) && !(oneLine && text.AsSpan().ContainsAny('\r', '\n'));
        if (element.ValueKind == JsonValueKind.String) return element.GetString() is { } said && Fits(said) ? Of(said) : null;
        if (element.ValueKind != JsonValueKind.Object) return null;
        var entries = new List<KeyValuePair<string, string>>();
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String || property.Value.GetString() is not { } text || !Fits(text)) return null;
            entries.Add(new(property.Name, text));
        }

        return FromTags(entries);
    }

    /// <summary>The text as written: the string, or the first entry.</summary>
    public string AsWritten => _plain ?? _entries[0].Value;

    /// <summary>The language tags the author gave, in order; none for a plain string.</summary>
    public IReadOnlyList<string> Tags => [.. _entries.Select(each => each.Key)];

    /// <summary>Every text the author wrote, for a search that reads them all.</summary>
    public IReadOnlyList<string> Texts => _plain is not null ? [_plain] : [.. _entries.Select(each => each.Value)];

    /// <summary>The text for <paramref name="language"/>, else for its base language, else as written.</summary>
    public string In(string language)
    {
        if (_plain is not null) return _plain;
        if (Find(language) is { } exact) return exact;
        var dash = language.IndexOf('-');
        return dash > 0 && Find(language[..dash]) is { } basic ? basic : AsWritten;
    }

    /// <summary>The text written for exactly <paramref name="tag"/>, or null.</summary>
    public string? Find(string tag) => _entries.FirstOrDefault(each => string.Equals(each.Key, tag, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>
    /// This text with <paramref name="text"/> added or replaced for <paramref name="tag"/>; a plain string becomes the first entry, tagged
    /// <paramref name="plainTag"/>. Null where the tag is no language tag or the text is blank.
    /// </summary>
    public AuthoredText? With(string tag, string text, string plainTag = "en") =>
        FromTags([.. _plain is not null ? [new KeyValuePair<string, string>(plainTag, _plain)] : _entries, new(tag, text)]);

    /// <summary>Writes the value as JSON: a plain string, or the object of tags.</summary>
    public void Write(Utf8JsonWriter writer)
    {
        if (_plain is not null)
        {
            writer.WriteStringValue(_plain);
            return;
        }

        writer.WriteStartObject();
        foreach (var (tag, text) in _entries) writer.WriteString(tag, text);
        writer.WriteEndObject();
    }

    /// <summary>The value as compact JSON.</summary>
    public string Json()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Compacted)) Write(writer);
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    public bool Equals(AuthoredText? other) =>
        other is not null && _plain == other._plain && _entries.SequenceEqual(other._entries);

    public override bool Equals(object? obj) => obj is AuthoredText other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_plain, _entries.Count, _entries.FirstOrDefault().Key);

    public override string ToString() => AsWritten;
}

internal sealed class AuthoredTextConverter : JsonConverter<AuthoredText>
{
    public override AuthoredText? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return AuthoredText.Read(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, AuthoredText value, JsonSerializerOptions options) => value.Write(writer);
}
