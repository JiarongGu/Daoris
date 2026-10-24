namespace Daoris.Driver;

/// <summary>
/// A driven line a limit or update cut off resumes its own conversation once its account is ready (LIMITRESUME1b/1i, D206; the
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
    public const string Words = "Your account's limit has lifted. Go on with the work where you stopped.";

    /// <summary>
    /// The limited record a start would resume in its own conversation: the last part of an open or taken quest that waits on nothing,
    /// ended by an account's limit or an update, and not the record its quest was handed over from (step 0: a hand-over names the agent it goes on
    /// with). Also a record ended past its transient bound (AGENTERRORS1a2, the agent-errors design §5.1): the planner plans one only once
    /// <i>Try again</i> released that end, so its start takes this step 1 too, the go-on kept under <see cref="DaorisKeys.TransientRelease"/>.
    /// Null for every other start.
    /// </summary>
    public static PriorSession? Line(Consideration start, bool handedFrom) =>
        !handedFrom
        && start.Quest is { Status: "Open" or "Taken", Awaits: null or "" }
        && start.Resumes is { Parked: false } limited
        && (limited is { Limit: true, State: "failed" } || UpdateBuild(limited) is not null || Planner.EndedTransient(limited))
            ? limited
            : null;

    /// <summary>
    /// Daoris's word at a released transient record's step 1 (AGENTERRORS1a2): the episode's go-on (<c>retry.go-on</c>) with the provider's
    /// sentence the record's end quotes, and its key, the record's session and the end released.
    /// </summary>
    public static (string Text, string Key) TransientWord(PriorSession record) =>
        (TransientRetry.GoOnText(record.NoteParts?.FirstOrDefault(part => part.By == NoteBy.Program)?.Words),
         DaorisKeys.TransientRelease(record.Session, record.Updated ?? DateTimeOffset.UnixEpoch));

    public static string? UpdateBuild(PriorSession record) => record.State == "stopped" && record.Interrupted
        ? record.NoteParts?.FirstOrDefault(part => part.Code == NoteCodes.UpdateClosed.Code)?.Value("build")?.ToString()
        : null;

    /// <summary>The cool-off's <c>until</c> its record's limit line names, as the note keeps it; a fixed word where none is read.</summary>
    public static string Until(PriorSession record) =>
        record.NoteParts?.FirstOrDefault(part =>
                part.Code == NoteCodes.AccountCoolingWindow.Code || part.Code == NoteCodes.AccountCoolingNoWindow.Code)
            ?.Value("until")?.ToString() is { Length: > 0 } until
            ? until
            : "unsaid";

    /// <summary>The ids of Daoris's limit and restart words among <paramref name="words"/>.</summary>
    public static IReadOnlyList<string> WordIds(IEnumerable<SaidWordView> words) =>
        [.. words.Where(word => word.Daoris && DaorisKeys.IsResume(word.Key)).Select(word => word.Id).OfType<string>()];

    /// <summary>
    /// The line's own hold where no account it may run on is ready (the design §3.3 step 3, <c>held.limit-waits</c>): the earliest
    /// moment one is ready again, the cool-off's <c>until</c> with its margin (§3.2), and whose account, by the person's name in the
    /// English and by its id in the values. At the first look past it, step 1 or step 2 takes the line.
    /// </summary>
    public static Noted Waits(CoolingEntry earliest, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names = null)
    {
        var said = $"Every account it may run on is at its limit: it goes on by itself once {CoolingWords.Who(earliest, names)} is ready again "
                   + $"at {CoolingWords.When(earliest.Until, zone)}.";
        (string Name, object? Value)[] facts = [("owner", earliest.Agent), ("until", NoteCodes.Moment(earliest.Until))];
        return earliest.Account is { } account
            ? Noted.Of(NoteCodes.HeldLimitWaits, said, [("account", account), .. facts])
            : Noted.Of(NoteCodes.HeldLimitWaitsOwn, said, facts);
    }

    /// <summary>
    /// The line where a reset did not lift the limit (<c>limit.not-lifted</c>, the design §3.3's bound): the account's last cool-off
    /// among <paramref name="ended"/>, read before <paramref name="now"/>'s replaced it, ended within the margin before now with the same
    /// kind of limit as <paramref name="cooled"/>. Null for every other limit.
    /// </summary>
    public static Noted? NotLifted(IReadOnlyList<CoolingEntry> ended, CoolingEntry cooled, DateTimeOffset now)
    {
        var last = ended.FirstOrDefault(entry =>
            string.Equals(entry.Agent, cooled.Agent, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Account ?? "", cooled.Account ?? "", StringComparison.OrdinalIgnoreCase));
        if (last is null || last.Until > now || now - last.Until > AccountLimits.Margin) return null;
        if (!string.Equals(last.Window ?? "", cooled.Window ?? "", StringComparison.OrdinalIgnoreCase)) return null;

        var hit = AccountLine.Bounded(cooled.Window) ?? "limit";
        return Noted.Of(NoteCodes.LimitNotLifted, $"Its last reset did not lift the limit: {hit}.", ("hit", hit));
    }

    /// <summary>
    /// What the record says where its own conversation could not be resumed, so the line went on in a new part handed the brief
    /// (<c>limit.resumeRefused</c>), the refusal's own code its <c>why</c>, never <c>account</c>.
    /// </summary>
    public static Noted Refused(ContinueReason why) => Noted.Of(
        NoteCodes.LimitResumeRefused,
        $"Its own conversation could not be resumed ({why.Code}), so it went on in a new session with Daoris's brief.",
        ("why", why.Code));
}
