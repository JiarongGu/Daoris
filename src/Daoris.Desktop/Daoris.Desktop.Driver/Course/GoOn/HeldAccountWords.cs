namespace Daoris.Driver;

/// <summary>How a look reads words held for a record's own account (<see cref="HeldAccountWords.Lift"/>).</summary>
public enum HeldLift
{
    /// <summary>They stay held: the mark judges them.</summary>
    Stays,

    /// <summary>The own account is usable again: the record goes on with them in its own conversation, as any record with words waiting.</summary>
    Own,

    /// <summary>The own account stayed unusable past the bound: they go on in a new session on a ready account, in the record's tree.</summary>
    Elsewhere,

    /// <summary>The person chose a new session for them after they were held (*Go on in a new session*): their choice is a word.</summary>
    Chosen,
}

/// <summary>
/// Words a record could not go on with because its own account could not carry them (a <see cref="GoOnMarks"/> mark, why
/// <c>account</c>), read again at each look (HELDWORDS1, D348). An account is a fact that moves by itself (a sign-in made again, an
/// account folder back after an update, a cool-off ended), so such a mark never holds its words for good the way a closed quest's or
/// another client's does. Pure: the account's hold, the person's choice and the time are handed in.
/// </summary>
public static class HeldAccountWords
{
    /// <summary>
    /// How long words wait for their own account while it stays unusable before they go on in a new session on a ready account,
    /// counted from their mark.
    /// </summary>
    public static readonly TimeSpan Bound = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How long after its mark a usable own account is tried again: a try that is refused again marks the words anew, so a reading the
    /// resume judges otherwise costs one note per this, never one per look.
    /// </summary>
    public static readonly TimeSpan Retry = TimeSpan.FromMinutes(5);

    /// <summary>What a look does with a record's mark.</summary>
    /// <param name="own">What holds the record's own account now (<see cref="HarnessRoster.OwnHold"/>).</param>
    /// <param name="choice">The person's choice of a new session for the record, or null.</param>
    public static HeldLift Lift(PriorSession record, GoOnMark mark, NextHold own, NewSessionChoice? choice, DateTimeOffset now)
    {
        if (mark.Why != ContinueWhy.Account || !GoOnMarks.Judged(record, mark)) return HeldLift.Stays;
        if (choice is not null && choice.At >= mark.At) return HeldLift.Chosen;
        if (Usable(own)) return now - mark.At >= Retry ? HeldLift.Own : HeldLift.Stays;

        // Only words a new session is handed are carried: another agent's findings alone went to the person when they were held, and
        // nothing would go on with them in a new session, so they stay where the person was told of them.
        return PastBound(mark, now) && record.Waiting.Any(word => DaorisWords.NewPartCarries(word) || HelperAnswersCarried.Is(word))
            ? HeldLift.Elsewhere
            : HeldLift.Stays;
    }

    /// <summary>
    /// Whether the own account could carry the words now: ready, or near its limit; never signed out, cooling, refused, missing,
    /// kept for conversations or outside the set. An unknown sign-in is ready, as a start treats it.
    /// </summary>
    public static bool Usable(NextHold own) => own is NextHold.Ready or NextHold.Near;

    /// <summary>
    /// Whether the mark held its words for their own account past the bound: then a closed quest's session goes on in a new session
    /// on a ready account (through the shell's runner) as it does for an account that cools while another is ready.
    /// </summary>
    public static bool PastBound(GoOnMark? mark, DateTimeOffset now) => mark is { Why: ContinueWhy.Account } && now - mark.At >= Bound;
}
