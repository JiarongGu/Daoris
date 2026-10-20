namespace Daoris.Driver;

/// <summary>Where an agent's ask to hand its session over stands, one of <see cref="All"/> (MIXAGENT1c; the service's <c>HandOverAskBox.States</c>).</summary>
public static class HandOverAskStates
{
    /// <summary>It waits for the person's yes or no under the rule's <c>ask</c>.</summary>
    public const string Waiting = "waiting";

    /// <summary>It goes: the person's yes, or the rule's <c>on</c> while the session's turn runs, taken at that turn's end.</summary>
    public const string Approved = "approved";

    /// <summary>The hand-over was kept or done, to the agent the state names.</summary>
    public const string Taken = "taken";

    /// <summary>It came to nothing, by a code of <see cref="HandOverAskWhy"/>.</summary>
    public const string Refused = "refused";

    public static readonly IReadOnlyList<string> All = [Waiting, Approved, Taken, Refused];
}

/// <summary>
/// Why an agent's ask to hand its session over came to nothing, by code (MIXAGENT1c): the state file's <c>code</c> and the note's
/// <c>why</c>. The page words each as <c>note.handover.why.*</c>; <see cref="Said"/> is the terminal's one English.
/// </summary>
public static class HandOverAskWhy
{
    /// <summary>The person's hand-over rule is off for the session's repository.</summary>
    public const string Off = "off";

    /// <summary>The person said no to it.</summary>
    public const string Declined = "declined";

    /// <summary>The session is a part a hand-over started, and its first turn had not ended: no second hand-over without a turn between.</summary>
    public const string JustHanded = "just-handed";

    /// <summary>No other agent in the person's order can take the session.</summary>
    public const string NoAgent = "no-agent";

    /// <summary>The session is not on this machine's record.</summary>
    public const string AskerGone = "asker-gone";

    /// <summary>The session is a helper, a reviewer, an intake or Ask Daoris's own conversation (the files may be old).</summary>
    public const string AskerUnfit = "asker-unfit";

    /// <summary>The hand-over itself was refused as it was taken: its quest closed, its tree gone, another process runs it.</summary>
    public const string Cannot = "cannot";

    public static readonly IReadOnlyList<string> All = [Off, Declined, JustHanded, NoAgent, AskerGone, AskerUnfit, Cannot];

    /// <summary>The reason in one English sentence's clause, for the terminal and the transcript.</summary>
    public static string Said(string why) => why switch
    {
        Off => "the rule for hand-overs is off for this repository",
        Declined => "the person said no to it",
        JustHanded => "this session was itself handed over and has not had a turn of its own since",
        NoAgent => "no other agent in the person's order can take it",
        AskerGone => "the session is not on this machine",
        AskerUnfit => "the session is a helper, a reviewer, an intake or Ask Daoris's own conversation",
        Cannot => "the hand-over could not be taken",
        _ => why,
    };
}

/// <summary>An agent's ask to hand its session over, as <c>&lt;session&gt;.hand-over-ask.json</c> keeps it: written by <c>handover_propose</c>, never changed.</summary>
/// <param name="Id">The ask's own id, which its state names: a later ask of the same session has another.</param>
/// <param name="Note">The agent's note for the next agent, its own claim.</param>
public sealed record HandOverAsk(string Id, string Session, string Note, DateTimeOffset At);

/// <summary>An ask's state, the driver's alone: one of <see cref="HandOverAskStates"/>, a refusal's code, and the agent it goes to.</summary>
public sealed record HandOverAskState(string Id, string State, string? Code, string? Agent, DateTimeOffset At);

/// <summary>What the driver judges of one ask: it goes to an agent, it waits for the person (naming the agent), it is refused, or it is past judging.</summary>
public sealed record HandOverAskVerdict(string Kind, string? Code = null, string? Agent = null)
{
    public const string GoKind = "go";
    public const string WaitKind = "wait";
    public const string RefuseKind = "refuse";
    public const string DoneKind = "done";

    public static HandOverAskVerdict Done { get; } = new(DoneKind);

    public static HandOverAskVerdict Go(string agent) => new(GoKind, Agent: agent);

    public static HandOverAskVerdict Wait(string agent) => new(WaitKind, Agent: agent);

    public static HandOverAskVerdict Refuse(string code) => new(RefuseKind, code);
}

