namespace Daoris.Driver;

/// <summary>
/// The lines a helper writes into the two sessions' records (MIXAGENT2a-ii, D163 point 2), each by its code with facts only: the
/// agent, the session and why, never an account's name or a path (D125 section 3.6).
/// </summary>
public static class HelperNotes
{
    /// <summary>On the asker: a helper was asked, and where it reads.</summary>
    public static Noted Asked(string agent, string helper) =>
        Noted.Of(NoteCodes.HelperAsked, $"A helper was asked: {agent} reads in session {helper} and answers the question.", ("agent", agent), ("session", helper));

    /// <summary>On the asker: the helper answered, and its answer is the next turn.</summary>
    public static Noted Answered(string agent, string helper, bool lastWords) =>
        lastWords
            ? Noted.Of(
                NoteCodes.HelperAnsweredLastWords,
                $"Helper {agent} (session {helper}) did not use its answer tool; its last words stand as the answer.",
                ("agent", agent), ("session", helper))
            : Noted.Of(
                NoteCodes.HelperAnswered,
                $"Helper {agent} (session {helper}) answered; its answer is this session's next turn, marked as the helper's.",
                ("agent", agent), ("session", helper));

    /// <summary>On the asker: no helper answered, with the agent where one was chosen.</summary>
    public static Noted Refused(string why, string? agent = null) =>
        agent is null
            ? Noted.Of(NoteCodes.HelperRefused, $"No helper answered this question: {HelperWhy.Said(why)}.", ("why", why))
            : Noted.Of(NoteCodes.HelperRefusedBy, $"Helper {agent} did not answer this question: {HelperWhy.Said(why)}.", ("agent", agent), ("why", why));

    /// <summary>
    /// On the asker: an account's limit cut its helper off and the session no longer waits on it, so it is not run again (LIMITRESUME1h,
    /// D206 design §3.8), with when that account is ready again where a cool-off this machine holds says so. Never which account.
    /// </summary>
    public static Noted Limited(string agent, DateTimeOffset? until, TimeZoneInfo zone) =>
        until is { } ready
            ? Noted.Of(
                NoteCodes.HelperLimited,
                $"Helper {agent} hit its account's limit before it answered, and this session no longer waits on it, so it is not run again; "
                + $"its account is ready again at {CoolingWords.When(ready, zone)}.",
                ("agent", agent), ("until", NoteCodes.Moment(ready)))
            : Noted.Of(
                NoteCodes.HelperLimitedNoReset,
                $"Helper {agent} hit its account's limit before it answered, and this session no longer waits on it, so it is not run again.",
                ("agent", agent));

    /// <summary>On the helper: whose session it helps.</summary>
    public static Noted Helping(string asker) =>
        Noted.Of(NoteCodes.HelperHelping, $"This session is helping session {asker}: it reads, answers one question and changes nothing.", ("session", asker));
}
