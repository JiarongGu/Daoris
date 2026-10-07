using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>windows.json</c> under the home (TOOL6b, TOOL6c; D130 §5.2, §16.3 steps 2, 4 and 5, §16.4): what is known of each
/// account's windows, per agent (the account's owner, AGT7) and account: the weekly reset a limit told, which a start reads
/// to spend a week's allowance before it lapses, and what the agent last said about each window on the door its session ran
/// on, or its own server answered where no door carries it (CODEXUSE1) — its use, its reset, its standing — which near and
/// pace read.
/// </summary>
/// <remarks>
/// <para><b>A weekly reset, once told, is that account's</b>: the maker fixes it at one time each week (C1), so where the
/// agent's toolchain says so (<see cref="HarnessToolchain.WeekFixed"/>) it is carried a week at a time once it passes, and
/// otherwise it is dropped at its reset (Codex: not read on any maker's page). Only a reset the agent named counts: a
/// limit that named none told nothing about its week.</para>
///
/// <para><b>Beside <c>cooling.json</c>, not in it</b>: a cool-off ends at its reset, and what the reset says about the
/// week does not. Not in <c>harnesses.json</c> either, which is the person's wiring.</para>
///
/// <para><b>What an agent said is a floor, as of when it said it, and gone at its reset</b> (TOOL6c, §5.2): each window's
/// newest reading replaces the older, and once its reset passes the account is unknown for that window until a session says
/// again. The weekly window's reading is the account's week too, so it is carried on as a limit's is; its use is not.</para>
///
/// <para><b>Missing or unreadable is nothing known</b> (D21's reading): nothing stands in for it, never spent and never
/// fresh (D57). Names, numbers, times and a session id only — never the agent's words, a key or who signed in. Written by
/// the driver alone, atomically, LF, keeping what it has no field for; read by the driver and by the CLI's
/// <c>windows.ts</c> for <c>daoris agent list</c> and <c>profile use</c>, which <c>WindowsTwinTests</c> holds to this
/// reader (TOOL6c amends §14's <i>not a twin</i>). No HTTP route reaches it (D47 §4).</para>
///
/// <para>The file's shape: <c>{ "&lt;agent&gt;": { "&lt;account&gt;": { "weekly": { "reset": "…Z", "used": 0.14, "seen": "…Z",
/// "session": "3f9c2a71" }, "session": { "reset": "…Z", "used": 0.88, "standing": "clear", "seen": "…Z" } } } }</c>, where a
/// week a limit told has no <c>used</c>, and <c>standing</c> and <c>credits</c> sit on the window the frame named.</para>
/// </remarks>
public static class AccountWindows
{
    public const string FileName = "windows.json";

    /// <summary>The window a weekly limit's reset names, as the agent's table reads it.</summary>
    public const string Weekly = "weekly";

    /// <summary>How far a fixed weekly reset is carried each time it passes (C1).</summary>
    public static readonly TimeSpan Week = TimeSpan.FromDays(7);

