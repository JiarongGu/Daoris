namespace Daoris.Knowledge;

/// <summary>
/// The mark a quest's publisher or its intake puts on the work (FOUNDATION1x, MODELROLE1l): a fact about the work, carrying no path or
/// name, which the receiving machine reads to choose the role that starts it. One field shared with MODELROLE1i's <c>exact</c>.
/// </summary>
public static class QuestWeights
{
    /// <summary>Design or decision work: a start takes the role taking the <c>design</c> capability, else Build's.</summary>
    public const string Design = "design";

    /// <summary>The build that must be exact (MODELROLE1i). Kept and carried; no start reads it yet.</summary>
    public const string Exact = "exact";

    /// <summary>
    /// The mark as it is kept and why it is refused: null or blank is none, <c>design</c> and <c>exact</c> are kept as written, anything
    /// else is refused, so no door keeps a word no machine reads.
    /// </summary>
    public static (string? Weight, string? Refusal) Judge(string? given)
    {
        if (string.IsNullOrWhiteSpace(given)) return (null, null);
        var kept = given.Trim();
        return kept is Design or Exact
            ? (kept, null)
            : (null, $"A weight is `{Design}` or `{Exact}`, or left out for none — `{kept}` is neither.");
    }
}
