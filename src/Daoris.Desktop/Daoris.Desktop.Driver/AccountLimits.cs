using System.Globalization;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One sentence an agent was seen refusing with for an account's limit (TOOL4a, D125 §1.3): the evidence an
/// entry in <see cref="LimitWords"/> stands on.
/// </summary>
/// <param name="Sentence">
/// As recorded, the zone written <see cref="AccountLimits.ZonePlaceholder"/>: a sentence names the machine's
/// zone, and that is the person's, not this repository's. A part the record given to the design elided is
/// written <c>…</c>.
/// </param>
/// <param name="Seen">The day it was recorded, <c>yyyy-MM-dd</c>.</param>
/// <param name="Channel">Where it arrived: which door, or which channel outside Daoris, and how.</param>
/// <param name="Version">The harness's version when it was seen, where known.</param>
public sealed record RecordedLimit(string Sentence, string Seen, string Channel, string? Version = null);

/// <summary>
/// The words one agent says an account's limit in (TOOL4a, D125 §1.3): its entry in the limit table,
/// declared on its toolchain as <see cref="HarnessToolchain.Limits"/>, beside AGT3b's <c>Refused</c>.
/// </summary>
/// <remarks>
/// <para><b>An entry grows only with a recorded sentence.</b> Every marker and every reset is matched by one
/// of <see cref="Recorded"/>, and every recorded sentence by a marker; <c>AccountLimitsTests</c> holds it
/// both ways. A string match on somebody else's words written blind is how D57 §b feared this would be got
/// wrong.</para>
///
/// <para>Each pattern is matched against one clause, the text between <c>·</c> separators, after
/// <see cref="AccountLimits"/> has made the case, the apostrophe and the white space not matter.</para>
/// </remarks>
/// <param name="Markers">
/// A clause that says a limit was hit, with a <c>hit</c> group: what was hit, kept for the record.
/// </param>
/// <param name="Resets">
/// A later clause, or the rest of the marker's own (TOOL4k), that says when it lifts, with a <c>when</c> group
/// the reader's grammar reads (D125 §2.1), and an optional <c>window</c> and <c>zone</c>.
/// </param>
/// <param name="Recorded">The sentences the patterns were written against.</param>
public sealed record LimitWords(
    IReadOnlyList<string> Markers,
    IReadOnlyList<string> Resets,
    IReadOnlyList<RecordedLimit> Recorded);

/// <summary>What a recognised limit gives (D125 §1.5).</summary>
/// <param name="Hit">What the agent said was hit (<i>individual spend</i>, <i>weekly</i>): for the record only.</param>
/// <param name="Window">The window its reset named (<i>session</i>, <i>weekly</i>), or null where it named none.</param>
/// <param name="Until">The instant the account is offered again.</param>
/// <param name="Stated">Whether the agent named that time. False is Daoris's default standing in.</param>
/// <param name="AssumedZone">Whether the machine's zone stood in for the one the agent named, or for none.</param>
/// <param name="NotBelieved">
/// Whether the agent named a time more than 8 days ahead, so the default stood in for it (D125 §2.1): a
/// date already past, carried into next year. Said apart from a sentence that named no time at all.
/// </param>
public sealed record LimitSeen(
    string Hit, string? Window, DateTimeOffset Until, bool Stated, bool AssumedZone, bool NotBelieved = false);

/// <summary>
/// The one reader of an account's limit (TOOL4a, D125 §1.3 rule 2): nothing else in the driver matches a
/// limit's words.
/// </summary>
/// <remarks>
/// <para><b>It reads the door's failure, and nothing else</b> (D125 §1.4): never the transcript, the agent's
/// messages or what its tools printed. A session working on this very feature prints these sentences, and a
/// failed one whose own output quoted one would cool an account that is not spent. The caller hands it the
/// failure the door carried apart from the agent's words.</para>
///
/// <para><b>Pure.</b> The moment it was seen and the machine's zone are arguments, so every row of §2.1 is a
/// test and none waits on a clock.</para>
/// </remarks>
public static class AccountLimits
{
    /// <summary>
    /// The cool-off for a limit whose sentence names no time this reads, or one it does not believe
    /// (D125 §2.2): long enough that a spent account is not started on at every look, short enough that a
    /// wrong guess costs an hour. Settable once TOOL4e lands <c>cooloff</c>.
    /// </summary>
    public static readonly TimeSpan DefaultCoolOff = TimeSpan.FromMinutes(60);

