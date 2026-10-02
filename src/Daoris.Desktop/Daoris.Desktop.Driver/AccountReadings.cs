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
/// grammar for another agent's: Codex's door forwards none of Codex's limits (§4), and a Codex entry waits for a door that
/// does.</para>
/// <para><b>How a clear word with no number ranks</b> (§5.2's last question) does not arise: the evidence found the number
/// on every frame (§1.1), so a reading is ranked by its numbers and its word together (<see cref="AccountReadings.Near"/>).</para>
/// </remarks>
/// <param name="Windows">The agent's name for each window it reads → Daoris's: <c>session</c> (five hours) or <c>weekly</c>.</param>
/// <param name="Clear">The status words that mean the account may run.</param>
/// <param name="Near">The status words that mean it is near a limit: the agent's own warning.</param>
/// <param name="Refused">The status words that mean a limit was reached.</param>
/// <param name="Scale">What a full window reads as on this agent's scale: 1 for a fraction.</param>
/// <param name="Credits">The field that says the request drew on usage credits, which are billed (C5, C6).</param>
/// <param name="Recorded">The frames the entry was written against.</param>
public sealed record WindowWords(
    IReadOnlyDictionary<string, string> Windows,
    IReadOnlyList<string> Clear,
    IReadOnlyList<string> Near,
    IReadOnlyList<string> Refused,
    double Scale,
    string Credits,
    IReadOnlyList<RecordedFrame> Recorded);

/// <summary>One window as an agent said it in one frame (TOOL6c): never the agent's words, only Daoris's.</summary>
/// <param name="Window">Daoris's name for it: <c>session</c> or <c>weekly</c>.</param>
/// <param name="Used">How much of it is used, from 0 to 1, or null where the frame gave no number.</param>
/// <param name="Reset">When it turns over, which is when this reading is dropped.</param>
/// <param name="Standing"><c>clear</c>, <c>near</c> or <c>refused</c>, on the window the frame names; else null.</param>
/// <param name="Credits">Whether the frame said the request drew on usage credits, on the window it names.</param>
public sealed record WindowReading(string Window, double? Used, DateTimeOffset Reset, string? Standing = null, bool Credits = false);

/// <summary>One window as <c>windows.json</c> keeps it: its reading, when it was seen, and on which session.</summary>
public sealed record WindowSaid(
    string Window, double? Used, DateTimeOffset Reset, string? Standing, bool Credits, DateTimeOffset Seen, string? Session);

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
/// the walk makes of it: near, and how far behind its week's pace. Pure: the frame, the table and the moment are arguments.
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
    /// <summary>The standings, in Daoris's words.</summary>
    public const string ClearWord = "clear", NearWord = "near", RefusedWord = "refused";

    /// <summary>
    /// What <paramref name="info"/> says, read by <paramref name="words"/>: each window the table names that has a reset, its
    /// use where the frame gives a number, and the frame's standing and credits on the window it names
    /// (<c>rateLimitType</c>). A named window the frame's windows leave out is read from the frame's own reset and use.
    /// </summary>
    public static IReadOnlyList<WindowReading> Read(JsonElement info, WindowWords words)
    {
        if (info.ValueKind != JsonValueKind.Object) return [];

        var standing = Text(info, "status") is { } status ? StandingOf(status, words) : null;
        var named = Text(info, "rateLimitType") is { } type && words.Windows.TryGetValue(type, out var mapped) ? mapped : null;
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

    /// <summary>
    /// Whether an account is near a limit (D130 §6 as the evidence corrects it): its agent's warning word or its word that a
    /// limit was reached, its word that it is drawing on usage credits, or any window's use at or over
    /// <paramref name="near"/> percent. Nothing said is never near.
    /// </summary>
    /// <remarks>
    /// §6 had the agent's word win over the number, where Claude Code's word was thought to come without one. The evidence
    /// found a number on every frame, and the word still <c>allowed</c> at 88% with two hours left (§1.3): read alone, the
    /// word would pass nothing the person's own <i>near</i> says. So either passes.
    /// </remarks>
    public static bool Near(AccountSaid? said, int near) => NearWindow(said, near) is not null;

    /// <summary>
    /// The window that makes an account near, and why: a limit reached by its word first, then its warning word, then
    /// credits, then the most used window at or over <paramref name="near"/>; null where none does.
    /// </summary>
    public static (WindowSaid Window, NearBy By)? NearWindow(AccountSaid? said, int near)
    {
        if (said is null) return null;
        if (said.Windows.FirstOrDefault(each => each.Standing == RefusedWord) is { } refused) return (refused, NearBy.Refused);
        if (said.Windows.FirstOrDefault(each => each.Standing == NearWord) is { } warned) return (warned, NearBy.Word);
        if (said.Windows.FirstOrDefault(each => each.Credits) is { } drawing) return (drawing, NearBy.Credits);
        return said.Windows.Where(each => each.Used is { } used && used * 100 >= near - 1e-9).MaxBy(each => each.Used) is { } full
            ? (full, NearBy.Number)
            : null;
    }

    /// <summary>
    /// An account's standing for the log (D94 §4's names only): <c>refused</c> by its word, <c>near</c> as
    /// <see cref="Near"/> reads it, <c>clear</c> where it said and is neither, and null where it said nothing.
    /// </summary>
    public static string? Standing(AccountSaid? said, int near) => NearWindow(said, near) switch
    {
        null => said is null ? null : ClearWord,
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
        : words.Near.Contains(status, StringComparer.Ordinal) ? NearWord
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

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>Why an account is near (TOOL6c, D130 §6): what a start's first line says of it.</summary>
public enum NearBy
{
    /// <summary>Its agent said a limit was reached.</summary>
    Refused,

    /// <summary>Its agent's own warning word.</summary>
    Word,

    /// <summary>Its agent said it is drawing on usage credits.</summary>
    Credits,

    /// <summary>A window's use at or over the scope's <i>near</i>.</summary>
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
        Near: ["allowed_warning"],
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
}