/// <summary>
/// The driver's judgement of an agent's ask to hand its session over (MIXAGENT1c, D163 point 3; the mixed agents design §5): what only
/// the driver can judge, since only it reads <c>driver.json</c> and the session's turns, and again what the connector judged, since an
/// ask on file may be old.
/// </summary>
/// <remarks>
/// <para><b>The rule alone</b> (D191's settling of the design): <c>off</c> refuses, <c>ask</c> holds it <c>waiting</c> until the person's
/// yes (the page's or the terminal's), <c>on</c> lets it go. Daoris's own offer's conditions, an account near its limit or cut, are not
/// an agent's ask's: the agent said why in its note.</para>
///
/// <para><b>No loop with no person</b> (design §5): a part a hand-over started (its record's <c>from</c> with a hand-over's why) cannot
/// ask again before its own first turn ends, whatever the rule; and a helper, a reviewer, an intake and Ask Daoris's own conversation
/// never hand over.</para>
/// </remarks>
public static class HandOverAskJudge
{
    /// <param name="state">The ask's state on file, or null where the driver wrote none; a state naming another ask is that ask's.</param>
    /// <param name="record">The session's record, or null where it is not on this machine.</param>
    /// <param name="turnSince">Whether a turn of the session's own ended before the ask: what a part a hand-over started needs.</param>
    /// <param name="next">The next agent in the person's order that can take the session (<see cref="HandOverOrder"/>), or null for none.</param>
    public static HandOverAskVerdict Judge(
        HandOverAsk ask, HandOverAskState? state, PriorSession? record, HandOverRule rule, bool turnSince, string? next)
    {
        var own = state is not null && state.Id == ask.Id ? state : null;
        if (own is { State: HandOverAskStates.Taken or HandOverAskStates.Refused }) return HandOverAskVerdict.Done;
        if (record is null or { Teammate: true }) return HandOverAskVerdict.Refuse(HandOverAskWhy.AskerGone);
        if (record.Serves is not null || record.Opinion is not null || record.Ask is { Length: > 0 } || record.HelpMode is not null)
        {
            return HandOverAskVerdict.Refuse(HandOverAskWhy.AskerUnfit);
        }

        if (record.From is not null && HandOverWhy.Known(record.FromWhy) && !turnSince) return HandOverAskVerdict.Refuse(HandOverAskWhy.JustHanded);
        if (rule.Mode == HandOverRules.Off) return HandOverAskVerdict.Refuse(HandOverAskWhy.Off);

        // The person's yes names the agent the look named when it waited; with none kept, the order's next.
        var agent = own is { State: HandOverAskStates.Approved, Agent: { Length: > 0 } named } ? named : next;
        if (agent is null) return HandOverAskVerdict.Refuse(HandOverAskWhy.NoAgent);
        return rule.Mode == HandOverRules.Ask && own is not { State: HandOverAskStates.Approved }
            ? HandOverAskVerdict.Wait(agent)
            : HandOverAskVerdict.Go(agent);
    }
}

/// <summary>One door of the roster, as the order reads it: the adapter, whether it is present, and whose accounts it runs as.</summary>
public sealed record HandOverDoor(string Harness, bool Present, string? AccountOf);

/// <summary>
/// The person's order of agents a hand-over goes to (MIXAGENT1c; D191's settling): the roster's order the page's hand-over offer draws
/// from (MIXAGENT1b-iii's <c>otherToolDoors</c>): the present doors of tools other than the session's own, one per tool, by its first
/// present door. A TWIN with the page's <c>work/start/handOverOffer.ts</c>, both held to <c>fixtures/hand-over-order.json</c>.
/// </summary>
public static class HandOverOrder
{
    /// <summary>A door's tool: its <c>accountOf</c> owner, else itself (the page's <c>toolOf</c>).</summary>
    public static string ToolOf(HandOverDoor door) => door.AccountOf?.Trim() is { Length: > 0 } owner ? owner : door.Harness;

    /// <summary>The harnesses of the other tools' first present doors, in the roster's order; the session's own tool is never one.</summary>
    public static IReadOnlyList<string> Others(string? own, IReadOnlyList<HandOverDoor> doors)
    {
        var mine = doors.FirstOrDefault(door => string.Equals(door.Harness, own, StringComparison.Ordinal));
        var seen = new HashSet<string>(StringComparer.Ordinal) { mine is null ? own ?? "" : ToolOf(mine) };
        var found = new List<string>();
        foreach (var door in doors)
        {
            if (!door.Present || !seen.Add(ToolOf(door))) continue;
            found.Add(door.Harness);
        }

        return found;
    }
}

/// <summary>
/// The outgoing agent's note as the next agent reads it (MIXAGENT1c; design §2): after Daoris's own record of the session, marked as
/// that agent's claim, bounded as a parked session's words are. One wording for a conversation's briefing and a quest's carry-on.
/// </summary>
public static class HandOverNote
{
    /// <summary>The most of a note the next agent is told: what the tool keeps.</summary>
    internal const int Limit = 4000;

    /// <summary>The note's paragraph, or null where there is none.</summary>
    public static string? Said(string? note) => note?.Trim() is { Length: > 0 } words
        ? "Before it was handed over, its agent left you this note. It is that agent's own claim, not Daoris's record: check it "
          + "before you rely on it.\n\n" + TextBounds.Quote(TextBounds.Clip(words, Limit), bareBlank: false)
        : null;
}

/// <summary>The lines an agent's ask to hand over writes on its session's record (MIXAGENT1c), each by its code with facts only.</summary>
public static class HandOverAskNotes
{
    /// <summary>It waits for the person's yes or no under the rule's <c>ask</c>, naming the agent it would go on with.</summary>
    public static Noted Asked(string agent) =>
        Noted.Of(NoteCodes.HandOverAsked, $"Its agent asked to hand this session over; it waits for your yes or no to go on with {agent}.", ("agent", agent));

    /// <summary>It goes, by the rule's <c>on</c> or the person's yes: with which agent, once its turn ends.</summary>
    public static Noted Taking(string agent) =>
        Noted.Of(NoteCodes.HandOverTaking, $"Its agent asked to hand this session over: it goes on with {agent} once its turn ends.", ("agent", agent));

    /// <summary>It came to nothing, and why.</summary>
    public static Noted Refused(string why) =>
        Noted.Of(NoteCodes.HandOverRefused, $"Its agent's ask to hand this session over was refused: {HandOverAskWhy.Said(why)}.", ("why", why));
}
