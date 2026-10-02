using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One account cooling (TOOL4d, D125 §2.3): until when, and what said so. Names only — never the agent's words, never a
/// key, never who signed in; the session id points at the record, which holds the words.
/// </summary>
/// <param name="Agent">Whose accounts these are: the account's owner (AGT7), so a door and its tool share one cool-off.</param>
/// <param name="Account">The profile's name, or null for the tool's own configuration home (kept as <c>""</c>).</param>
/// <param name="Until">When the account is offered again.</param>
/// <param name="Stated">Whether the agent named that time; false is Daoris's default standing in.</param>
/// <param name="Window">The window the reset named (<i>session</i>, <i>weekly</i>), or null.</param>
/// <param name="Seen">When the limit was read.</param>
/// <param name="Session">The session the limit refused, or null.</param>
/// <param name="AssumedZone">Whether the machine's zone stood in for the one the agent named.</param>
/// <param name="NotBelieved">Whether the agent named a date more than 8 days off, so the default stood in for it.</param>
public sealed record CoolingEntry(
    string Agent, string? Account, DateTimeOffset Until, bool Stated, string? Window, DateTimeOffset Seen, string? Session,
    bool AssumedZone = false, bool NotBelieved = false);

/// <summary>
/// <c>cooling.json</c> under the home (TOOL4d, D125 §2.3): each cooling account, keyed by its owner and profile name,
/// <c>""</c> for the tool's own configuration home (AGT3b's key).
/// </summary>
/// <remarks>
/// <para><b>On disk, not in memory</b>, as AGT3b's refusals are: a reset at 07:52 must survive a restart at 03:00. A
/// refused key waits for a person; a limit waits for a time.</para>
///
/// <para><b>Not in <c>harnesses.json</c></b>: that file is the person's wiring, written by two editors, and a driver
/// writing it at each refusal would race them. A cool-off is an observation, not a choice.</para>
///
/// <para><b>Missing or unreadable is no account cooling</b> (D21's reading): an observation lost costs at most one start.
/// An entry whose <c>until</c> has passed is ready, and the next write drops it. A writer keeps what it has no field for,
/// since the CLI reads and ends entries too: its <c>cooling.ts</c> is the twin of <see cref="Read"/>, <see cref="Of"/> and
/// <see cref="End"/>, and <c>CoolingTwinTests</c> holds the tables both keep (TOOL4e). No HTTP route reaches it (D47 §4).</para>
///
/// <para>The file's shape, each field written only where it says something:
/// <c>{ "&lt;agent&gt;": { "&lt;profile or empty&gt;": { "until": "…Z", "stated": true, "window": "weekly",
/// "seen": "…Z", "session": "3f9c2a71", "assumedZone": true, "notBelieved": true } } }</c>.</para>
/// </remarks>
public static class AccountCooling
{
    public const string FileName = "cooling.json";

