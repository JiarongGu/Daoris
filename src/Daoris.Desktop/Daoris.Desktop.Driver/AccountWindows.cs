using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>windows.json</c> under the home (TOOL6b, D130 §5.2, §16.3 step 4, §16.4): what is known of each account's windows,
/// per agent (the account's owner, AGT7) and account. Today that is the weekly reset a limit told, which a start reads to
/// spend a week's allowance before it lapses.
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
/// <para><b>Missing or unreadable is nothing known</b> (D21's reading): nothing stands in for it, never spent and never
/// fresh (D57). Names, times and a session id only — never the agent's words, a key or who signed in. Written and read by
/// the driver alone (§14: not a twin), atomically, LF, keeping what it has no field for, so the readings TOOL6c adds
/// outlive this build's write. No HTTP route reaches it (D47 §4).</para>
///
/// <para>The file's shape: <c>{ "&lt;agent&gt;": { "&lt;account&gt;": { "weekly": { "reset": "…Z", "seen": "…Z",
/// "session": "3f9c2a71" } } } }</c>.</para>
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
