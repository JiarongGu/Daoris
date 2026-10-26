using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One frame an agent was seen sending about its windows (TOOL6c, D130 §5.2): the evidence an entry in
/// <see cref="WindowWords"/> stands on, as <see cref="RecordedLimit"/> is for a limit's words.
/// </summary>
/// <param name="Frame">
/// The object both doors carry, as recorded: each moment written relative to when it was seen (<c>&lt;+2.0 h&gt;</c>, about
/// two hours after), never a machine's clock; an id the record held written <c>…</c>.
/// </param>
/// <param name="Seen">The day it was recorded, <c>yyyy-MM-dd</c>.</param>
/// <param name="Channel">Where it arrived, or where it was read when it was declared and not seen.</param>
/// <param name="Version">The harness's version when it was seen, where known.</param>
public sealed record RecordedFrame(string Frame, string Seen, string Channel, string? Version = null);

/// <summary>
/// How one agent says an account's windows (TOOL6c, D130 §5.2): its entry in the readings table, declared on its toolchain as
/// <see cref="HarnessToolchain.Windows"/>, beside <see cref="HarnessToolchain.Limits"/>.
/// </summary>
/// <remarks>
/// <para><b>An entry grows only with a recorded frame</b> (§5.2): every window it names, every status word and the credits
/// field appear in one of <see cref="Recorded"/>, which <c>AccountReadingsTests</c> holds. The frame's shape is Claude Code's
/// <c>rate_limit_info</c>, the one shape recorded (limit-signals evidence §1.2), so the table holds its words, not a second
/// grammar for another agent's: Codex's door forwards none of Codex's limits (§4), so its windows are asked of its own
/// server instead (<see cref="CodexUsage"/>, CODEXUSE1) and read into the same <see cref="WindowReading"/>.</para>
/// <para><b>How a clear word with no number ranks</b> (§5.2's last question) does not arise: the evidence found the number
/// on every frame (§1.1), so a reading is ranked by its numbers and its word together (<see cref="AccountReadings.Near"/>).</para>
/// </remarks>
/// <param name="Windows">The agent's name for each window it reads → Daoris's: <c>session</c> (five hours) or <c>weekly</c>.</param>
/// <param name="Clear">The status words that mean the account may run.</param>
/// <param name="Warned">
/// The status words of the agent's own warning, kept as <c>warned</c> (USAGEREAD3a, the account-switch design §1.3): Claude
/// Code's says a window's use is ahead of the time gone in it, by thresholds of its own, not that it is near its limit, so it
/// never makes an account near alone.
/// </param>
/// <param name="Refused">The status words that mean a limit was reached.</param>
/// <param name="Scale">What a full window reads as on this agent's scale: 1 for a fraction.</param>
/// <param name="Credits">The field that says the request drew on usage credits, which are billed (C5, C6).</param>
/// <param name="Recorded">The frames the entry was written against.</param>
public sealed record WindowWords(
    IReadOnlyDictionary<string, string> Windows,
    IReadOnlyList<string> Clear,
    IReadOnlyList<string> Warned,
    IReadOnlyList<string> Refused,
    double Scale,
    string Credits,
    IReadOnlyList<RecordedFrame> Recorded);

/// <summary>One window as an agent said it in one frame (TOOL6c): never the agent's words, only Daoris's.</summary>
/// <param name="Window">Daoris's name for it: <c>session</c> or <c>weekly</c>.</param>
/// <param name="Used">How much of it is used, from 0 to 1, or null where the frame gave no number.</param>
/// <param name="Reset">When it turns over, which is when this reading is dropped.</param>
/// <param name="Standing"><c>clear</c>, <c>warned</c> or <c>refused</c>, on the window the frame names; else null.</param>
/// <param name="Credits">Whether the frame said the request drew on usage credits, on the window it names.</param>
public sealed record WindowReading(string Window, double? Used, DateTimeOffset Reset, string? Standing = null, bool Credits = false);

/// <summary>
/// One window as <c>windows.json</c> keeps it: its reading, when it was seen, and on which session. <paramref name="Source"/>
/// is <c>read</c> for a window asked of its agent's own server (USAGEREAD2a) and <c>said</c> for a session's frame, the
/// kept form where none is written.
/// </summary>
public sealed record WindowSaid(
    string Window, double? Used, DateTimeOffset Reset, string? Standing, bool Credits, DateTimeOffset Seen, string? Session,
    string Source = AccountWindows.SourceSaid);

