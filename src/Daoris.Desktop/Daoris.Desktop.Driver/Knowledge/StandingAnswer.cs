using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// How a standing answer is written into an instruction (KNOWUSE1b): beneath the quest, the person's words quoted verbatim
/// line by line, saying what it answers and that newer words win; bounded at what a door takes, saying what was cut.
/// </summary>
public static class StandingText
{
    /// <summary>The most characters of the answer an instruction quotes: what a door takes (<see cref="DriverConfig.StandingLimit"/>).</summary>
    public const int Limit = DriverConfig.StandingLimit;

    /// <summary>The answer beneath a quest, or nothing where the repository keeps none, so the instruction reads as it did.</summary>
    internal static string Beneath(StandingAnswer? standing, string repository)
    {
        if (standing is null) return "";

        var set = standing.At is { } at
            ? ", set " + at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
            : "";
        var text = new StringBuilder("\n");
        text.Append($"What the person has told this machine holds for every quest in `{repository}`, in their own words{set}: ")
            .Append("it answers what it covers, so do not ask them for what it already says. The quest's own words, and theirs ")
            .Append("on its ask, are newer and win where they differ.\n\n");

        var cut = standing.Says.Length > Limit;
        var quoted = cut ? standing.Says[..Limit] : standing.Says;
        text.Append(TextBounds.Quote(quoted, indent: "  "))
            .Append('\n');
        if (cut)
        {
            text.Append($"  (… and {standing.Says.Length - Limit} more characters of it, left out here to keep this instruction ")
                .Append("bounded; `daoris driver list` shows it whole.)\n");
        }

        return text.ToString();
    }
}
