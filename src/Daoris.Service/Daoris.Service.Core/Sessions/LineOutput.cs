using System.Globalization;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// A tool call's output as <c>line_read</c> shows it. The driver already cuts an output it records, keeping its head and
/// ending it with <c>… (N chars)</c>, so a longer one is read for the length that mark gives and shown once, never cut again
/// under a length of its own. No cut leaves half of a surrogate pair.
/// </summary>
public static partial class LineOutput
{
    /// <summary>The driver's mark after the head it kept, one character short where its cut fell before a surrogate pair.</summary>
    [GeneratedRegex(@"^(?<head>.+)… \((?<chars>\d+) chars\)$", RegexOptions.Singleline)]
    private static partial Regex DriverMark();

    /// <summary>The output as the reader reads it, with its real length said where a cut is made.</summary>
    public static string Shown(string output)
    {
        if (output.Length <= LineEvents.OutputLimit) return output;
        if (DriverMark().Match(output) is { Success: true } mark
            && mark.Groups["head"].Length <= LineEvents.OutputLimit
            && mark.Groups["head"].Length >= LineEvents.OutputLimit - 1
            && long.TryParse(mark.Groups["chars"].Value, CultureInfo.InvariantCulture, out var chars))
        {
            return Cut(mark.Groups["head"].Value, chars);
        }

        return Cut(output[..SafeEnd(output)], output.Length);
    }

    private static string Cut(string head, long chars) =>
        $"{head}… ({chars.ToString("N0", CultureInfo.InvariantCulture)} characters in all)";

    private static int SafeEnd(string text) =>
        char.IsHighSurrogate(text[LineEvents.OutputLimit - 1]) ? LineEvents.OutputLimit - 1 : LineEvents.OutputLimit;
}