    // One writer at a time in this process: the conclusions of sessions side by side.
    private static readonly object Gate = new();

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, NewLine = "\n" };

    /// <summary>Where the file is: beside <c>cooling.json</c>, under the home.</summary>
    public static string PathOf(string home) => Path.Combine(home, FileName);

    /// <summary>
    /// The next weekly reset after <paramref name="now"/> from one told as <paramref name="reset"/>: itself until it passes,
    /// then a week on as many times as it takes where the maker fixes it, else none.
    /// </summary>
    public static DateTimeOffset? Next(DateTimeOffset reset, DateTimeOffset now, bool fixedWeek)
    {
        if (reset > now) return reset;
        if (!fixedWeek) return null;
        var weeks = (long)Math.Floor((now - reset).Ticks / (double)Week.Ticks) + 1;
        var next = reset + TimeSpan.FromTicks(Week.Ticks * weeks);
        return next > now ? next : next + Week;
    }

    /// <summary>The account's next weekly reset known at <paramref name="now"/>, or null where none is known.</summary>
    public static DateTimeOffset? WeekOf(string home, string agent, string? account, DateTimeOffset now, bool fixedWeek)
    {
        if (account is null) return null;
        var week = Child(Child(Child(Load(home), agent), account), Weekly);
        return week is not null && Moment(week, "reset") is { } reset ? Next(reset, now, fixedWeek) : null;
    }

    /// <summary>
    /// A weekly reset a limit told (D125 §2): the account's weekly entry replaced by it, and everything else kept as written.
    /// </summary>
    public static void Told(string home, string agent, string account, DateTimeOffset reset, DateTimeOffset seen, string? session)
    {
        lock (Gate)
        {
            var root = Load(home);
            var owner = Child(root, agent) ?? (JsonObject)(root[agent] = new JsonObject());
            var held = Child(owner, account) ?? (JsonObject)(owner[account] = new JsonObject());
            var week = new JsonObject { ["reset"] = AccountCooling.Stamp(reset), ["seen"] = AccountCooling.Stamp(seen) };
            if (session is { Length: > 0 }) week["session"] = session;
            if (Key(held, Weekly) is { } was) held.Remove(was);
            held[Weekly] = week;

            Directory.CreateDirectory(home);
            AtomicFile.WriteText(PathOf(home), root.ToJsonString(Indented) + "\n");
        }
    }

    /// <summary>
    /// What an agent said about an account's windows (TOOL6c, D130 §5.2): each window's entry replaced whole by its newest
    /// reading — its reset, its use where said, its standing and credits where said, when, and on which session — and
    /// everything else kept as written. The weekly window's reset is the account's week from then on (§16.3 step 4).
    /// </summary>
    public static void Said(
        string home, string agent, string account, IReadOnlyList<WindowReading> readings, DateTimeOffset seen, string? session)
    {
        if (readings.Count == 0) return;
        lock (Gate)
        {
            var root = Load(home);
            var owner = Child(root, agent) ?? (JsonObject)(root[agent] = new JsonObject());
            var held = Child(owner, account) ?? (JsonObject)(owner[account] = new JsonObject());
            foreach (var reading in readings)
            {
                var window = new JsonObject { ["reset"] = AccountCooling.Stamp(reading.Reset) };
                if (reading.Used is { } used) window["used"] = used;
                if (reading.Standing is { Length: > 0 } standing) window["standing"] = standing;
                if (reading.Credits) window["credits"] = true;
                window["seen"] = AccountCooling.Stamp(seen);
                if (session is { Length: > 0 }) window["session"] = session;
                if (Key(held, reading.Window) is { } was) held.Remove(was);
                held[reading.Window] = window;
            }

            Directory.CreateDirectory(home);
            AtomicFile.WriteText(PathOf(home), root.ToJsonString(Indented) + "\n");
        }
    }

    /// <summary>
    /// What an account's agent last said about its windows, as of <paramref name="now"/> (TOOL6c, D130 §5.2, §5.3): each
    /// window whose reset has not passed and that says a use or a standing, in the file's order; null where none does, which
    /// is unknown — never spent and never fresh. A week a limit told says no use, so it is no reading.
    /// </summary>
    /// <remarks>The CLI's <c>windows.ts</c> is the twin of this reading; <c>WindowsTwinTests</c> holds the table both keep.</remarks>
    public static AccountSaid? SaidOf(string home, string agent, string? account, DateTimeOffset now)
    {
        if (account is null || Child(Child(Load(home), agent), account) is not { } held) return null;

        var windows = new List<WindowSaid>();
        foreach (var (name, node) in held)
        {
            if (node is not JsonObject entry || Moment(entry, "reset") is not { } reset || reset <= now
                || Moment(entry, "seen") is not { } seen)
            {
                continue;
            }

            var used = entry["used"] is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number) && number >= 0
                ? number : (double?)null;
            var standing = Text(entry, "standing");
            if (used is null && standing is null && !Flag(entry, "credits")) continue;
            windows.Add(new WindowSaid(name, used, reset, standing, Flag(entry, "credits"), seen, Text(entry, "session")));
        }

        return windows.Count == 0 ? null : new AccountSaid(windows);
    }

    private static string? Text(JsonObject entry, string name) =>
        entry[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    private static bool Flag(JsonObject entry, string name) =>
        entry[name] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    /// <summary>The file's root, or an empty one when it is missing or does not read.</summary>
    private static JsonObject Load(string home)
    {
        try
        {
            return File.Exists(PathOf(home)) && JsonNode.Parse(File.ReadAllText(PathOf(home))) is JsonObject root ? root : new JsonObject();
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return new JsonObject();
        }
    }

    /// <summary>A name in an object, compared without case, as the wiring compares names.</summary>
    private static string? Key(JsonObject held, string name) =>
        held.Select(pair => pair.Key).FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

    private static JsonObject? Child(JsonObject? parent, string name) =>
        parent is not null && Key(parent, name) is { } key ? parent[key] as JsonObject : null;

    /// <summary>A moment as ISO 8601 writes it, and only so, as <c>cooling.json</c> reads one.</summary>
    private static DateTimeOffset? Moment(JsonObject entry, string name) =>
        entry[name] is JsonValue value && value.TryGetValue<string>(out var text)
        && DateTimeOffset.TryParseExact(
            text, IsoMoments, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var moment)
            ? moment
            : null;

    private static readonly string[] IsoMoments =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
    ];
}
