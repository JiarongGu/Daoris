namespace Daoris.Driver;

/// <summary>
/// The tier a brainstorm search found its sources by (BRAINSTORM1a, TIER1, D24), as the service client names it: <see cref="Meaning"/> where
/// the search answered by semantic reading, <see cref="Words"/> otherwise. <see cref="BrainstormPackets.Meaning"/> and
/// <see cref="BrainstormPackets.Words"/> are these, for the packet's reading.
/// </summary>
public static class BrainstormTier
{
    /// <summary>The search answered by meaning.</summary>
    public const string Meaning = "meaning";

    /// <summary>The search answered by words.</summary>
    public const string Words = "words";
}

/// <summary>
/// What a search of Daoris's own knowledge found for a brainstorm's packet (BRAINSTORM1a, D176 point 2), before the bounds: an
/// entry's kind, its path from the repository's root, its lines, its text, and the words that found it.
/// </summary>
/// <param name="Words">The wording of the question the search was asked in.</param>
public sealed record BrainstormFound(string Kind, string Path, int? First, int? Last, string Text, string Words);

/// <summary>What one wording's search found, and the tier that answered: <c>meaning</c>, <c>words</c> or <c>files</c>.</summary>
public sealed record BrainstormSearch(IReadOnlyList<BrainstormFound> Found, string Tier);
