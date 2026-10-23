using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Daoris.Driver.AccountFileNodes;

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
    // One writer at a time in this process: the conclusions of sessions side by side, and a screen's early end.
    private static readonly object Gate = new();

    /// <summary>Where the file is: beside <c>harnesses.json</c> and the accounts, under the home.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Cooling);

    /// <summary>Every account cooling at <paramref name="now"/>, by agent and account.</summary>
    public static IReadOnlyList<CoolingEntry> Read(string home, DateTimeOffset now)
    {
        var unkept = Unwritten(home, now);
        var written = Entries(Load(home)).Where(entry => entry.Until > now && !unkept.Any(held => Same(held, entry.Agent, entry.Account)));
        return [.. written.Concat(unkept)
            .OrderBy(entry => entry.Agent, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Account ?? "", StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// The cool-offs the file would not take (LIMITRESUME1b, D206; the limit-resume design §3.1, round 3's Codex 10), by home, agent and
    /// account: the limit was read, and only keeping it failed, so it is held in this process's memory, which every reader reads as it
    /// reads the file, until the file takes it or its <c>until</c> passes. A shell that ends loses it, and the first attempt after meets
    /// the refusal again, limited again: one attempt per come-up.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Home, string Agent, string Account), CoolingEntry> Unkept = new();

    private static (string, string, string) UnkeptKey(string home, string agent, string? account) =>
        (Path.GetFullPath(home).ToUpperInvariant(), agent.ToUpperInvariant(), (account ?? "").ToUpperInvariant());

    /// <summary>
    /// Cool an account, as <see cref="Cool"/> does, or, where the file will not take it, hold it in memory (LIMITRESUME1b): the account is
    /// passed until its <c>until</c> all the same. True where the file took it.
    /// </summary>
    public static bool Keep(string home, CoolingEntry entry, DateTimeOffset now)
    {
        var key = UnkeptKey(home, entry.Agent, entry.Account);
        try
        {
            Cool(home, entry, now);
            Unkept.TryRemove(key, out _);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Unkept[key] = entry;
            return false;
        }
    }

    /// <summary>Whether the account's cool-off is held in memory only, the file not having taken it (LIMITRESUME1b).</summary>
    public static bool Unwritten(string home, string agent, string? account) => Unkept.ContainsKey(UnkeptKey(home, agent, account));

    /// <summary>The cool-offs held in memory for this home that still hold at <paramref name="now"/>; one whose <c>until</c> passed is let go.</summary>
    private static List<CoolingEntry> Unwritten(string home, DateTimeOffset now)
    {
        var held = new List<CoolingEntry>();
        if (Unkept.IsEmpty) return held;

        var here = Path.GetFullPath(home).ToUpperInvariant();
        foreach (var (key, entry) in Unkept)
        {
            if (key.Home != here) continue;
            if (entry.Until <= now) Unkept.TryRemove(key, out _);
            else held.Add(entry);
        }

        return held;
    }

    /// <summary>
    /// Each cool-off held in memory for this home written to the file again (LIMITRESUME1b): once per look, never per read, since a file
    /// that will not take it may take seconds to say so. One it takes leaves memory; one whose <c>until</c> passed is let go.
    /// </summary>
    public static void WriteUnkept(string home, DateTimeOffset now)
    {
        foreach (var entry in Unwritten(home, now))
        {
            try
            {
                Cool(home, entry, now);
                Unkept.TryRemove(UnkeptKey(home, entry.Agent, entry.Account), out _);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Still unkept: the next look tries again.
            }
        }
    }

    /// <summary>
    /// How long after a cool-off ends its account is said to be offered again (TOOL6e): a day, the walk's own horizon
    /// (<see cref="AccountRotation.LastDay"/>); a cool-off that ended longer ago explains no start a person is looking at.
    /// </summary>
    public static readonly TimeSpan OfferedFor = TimeSpan.FromDays(1);

    /// <summary>
    /// Every account whose cool-off ended within <see cref="OfferedFor"/> before <paramref name="now"/>, as the file still
    /// holds it (TOOL6e): its <c>until</c> is when it was offered again. The next write drops a passed entry, so this says
    /// only what the file has not lost; an early end (<i>Try now</i>, a sign-in) removes the entry and says nothing here.
    /// </summary>
    public static IReadOnlyList<CoolingEntry> Offered(string home, DateTimeOffset now) =>
        [.. Entries(Load(home)).Where(entry => entry.Until <= now && now - entry.Until <= OfferedFor)
            .OrderBy(entry => entry.Agent, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Account ?? "", StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// The cool-off a limit wrote for <paramref name="session"/>, passed or not, as the file still holds it (LIMITRESUME1h, D206 design
    /// §3.8): a run's own reset. Null where none names it: never written, ended early, or replaced by a later limit on the same account.
    /// </summary>
    public static CoolingEntry? OfSession(string home, string session) =>
        Entries(Load(home)).FirstOrDefault(entry => string.Equals(entry.Session, session, StringComparison.Ordinal));

    /// <summary>The account's cool-off at <paramref name="now"/>, or null when it is ready.</summary>
    public static CoolingEntry? Of(string home, string agent, string? account, DateTimeOffset now) =>
        Read(home, now).FirstOrDefault(entry => Same(entry, agent, account));

    /// <summary>
    /// Cool an account (a limit read): its entry replaced whole, every passed entry dropped, and the rest kept as written.
    /// </summary>
    /// <param name="read">Called once the file is read, before the edit is written: a test's other writer, held off by the keep lock.</param>
    public static void Cool(string home, CoolingEntry entry, DateTimeOffset now, Action? read = null) => Edit(home, now, root =>
    {
        var agent = Made(root, entry.Agent);
        if (Key(agent, entry.Account ?? "") is { } held) agent.Remove(held);
        agent[entry.Account ?? ""] = Written(entry);
        return true;
    }, read);

    /// <summary>End one account's cool-off early (<i>Try now</i>, a sign-in, a key). True when it was cooling.</summary>
    public static bool End(string home, string agent, string? account, DateTimeOffset now)
    {
        // One held in memory ends too (LIMITRESUME1b), whether or not the file reads.
        var ended = Unkept.TryRemove(UnkeptKey(home, agent, account), out var unkept) && unkept.Until > now;
        try
        {
            Edit(home, now, root =>
            {
                if (Child(root, agent) is not { } held || Key(held, account ?? "") is not { } key) return false;
                ended |= held[key] is JsonObject entry && Moment(entry, "until") is { } until && until > now;
                held.Remove(key);
                return true;
            });
        }
        catch (Exception error) when (unkept is not null && error is IOException or UnauthorizedAccessException)
        {
            // The file would not take it before either: the memory was all there was.
        }

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
        // The layout `HarnessSettings.ProfileHome` makes, at either place of the account homes (HOMELAYOUT3).
        return HarnessSettings.AccountAt(profileHome) is { } at && End(at.Home, at.Agent, at.Account, now);
    }

    private static bool Same(CoolingEntry entry, string agent, string? account) =>
        string.Equals(entry.Agent, agent, StringComparison.OrdinalIgnoreCase)
        && string.Equals(entry.Account ?? "", account ?? "", StringComparison.OrdinalIgnoreCase);

    /// <summary>The file's root, or an empty one when it is missing or does not read: no account cooling.</summary>
    private static JsonObject Load(string home) => AccountFileNodes.Load(PathOf(home));

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
                    agent, account.Length == 0 ? null : account, until, Json.Flag(entry, "stated"), Json.NonEmpty(entry, "window"),
                    Moment(entry, "seen") ?? until, Json.NonEmpty(entry, "session"), Json.Flag(entry, "assumedZone"),
                    Json.Flag(entry, "notBelieved"));
            }
        }
    }

    /// <summary>Read, change and write the file whole, atomically, with every passed entry dropped.</summary>
    /// <remarks>
    /// 🔴 <b>The file's <see cref="KeepLock"/> is held from the read to the write</b> (PUSH1f2e, the alone-path design §1.4): the gate keeps
    /// this process's writers apart, and the lock another process's, a terminal's sign-in beside the window's limit read among them, so
    /// neither writes the file whole over an entry the other wrote after its read.
    /// </remarks>
    private static void Edit(string home, DateTimeOffset now, Func<JsonObject, bool> change, Action? read = null)
    {
        lock (Gate)
        {
            using var locked = KeepLock.Take(PathOf(home));
            var root = Load(home);
            read?.Invoke();
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

            // LF on every platform, text as written, as the CLI's `endCooling` writes the file (TOOL4e, DEDUP3a).
            JsonFile.Write(PathOf(home), root);
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
    /// <param name="names">
    /// The owner's names by id, read as the hold is said (ACCT2b): the account is said by the name the person gave it, else its
    /// id. Null says the id.
    /// </param>
    public static string Hold(CoolingEntry entry, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names = null) =>
        HoldOf(entry, zone, names).Note;

    /// <summary>
    /// <see cref="Hold"/> by code (ROSTERREFUSE1), its English the same to the byte: the account by its owner and its id, never by
    /// the name the person gave it, which only the English says; until when a moment; why a cool-off's reason. The tool's own
    /// sign-in is a line of its own, its refresh <c>waits.read-again</c>.
    /// </summary>
    public static Noted HoldOf(CoolingEntry entry, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names = null)
    {
        var said = $"{Who(entry, names)} is cooling until {When(entry.Until, zone)}, {Why(entry)}. Daoris starts nothing on it until then.";
        (string Name, object? Value)[] facts =
            [("owner", entry.Agent), ("until", NoteCodes.Moment(entry.Until)), ("why", CoolingWhy.Of(entry))];
        if (entry.Account is { } account) return Noted.Of(NoteCodes.HeldCoolingAccount, said, [("account", account), .. facts]);

        // The own home holds whoever last signed in at the person's terminal, and the roster's refresh ends its
        // cool-off, since Daoris cannot see that sign-in change (D125 §3.7). UX6e2: opening the Agents place asks nothing
        // (D150 §5.3), so the refresh is its Read again.
        return Noted.Of(NoteCodes.HeldCoolingOwn, said, facts).Then(" ", Noted.Of(
            NoteCodes.WaitsReadAgain,
            "If you have signed in to another account at your own terminal since, press Read again under Agents → the agent's page → Accounts."));
    }

    /// <summary>
    /// What the refused session's record adds to its note: whose accounts they are, the kind of limit where its reset named
    /// one (AGT3d), until when, and why. 🔴 It names no account, its own included: the note travels to another machine, and
    /// its scrubber elides only the record's own profile name (D125 §3.6). The owner is the agent, which names none.
    /// </summary>
    public static string Note(CoolingEntry entry, TimeZoneInfo zone) =>
        $"The `{entry.Agent}` account it ran on {Reached(entry)}is cooling until {When(entry.Until, zone)}, {Why(entry)}, and "
        + "nothing starts on it until then.";

    /// <summary>
    /// <see cref="Note"/> with its code (LANG1a, the language design §4 row 22): until when as a moment the page formats in the
    /// reader's zone, why by its code, and, since AGT3d, whose accounts and the window, which the page words
    /// (<c>harness.window.*</c>). It names no account, as the sentence does not.
    /// </summary>
    public static Noted NoteOf(CoolingEntry entry, TimeZoneInfo zone) => Coded(entry, Note(entry, zone));

    /// <summary>What a conversation whose turn was refused is told in its record; the conversation goes on.</summary>
    public static string Conversation(CoolingEntry entry, TimeZoneInfo zone) =>
        $"The `{entry.Agent}` account this conversation runs on {Reached(entry)}is cooling until {When(entry.Until, zone)}, "
        + $"{Why(entry)}, and nothing new starts on it until then.";

    /// <summary>
    /// <see cref="Conversation"/> with the code a session record's cooling line carries (CONVNOTE1b; <see cref="NoteOf"/>,
    /// LANG1a), so the page words a conversation's cooling in the reader's language as it words a record's. Its English stays
    /// the conversation's sentence, and it names no account, as neither sentence does.
    /// </summary>
    public static Noted ConversationOf(CoolingEntry entry, TimeZoneInfo zone) => Coded(entry, Conversation(entry, zone));

    /// <summary>
    /// The cooling line by its code (AGT3d): <c>account.cooling-window</c> with the window its reset named, else
    /// <c>account.cooling-no-window</c>, since a line whose entry says a value the part lacks is shown as recorded. Never
    /// <c>account.cooling</c>, which keeps its old shape for the notes written before.
    /// </summary>
    private static Noted Coded(CoolingEntry entry, string text) => WindowOf(entry) is { } window
        ? Noted.Of(NoteCodes.AccountCoolingWindow, text,
            ("until", NoteCodes.Moment(entry.Until)), ("why", CoolingWhy.Of(entry)), ("owner", entry.Agent), ("window", window))
        : Noted.Of(NoteCodes.AccountCoolingNoWindow, text,
            ("until", NoteCodes.Moment(entry.Until)), ("why", CoolingWhy.Of(entry)), ("owner", entry.Agent));

    /// <summary>The kind of limit, as its reset named it (<i>hit its weekly limit and </i>), or nothing where it named none.</summary>
    private static string Reached(CoolingEntry entry) => WindowOf(entry) is { } window ? $"hit its {window} limit and " : "";

    /// <summary>
    /// The window the reset named, a word read out of the agent's sentence (<i>session</i>, <i>weekly</i>), bounded as the log
    /// bounds it (<see cref="AccountLine.Bounded"/>): a value is a fact, never a phrase, and the note travels.
    /// </summary>
    private static string? WindowOf(CoolingEntry entry) => AccountLine.Bounded(entry.Window);

    /// <summary>A moment as a person reads it here: <i>Oct 3, 16:02 (Asia/Kathmandu)</i>, in the machine's zone, named.</summary>
    public static string When(DateTimeOffset moment, TimeZoneInfo zone) =>
        $"{TimeZoneInfo.ConvertTime(moment, zone).ToString("MMM d, HH:mm", CultureInfo.InvariantCulture)} ({ZoneName(zone)})";

    /// <summary>
    /// Whose account: by its owner and profile name, or the tool's own sign-in. Read by <see cref="RotationWords"/> too (TOOL4f),
    /// whose record's opening line passes no names, so a record keeps the id (ACCT2b).
    /// </summary>
    internal static string Who(CoolingEntry entry, IReadOnlyDictionary<string, string>? names = null) =>
        entry.Account is { } account ? $"the `{entry.Agent}` account `{AccountNames.Said(names, account)}`" : $"`{entry.Agent}`'s own sign-in";

    /// <summary>Why it lasts until then: the agent said so, or Daoris's default stood in.</summary>
    internal static string Why(CoolingEntry entry) =>
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
/// The machine log's account lines (TOOL4d, D125 §5.4, D94 §4): <c>account.limited</c>, <c>starts.waiting</c> and, since TOOL4f,
/// <c>account.rotated</c>, each with
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

    /// <summary>
    /// A cool-off the file would not take, held in memory until it does or its reset passes (LIMITRESUME1b, D206): once per entry.
    /// </summary>
    public static AccountLine CoolingUnkept(string session, string adapter, string? account, LimitSeen seen) =>
        new("account.cooling-unkept",
        [
            ("session", session), ("adapter", adapter), ("account", Profile(account)), ("until", AccountCooling.Stamp(seen.Until)),
        ]);

    /// <summary>
    /// An attempt refused with the same kind of limit just after its account's cool-off ended (LIMITRESUME1b, D206): that reset did not
    /// lift it. Written once, beside its <c>account.limited</c>.
    /// </summary>
    public static AccountLine NotLifted(string session, string adapter, string? account, LimitSeen seen) =>
        new("account.not-lifted",
        [
            ("session", session), ("adapter", adapter), ("account", Profile(account)), ("hit", Bounded(seen.Hit)),
            ("window", Bounded(seen.Window)), ("until", AccountCooling.Stamp(seen.Until)),
        ]);

    /// <summary>
    /// Every account a start may use was cooling or not signed in: written once per wait. Where none cools, nothing comes
    /// ready by itself, and <paramref name="until"/> is null (TOOL6g).
    /// </summary>
    /// <param name="account">The account whose cool-off ends first, or null for the tool's own home and where none cools.</param>
    /// <param name="quests">How many quests the wait held when it was first written.</param>
    /// <param name="signedOut">
    /// The accounts it passed not signed in (TOOL6g), written as their profile names joined by commas, which no profile name
    /// holds; a name that is not one is left out, and none is null.
    /// </param>
    public static AccountLine Waiting(
        string adapter, string? account, string? workspace, DateTimeOffset? until, int quests, IReadOnlyList<string>? signedOut = null) =>
        new("starts.waiting",
        [
            ("adapter", adapter), ("account", Profile(account)), ("workspace", workspace),
            ("until", until is { } at ? AccountCooling.Stamp(at) : null), ("quests", quests),
            ("signedOut", (signedOut ?? []).Select(Profile).OfType<string>().ToList() is { Count: > 0 } named ? string.Join(",", named) : null),
        ]);

    /// <summary>
    /// A start that ran on another account of its scope's list than the one the scope begins at (TOOL4f, D125 §5.4; TOOL6b,
    /// D130 §13 as §16 amends it): which step moved it, from which scope's list, and whether any account had said what it has
    /// left.
    /// </summary>
    /// <param name="from">Where the scope begins: its default, else its list's first.</param>
    /// <param name="to">The account the start ran on.</param>
    /// <param name="carries">The cut-off session a carry-on carries on, or null for any other start.</param>
    /// <param name="why">The step that moved it, by its word (<c>cooling</c>, <c>kept</c>, <c>fewest</c>, <c>leastRecent</c>, …).</param>
    /// <param name="scope">The workspace whose list it was, or null for this machine's.</param>
    /// <param name="said">
    /// Whether any account of the list had said what it has left (§16.4): read from <c>windows.json</c>, where a door that
    /// carries the agent's word about its windows kept it (TOOL6c).
    /// </param>
    /// <param name="fromSaid">
    /// What <paramref name="from"/> last said, by Daoris's word only (TOOL6c): <c>refused</c>, <c>near</c>, <c>warned</c>
    /// (its agent's pace warning, and not near: USAGEREAD3a) or <c>clear</c>, or null where it said nothing. Never a number
    /// or the agent's words.
    /// </param>
    /// <param name="toSaid">What <paramref name="to"/> last said, as <paramref name="fromSaid"/> names it.</param>
    public static AccountLine Rotated(
        string session, string adapter, string from, string to, string? carries, WalkStep why, string? scope, bool said = false,
        string? fromSaid = null, string? toSaid = null) =>
        new("account.rotated",
        [
            ("session", session), ("adapter", adapter), ("from", Profile(from)), ("to", Profile(to)), ("carries", carries),
            ("why", Word(why)), ("scope", Profile(scope)), ("said", said), ("fromSaid", Standing(fromSaid)), ("toSaid", Standing(toSaid)),
        ]);

    /// <summary>A standing in a line: one of Daoris's four words, or null.</summary>
    private static string? Standing(string? word) =>
        word is AccountReadings.ClearWord or AccountReadings.WarnedWord or AccountReadings.NearWord or AccountReadings.RefusedWord ? word : null;

    /// <summary>A step by its word in a line: <c>signedOut</c>, <c>leastRecent</c>.</summary>
    private static string Word(WalkStep step) => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(step.ToString());

    private static string? Profile(string? account) => account is not null && Name.IsMatch(account) ? account : null;

    /// <summary>The marker's own word, kept short: a window's name, never a clause.</summary>
    internal static string? Bounded(string? word) => word is { Length: > 0 and <= 40 } ? word : null;
}

/// <summary>
/// Starts held because the account they would run on is cooling (TOOL4d, D125 §4): the agent, the account, the first
/// ready time, and what was held. The tick report carries it, so a screen shows a quest as waiting for an account rather
/// than parked, and the attention watch says it once.
/// </summary>
/// <remarks>
/// <b>Or held on accounts not signed in with none cooling</b> (UX6d1, TOOL6g): a wait for a person, not a time, with no
/// <see cref="Until"/> and no <see cref="Account"/>, its <see cref="SignedOut"/> the accounts a sign-in frees. One per agent,
/// as the log's <c>starts.waiting</c> line is, so an ask's intake held so reaches the page, which a consideration (quests
/// alone) never carried.
/// </remarks>
/// <param name="Adapter">The adapter the held starts ride.</param>
/// <param name="Agent">Whose accounts: the owner a door runs as (AGT7).</param>
/// <param name="Account">The profile's name, or null for the tool's own home; null too for a wait on signed-out accounts.</param>
/// <param name="Workspace">The workspace the held starts belong to, where they share one; else null.</param>
/// <param name="Until">When the account is offered again: the first ready time. Null where none cools and a sign-in is the wait.</param>
/// <param name="Stated">Whether the agent named <paramref name="Until"/>; false where there is none.</param>
/// <param name="Sentence">The hold's sentence, which each held quest's reason carries.</param>
public sealed record AccountWait(
    string Adapter, string Agent, string? Account, string? Workspace, DateTimeOffset? Until, bool Stated, string Sentence)
{
    /// <summary>The quests held, in plan order.</summary>
    public IReadOnlyList<string> Quests { get; init; } = [];

    /// <summary>The asks whose intakes were held.</summary>
    public IReadOnlyList<string> Asks { get; init; } = [];

    /// <summary>Where the held starts would have run: each quest's repository, or <c>ask #id</c>.</summary>
    public IReadOnlyList<string> Repositories { get; init; } = [];

    /// <summary>
    /// The accounts the held starts passed not signed in (TOOL6g), in the order the walk tried them: a sign-in starts them
    /// sooner than the reset, or at all where none cools (UX6d1). Empty where none was.
    /// </summary>
    public IReadOnlyList<string> SignedOut { get; init; } = [];

    /// <summary>
    /// True where the starts were held by OWNSPACE1a's no-account hold (D175): no Daoris account of the agent's is signed in.
    /// A fact the page says the hold from and offers <i>Sign in for Daoris</i> by, never read off an empty
    /// <see cref="SignedOut"/> (OWNSPACE1b-B).
    /// </summary>
    public bool NoAccount { get; init; }

    /// <summary>
    /// The name the person gave <see cref="Account"/> (ACCT2b), read when the look said the wait; null where it has none, and
    /// for the tool's own home. The page says the wait by it, and the log keeps <see cref="Account"/>.
    /// </summary>
    public string? Name { get; init; }
}