/// <summary>
/// What an account's agent last said about its windows, as of a moment (TOOL6c, D130 §5.2): each window whose reset has not
/// passed. A floor, as of when each was seen: within a window use only grows, and the person may have added to it since.
/// </summary>
public sealed record AccountSaid(IReadOnlyList<WindowSaid> Windows)
{
    /// <summary>The window by Daoris's name, or null where it was not said.</summary>
    public WindowSaid? Of(string window) =>
        Windows.FirstOrDefault(each => string.Equals(each.Window, window, StringComparison.OrdinalIgnoreCase));

    /// <summary>When the newest of its windows was said.</summary>
    public DateTimeOffset Seen => Windows.Max(each => each.Seen);
}

/// <summary>
/// The one reader of what an agent says about an account's windows (TOOL6c, D130 §5.2, §6, §16.3), and the two judgements
/// the walk makes of it: near, by its curve (SWITCH1b-ii), and how far behind its week's pace, which <c>switch: off</c> still
/// ranks by. Pure: the frame, the table, the scope's settings and the moment are arguments.
/// </summary>
/// <remarks>
/// <para><b>It reads the frame the door carried apart from the agent's words</b>, as the limit reader reads the door's
/// failure (D125 §1.4): Claude Code's <c>rate_limit_event</c> on the native door, and the same object forwarded on its
/// protocol door (limit-signals evidence §1, §3). Never the transcript.</para>
/// <para><b>Absent is never zero, and never full</b> (D57): a window the frame does not name, or names without a reset, says
/// nothing, and an account that said nothing is neither near nor behind.</para>
/// </remarks>
public static class AccountReadings
{
    /// <summary>
    /// The standings, in Daoris's words: what a window's frame said (<c>clear</c>, <c>warned</c>, <c>refused</c>), and
    /// <c>near</c>, which only the log writes, for an account <see cref="Near"/> judges near.
    /// </summary>
    public const string ClearWord = "clear", WarnedWord = "warned", NearWord = "near", RefusedWord = "refused";

    /// <summary>
    /// What <paramref name="info"/> says, read by <paramref name="words"/>: each window the table names that has a reset, its
    /// use where the frame gives a number, and the frame's standing and credits on the window it names
    /// (<c>rateLimitType</c>). A named window the frame's windows leave out is read from the frame's own reset and use.
    /// </summary>
    public static IReadOnlyList<WindowReading> Read(JsonElement info, WindowWords words)
    {
        if (info.ValueKind != JsonValueKind.Object) return [];

        var standing = Json.Text(info, "status") is { } status ? StandingOf(status, words) : null;
        var named = Json.Text(info, "rateLimitType") is { } type && words.Windows.TryGetValue(type, out var mapped) ? mapped : null;
        var credits = info.TryGetProperty(words.Credits, out var drawing) && drawing.ValueKind == JsonValueKind.True;

        var readings = new List<WindowReading>();
        if (info.TryGetProperty("unifiedWindows", out var windows) && windows.ValueKind == JsonValueKind.Object)
        {
            foreach (var window in windows.EnumerateObject())
            {
                if (!words.Windows.TryGetValue(window.Name, out var name) || window.Value.ValueKind != JsonValueKind.Object) continue;
                if (Moment(window.Value, "resetsAt") is not { } reset) continue;
                var said = name == named;
                Add(new WindowReading(name, Used(window.Value, "utilization", words.Scale), reset,
                    said ? standing : null, said && credits));
            }
        }

        if (named is not null && readings.All(reading => reading.Window != named) && Moment(info, "resetsAt") is { } own)
        {
            Add(new WindowReading(named, Used(info, "utilization", words.Scale), own, standing, credits));
        }

        return readings;

        // A reading says something only where it gives a number or a standing.
        void Add(WindowReading reading)
        {
            if (reading.Used is null && reading.Standing is null && !reading.Credits) return;
            readings.RemoveAll(each => each.Window == reading.Window);
            readings.Add(reading);
        }
    }

    /// <summary>The version of <see cref="Seen"/>, which each reading the usage history keeps from a frame carries.</summary>
    public const int HistoryReader = 1;

