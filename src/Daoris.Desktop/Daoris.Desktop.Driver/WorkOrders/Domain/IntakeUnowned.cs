namespace Daoris.Driver;

/// <summary>
/// The intake's one more answer beside routing the work: <i>no department owns this</i>. It is the last line of the intake's last
/// message, <c>no-department-owns-this: &lt;the work, and why nothing here owns it&gt;</c>, read from what the intake last said and
/// never from what it says of itself elsewhere. The driver reads it where an intake ended cleanly with nothing published
/// (<see cref="IntakeObservation.Conclude"/>), and the intake's park then carries the sentence that names Ask Daoris. The intake
/// creates nothing and routes nothing on it: opening a department is a press with a card, and an intake has none.
/// </summary>
public static class IntakeUnowned
{
    /// <summary>The line's code, written as the instruction tells the intake to write it.</summary>
    public const string Marker = "no-department-owns-this:";

    /// <summary>The most a reason may say: one sentence, not the intake's reasoning.</summary>
    public const int ReasonChars = 300;

    /// <summary>
    /// The reason the last line of <paramref name="lastWords"/> gives, or null where it is not the verdict: the marker is the
    /// whole of the last non-empty line (a surrounding pair of backticks aside, its case free), it names the work, and what it
    /// names fits <see cref="ReasonChars"/>. A marker inside prose or on an earlier line, one that names nothing, and a
    /// reason past the bound are no verdict; the intake's ordinary park stands then, never a guessed one.
    /// </summary>
    public static string? Read(string? lastWords)
    {
        if (lastWords is null) return null;
        var line = lastWords.Split('\n').Select(part => part.Trim()).LastOrDefault(part => part.Length > 0);
        if (line is null) return null;
        if (line.Length > 1 && line[0] == '`' && line[^1] == '`') line = line[1..^1].Trim();
        if (!line.StartsWith(Marker, StringComparison.OrdinalIgnoreCase)) return null;
        var reason = line[Marker.Length..].Trim();
        return reason.Length is > 0 and <= ReasonChars ? reason : null;
    }
}
