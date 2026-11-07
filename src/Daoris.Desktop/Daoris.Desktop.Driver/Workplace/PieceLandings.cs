namespace Daoris.Driver;

/// <summary>
/// A partner's record lands nothing: its piece reaches the line only through its lead,
/// which merges the frozen ref in its own tree. The one sentence every landing door refuses it in: the session's press, its plan and the
/// terminal's verb (<see cref="TreesWords.PartnerLands"/>), and a run's <c>land</c> (<see cref="LandPerformer"/>). The page's and an
/// entry's Accept never offer it, since the work orders' eligibility leaves a partner's tree out.
/// </summary>
public static class PieceLandings
{
    /// <summary>Why <paramref name="record"/> lands nothing, or null for a record that is no partner's.</summary>
    public static string? Refusal(SessionRecord? record) =>
        record is { Partner: true, Piece: { } piece }
            ? $"session `{record.Id}` is a partner writing piece `{piece}` for session `{record.Serves}`, and lands nothing: "
              + $"a piece lands only through its lead, which takes it in with `git merge --no-ff {PieceRefs.Of(piece)}` in its own tree."
            : null;

    /// <summary>The landing a run's <c>land</c> keeps for a partner's record, refused in <see cref="Refusal"/>'s words; null for any other.</summary>
    internal static TreeLanding? Refused(SessionRecord? record) =>
        Refusal(record) is { } said ? new TreeLanding(false, said) { Refusal = LandingCode.Refused } : null;
}