    /// <summary>
    /// What the usage history keeps of a frame (USAGECAP1a1, D179 point 2): a parse beside <see cref="Read"/>, which it leaves
    /// unchanged. Every window the frame says a use or a reset of, the named window (<c>rateLimitType</c>) from the frame's own
    /// where its windows leave it out, each by the agent's key with its length and scope (<see cref="ClaudeWindows.Shape"/>),
    /// Daoris's name where the walk reads it, and the precision its number was written to. Never a standing or a credit: the
    /// history keeps numbers and times.
    /// </summary>
    public static IReadOnlyList<WindowSeen> Seen(JsonElement info, WindowWords words)
    {
        if (info.ValueKind != JsonValueKind.Object) return [];

        var seen = new List<WindowSeen>();
        if (info.TryGetProperty("unifiedWindows", out var windows) && windows.ValueKind == JsonValueKind.Object)
        {
            foreach (var window in windows.EnumerateObject())
            {
                if (window.Value.ValueKind == JsonValueKind.Object) Add(window.Name, window.Value);
            }
        }

        if (Json.NonEmpty(info, "rateLimitType") is { } named && seen.All(each => each.Window != named)) Add(named, info);
        return seen;

        void Add(string key, JsonElement at)
        {
            var used = Used(at, "utilization", words.Scale);
            var reset = Moment(at, "resetsAt");
            if (used is null && reset is null) return;

            var (minutes, scope) = ClaudeWindows.Shape(key);
            seen.RemoveAll(each => each.Window == key);
            seen.Add(new WindowSeen(
                key, words.Windows.GetValueOrDefault(key), scope, minutes, used,
                used is null ? null : UsageHistory.Precision(at.GetProperty("utilization"), words.Scale),
                reset, reset is null ? null : UsageHistory.ResetSaid));
        }
    }

    /// <summary>
    /// Whether an account is near a limit, which a start passes (D130 §6 as the evidence corrects it, D165 point 2): its
    /// agent's word that a limit was reached, its word that it is drawing on usage credits, or a window's use over the
    /// scope's curve now (<see cref="UsageCurves"/>, SWITCH1b-ii). Nothing said is never near, and its agent's pace warning
    /// (<c>warned</c>) is never near alone.
    /// </summary>
    /// <remarks>
    /// <para>§6 had the agent's word win over the number, where Claude Code's word was thought to come without one. The
    /// evidence found a number on every frame, and the word still <c>allowed</c> at 88% with two hours left (§1.3): read
    /// alone, the word would pass nothing the person's curve says. So either passes.</para>
    /// <para>Its <c>allowed_warning</c> passes nothing alone (USAGEREAD3a, D165 point 2, the account-switch design §1.3): it
    /// says a window's use is ahead of the time gone in it, not that the window is near its limit.</para>
    /// <para>The curve is read under every switch: <c>off</c> reads the smart curve, since a start still passes an account
    /// over it (the switch design §3). A window this build has no curve for is not measured, so its number passes nothing.</para>
    /// </remarks>
    public static bool Near(AccountSaid? said, RotationUse use, DateTimeOffset now) => NearWindow(said, use, now) is not null;

    /// <summary>
    /// The window that makes an account near, and why: a limit reached by its word first, then credits, then the window with
    /// the least room under its curve, where that room is below zero; null where none does.
    /// </summary>
    public static (WindowSaid Window, NearBy By)? NearWindow(AccountSaid? said, RotationUse use, DateTimeOffset now)
    {
        if (said is null) return null;
        if (said.Windows.FirstOrDefault(each => each.Standing == RefusedWord) is { } refused) return (refused, NearBy.Refused);
        if (said.Windows.FirstOrDefault(each => each.Credits) is { } drawing) return (drawing, NearBy.Credits);
        return UsageCurves.TightestSaid(use, said, now) is { Room: < 0 } over ? (over.Window, NearBy.Number) : null;
    }

    /// <summary>The share of its limit used in the window it said the most of, as a fraction; null where it said no number (ACCTWALK3).</summary>
    public static double? MostUsed(AccountSaid? said) => said?.Windows.Max(each => each.Used);

    /// <summary>
    /// An account's standing for the log (D94 §4's names only): <c>refused</c> by its word, <c>near</c> as
    /// <see cref="Near"/> reads it (over its curve among them, SWITCH1b-ii), <c>warned</c> where its agent's pace warning
    /// stands and it is not near (USAGEREAD3a), <c>clear</c> where it said and is none of those, and null where it said nothing.
    /// </summary>
    public static string? Standing(AccountSaid? said, RotationUse use, DateTimeOffset now) => NearWindow(said, use, now) switch
    {
        null when said is null => null,
        null => said.Windows.Any(each => each.Standing == WarnedWord) ? WarnedWord : ClearWord,
        { By: NearBy.Refused } => RefusedWord,
        _ => NearWord,
    };

    /// <summary>
    /// How far an account is behind its week's pace (D130 §16.3 step 5): the share of its week already gone, less the share of
    /// its weekly limit it said is used. Positive is behind, negative ahead; null where its week's use was not said.
    /// </summary>
    /// <remarks>The week is the seven days before its reset (C1); a reset further off than that reads as a week just begun.</remarks>
    public static double? Behind(AccountSaid? said, DateTimeOffset now)
    {
        if (said?.Of(AccountWindows.Weekly) is not { Used: { } used } week) return null;
        var gone = 1 - (week.Reset - now).Ticks / (double)AccountWindows.Week.Ticks;
        return Math.Clamp(gone, 0, 1) - used;
    }

