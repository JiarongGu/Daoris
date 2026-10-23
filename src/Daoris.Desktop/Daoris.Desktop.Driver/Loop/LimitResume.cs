namespace Daoris.Driver;

/// <summary>
/// A driven line an account's limit cut off resumes its own conversation once that account is ready again (LIMITRESUME1b, D206; the
/// limit-resume design §3.3 step 1): decided inside the carry-on's own start, never beside the look, and carried by Daoris's word kept
/// on the ended record through the say door just before the record goes on, so the conversation, whole, is told to go on.
/// </summary>
/// <remarks>
/// <para><b>One word per cool-off.</b> The word is keyed by the record and the cool-off's <c>until</c> that held it
/// (<see cref="DaorisKeys.LimitReset"/>), so a start tried again at a later look keeps the one already kept. It is never briefed: the
/// conversation is whole.</para>
///
/// <para><b>Every conclusion takes it off</b>, whatever the run came to, limited included; a refusal before or on the wire takes it off
/// too, but for <c>elsewhere</c>, whose words are kept and marked for the person. So it never reaches a later attempt or a new part.</para>
/// </remarks>
internal static class LimitResume
{
    /// <summary>Daoris's word to the resumed conversation: it tells the agent to go on, after whatever the person said.</summary>
    public const string Words = LimitResetWord.Text;

    /// <summary>
    /// The limited record a start would resume in its own conversation: the last part of an open or taken quest that waits on nothing,
    /// ended by an account's limit, and not the record its quest was handed over from (step 0: a hand-over names the agent it goes on
    /// with). Null for every other start.
    /// </summary>
    public static PriorSession? Line(Consideration start, bool handedFrom) =>
        !handedFrom
        && start.Quest is { Status: "Open" or "Taken", Awaits: null or "" }
        && start.Resumes is { Limit: true, State: "failed", Parked: false } limited
            ? limited
            : null;

    /// <summary>
    /// The cool-off's <c>until</c> its record's limit line names, as the note keeps it; the epoch where none is read, so a record with
    /// none still keeps one word under one key.
    /// </summary>
    public static DateTimeOffset Until(PriorSession record) =>
        record.NoteParts?.FirstOrDefault(part =>
                part.Code == NoteCodes.AccountCoolingWindow.Code || part.Code == NoteCodes.AccountCoolingNoWindow.Code)
            ?.Value("until")?.ToString() is { Length: > 0 } until
        && DateTimeOffset.TryParse(until, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var at)
            ? at
            : DateTimeOffset.UnixEpoch;

    /// <summary>The ids of Daoris's limit words among <paramref name="words"/>.</summary>
    public static IReadOnlyList<string> WordIds(IEnumerable<SaidWordView> words) =>
        [.. words.Where(word => word.Daoris && DaorisKeys.IsLimitReset(word.Key)).Select(word => word.Id).OfType<string>()];

    /// <summary>What the record says where its own conversation could not be resumed (<c>limit.resume-refused</c>), as a wake says it.</summary>
    public static Noted Refused(ContinueReason why) => LimitNotes.ResumeRefused(why);
}
