namespace Daoris.Knowledge;

/// <summary>
/// How a done answers one requirement (DRIFT1d, D133 §4): <see cref="Met"/>, saying how its check was met, or
/// <see cref="Departed"/>, with the reason and the person's own words the departure turns on, quoted. One shape at
/// every door, in the log and on the wire; the exchange judges that exactly one of the two is said.
/// </summary>
/// <param name="Requirement">Which requirement it answers: its number, from 1, in the order the quest lists them.</param>
/// <param name="Met">How its check was met — null for a departure.</param>
/// <param name="Departed">Why the work departed from it — null for one met.</param>
/// <param name="Quote">A departure's: the person's own words it turns on, quoted from what they said on the ask.</param>
public sealed record QuestAnswer(int Requirement, string? Met, string? Departed = null, string? Quote = null)
{
    /// <summary>Whether the work departed from its requirement rather than met it.</summary>
    public bool IsDeparture => Departed is not null;
}

/// <summary>
/// One thing the person requires of a quest (DRIFT1c, D133 §3): their own words, quoted, and the check that
/// proves the work meets them.
/// </summary>
/// <param name="Quote">The person's words, verbatim, found in what they said on the ask the quest is asked by.</param>
/// <param name="Check">How to tell the work meets them — written by whoever composed the quest.</param>
public sealed record QuestRequirement(string Quote, string Check)
{
    /// <summary>
    /// The facts its check turns on that Daoris reads itself (EVID1a, D144 point 1): at most
    /// <see cref="QuestEvidence.MaxItems"/>, each a path the done's commit must hold or a gate. Empty for a requirement
    /// that names none, which closes exactly as before: on the session's word.
    /// </summary>
    public IReadOnlyList<QuestEvidence> Evidence { get; init; } = [];

    // Compared by what it names, not by which list holds it: a requirement read back from the log is the one published.
    public bool Equals(QuestRequirement? other) =>
        other is not null && Quote == other.Quote && Check == other.Check && Evidence.SequenceEqual(other.Evidence);

    public override int GetHashCode() => HashCode.Combine(Quote, Check, Evidence.Count);

    /// <summary>
    /// Whether <paramref name="quote"/> stands in <paramref name="words"/> verbatim, whitespace and case aside
    /// (DRIFT1c): every run of white space reads as one space and the ends are trimmed, so a line break or a
    /// doubled space the person typed is not a different word, and neither is a capital. Nothing else is
    /// forgiven — a reworded quote is a paraphrase, and a paraphrase is what drifted.
    /// </summary>
    public static bool QuotedIn(string quote, string words)
    {
        var folded = Folded(quote);
        return folded.Length > 0 && Folded(words).Contains(folded, StringComparison.Ordinal);
    }

    private static string Folded(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