    /// <summary>The share of its week gone at <paramref name="now"/>, for a sentence; null where its week was not said.</summary>
    public static double? Gone(AccountSaid? said, DateTimeOffset now) =>
        said?.Of(AccountWindows.Weekly) is { } week ? Math.Clamp(1 - (week.Reset - now).Ticks / (double)AccountWindows.Week.Ticks, 0, 1) : null;

    private static string? StandingOf(string status, WindowWords words) =>
        words.Refused.Contains(status, StringComparer.Ordinal) ? RefusedWord
        : words.Warned.Contains(status, StringComparer.Ordinal) ? WarnedWord
        : words.Clear.Contains(status, StringComparer.Ordinal) ? ClearWord
        : null;

    /// <summary>A use on the agent's scale, as a fraction; null where it is not a number from nothing up.</summary>
    private static double? Used(JsonElement element, string name, double scale) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var used)
        && double.IsFinite(used) && used >= 0 && scale > 0
            ? used / scale
            : null;

    /// <summary>A moment written as Unix seconds; null where it is not one a calendar holds.</summary>
    private static DateTimeOffset? Moment(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDouble(out var seconds) || !double.IsFinite(seconds))
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(seconds * 1000));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}

/// <summary>
/// Why an account is near (TOOL6c, D130 §6): what a start's first line says of it. Never its agent's pace warning, which is
/// not near (USAGEREAD3a); the page reads these names as <c>near[].by</c>.
/// </summary>
public enum NearBy
{
    /// <summary>Its agent said a limit was reached.</summary>
    Refused,

    /// <summary>Its agent said it is drawing on usage credits.</summary>
    Credits,

    /// <summary>A window's use over the scope's curve now (SWITCH1b-ii; the scope's <i>near</i> before it).</summary>
    Number,
}

/// <summary>
/// Claude Code's words for its windows (TOOL6c, limit-signals evidence §1): the <c>rate_limit_event</c>'s
/// <c>rate_limit_info</c>, which its protocol door forwards unchanged as <c>usage_update._meta["_claude/rateLimit"]</c> (§3).
/// </summary>
public static class ClaudeWindows
{
    private const string Declared =
        "declared, not seen: the Agent SDK's SDKRateLimitInfo.status (sdk.d.ts:5584, 0.3.284 and 0.3.287), read by TOOL4b (limit-signals evidence §1.2); the CLI derives its warning itself (§1.3)";

    public static WindowWords Words { get; } = new(
        Windows: new Dictionary<string, string>(StringComparer.Ordinal) { ["five_hour"] = "session", ["seven_day"] = AccountWindows.Weekly },
        Clear: ["allowed"],
        Warned: ["allowed_warning"],
        Refused: ["rejected"],
        Scale: 1,
        Credits: "isUsingOverage",
        Recorded:
        [
            new(
                """{"status":"allowed","resetsAt":<+2.0 h>,"rateLimitType":"five_hour","overageStatus":"rejected","overageDisabledReason":"member_zero_credit_limit","isUsingOverage":false,"unifiedWindows":{"five_hour":{"utilization":0.88,"resetsAt":<+2.0 h>},"seven_day":{"utilization":0.14,"resetsAt":<+105.3 h>}}}""",
                "2026-10-02",
                "the native door's binary and flags, one headless turn outside Daoris's driver, as rate_limit_event's rate_limit_info (limit-signals evidence §1.1); KNOW3's 116 frames that day share its keys",
                "2.1.287"),
            new("""{"status":"allowed_warning"}""", "2026-10-02", Declared),
            new("""{"status":"rejected"}""", "2026-10-02", Declared),
        ]);

    /// <summary>
    /// A window's length and scope as Claude Code's key names it, for the usage history (USAGECAP1a1): <c>five_hour</c> five
    /// hours and <c>seven_day</c> a week, the whole plan's; <c>seven_day_&lt;scope&gt;</c> a week of what the rest names (a model
    /// family: <c>seven_day_opus</c>, limit-signals evidence §1.2 and the usage-read evidence's field table); any other key
    /// neither, never guessed.
    /// </summary>
    public static (int? Minutes, string? Scope) Shape(string key) => key switch
    {
        "five_hour" => (300, null),
        "seven_day" => (10080, null),
        _ when key.StartsWith("seven_day_", StringComparison.Ordinal) && key.Length > "seven_day_".Length => (10080, key["seven_day_".Length..]),
        _ => (null, null),
    };
}