    /// <summary>Added to a stated reset, so the next start does not race the provider's own clock (D125 §2.1).</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A reset up to this long before the limit was seen has not landed: the two clocks disagree, and the
    /// account waits the margin and is tried again (D125 §2.1).
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The furthest reset believed (D125 §2.1). The longest window any sentence named is a week, so a moment
    /// further off is a date already past, carried into next year.
    /// </summary>
    public static readonly TimeSpan Furthest = TimeSpan.FromDays(8);

    /// <summary>How a recorded sentence writes the zone the agent printed.</summary>
    public const string ZonePlaceholder = "<zone>";

    /// <summary>
    /// The limit <paramref name="failure"/> says, read by <paramref name="entry"/>, or null when it says none:
    /// a failure as today.
    /// </summary>
    /// <param name="entry">The agent's entry; null for an agent never seen hitting a limit, which reads none.</param>
    /// <param name="failure">The door's failure, as the conclusion receives it.</param>
    /// <param name="seen">When the failure arrived.</param>
    /// <param name="machineZone">This machine's zone, for a sentence whose zone is not an IANA name, or absent.</param>
    /// <param name="coolOff">The default when no time is read; <see cref="DefaultCoolOff"/> when not given.</param>
    public static LimitSeen? Read(
        LimitWords? entry, string? failure, DateTimeOffset seen, TimeZoneInfo machineZone, TimeSpan? coolOff = null)
    {
        if (entry is null || string.IsNullOrWhiteSpace(failure)) return null;

        var clauses = Clauses(failure);
        for (var i = 0; i < clauses.Count; i++)
        {
            if (First(entry.Markers, clauses[i]) is not { } marker) continue;

            var hit = marker.Groups["hit"] is { Success: true } said ? said.Value.Trim() : clauses[i];
            var byDefault = new LimitSeen(hit, null, seen + (coolOff ?? DefaultCoolOff), Stated: false, AssumedZone: false);

            // The reset comes AFTER what was hit (D125 §0.2): a later clause, or, for an agent that writes its refusal as
            // sentences in one clause, the rest of the marker's own (TOOL4k, Codex). A marker that ends its clause, as
            // Claude Code's does, leaves no rest, so its sentences read as they always did.
            var rest = clauses[i][(marker.Index + marker.Length)..].Trim();
            foreach (var later in (rest.Length > 0 ? [rest] : Array.Empty<string>()).Concat(clauses.Skip(i + 1)))
            {
                if (First(entry.Resets, later) is not { } reset) continue;

                var window = reset.Groups["window"] is { Success: true, Value.Length: > 0 } w ? w.Value.Trim() : null;
                var named = reset.Groups["zone"] is { Success: true } z ? Iana(z.Value.Trim()) : null;
                var moment = When(reset.Groups["when"].Value.Trim(), seen, named ?? machineZone);

                if (moment is null) return byDefault with { Window = window };
                // A year names one date, which may be past (TOOL4k): not believed, as a date too far ahead is not.
                if (moment.Value < seen || moment.Value - seen > Furthest) return byDefault with { Window = window, NotBelieved = true };
                return new LimitSeen(hit, window, moment.Value + Margin, Stated: true, AssumedZone: named is null);
            }

            return byDefault;
        }

        return null;
    }

    /// <summary>Whether some clause of <paramref name="text"/> matches <paramref name="pattern"/>, as the reader compares.</summary>
    internal static bool Matches(string pattern, string text) => Clauses(text).Any(clause => Match(pattern, clause) is not null);

