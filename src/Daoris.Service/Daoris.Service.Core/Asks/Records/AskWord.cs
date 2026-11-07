namespace Daoris.Knowledge;

/// <summary>One thing the person said on an ask, verbatim.</summary>
/// <param name="Kind">How they gave it.</param>
/// <param name="Text">Their words, as given, trimmed at the ends only.</param>
/// <param name="At">When they gave it.</param>
/// <param name="Session">The session they gave it to; null for the ask's own sentence.</param>
/// <param name="Quest">The quest that session worked; null for the ask's sentence and for an intake, which works none yet.</param>
public sealed record AskWord(AskWordKind Kind, string Text, DateTimeOffset At, string? Session = null, string? Quest = null)
{
    /// <summary>The kind's spelling, in the store and on the wire: a not-yet as the verdict says it.</summary>
    public static string Spell(AskWordKind kind) => kind == AskWordKind.NotYet ? Reviews.NotYet : kind.ToString().ToLowerInvariant();

    /// <summary>The reverse of <see cref="Spell"/>: null for a kind this build does not know.</summary>
    internal static AskWordKind? Parse(string? spelled) =>
        Enum.GetValues<AskWordKind>().Where(kind => Spell(kind) == spelled).Select(kind => (AskWordKind?)kind).FirstOrDefault();
}
