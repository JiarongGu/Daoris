using System.Globalization;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The one way a moment is written and read in the core (DEDUP9b). Every store and wire had its own reader and
/// writer, and they drifted: some read in the machine's culture, some turned a stored offset into local time, and
/// most writers left the culture out, so a calendar other than the Gregorian one would have written other digits.
/// </summary>
/// <remarks>
/// A moment is written round-trip (<c>O</c>: seven fraction digits and the offset as it was) in the invariant culture,
/// and read in the invariant culture with <see cref="DateTimeStyles.RoundtripKind"/>, so the offset a record was
/// written with is the offset read back. A reader answers null for text that is no moment; <see cref="ReadRequired"/>
/// is for a column the store wrote itself, where unreadable is a corrupt store and throws as <c>Parse</c> did.
/// </remarks>
internal static class Moments
{
    public static string Write(DateTimeOffset moment) => moment.ToString("O", CultureInfo.InvariantCulture);

    public static string? Write(DateTimeOffset? moment) => moment is { } value ? Write(value) : null;

    public static DateTimeOffset? Read(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var moment) ? moment : null;

    /// <summary>The string field <paramref name="name"/> of <paramref name="element"/> as a moment; null when absent, not a string or unreadable.</summary>
    public static DateTimeOffset? Read(JsonElement element, string name) => Read(JsonFields.Text(element, name));

    public static DateTimeOffset ReadRequired(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
