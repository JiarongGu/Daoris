namespace Daoris.Driver;

/// <summary>
/// The person's two acts on a piece after its partner's turn (COAGENT1b2, D199; the co-agent design §6.1's table): <i>Discard…</i>, which
/// removes the piece's ref and copy in one verified step, and <i>Hand over what it left</i>, which keeps, imports and hands the work of a
/// piece a restart left uncertain. Pure: which state admits which act, and what the act's file holds.
/// </summary>
public static class PartnerPersonActs
{
    public const string Discard = "discard";
    public const string HandOver = "hand-over";

    public static readonly IReadOnlyList<string> All = [Discard, HandOver];

    /// <summary>
    /// Whether a piece standing at <paramref name="state"/> takes the act. Discard: what holds work or a copy for the person's look, never
    /// a piece taken in (its work is the lead's), refused, discarded, or still in its partner's hands. Hand-over: only an uncertain one.
    /// </summary>
    public static bool Allows(string act, string state) => act switch
    {
        Discard => state is PartnerStates.Held or PartnerStates.Imported or PartnerStates.Answered or PartnerStates.Handed
            or PartnerStates.NotTakenIn or PartnerStates.Uncertain,
        HandOver => state == PartnerStates.Uncertain,
        _ => false,
    };

    /// <summary>The codes of an act that was not written or not done, by what the page and the verb say.</summary>
    public static class Why
    {
        /// <summary>The act is not one of <see cref="All"/>.</summary>
        public const string Unknown = "unknown-act";

        /// <summary>No piece of this id is on file.</summary>
        public const string NoPiece = "no-piece";

        /// <summary>The piece's state does not take the act.</summary>
        public const string State = "state";

        /// <summary>An uncertain piece's copy is not where its state names it: there is nothing to hand over.</summary>
        public const string NoCopy = "no-copy";
    }
}

/// <summary>The person's act on one piece as <c>&lt;id&gt;.act.json</c> keeps it: written by the press, taken and removed by the driver's look.</summary>
public sealed record PartnerActAsk(string Id, string Act, DateTimeOffset At);

/// <summary>What the person's press of an act came to: whether it was written, and the code and sentence of why not.</summary>
public sealed record PartnerActed(bool Acted, string? Why, string Message);

/// <summary>
/// Whether a piece was taken in (the design §2.3 step 10), pure over what the look read: the frozen commit as an ancestor of the lead's
/// branch is <see cref="PartnerStates.TakenIn"/>, recorded with the lead's commit and never unset; a lead whose work landed without it
/// leaves <see cref="PartnerStates.NotTakenIn"/>, its ref kept until the person's <i>Discard…</i>.
/// </summary>
public static class PartnerTakenIn
{
    /// <summary>The states a look reads for it: the piece is handed and not yet taken in (a not-taken-in one may still be merged later).</summary>
    public static bool Reads(string state) => state is PartnerStates.Handed or PartnerStates.NotTakenIn;

    /// <param name="tip">The lead's branch's commit when the ancestry was read, or null where it could not be read.</param>
    /// <param name="ancestor">The frozen commit is an ancestor of <paramref name="tip"/>.</param>
    /// <param name="landed">The lead's work has landed and its record ended.</param>
    /// <returns>The state to write, or null where it stands.</returns>
    public static PartnerState? Next(PartnerState state, string? tip, bool ancestor, bool landed, DateTimeOffset now)
    {
        if (!Reads(state.State) || state.Commit is not { Length: > 0 }) return null;
        if (tip is { Length: > 0 } && ancestor) return state with { State = PartnerStates.TakenIn, TakenBy = tip, At = now };
        if (landed && state.State == PartnerStates.Handed) return state with { State = PartnerStates.NotTakenIn, At = now };
        return null;
    }
}