    /// <summary>
    /// The text's clauses, compared as D125 §1.3 rule 1 says: the apostrophe in either form, a run of white
    /// space as one, split at each <c>·</c>. Case is the patterns' to ignore.
    /// </summary>
    private static List<string> Clauses(string text) =>
        Regex.Replace(text.Replace('’', '\''), @"\s+", " ")
            .Split('·')
            .Select(clause => clause.Trim())
            .Where(clause => clause.Length > 0)
            .ToList();

    private static Match? First(IEnumerable<string> patterns, string clause) =>
        patterns.Select(pattern => Match(pattern, clause)).FirstOrDefault(match => match is not null);

    private static Match? Match(string pattern, string clause)
    {
        try
        {
            var match = Regex.Match(
                clause, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            return match.Success ? match : null;
        }
        catch (RegexMatchTimeoutException)
        {
            // A clause no pattern can settle in a second is not a limit this table recognises.
            return null;
        }
    }

    /// <summary>
    /// The zone a sentence named, when it is an IANA identifier the platform resolves (<i>Area/Location</i>,
    /// or <i>UTC</i>); null for any other name, which the machine's zone then stands in for.
    /// </summary>
    /// <remarks>
    /// 🔴 The shape is checked before the platform is asked, because the platform answers more than IANA:
    /// on Windows, ICU resolves <c>PST</c> as one of its own three-letter aliases, and Windows resolves its
    /// display ids (<c>Pacific Standard Time</c>). An abbreviation is ambiguous (<c>CST</c> is two zones),
    /// so D125 §2.1 reads it as the machine's own, on every platform alike.
    /// </remarks>
    private static TimeZoneInfo? Iana(string zone) =>
        IanaShape.IsMatch(zone) && TimeZoneInfo.TryFindSystemTimeZoneById(zone, out var found) ? found : null;

    private static readonly Regex IanaShape = new(
        @"^(?:UTC|[A-Za-z]+(?:/[A-Za-z0-9_+\-]+)+)$", RegexOptions.CultureInvariant);

    private static readonly Regex TimeOfDay = new(
        @"^(?<hour>\d{1,2})(?::(?<minute>\d{2}))? ?(?<half>am|pm)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// A month, a day and a time (<i>Oct 3, 4pm</i>), the day with an ordinal and a year where Codex writes them
    /// (<i>Oct 3rd, 2026 4:05 PM</i>, TOOL4k).
    /// </summary>
    private static readonly Regex DayAndTime = new(
        @"^(?<month>jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec) (?<day>\d{1,2})(?:st|nd|rd|th)?,? (?:(?<year>\d{4}),? )?(?<time>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>A weekday and a time (<i>Mon 12:00am</i>), as the maker documents a weekly reset (TOOL4k).</summary>
    private static readonly Regex WeekdayAndTime = new(
        @"^(?<weekday>sun|mon|tue|wed|thu|fri|sat) (?<time>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    /// <summary>Three-letter English weekdays, in <see cref="DayOfWeek"/>'s order.</summary>
    private static readonly string[] Weekdays = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];

    /// <summary>
    /// The moment a reset names (D125 §2.1), in <paramref name="zone"/>: a time of day, or a weekday and a time, is
    /// the first such moment after <paramref name="seen"/>; a month, a day and a time the one in the year that puts
    /// it first after it, or in the year it names; and a dated one up to <see cref="Grace"/> before it is
    /// <paramref name="seen"/> itself. Null when this grammar does not read it. Month and weekday names are English,
    /// as the sentences print them.
    /// </summary>
    /// <remarks>
    /// <para>🔴 A time of day gets no grace (FIX 2026-10-02): the agent drops the date once the reset is under a day
    /// away, so <i>resets 4pm</i> refused at 4:12pm is tomorrow's, and the refusal itself says it has not come.
    /// Read as today's in the grace, it cooled the account for the margin and the next start was refused again.</para>
    ///
    /// <para>A weekday gets none either (TOOL4k), for the same reason: it stands where the date was dropped, so
    /// <i>Mon 12:00am</i> refused at 00:05 on a Monday is next week's.</para>
    ///
    /// <para>A named year is that one date, so it may lie behind <paramref name="seen"/>, which the reader does not
    /// believe. Only a year next to this one is read: any other is a date no limit names, and is not read.</para>
    /// </remarks>
    private static DateTimeOffset? When(string when, DateTimeOffset seen, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(seen, zone).DateTime;
        IEnumerable<DateTime> candidates;
        var dated = false;

        if (Clock(when) is { } time)
        {
            candidates = new[] { 0, 1 }.Select(days => local.Date.AddDays(days) + time);
        }
        else if (WeekdayAndTime.Match(when) is { Success: true } weekday && Clock(weekday.Groups["time"].Value) is { } on)
        {
            var named = (DayOfWeek)Array.IndexOf(Weekdays, weekday.Groups["weekday"].Value.ToLowerInvariant());
            candidates = Enumerable.Range(0, 8)
                .Select(days => local.Date.AddDays(days))
                .Where(day => day.DayOfWeek == named)
                .Select(day => day + on);
        }
        else if (DayAndTime.Match(when) is { Success: true } date && Clock(date.Groups["time"].Value) is { } at)
        {
            dated = true;
            var month = Array.IndexOf(Months, date.Groups["month"].Value.ToLowerInvariant()) + 1;
            var day = int.Parse(date.Groups["day"].Value, CultureInfo.InvariantCulture);
            var near = new[] { local.Year - 1, local.Year, local.Year + 1 };
            var years = date.Groups["year"] is { Success: true } said
                ? near.Where(year => year == int.Parse(said.Value, CultureInfo.InvariantCulture))
                : near;
            candidates = years
                .Where(year => day >= 1 && day <= DateTime.DaysInMonth(year, month))
                .Select(year => new DateTime(year, month, day) + at);
        }
        else
        {
            return null;
        }

        // The zone's offset AT that wall-clock moment, so a reset across a change of offset lands on its minute.
        var moments = candidates.Select(c => new DateTimeOffset(c, zone.GetUtcOffset(c))).OrderBy(m => m).ToList();
        if (dated && moments.Any(m => m <= seen && seen - m <= Grace)) return seen;
        // Every moment behind the refusal happens only for a named year: the latest, for the reader not to believe.
        return moments.Where(m => m > seen).Select(m => (DateTimeOffset?)m).FirstOrDefault()
            ?? moments.Select(m => (DateTimeOffset?)m).LastOrDefault();
    }

    /// <summary>A twelve-hour clock's time of day (<i>7am</i>, <i>7:50am</i>, <i>12pm</i>, <i>4:05 PM</i>), or null.</summary>
    private static TimeSpan? Clock(string text)
    {
        if (TimeOfDay.Match(text.Trim()) is not { Success: true } clock) return null;

        var hour = int.Parse(clock.Groups["hour"].Value, CultureInfo.InvariantCulture);
        var minute = clock.Groups["minute"].Success ? int.Parse(clock.Groups["minute"].Value, CultureInfo.InvariantCulture) : 0;
        if (hour is < 1 or > 12 || minute > 59) return null;

        var afternoon = clock.Groups["half"].Value.Equals("pm", StringComparison.OrdinalIgnoreCase);
        return new TimeSpan(hour % 12 + (afternoon ? 12 : 0), minute, 0);
    }
}

/// <summary>
/// Claude Code's words for an account's limit (TOOL4a, D125 §0.2): five recorded sentences, on its ACP
/// adapter's door and on the maker's own CLI outside Daoris, which inform the grammar, and the weekly reset
/// the maker's errors page documents with a weekday (TOOL4k).
/// </summary>
public static class ClaudeLimits
{
    public static LimitWords Words { get; } = new(
        Markers: [@"\byou've hit your (?<hit>.+?) limit$"],
        Resets: [@"^(?:your (?<window>.+?) limit )?resets (?<when>.+?)(?: \((?<zone>[^()]*)\))?$"],
        Recorded:
        [
            new(
                "Internal error: You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your session limit resets 7am (<zone>)",
                "2026-09-27",
                "the protocol door: the JSON-RPC error answering session/prompt, Claude Code over its ACP adapter, 370k tokens into a driven turn; the adapter then exited 0"),
            new(
                "the ACP agent refused the call: Internal error: You've hit your individual spend limit · … · your session limit resets 7:50am (<zone>)",
                "2026-09-29",
                "the protocol door, in two driven sessions, as the driver's note; the record given to the design kept the note's start and the reset, and TOOL4's row names it the spend-limit refusal, written here in observation 1's words"),
            new(
                "You've hit your weekly limit · resets Oct 6, 10pm (<zone>)",
                "2026-10-01",
                "the maker's CLI outside Daoris, an HTTP 429 rate_limit: no door of Daoris's"),
            new(
                "You've hit your individual spend limit · … · your weekly limit resets Oct 3, 4pm (<zone>)",
                "2026-10-01",
                "the protocol door, a driven carry-on on the tool's own sign-in, refused mid-turn and then twice at once; the record given to the design elided the middle clause"),
            new(
                "You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your weekly limit resets Oct 3, 4pm (<zone>)",
                "2026-10-02",
                "the maker's CLI outside Daoris, an HTTP 429 rate_limit mid-run: no door of Daoris's"),
            new(
                "You've hit your weekly limit · resets Mon 12:00am",
                "2026-10-02",
                "documented, not seen: the maker's errors page, read by TOOL4b (limit-signals evidence §2), which prints it with no zone; the weekday it names is TOOL4k's grammar"),
        ]);
}

/// <summary>
/// Codex's words for an account's limit (TOOL4k, limit-signals evidence §4), declared by its protocol door,
/// <c>codex-acp</c>, since no <c>codex</c> adapter exists to own them. Read in Codex's source and never seen
/// on a door: they reach Daoris as the JSON-RPC error's <c>data.message</c>, which the door says after the
/// message <c>Internal error</c> (ACPDATA1).
/// </summary>
/// <remarks>
/// <para><b>One clause of sentences.</b> Codex joins no <c>·</c>: the reset is the rest of the marker's own
/// clause, <i>Try again at {time}.</i>, and <i>Try again later.</i> names none.</para>
///
/// <para><b>Its time has no zone</b>: the machine's local time, <i>4:05 PM</i> the same day, else
/// <i>Oct 3rd, 2026 4:05 PM</i>, read in the machine's zone and said as assumed.</para>
///
/// <para><b>The marker starts a clause or follows what a door put before it</b> (<c>: </c>), since a clause that
/// does not end on it, as Claude Code's must, cannot tell a refusal from a quotation any other way.</para>
/// </remarks>
public static class CodexLimits
{
    private const string Source =
        "read, not measured: codex 0.159.3's source at rust-v0.159.3 (codex-rs/protocol/src/error.rs, UsageLimitReachedError), which codex-acp 2.1.1 sends as the JSON-RPC error's data.message beside the message `Internal error` (limit-signals evidence §4)";

    public static LimitWords Words { get; } = new(
        Markers: [@"(?:^|: )you've hit your (?<hit>usage) limit(?: for .+?)?\.(?= |$)"],
        Resets: [@"\btry again at (?<when>.+?)\.?$"],
        Recorded:
        [
            new("You’ve hit your usage limit. … Try again at 4:05 PM.", "2026-10-02", $"{Source}; the plan's own next step elided", "0.159.3"),
            new("You’ve hit your usage limit. … or try again at Oct 3rd, 2026 4:05 PM.", "2026-10-02", $"{Source}; the plan's own next step elided", "0.159.3"),
            new("You’ve hit your usage limit for …. Switch to another model now, or try again at 4:05 PM.", "2026-10-02", $"{Source}; a named model's limit, the model elided", "0.159.3"),
            new("You’ve hit your usage limit. … Try again later.", "2026-10-02", $"{Source}; no reset known, the plan's own next step elided", "0.159.3"),
        ]);
}
