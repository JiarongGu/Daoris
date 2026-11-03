using System.Text;

namespace Daoris.Driver;

/// <summary>
/// How a standing answer is quoted (KNOWUSE1b): the person's words verbatim, line by line, bounded at what a door takes and saying what
/// was cut. The standing block says it as the person's choices on this machine (<see cref="StandingBlock.Choices"/>).
/// </summary>
public static class StandingText
{
    /// <summary>The most characters of the answer an instruction quotes: what a door takes (<see cref="DriverConfig.StandingLimit"/>).</summary>
    public const int Limit = DriverConfig.StandingLimit;

    /// <summary>The answer's words quoted, cut at <see cref="Limit"/> with the cut said.</summary>
    internal static string Quoted(StandingAnswer standing)
    {
        var cut = standing.Says.Length > Limit;
        var text = new StringBuilder(TextBounds.Quote(cut ? standing.Says[..Limit] : standing.Says, indent: "  "));
        if (cut)
        {
            text.Append($"\n  (… and {standing.Says.Length - Limit} more characters of it, left out here to keep this instruction ")
                .Append("bounded; `daoris driver list` shows it whole.)");
        }

        return text.ToString();
    }
}
