using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The access setup a driven session started under, as its record keeps it (AUTONOMY1a, D184 point 1): each pause with its value, as
/// the driver resolved it. The ledger judges none of the values (D46 §7): the driver's <c>AccessRules</c> owns which pauses and values
/// there are, so a pause added there needs nothing here. It keeps only a short map of words, which is all a setup is.
/// </summary>
/// <remarks>
/// <b>This machine's own</b> (D47 §4): no feed carries it, and the host answers it on loopback only. Kept as compact JSON in the
/// record's <c>access</c> column; a record from before the column, or one told none, reads none.
/// </remarks>
public static class SessionAccess
{
    /// <summary>The most pauses a setup holds: more is no setup the driver made.</summary>
    public const int MostPauses = 16;

    /// <summary>The longest pause's name or value, in characters.</summary>
    public const int Longest = 40;

    /// <summary>
    /// The setup a door was told, judged as a short map of words (letters, digits and <c>-</c>): none for none or an empty one.
    /// </summary>
    /// <exception cref="ArgumentException">A setup that is not a short map of words: the caller's mistake, and nothing is kept.</exception>
    public static IReadOnlyDictionary<string, string>? Judged(IReadOnlyDictionary<string, string>? access)
    {
        if (access is null || access.Count == 0) return null;
        if (access.Count > MostPauses || access.Any(pause => !Word(pause.Key) || !Word(pause.Value)))
        {
            throw new ArgumentException(
                $"access is the setup a session started under: at most {MostPauses} pauses, each named by a word and given a word, "
                + $"letters, digits and `-`, at most {Longest} characters each.");
        }

        return new Dictionary<string, string>(access, StringComparer.Ordinal);
    }

    private static bool Word(string? word) =>
        word is { Length: > 0 and <= Longest } && word.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    /// <summary>The setup as the column keeps it: compact JSON, or null for none.</summary>
    public static string? Written(IReadOnlyDictionary<string, string>? access)
    {
        if (access is null) return null;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (pause, value) in access) writer.WriteString(pause, value);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The setup as the column holds it; none for none, and for anything that does not read as a map of words.</summary>
    public static IReadOnlyDictionary<string, string>? Read(string? kept)
    {
        if (string.IsNullOrWhiteSpace(kept)) return null;
        try
        {
            using var document = JsonDocument.Parse(kept);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            var access = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pause in document.RootElement.EnumerateObject())
            {
                if (pause.Value.ValueKind == JsonValueKind.String) access[pause.Name] = pause.Value.GetString()!;
            }

            return access.Count == 0 ? null : access;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
