namespace Daoris.Driver;

/// <summary>
/// A pass whose reviewer's session ended without giving an opinion (<c>ended</c>), ran out of its minutes first (<c>out-of-time</c>) or
/// whose provider kept failing (<c>transient</c>) is no question for the person: the gate's look asks again by itself, the next ready
/// reviewer in the rule's order first, a few times (<see cref="Bound"/>), the reason in the look's line. Past them the opinion is given up as the
/// rule says: where it requires one its step is stopped by Daoris with the reason, which ends its run and holds no row for anyone; where it
/// does not, the step is passed over by the gate's own record. A cool-off a pass met (<c>limited</c>) is the look's cool-off re-ask, not this.
/// The person's own ask (<c>daoris session opinion ask</c>) is never capped, and starts the count afresh.
/// </summary>
public static class OpinionAgain
{
    /// <summary>The passes one ask chain takes, the first included, before its opinion is given up.</summary>
    public const int Bound = 3;

    /// <summary>The code of a required opinion given up as said.</summary>
    public const string GaveUp = "unsaid";

    /// <summary>Whether <paramref name="code"/> is a pass that ended with no opinion given, which the look asks again.</summary>
    public static bool Is(string? code) => code is OpinionStepWaits.Ended or OpinionStepWaits.OutOfTime or OpinionViews.Transient;

    /// <summary>Whether the gate stands on such a pass with asks left: its look asks again.</summary>
    public static bool Pending(OpinionGateState gate) =>
        gate is { State: OpinionGateStates.Unavailable, Opinion: not null, Required: true } && Is(gate.Code) && gate.Ended < Bound;

    /// <summary>Whether the gate stands on such a pass with its asks spent, the rule requiring one: given up.</summary>
    public static bool Spent(OpinionGateState gate) =>
        gate is { State: OpinionGateStates.Unavailable, Opinion: not null, Required: true } && Is(gate.Code) && gate.Ended >= Bound;

    /// <summary>
    /// The reviewers of the next walk: those of the rule in their order with the one that ended last, <paramref name="ended"/>, after the others, so the
    /// next ready reviewer reads first; where it is the only one, it is asked again.
    /// </summary>
    public static IReadOnlyList<string> Rotated(IReadOnlyList<string> reviewers, string? ended) =>
        ended is not { Length: > 0 } ? reviewers
            : [.. reviewers.Where(each => !string.Equals(each, ended, StringComparison.OrdinalIgnoreCase)),
               .. reviewers.Where(each => string.Equals(each, ended, StringComparison.OrdinalIgnoreCase))];

    /// <summary>The words a required opinion is given up with: why, and that Daoris gave it up.</summary>
    public static string GaveUpWords(OpinionGateState gate) =>
        $"its reviewers ended without giving a second opinion in {gate.Ended} asks, so Daoris gave it up: "
        + OpinionGateWords.UnavailableWhy(gate.Code, gate.Until, gate.Opinion) + ".";
}