    // One writer at a time in this process: the conclusions of sessions side by side, and a screen's early end.
    private static readonly object Gate = new();

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, NewLine = "\n" };

    /// <summary>Where the file is: beside <c>harnesses.json</c> and the accounts, under the home.</summary>
    public static string PathOf(string home) => Path.Combine(home, FileName);

    /// <summary>Every account cooling at <paramref name="now"/>, by agent and account.</summary>
    public static IReadOnlyList<CoolingEntry> Read(string home, DateTimeOffset now) =>
        [.. Entries(Load(home)).Where(entry => entry.Until > now)
            .OrderBy(entry => entry.Agent, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Account ?? "", StringComparer.OrdinalIgnoreCase)];

    /// <summary>The account's cool-off at <paramref name="now"/>, or null when it is ready.</summary>
    public static CoolingEntry? Of(string home, string agent, string? account, DateTimeOffset now) =>
        Read(home, now).FirstOrDefault(entry => Same(entry, agent, account));

    /// <summary>
    /// Cool an account (a limit read): its entry replaced whole, every passed entry dropped, and the rest kept as written.
    /// </summary>
    public static void Cool(string home, CoolingEntry entry, DateTimeOffset now) => Edit(home, now, root =>
    {
        var agent = Child(root, entry.Agent) ?? (JsonObject)(root[entry.Agent] = new JsonObject());
        if (Key(agent, entry.Account ?? "") is { } held) agent.Remove(held);
        agent[entry.Account ?? ""] = Written(entry);
        return true;
    });

    /// <summary>End one account's cool-off early (<i>Try now</i>, a sign-in, a key). True when it was cooling.</summary>
    public static bool End(string home, string agent, string? account, DateTimeOffset now)
    {
        var ended = false;
        Edit(home, now, root =>
        {
            if (Child(root, agent) is not { } held || Key(held, account ?? "") is not { } key) return false;
            ended = held[key] is JsonObject entry && Moment(entry, "until") is { } until && until > now;
            held.Remove(key);
            return true;
        });
        return ended;
    }

    /// <summary>
    /// End every tool's own configuration home's cool-off (the roster's refresh, D125 §3.7): a sign-in there happens at
    /// the person's own terminal, where Daoris does not see it. A named account's reset is a stated fact, and stands.
    /// </summary>
    /// <returns>How many were cooling.</returns>
    public static int EndOwnHomes(string home, DateTimeOffset now)
    {
        var ended = Read(home, now).Count(entry => entry.Account is null);
        if (ended == 0) return 0;
        Edit(home, now, root =>
        {
            foreach (var (_, agent) in root)
            {
                if (agent is JsonObject held && Key(held, "") is { } own) held.Remove(own);
            }

            return true;
        });
        return ended;
    }

    /// <summary>
    /// A sign-in into the account at <paramref name="profileHome"/> ended (D125 §2.3): the directory may now hold another
    /// account, so its cool-off ends. True when one was cooling; a home outside the accounts' layout ends nothing.
    /// </summary>
    public static bool SignedIn(string profileHome, DateTimeOffset now)
    {
        // The layout `HarnessSettings.ProfileHome` makes: <home>/harnesses/<agent>/<profile>.
        var account = new DirectoryInfo(Path.GetFullPath(profileHome));
        if (account.Parent is not { Parent: { Name: "harnesses", Parent: { } home } } agent) return false;
        return End(home.FullName, agent.Name, account.Name, now);
    }

    private static bool Same(CoolingEntry entry, string agent, string? account) =>
        string.Equals(entry.Agent, agent, StringComparison.OrdinalIgnoreCase)
        && string.Equals(entry.Account ?? "", account ?? "", StringComparison.OrdinalIgnoreCase);

    /// <summary>The file's root, or an empty one when it is missing or does not read: no account cooling.</summary>
    private static JsonObject Load(string home)
    {
        try
        {
            return File.Exists(PathOf(home)) && JsonNode.Parse(File.ReadAllText(PathOf(home))) is JsonObject root
                ? root
                : new JsonObject();
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return new JsonObject();
        }
    }

    /// <summary>Every entry that reads, whatever its <c>until</c>; one that does not read is none.</summary>
    private static IEnumerable<CoolingEntry> Entries(JsonObject root)
    {
        foreach (var (agent, accounts) in root)
        {
            if (accounts is not JsonObject held) continue;
            foreach (var (account, node) in held)
            {
                if (node is not JsonObject entry || Moment(entry, "until") is not { } until) continue;
                yield return new CoolingEntry(
                    agent, account.Length == 0 ? null : account, until, Flag(entry, "stated"), Text(entry, "window"),
                    Moment(entry, "seen") ?? until, Text(entry, "session"), Flag(entry, "assumedZone"), Flag(entry, "notBelieved"));
            }
        }
    }

    /// <summary>Read, change and write the file whole, atomically, with every passed entry dropped.</summary>
    private static void Edit(string home, DateTimeOffset now, Func<JsonObject, bool> change)
    {
        lock (Gate)
        {
            var root = Load(home);
            if (!change(root)) return;

            foreach (var (name, accounts) in root.ToList())
            {
                if (accounts is not JsonObject held) continue;
                foreach (var (account, node) in held.ToList())
                {
                    // Passed, or never readable: nothing a reader would ever call cooling.
                    if (node is not JsonObject entry || Moment(entry, "until") is not { } until || until <= now) held.Remove(account);
                }

                if (held.Count == 0) root.Remove(name);
            }

            Directory.CreateDirectory(home);
            // LF on every platform, as the CLI's `endCooling` writes the file (TOOL4e).
            AtomicFile.WriteText(PathOf(home), root.ToJsonString(Indented) + "\n");
        }
    }

    private static JsonObject Written(CoolingEntry entry)
    {
        var written = new JsonObject { ["until"] = Stamp(entry.Until), ["stated"] = entry.Stated };
        if (entry.Window is { Length: > 0 } window) written["window"] = window;
        written["seen"] = Stamp(entry.Seen);
        if (entry.Session is { Length: > 0 } session) written["session"] = session;
        if (entry.AssumedZone) written["assumedZone"] = true;
        if (entry.NotBelieved) written["notBelieved"] = true;
        return written;
    }

    /// <summary>UTC to the second, as the design writes it.</summary>
    internal static string Stamp(DateTimeOffset moment) =>
        moment.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>A name in an object, compared without case, as the wiring compares names.</summary>
    private static string? Key(JsonObject held, string name) =>
        held.Select(pair => pair.Key).FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

    private static JsonObject? Child(JsonObject root, string name) =>
        Key(root, name) is { } key ? root[key] as JsonObject : null;

    /// <summary>
    /// A moment as ISO 8601 writes it, and only so: a lenient parse reads <i>Oct 3</i> as this year's, which would make a
    /// hand-mangled entry an account cooling.
    /// </summary>
    private static DateTimeOffset? Moment(JsonObject entry, string name) =>
        Text(entry, name) is { } text
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

    private static string? Text(JsonObject entry, string name) =>
        entry[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    private static bool Flag(JsonObject entry, string name) =>
        entry[name] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;
}

