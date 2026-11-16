using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// A second opinion nobody is free to give: every declared reviewer held (cooling, signed out, none installed or refused), a subject of the
/// reporting line's lead rather than a wait with nobody asked.
/// </summary>
/// <param name="Code">Why no reviewer reads it (<see cref="ReviewerUnavailable"/>).</param>
/// <param name="Until">The first reset among the reviewers cooling; none where none is.</param>
/// <param name="Back">The reviewer that reset frees; none where the gate alone is read.</param>
public sealed record OpinionVacancy(string Code, DateTimeOffset? Until, string? Back)
{
    private static readonly IReadOnlyList<string> Held =
    [
        ReviewerUnavailable.NoReviewer, ReviewerUnavailable.Cooling, ReviewerUnavailable.SignedOut, ReviewerUnavailable.NotIndependent,
        ReviewerUnavailable.Refused,
    ];

    /// <summary>The vacancy a walk that chose nobody leaves; none where it chose a reviewer.</summary>
    public static OpinionVacancy? Of(ReviewerChoice choice) =>
        choice.Chosen ? null : new(choice.Code ?? ReviewerUnavailable.Refused, choice.Cooling?.Until, choice.Cooling?.Agent);

    /// <summary>The vacancy a gate holds for want of a reviewer; none for a gate standing anywhere else, or a pass that ended.</summary>
    public static OpinionVacancy? Of(OpinionGateState gate) =>
        gate is { State: OpinionGateStates.Unavailable, Code: { } code } && Held.Contains(code, StringComparer.Ordinal) ? new(code, gate.Until, null) : null;

    /// <summary><c>waiting on lead: `role` · second opinion: nobody free, `codex-acp` back 07:02</c>, the reset in <paramref name="zone"/>.</summary>
    public string Said(LineWait wait, TimeZoneInfo zone)
    {
        var back = Until is { } until
            ? $", {(Back is null ? "" : $"`{Back}` ")}back {TimeZoneInfo.ConvertTime(until, zone).ToString("HH:mm", CultureInfo.InvariantCulture)}"
            : "";
        return $"{wait.Said()} · second opinion: nobody free{back}";
    }
}

/// <summary>
/// One act of a line level's holder on a vacancy, journalled beside the gate (<see cref="OpinionCasts"/>): a re-cast on another family or
/// account, the family difference relaxed, the prepaid balance spent, the reset waited for, or <c>left</c>, its pass up.
/// </summary>
/// <param name="Actor">The level that acted (<see cref="LineActors"/>).</param>
/// <param name="Role">The role holding that level.</param>
/// <param name="Act">One of the vacancy's acts (<see cref="SupervisorKinds.Vacancy"/>), or <see cref="LinePasses.Left"/>.</param>
/// <param name="Agent">The agent a re-cast names; none for the other acts.</param>
/// <param name="Why">The holder's reading, which the record and a pass up carry.</param>
public sealed record OpinionCast(DateTimeOffset At, string Session, string Actor, string Role, string Act, string? Agent, string Why);

/// <summary>What the journalled acts make of the next walk, and of the line.</summary>
public static class OpinionCasting
{
    /// <summary>The harness whose prepaid key reviews may spend, the one paid account a vacancy may use unasked (<c>daoris.gates.json</c>).</summary>
    public const string PrepaidAgent = "dsh";

    /// <summary>The act the look journals when nobody is free to read the work, by the step's own actor (<see cref="Step"/>).</summary>
    public const string Vacant = "vacant";

    /// <summary>The actor whose step stands vacant: the second opinion.</summary>
    public const string Step = "opinion";

    /// <summary>
    /// The reviewers the next walk reads after <paramref name="cast"/>: a re-cast's agent first; the prepaid harness first; relaxed, the
    /// work's own families after the rule's, read as not independent and said so on the opinion; waiting, the rule's as they are.
    /// </summary>
    public static IReadOnlyList<string> Reviewers(IReadOnlyList<string> reviewers, OpinionCast? cast, IReadOnlyCollection<string> working) =>
        cast?.Act switch
        {
            SupervisorKinds.Recast when cast.Agent is { Length: > 0 } agent => First(agent, reviewers),
            SupervisorKinds.Prepaid => First(PrepaidAgent, reviewers),
            SupervisorKinds.Relax => [.. reviewers, .. working.Where(each => !reviewers.Contains(each, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase)],
            _ => reviewers,
        };

    /// <summary>The newest act that shapes a walk, its leaves apart.</summary>
    public static OpinionCast? Newest(IEnumerable<OpinionCast> casts) => casts.Where(cast => cast.Act is not (LinePasses.Left or Vacant)).MaxBy(cast => cast.At);

    /// <summary>
    /// Whether the newest journalled act leaves the vacancy waiting on the line: the look's <c>vacant</c>, or a level's leave; a level's
    /// other act answers it until the look journals it vacant again.
    /// </summary>
    public static bool Waiting(IReadOnlyList<OpinionCast> casts) => casts.Count > 0 && casts[^1].Act is Vacant or LinePasses.Left;

    /// <summary>The levels whose newest act passed the vacancy up, each with its reading, as <see cref="ReportingLine.Climb"/> reads them.</summary>
    public static IReadOnlyDictionary<string, string> Left(IEnumerable<OpinionCast> casts) =>
        casts.GroupBy(cast => cast.Actor, StringComparer.Ordinal).Select(level => level.MaxBy(cast => cast.At)!)
            .Where(cast => cast.Act == LinePasses.Left).ToDictionary(cast => cast.Actor, cast => cast.Why, StringComparer.Ordinal);

    private static IReadOnlyList<string> First(string agent, IReadOnlyList<string> reviewers) =>
        [agent, .. reviewers.Where(each => !string.Equals(each, agent, StringComparison.OrdinalIgnoreCase))];
}
