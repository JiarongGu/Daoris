namespace Daoris.Driver;

/// <summary>A conversation's own line where its limit's move could not open the next part (LIMITGOON1), by its code.</summary>
public static class LimitNotes
{
    /// <summary>
    /// The new part did not open: the conversation ended, the words its refused turns held stay in it, and the start's own words follow
    /// in the English alone, as a program's.
    /// </summary>
    public static Noted NotMoved(string why) =>
        Noted.Of(
                NoteCodes.ChatLimitNotMoved,
                "Its account reached its limit, and it could not go on on the next account: write to it again once one has room.")
            .Then(" ", Noted.Said(why, NoteBy.Program));

    /// <summary>
    /// LIMITGOON1's *stays* written as a wait (LIMITRESUME1c, D206 §3.5): no account of its agent has room, the first ready again at
    /// <paramref name="first"/>'s <c>until</c>, the margin-added moment (§3.2), and it goes on by itself then, its words with it.
    /// </summary>
    public static Noted Waits(CoolingEntry first, TimeZoneInfo zone) =>
        Noted.Of(
            NoteCodes.ChatLimitWaits,
            $"No account of `{first.Agent}` has room now; the first is ready again at {CoolingWords.When(first.Until, zone)}. "
            + "It waits, your words with it, and goes on by itself then.",
            ("owner", first.Agent), ("until", NoteCodes.Moment(first.Until)), ("why", CoolingWhy.Of(first)));

    /// <summary>
    /// Why a conversation whose agent has no account left does not go on with another (LIMITGOON2, D205 §3.5(e)): it runs on the agent
    /// the person picked, which never moves (§3.5(d)). Said before its wait's line.
    /// </summary>
    public static Noted Picked(string agent) =>
        Noted.Of(
            NoteCodes.ChatLimitPicked,
            $"You picked `{agent}` for it, so it does not go on with another agent: it waits for an account of `{agent}`.",
            ("owner", agent));

    /// <summary>
    /// Why a conversation whose agent has no account left does not go on with another (LIMITGOON2, D205 §3.5(e)): no choice of its actor's
    /// on another agent can run now, or it has none. The walk's own words follow in the English alone, as a program's.
    /// </summary>
    public static Noted NoOtherChoice(string agent, string why) =>
        Noted.Of(
                NoteCodes.ChatLimitNoOther,
                $"None of its choices on another agent can run now, so it waits for an account of `{agent}`.",
                ("owner", agent))
            .Then(" ", Noted.Said(why, NoteBy.Program));

    /// <summary>The person's *Stop waiting* (LIMITRESUME1c, D206 §3.7): nothing goes on by itself, and their next word goes on as any does.</summary>
    public static Noted StoppedWaiting() =>
        Noted.Of(
            NoteCodes.ChatLimitStoppedWaiting,
            "It no longer waits for its account's limit: your words stay with it, and it goes on when you write to it.");

    /// <summary>The wake's first line (D206 §3.7, <c>limit.resumed</c>): its own account ready again at the margin-added <paramref name="until"/>.</summary>
    public static Noted Resumed(DateTimeOffset until) =>
        Noted.Of(
            NoteCodes.LimitResumed, "Its account was ready again; it went on in its own conversation.",
            ("until", NoteCodes.Moment(until)));

    /// <summary>
    /// Its own conversation could not be resumed at the wake, by the refusal's code, so it went on in a new part handed the brief (D206
    /// §3.5, §3.7, <c>limit.resume-refused</c>).
    /// </summary>
    public static Noted ResumeRefused(ContinueReason why) =>
        Noted.Of(
            NoteCodes.LimitResumeRefused,
            $"Its own conversation could not be resumed ({why.Code}), so it went on in a new session with Daoris's brief.",
            ("why", why.Code));
}