/// <summary>
/// What a cool-off says (TOOL4d, D125 §2.4, §4): whose account, until when in this machine's zone with the zone named,
/// and whether the agent said so. The driver's own sentences, rendered verbatim like every sentence it writes (D24).
/// </summary>
public static class CoolingWords
{
    /// <summary>
    /// A start's hold, a conversation's refusal, and a quest's reason while it waits: the account by its owner and its
    /// profile name. Shown on this machine only — a consideration is the machine's — so it may name the account.
    /// </summary>
    public static string Hold(CoolingEntry entry, TimeZoneInfo zone)
    {
        var said = $"{Who(entry)} is cooling until {When(entry.Until, zone)}, {Why(entry)}. Daoris starts nothing on it until then.";
        // The own home holds whoever last signed in at the person's terminal, and the roster's refresh ends its
        // cool-off, since Daoris cannot see that sign-in change (D125 §3.7).
        return entry.Account is null
            ? $"{said} If you have signed in to another account at your own terminal since, refresh Settings → Agents."
            : said;
    }

    /// <summary>
    /// What the refused session's record adds to its note. 🔴 It names no account, its own included: the note travels to
    /// another machine, and its scrubber elides only the record's own profile name (D125 §3.6).
    /// </summary>
    public static string Note(CoolingEntry entry, TimeZoneInfo zone) =>
        $"The account it ran on is cooling until {When(entry.Until, zone)}, {Why(entry)}, and nothing starts on it until then.";

    /// <summary>What a conversation whose turn was refused is told in its record; the conversation goes on.</summary>
    public static string Conversation(CoolingEntry entry, TimeZoneInfo zone) =>
        $"The account this conversation runs on is cooling until {When(entry.Until, zone)}, {Why(entry)}, and nothing new "
        + "starts on it until then.";

    /// <summary>A moment as a person reads it here: <i>Oct 3, 16:02 (Asia/Kathmandu)</i>, in the machine's zone, named.</summary>
    public static string When(DateTimeOffset moment, TimeZoneInfo zone) =>
        $"{TimeZoneInfo.ConvertTime(moment, zone).ToString("MMM d, HH:mm", CultureInfo.InvariantCulture)} ({ZoneName(zone)})";

