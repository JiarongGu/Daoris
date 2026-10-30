namespace Daoris.Driver;

/// <summary>
/// What a conversation whose last turn its account's limit refused holds (LIMITGOON1, the limit-goes-on design §2): the person's words
/// the refused turns took, in the order they were said, and where the first of them stands in the record. Read from the record's events
/// alone, so a shell that comes up reads what the one before it saw: a turn is refused by a limit where its account's cooling line
/// (<c>account.cooling</c>, CONVNOTE1b) was said before its end, which the door ends <c>error</c>.
/// </summary>
/// <remarks>
/// FOUNDATION1g part c (the foundation design §3.5): whether an ended conversation is limited is its record's <c>limited</c> flag, which its
/// end writes as a driven end's does (<c>ChatRunner</c>'s conclusion), so every reader reads one record; the events still give the words and
/// where they stand, which a flag cannot hold. A record with no flag is read from its events alone, as one an older build ended, which
/// wrote none, and as a live conversation, which has not ended.
/// </remarks>
/// <param name="Words">The person's words of the refused turns since the last turn that ended otherwise, oldest first.</param>
/// <param name="Before">How many of the record's events stand before the first of them: the brief is composed from those alone.</param>
public sealed record LimitHeld(IReadOnlyList<SessionEvent> Words, int Before)
{
    /// <summary>The last refused turn's cooling line, which a conversation's end carries in its note; null where the events hold none.</summary>
    public SessionEvent? Cooled { get; init; }

    /// <summary>
    /// What an ended conversation's record holds (FOUNDATION1g part c): limited where its <paramref name="limited"/> flag says so, with the
    /// words its events hold, none where they hold none; without the flag, its events decide, as <see cref="Of(IReadOnlyList{SessionEvent})"/>.
    /// </summary>
    public static LimitHeld? Of(IReadOnlyList<SessionEvent> events, bool limited) =>
        Of(events) ?? (limited ? new LimitHeld([], events.Count) : null);

    /// <summary>
    /// The words held where the record's last turn ended refused by its account's limit; null where it ended otherwise, or where no
    /// turn ended. Only the person's words are held: Daoris's own and another agent's are never a new part's words (XAGENT1e).
    /// </summary>
    public static LimitHeld? Of(IReadOnlyList<SessionEvent> events)
    {
        var held = new List<SessionEvent>();
        var heldFrom = -1;
        var asked = new List<(SessionEvent Word, int At)>();
        SessionEvent? cooled = null;
        SessionEvent? lastCooled = null;
        bool? lastLimited = null;
        for (var at = 0; at < events.Count; at++)
        {
            var e = events[at];
            if (e.Kind == SessionEventKind.User && e.Origin == "person" && e.From is null && e.Text is { Length: > 0 })
            {
                asked.Add((e, at));
            }
            else if (e.Kind == SessionEventKind.Note && e.Parts?.Any(part => part.Code is { } code && Cooling(code)) == true)
            {
                cooled = e;
            }
            else if (e.Kind == SessionEventKind.Transient)
            {
                // AGENTERRORS1b (D210, the agent-errors design §6.1, round 3's R2-C2 and R3-O6): a transient episode's quiet event follows a
                // prompt the provider's failure answered, the turn's own or a try's. Each of the person's words asked until then went on the
                // wire with it, and may have been read, so it is never held to be sent again: a limit that ends the episode holds only the
                // words no try carried, and not the failed turn's own. Every word said since is carried by a later try, or waits unrecorded.
                asked.Clear();
            }
            else if (e.Kind == SessionEventKind.Turn)
            {
                var limited = cooled is not null && e.StopReason == "error";
                if (limited)
                {
                    if (held.Count == 0 && asked.Count > 0) heldFrom = asked[0].At;
                    held.AddRange(asked.Select(each => each.Word));
                    lastCooled = cooled;
                }
                else
                {
                    held.Clear();
                    heldFrom = -1;
                }

                asked.Clear();
                cooled = null;
                lastLimited = limited;
            }
        }

        return lastLimited == true ? new LimitHeld(held, heldFrom < 0 ? events.Count : heldFrom) { Cooled = lastCooled } : null;
    }

    /// <summary>
    /// The words held where the conversation still waits for its limit (LIMITRESUME1c, D206 §3.5): its last turn refused by its account's
    /// limit, and nothing since let it go. The person's *Stop waiting* (<c>chat.limit-stopped-waiting</c>) and a refusal that waits for
    /// them (a cannot line, which names its reason) each leave the wait: no look then tries it again by itself.
    /// </summary>
    public static LimitHeld? Waiting(IReadOnlyList<SessionEvent> events) => Waiting(events, limited: false);

    /// <summary>
    /// The same for an ended record whose <paramref name="limited"/> flag its end wrote (FOUNDATION1g part c): the flag says the limit ended
    /// it, and its events whether anything since let it go.
    /// </summary>
    public static LimitHeld? Waiting(IReadOnlyList<SessionEvent> events, bool limited)
    {
        if (Of(events, limited) is not { } held) return null;
        for (var at = events.Count - 1; at >= 0; at--)
        {
            var e = events[at];
            if (e.Kind == SessionEventKind.Turn) break;
            if (e.Kind == SessionEventKind.Note
                && (e.Why is not null || e.Parts?.Any(part => part.Code == NoteCodes.ChatLimitStoppedWaiting.Code) == true))
            {
                return null;
            }
        }

        return held;
    }

    /// <summary>A cooling line's code, whichever of its three shapes (<see cref="NoteCodes.AccountCooling"/> and its window's).</summary>
    private static bool Cooling(string code) =>
        code == NoteCodes.AccountCooling.Code || code == NoteCodes.AccountCoolingWindow.Code || code == NoteCodes.AccountCoolingNoWindow.Code;
}
