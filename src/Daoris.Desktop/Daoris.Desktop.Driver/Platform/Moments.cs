using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The one way a moment is written and read in the driver (DEDUP2a), the twin of the service core's <c>Moments</c>
/// (DEDUP9b; the projects share no code). Every store and wire had its own reader and writer, and they drifted: some read
/// in the machine's culture, some turned a stored offset into local time, and some writers left the culture out, so a
/// calendar other than the Gregorian one would have written other digits.
/// </summary>
/// <remarks>
/// A moment is written round-trip (<c>O</c>: seven fraction digits and the offset as it was) in the invariant culture,
/// and read in the invariant culture with <see cref="DateTimeStyles.RoundtripKind"/>, so the offset a record was written
/// with is the offset read back. A reader answers null for text that is no moment; each caller keeps its own fallback
/// (now, a file's time, <c>MinValue</c>). <see cref="ReadStrict"/> is for a file a person or the CLI also writes, where
/// a lenient parse would read <i>Oct 3</i> as this year's.
/// </remarks>
public static class Moments
{
    private static readonly string[] IsoMoments =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
    ];

    public static string Write(DateTimeOffset moment) => moment.ToString("O", CultureInfo.InvariantCulture);

    public static string? Write(DateTimeOffset? moment) => moment is { } value ? Write(value) : null;

    public static DateTimeOffset? Read(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var moment) ? moment : null;

    /// <summary>The string field <paramref name="name"/> of <paramref name="element"/> as a moment; null when absent, not a string or unreadable.</summary>
    public static DateTimeOffset? Read(JsonElement element, string name) => Read(JsonText.Of(element, name));

    /// <summary>The string a node holds as a moment; null when it is missing, not a string or unreadable.</summary>
    public static DateTimeOffset? Read(JsonNode? node) => Read(JsonText.Of(node));

    /// <summary>
    /// A moment as ISO 8601 writes it in the four forms the cooling, window and config files hold (UTC <c>Z</c> or an offset,
    /// with or without a fraction), and only so; the answer is in UTC. Null for anything else.
    /// </summary>
    public static DateTimeOffset? ReadStrict(string? text) =>
        !string.IsNullOrEmpty(text)
        && DateTimeOffset.TryParseExact(
            text, IsoMoments, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var moment)
            ? moment
            : null;
}