    private static string Who(CoolingEntry entry) =>
        entry.Account is { } account ? $"the `{entry.Agent}` account `{account}`" : $"`{entry.Agent}`'s own sign-in";

    private static string Why(CoolingEntry entry) =>
        entry.Stated ? (entry.AssumedZone ? "as the agent said, in this machine's zone" : "as the agent said")
        : entry.NotBelieved ? "Daoris's default: the agent named a date more than 8 days off"
        : "Daoris's default: the agent named no time";

    /// <summary>The zone's IANA name where the platform has one, a Windows id converted, else its own id.</summary>
    private static string ZoneName(TimeZoneInfo zone) =>
        zone.HasIanaId ? zone.Id
        : TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana
        : zone.Id;
}

/// <summary>
/// The machine log's account lines (TOOL4d, D125 §5.4, D94 §4): <c>account.limited</c> and <c>starts.waiting</c>, each with
/// the fields its catalogue gives it and nothing else. An account is its profile name, null for the tool's own home:
/// never a key, a key's handle, who signed in, or the agent's sentence.
/// </summary>
public sealed record AccountLine(string Event, IReadOnlyList<(string Key, object? Value)> Data)
{
    /// <summary>What a profile name may be in a line: an identifier, never a phrase.</summary>
    private static readonly Regex Name = new("^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$", RegexOptions.CultureInvariant);

    /// <summary>A limit met: which account, which window, until when, said or defaulted, at which turn and context.</summary>
    /// <param name="turn">The refused turn: the turns its record ended, plus one.</param>
    /// <param name="used">Its context at its high-water, where the door reported one.</param>
    public static AccountLine Limited(string session, string adapter, string? account, LimitSeen seen, long turn, long? used) =>
        new("account.limited",
        [
            ("session", session), ("adapter", adapter), ("account", Profile(account)), ("hit", Bounded(seen.Hit)),
            ("window", Bounded(seen.Window)), ("until", AccountCooling.Stamp(seen.Until)), ("stated", seen.Stated),
            ("assumedZone", seen.AssumedZone), ("turn", turn), ("used", used),
        ]);

    /// <summary>Every account a start may use was cooling: written once per wait.</summary>
    /// <param name="quests">How many quests the wait held when it was first written.</param>
    public static AccountLine Waiting(string adapter, string? account, string? workspace, DateTimeOffset until, int quests) =>
        new("starts.waiting",
        [
            ("adapter", adapter), ("account", Profile(account)), ("workspace", workspace), ("until", AccountCooling.Stamp(until)),
            ("quests", quests),
        ]);

    private static string? Profile(string? account) => account is not null && Name.IsMatch(account) ? account : null;

    /// <summary>The marker's own word, kept short: a window's name, never a clause.</summary>
    private static string? Bounded(string? word) => word is { Length: > 0 and <= 40 } ? word : null;
}

/// <summary>
/// Starts held because the account they would run on is cooling (TOOL4d, D125 §4): the agent, the account, the first
/// ready time, and what was held. The tick report carries it, so a screen shows a quest as waiting for an account rather
/// than parked, and the attention watch says it once.
/// </summary>
/// <param name="Adapter">The adapter the held starts ride.</param>
/// <param name="Agent">Whose accounts: the owner a door runs as (AGT7).</param>
/// <param name="Account">The profile's name, or null for the tool's own home.</param>
/// <param name="Workspace">The workspace the held starts belong to, where they share one; else null.</param>
/// <param name="Until">When the account is offered again: the first ready time.</param>
/// <param name="Sentence">The hold's sentence, which each held quest's reason carries.</param>
public sealed record AccountWait(
    string Adapter, string Agent, string? Account, string? Workspace, DateTimeOffset Until, bool Stated, string Sentence)
{
    /// <summary>The quests held, in plan order.</summary>
    public IReadOnlyList<string> Quests { get; init; } = [];

    /// <summary>The asks whose intakes were held.</summary>
    public IReadOnlyList<string> Asks { get; init; } = [];

    /// <summary>Where the held starts would have run: each quest's repository, or <c>ask #id</c>.</summary>
    public IReadOnlyList<string> Repositories { get; init; } = [];
}
