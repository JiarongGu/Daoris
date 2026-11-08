using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Daoris.Driver.AccountFileNodes;

namespace Daoris.Driver;

/// <summary>
/// The usage history: every usage reading, appended under the home's <c>usage-history/</c> before
/// <see cref="AccountWindows.Said"/> keeps the newest of each window, so the readings an estimate of each window's capacity
/// needs gather while the estimator is built. Beside it, <c>epochs.json</c>: each account's binding epoch.
/// </summary>
/// <remarks>
/// <para><b>One file per month</b> (<c>readings-YYYY-MM.jsonl</c>, the month in UTC), one JSON object per line, BOM-less UTF-8
/// and LF, each record versioned, with its own id and the time it is written. Appended, not written whole, as the machine log
/// and a session's events are; one writer at a time in this process. Machine-local: never on a shared host, never synced, and
/// no route reads it.</para>
/// <para><b>Bounded by age and size together</b>: 90 days and 64 MiB to begin with. Whole months are pruned oldest first, a
/// month once it ended <see cref="KeptDays"/> ago and the oldest while the files and the write exceed <see cref="CapBytes"/>,
/// and a <c>gap</c> record at the head of the next write says what went, from when to when, where a reader meets it. The
/// month being written is never pruned: where it alone reaches the cap, a <c>full</c> record is written once and what follows
/// that month is dropped, which a reader says as a gap to the month's end.</para>
/// <para><b>Every turn's tokens</b> are kept beside the readings (<c>turns-YYYY-MM.jsonl</c>), each keyed by its
/// session and its event's sequence so a turn heard twice is written once, and carrying its account's epoch: a turn is a
/// session that ran, so it begins an account's first epoch or its next after a sign-out as a reading does, and changes no plan.
/// A model's name is kept only where it is a plain one (<see cref="TurnModels.Name"/>).</para>
/// <para><b>The turns' log shares the bound</b>: the cap counts both logs' files, and a month pruned for size goes from both at
/// once, each log's gap said in that log. A month of readings without its turns, or of turns without the readings around them,
/// is nothing an estimate can be fitted on.</para>
/// <para><b>Never a reason to fail</b>: a write that cannot be made is dropped, as the machine log's is, and its caller goes on
/// to <c>windows.json</c> as before.</para>
/// <para><b>Never written</b>: a prompt, a word, a raw frame, a key, an email, an organisation or who signed in.
/// A record holds Daoris's names, numbers and times; a maker's name (a window's key, a scope, a plan) is kept only where it is
/// a plain name (<see cref="Word"/>), and anything else is left out.</para>
/// <para><b>An exact duplicate is written once</b>: the same account's window, from the same source, observed at the same
/// moment, remembered per home for the life of the process.</para>
/// <para><b>The binding epoch</b>: an account's first reading begins its first; another plan as read, or a sign-out and then
/// a sign-in or a reading, or the account's removal and a reading under its name again, begins the next. A plan first learned
/// is the epoch's own. Only an account the history has met is marked.</para>
/// </remarks>
public static class UsageHistory
{
    /// <summary>The records' version.</summary>
    public const int Version = 1;

    /// <summary>How many days of readings are kept: a month is pruned once it ended this long ago.</summary>
    public const int KeptDays = 90;

    /// <summary>How large the readings' files may grow together before the oldest month is pruned.</summary>
    public const long CapBytes = 64L * 1024 * 1024;

    /// <summary>A reading's source where a session's frame carried it, on either door.</summary>
    public const string SourceFrame = "frame";

    /// <summary>Where a reading's reset came from: its maker said it, or the walk made it up for a window not yet opened.</summary>
    public const string ResetSaid = "said", ResetMadeUp = "made-up";

    /// <summary>How a plan is read where the account's status question reads it (<see cref="LoginRead"/>).</summary>
    public const string PlanFromStatus = "status";

    /// <summary>A door's turn counts are each turn's own (the measure on both Claude doors).</summary>
    public const string PerTurn = "per-turn";

    /// <summary>A door's turn counts whose meaning is never measured: Codex's until a frame is recorded.</summary>
    public const string MeaningUnknown = "unknown";

    /// <summary>The version of the reading of a turn's tokens and models.</summary>
    public const int TurnReader = 1;

    internal const string ReadingsLog = "readings";
    internal const string TurnsLog = "turns";
    private const string EpochsFile = "epochs.json";

    /// <summary>The logs the bound counts together: a month pruned for size goes from each.</summary>
    private static readonly string[] Logs = [ReadingsLog, TurnsLog];

    /// <summary>How many readings' keys each home's history remembers, to write an exact duplicate once.</summary>
    private const int RememberedMost = 4096;

    private static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly Regex Plain = new("^[A-Za-z0-9][A-Za-z0-9_.:-]{0,63}$", RegexOptions.CultureInvariant);

    // One writer at a time in this process: the conclusions of sessions side by side, and a press's reads.
    private static readonly object Gate = new();

    // The readings each home's history kept lately, by folder: an exact duplicate is written once.
    private static readonly Dictionary<string, (HashSet<string> Keys, Queue<string> Order)> Recent = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where the history is: its own folder under the home, beside <c>windows.json</c>.</summary>
    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.UsageHistory);

    /// <summary>The readings' file a moment's reading is kept in: its month's, in UTC.</summary>
    public static string SegmentOf(DateTimeOffset at) => SegmentOf(ReadingsLog, at);

    /// <summary>The turns' file a moment's turn is kept in: its month's, in UTC.</summary>
    public static string TurnsSegmentOf(DateTimeOffset at) => SegmentOf(TurnsLog, at);

    /// <summary>
    /// Keep what a reader saw: one record per window, with its account's epoch, appended to its month's file after
    /// any gap a prune made. Never throws: a history not kept costs the history, never its caller.
    /// </summary>
    /// <param name="now">When it is written, the caller's clock.</param>
    /// <param name="cap">Where the readings' files stop growing together; <see cref="CapBytes"/> but in a test.</param>
    public static void Kept(string home, ReadingFrom from, IReadOnlyList<WindowSeen> windows, DateTimeOffset now, long cap = CapBytes)
    {
        try
        {
            var plain = windows.Where(window => Word(window.Window) is not null).Select(window => window with { Scope = Word(window.Scope) }).ToList();
            if (plain.Count == 0) return;
            lock (Gate)
            {
                var folder = FolderOf(home);
                var fresh = plain.Where(window => !Remembered(folder, KeyOf(from, window))).ToList();
                if (fresh.Count == 0) return;

                Directory.CreateDirectory(folder);
                var plan = Word(from.Plan);
                var epoch = EpochOf(home, from.Agent, from.Account, plan, from.Seen);
                Append(home, ReadingsLog, [.. fresh.Select(window => Reading(from, window, epoch, plan, now))], now, cap);
                // Only what was appended is remembered: a write that failed is tried again with the next reading of it.
                foreach (var window in fresh) Remember(folder, KeyOf(from, window));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Dropped: the history is evidence, never a reason to fail.
        }
    }

    /// <summary>
    /// What a status question said of an account the history has met (<see cref="LoginRead"/>): signed out marks
    /// it, so its next sign-in or reading begins the next epoch; signed in with another plan begins it too. Never throws.
    /// </summary>
    public static void Login(string home, string agent, string account, LoginState login, string? plan, DateTimeOffset at)
    {
        if (login == LoginState.Unknown) return;
        try
        {
            lock (Gate)
            {
                var path = DaorisHome.Entry(home, HomeEntry.UsageHistory, EpochsFile);
                if (!File.Exists(path)) return;
                // The file's keep lock is held from the read to the write, as cooling.json's is: another process's edit is not written over.
                using var locked = KeepLock.Take(path);
                var (root, unreadable) = AccountFileNodes.Read(path);
                if (unreadable || Child(Child(root, agent), account) is not { } entry) return;

                var changed = login == LoginState.Out ? MarkedOut(entry) : Rebound(entry, Word(plan), at);
                if (changed) JsonFile.Write(path, root);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // An epoch not marked splits nothing; the plan's next reading still bumps it where it changed.
        }
    }

    /// <summary>An account removed from the machine: one made later under its name is another binding.</summary>
    public static void Unbound(string home, string agent, string account, DateTimeOffset at) =>
        Login(home, agent, account, LoginState.Out, null, at);

    /// <summary>An account's binding epoch now, or null where the history has not met it or its file does not read.</summary>
    public static int? EpochOf(string home, string agent, string account)
    {
        try
        {
            var (root, unreadable) = AccountFileNodes.Read(DaorisHome.Entry(home, HomeEntry.UsageHistory, EpochsFile));
            return unreadable ? null : Child(Child(root, agent), account) is { } entry ? EpochIn(entry) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc cref="UsageHistoryReads.Read"/>
    public static HistoryRead Read(string home) => UsageHistoryReads.Read(home);

    /// <summary>
    /// Keep a turn: one record, with its account's epoch where a start bound it, appended to its month's file after
    /// any gap a prune made. Never throws: a turn not kept costs the history, never the session.
    /// </summary>
    /// <param name="binding">Whose the session is, as its start chose; null where no start bound it.</param>
    /// <param name="now">When it is written, the caller's clock.</param>
    /// <param name="cap">Where the logs' files stop growing together; <see cref="CapBytes"/> but in a test.</param>
    public static void Turned(string home, TurnBinding? binding, TurnSeen turn, DateTimeOffset now, long cap = CapBytes)
    {
        if (!SessionEventText.IsId(turn.Session)) return;
        try
        {
            lock (Gate)
            {
                var folder = FolderOf(home);
                var key = string.Join('\n', TurnsLog, turn.Session, turn.Seq.ToString(CultureInfo.InvariantCulture));
                if (Remembered(folder, key)) return;

                Directory.CreateDirectory(folder);
                var bound = binding is { Agent: not null, Account: not null } ? binding : null;
                var epoch = bound is null ? null : EpochOf(home, bound.Agent!, bound.Account!, null, turn.Ended);
                Append(home, TurnsLog, [TurnLine(binding, bound, turn, epoch, now)], now, cap);
                Remember(folder, key);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Dropped: the history is evidence, never a reason to fail.
        }
    }

    /// <inheritdoc cref="UsageHistoryReads.ReadTurns"/>
    public static TurnsRead ReadTurns(string home) => UsageHistoryReads.ReadTurns(home);

    /// <inheritdoc cref="UsageHistoryReads.TurnLinesOf"/>
    public static IReadOnlyDictionary<string, IReadOnlyList<KeptTurn>> TurnLinesOf(string home, IReadOnlyCollection<string> sessions)
        => UsageHistoryReads.TurnLinesOf(home, sessions);

    /// <inheritdoc cref="UsageHistoryReads.ReadingMonths"/>
    internal static IReadOnlyList<HistoryMonth> ReadingMonths(string home) => UsageHistoryReads.ReadingMonths(home);

    /// <inheritdoc cref="UsageHistoryReads.TurnMonths"/>
    internal static IReadOnlyList<HistoryMonth> TurnMonths(string home) => UsageHistoryReads.TurnMonths(home);

    /// <inheritdoc cref="UsageHistoryReads.ReadingsOn"/>
    internal static long ReadingsOn(string home, HistoryMonth month, long offset, List<KeptReading> into)
        => UsageHistoryReads.ReadingsOn(home, month, offset, into);

    /// <inheritdoc cref="UsageHistoryReads.TurnsOn"/>
    internal static long TurnsOn(string home, HistoryMonth month, long offset, List<KeptTurn> into)
        => UsageHistoryReads.TurnsOn(home, month, offset, into);

    /// <summary>A maker's name as the history keeps it: a plain name of letters, digits and <c>_ . : -</c>, else null.</summary>
    public static string? Word(string? said) => said is not null && Plain.IsMatch(said) ? said : null;

    /// <summary>
    /// The least step <paramref name="value"/> shows as written, on a scale where <paramref name="scale"/> is whole (100 for a
    /// percent): 0.01 for <c>23</c> of 100 or <c>0.88</c> of 1. Null for no number, or one written with an exponent.
    /// </summary>
    public static double? Precision(JsonElement value, double scale)
    {
        if (value.ValueKind != JsonValueKind.Number || scale <= 0) return null;
        var raw = value.GetRawText();
        if (raw.Contains('e', StringComparison.OrdinalIgnoreCase)) return null;
        var dot = raw.IndexOf('.', StringComparison.Ordinal);
        var decimals = dot < 0 ? 0 : raw.Length - dot - 1;
        return 1 / (Math.Pow(10, decimals) * scale);
    }

    // ——— The epoch.

    /// <summary>
    /// The account's epoch for a reading or a turn (a session that ran), begun or moved on as <see cref="Rebound"/> says; null
    /// where the file does not read.
    /// </summary>
    private static int? EpochOf(string home, string agent, string account, string? plan, DateTimeOffset at)
    {
        var path = DaorisHome.Entry(home, HomeEntry.UsageHistory, EpochsFile);
        using var locked = KeepLock.Take(path);
        var (root, unreadable) = AccountFileNodes.Read(path);
        if (unreadable) return null;

        var held = Made(root, agent);
        bool changed;
        if (Child(held, account) is not { } entry)
        {
            entry = new JsonObject { ["epoch"] = 1, ["plan"] = plan, ["since"] = Stamp(at) };
            held[account] = entry;
            changed = true;
        }
        else
        {
            changed = Rebound(entry, plan, at);
        }

        if (changed)
        {
            try
            {
                JsonFile.Write(path, root);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // The readings are still kept: the next one finds the same change, and writes it then.
            }
        }

        return EpochIn(entry);
    }

    /// <summary>
    /// A word that the account is signed in, a reading among them (a reading is a session or a read that ran): after a sign-out,
    /// or with another plan than its epoch's, the next epoch begins under the plan read; a plan first learned is the epoch's own.
    /// Whether anything changed.
    /// </summary>
    private static bool Rebound(JsonObject entry, string? plan, DateTimeOffset at)
    {
        var kept = Json.NonEmpty(entry, "plan");
        if (Json.Flag(entry, "out") || (plan is not null && kept is not null && !string.Equals(plan, kept, StringComparison.OrdinalIgnoreCase)))
        {
            entry["epoch"] = (EpochIn(entry) ?? 0) + 1;
            entry["plan"] = plan;
            entry["since"] = Stamp(at);
            entry.Remove("out");
            return true;
        }

        if (plan is null || kept is not null) return false;
        entry["plan"] = plan;
        return true;
    }

    /// <summary>A sign-out marked once, for the next sign-in or reading to begin the next epoch. Whether it is newly marked.</summary>
    private static bool MarkedOut(JsonObject entry)
    {
        if (Json.Flag(entry, "out")) return false;
        entry["out"] = true;
        return true;
    }

    private static int? EpochIn(JsonObject entry) =>
        entry["epoch"] is JsonValue value && value.TryGetValue<int>(out var epoch) && epoch >= 1 ? epoch : null;

    // ——— The log: appended a month to a file, bounded, its gaps said.

    internal sealed record Segment(string Log, string Name, DateTimeOffset Month, long Length);

    private static string SegmentOf(string log, DateTimeOffset at) =>
        $"{log}-{at.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture)}.jsonl";

    /// <summary>The month a moment falls in, in UTC: the month every log is writing then.</summary>
    private static DateTimeOffset MonthOf(DateTimeOffset at) => new(at.UtcDateTime.Year, at.UtcDateTime.Month, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The log's files, oldest month first: each named <c>&lt;log&gt;-YYYY-MM.jsonl</c>; any other name is not the log's.</summary>
    internal static List<Segment> Segments(string home, string log)
    {
        var segments = new List<Segment>();
        foreach (var path in Directory.EnumerateFiles(FolderOf(home), $"{log}-*.jsonl"))
        {
            var name = Path.GetFileName(path);
            var month = name.Length == log.Length + 14 ? name.Substring(log.Length + 1, 7) : "";
            if (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var start))
            {
                continue;
            }

            segments.Add(new(log, name, new DateTimeOffset(start, TimeSpan.Zero), new FileInfo(path).Length));
        }

        return [.. segments.OrderBy(segment => segment.Month)];
    }

    /// <summary>
    /// Append <paramref name="lines"/> to <paramref name="log"/>'s month's file, after pruning across every log the bound counts
    /// (<see cref="Logs"/>): months that ended <see cref="KeptDays"/> ago, then the oldest month while the files and the write
    /// exceed <paramref name="cap"/>, from each log at once. Each prune is said by a gap record in its own log: at the head of
    /// this write in this one, at the end of its month's file in another. Where the month alone is at the cap, a <c>full</c>
    /// record once in this log, and the lines are dropped.
    /// </summary>
    private static void Append(string home, string log, IReadOnlyList<byte[]> lines, DateTimeOffset now, long cap)
    {
        var current = SegmentOf(log, now);
        var month = MonthOf(now);
        var left = Logs.SelectMany(each => Segments(home, each)).ToList();
        var pruned = new List<(Segment Segment, string Why)>();

        var oldest = now.ToUniversalTime().AddDays(-KeptDays);
        foreach (var segment in left.Where(segment => segment.Month != month && segment.Month.AddMonths(1) <= oldest).ToList())
        {
            left.Remove(segment);
            if (Deleted(home, segment)) pruned.Add((segment, "age"));
        }

        // Another log's gaps are written too, so they count against the cap as this write's own do.
        long Elsewhere() => Logs.Where(other => other != log).Sum(other => (long)Body(Gaps(pruned, other, now), []).Length);
        var body = Body(Gaps(pruned, log, now), lines);
        while (left.Sum(segment => segment.Length) + body.Length + Elsewhere() > cap)
        {
            if (left.Where(segment => segment.Month != month).MinBy(segment => segment.Month) is not { } older)
            {
                SaidElsewhere(home, log, pruned, now);
                Full(home, current, Gaps(pruned, log, now), now, cap);
                return;
            }

            foreach (var segment in left.Where(segment => segment.Month == older.Month).ToList())
            {
                left.Remove(segment);
                if (Deleted(home, segment)) pruned.Add((segment, "size"));
            }

            body = Body(Gaps(pruned, log, now), lines);
        }

        SaidElsewhere(home, log, pruned, now);
        Write(home, current, body);
    }

    /// <summary>The gaps a write pruned from the other logs, each at the end of its own log's month's file.</summary>
    private static void SaidElsewhere(string home, string log, IReadOnlyList<(Segment Segment, string Why)> pruned, DateTimeOffset now)
    {
        foreach (var other in Logs.Where(other => other != log)) Write(home, SegmentOf(other, now), Body(Gaps(pruned, other, now), []));
    }

    /// <summary>A month removed, or false where it could not be: the next write tries again.</summary>
    private static bool Deleted(string home, Segment segment)
    {
        try
        {
            File.Delete(DaorisHome.Entry(home, HomeEntry.UsageHistory, segment.Name));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// The month being written is at the cap alone: a <c>full</c> record, once, after any gap the prune made, and nothing more
    /// until the next month's file begins.
    /// </summary>
    private static void Full(string home, string current, IReadOnlyList<byte[]> gaps, DateTimeOffset now, long cap)
    {
        if (MarkedFull(home, current))
        {
            if (gaps.Count > 0) Write(home, current, Body(gaps, []));
            return;
        }

        Write(home, current, Body(gaps, [Line(json =>
        {
            Head(json, "full", now);
            json.WriteString("segment", current);
            json.WriteNumber("capBytes", cap);
        })]));
    }

    /// <summary>Whether the month's file already ends in a <c>full</c> record, the gaps a prune said after it set aside.</summary>
    private static bool MarkedFull(string home, string current)
    {
        var path = DaorisHome.Entry(home, HomeEntry.UsageHistory, current);
        if (!File.Exists(path)) return false;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        for (var length = Math.Min(stream.Length, 512L); ; length = Math.Min(stream.Length, length * 4))
        {
            stream.Seek(-length, SeekOrigin.End);
            var tail = new byte[length];
            stream.ReadExactly(tail);
            var lines = Utf8.GetString(tail).TrimEnd('\n').Split('\n');
            // The first line of a tail that does not reach the file's start may be cut, so it is read only once the tail does.
            var whole = length == stream.Length;
            for (var at = lines.Length - 1; at >= (whole ? 0 : 1); at--)
            {
                if (lines[at].Contains("\"kind\":\"gap\"", StringComparison.Ordinal)) continue;
                return lines[at].Contains("\"kind\":\"full\"", StringComparison.Ordinal);
            }

            if (whole) return false;
        }
    }

    /// <summary>One of the log's gap records per reason, from the oldest month pruned to the end of the newest, never past now.</summary>
    private static IReadOnlyList<byte[]> Gaps(IReadOnlyList<(Segment Segment, string Why)> pruned, string log, DateTimeOffset now) =>
        [.. pruned.Where(each => each.Segment.Log == log).GroupBy(each => each.Why).Select(group =>
        {
            var months = group.Select(each => each.Segment).OrderBy(segment => segment.Month).ToList();
            var to = months[^1].Month.AddMonths(1);
            return Line(json =>
            {
                Head(json, "gap", now);
                json.WriteString("from", Stamp(months[0].Month));
                json.WriteString("to", Stamp(to < now ? to : now));
                json.WriteString("why", group.Key);
                json.WriteStartArray("segments");
                foreach (var segment in months) json.WriteStringValue(segment.Name);
                json.WriteEndArray();
            });
        })];

    private static byte[] Body(IReadOnlyList<byte[]> first, IReadOnlyList<byte[]> then) => [.. first.Concat(then).SelectMany(line => line)];

    /// <summary>Append the bytes whole, the file shared both ways: a reader may read it, and a prune elsewhere remove it.</summary>
    private static void Write(string home, string segment, byte[] body)
    {
        if (body.Length == 0) return;
        using var stream = new FileStream(
            DaorisHome.Entry(home, HomeEntry.UsageHistory, segment), FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        stream.Write(body);
        stream.Flush();
    }

    // ——— The records.

    private static byte[] Reading(ReadingFrom from, WindowSeen window, int? epoch, string? plan, DateTimeOffset now) => Line(json =>
    {
        Head(json, "reading", now);
        json.WriteString("agent", from.Agent);
        json.WriteString("account", from.Account);
        Number(json, "epoch", epoch);
        json.WriteString("door", from.Door);
        json.WriteString("window", window.Window);
        Text(json, "walk", window.Walk);
        Text(json, "scope", window.Scope);
        Number(json, "minutes", window.Minutes);
        Number(json, "used", window.Used);
        Number(json, "precision", window.Precision);
        Text(json, "reset", window.Reset is { } reset ? Stamp(reset) : null);
        Text(json, "resetFrom", window.Reset is null ? null : window.ResetFrom);
        json.WriteString("source", from.Source);
        json.WriteString("seen", Stamp(from.Seen));
        Text(json, "session", from.Session is { Length: > 0 } session ? session : null);
        Text(json, "plan", plan);
        Text(json, "planFrom", plan is null ? null : Word(from.PlanFrom));
        json.WriteNumber("reader", from.Reader);
    });

    private static byte[] TurnLine(TurnBinding? binding, TurnBinding? bound, TurnSeen turn, int? epoch, DateTimeOffset now) => Line(json =>
    {
        var models = turn.Models;
        // A split is written whole or not at all: one share left out would leave the rest summing to nothing said.
        var split = models?.Split is { } shares && shares.All(each => TurnModels.Name(each.Model) is not null) ? shares : null;

        Head(json, "turn", now);
        Text(json, "agent", bound?.Agent);
        Text(json, "account", bound?.Account);
        Number(json, "epoch", epoch);
        Text(json, "door", Word(binding?.Door));
        Text(json, "actor", Word(binding?.Actor));
        if (Word(binding?.Brainstorm) is { } brainstorm) json.WriteString("brainstorm", brainstorm);
        // The role's choice the turn is spent on, as its policy keys it (agent, model, effort), where its start chose one: what a
        // daily budget of tokens is weighed against. Written only then, so a line without one reads as having none.
        if (binding?.Choice is { Length: > 0 and <= 400 } choice) json.WriteString("choice", choice);
        json.WriteString("session", turn.Session);
        json.WriteNumber("seq", turn.Seq);
        Text(json, "started", turn.Started is { } started ? Stamp(started) : null);
        json.WriteString("ended", Stamp(turn.Ended));
        Counts(json, turn.Tokens);
        json.WriteString("model", models?.Model is TurnModels.Mixed or TurnModels.Unknown ? models.Model : TurnModels.Name(models?.Model) ?? TurnModels.Unknown);
        if (split is null)
        {
            json.WriteNull("split");
        }
        else
        {
            json.WriteStartArray("split");
            foreach (var share in split)
            {
                json.WriteStartObject();
                json.WriteString("model", share.Model);
                Counts(json, share.Tokens);
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

        Text(json, "attribution", Word(models?.From));
        if (split is not null && models?.Whole is { } whole) json.WriteBoolean("whole", whole);
        else json.WriteNull("whole");
        json.WriteBoolean("cancelled", turn.Cancelled);
        json.WriteString("meaning", Word(binding?.Meaning) ?? MeaningUnknown);
        json.WriteNumber("reader", TurnReader);
    });

    private static void Counts(Utf8JsonWriter json, TurnTokens? tokens)
    {
        Number(json, "input", tokens?.Input);
        Number(json, "output", tokens?.Output);
        Number(json, "cacheRead", tokens?.CacheRead);
        Number(json, "cacheWrite", tokens?.CacheWrite);
    }

    private static void Head(Utf8JsonWriter json, string kind, DateTimeOffset now)
    {
        json.WriteNumber("v", Version);
        json.WriteString("kind", kind);
        json.WriteString("id", Guid.NewGuid().ToString("N"));
        json.WriteString("written", Stamp(now));
    }

    private static byte[] Line(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, Options))
        {
            json.WriteStartObject();
            write(json);
            json.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');
        return buffer.ToArray();
    }

    private static void Text(Utf8JsonWriter json, string name, string? value)
    {
        if (value is null) json.WriteNull(name);
        else json.WriteString(name, value);
    }

    private static void Number(Utf8JsonWriter json, string name, double? value)
    {
        if (value is { } number && double.IsFinite(number)) json.WriteNumber(name, number);
        else json.WriteNull(name);
    }

    private static void Number(Utf8JsonWriter json, string name, int? value)
    {
        if (value is { } number) json.WriteNumber(name, number);
        else json.WriteNull(name);
    }

    private static void Number(Utf8JsonWriter json, string name, long? value)
    {
        if (value is { } number) json.WriteNumber(name, number);
        else json.WriteNull(name);
    }

    /// <summary>A moment as the history writes it: UTC, to the millisecond.</summary>
    private static string Stamp(DateTimeOffset moment) =>
        moment.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// A log read back, oldest month first and each in the order written: its records of <paramref name="kind"/>, the gaps a
    /// reader meets, and how many lines did not read, which are counted and never guessed at. A record of a kind this reader
    /// does not know is passed over. Never throws: what could not be read is not there.
    /// </summary>
    /// <param name="parse">A record of the kind read whole, or null where it does not read.</param>
    internal static (List<T> Records, List<HistoryGap> Gaps, int Unread) ReadLog<T>(
        string home, string log, string kind, Func<JsonObject, T?> parse)
        where T : class
    {
        var records = new List<T>();
        var gaps = new List<HistoryGap>();
        var unread = 0;
        try
        {
            if (!Directory.Exists(FolderOf(home))) return (records, gaps, 0);
            foreach (var segment in Segments(home, log))
            {
                using var stream = new FileStream(
                    DaorisHome.Entry(home, HomeEntry.UsageHistory, segment.Name), FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Utf8);
                while (reader.ReadLine() is { } line)
                {
                    if (line.Length == 0) continue;
                    switch (Record(line, segment, kind, parse))
                    {
                        case HistoryGap gap: gaps.Add(gap); break;
                        case T record: records.Add(record); break;
                        case false: unread++; break;
                    }
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // What is read stands; the rest is not there.
        }

        return (records, gaps, unread);
    }

    internal static object? Record<T>(string line, Segment segment, string kind, Func<JsonObject, T?> parse)
        where T : class => UsageHistoryReads.Record(line, segment, kind, parse);

    private static string KeyOf(ReadingFrom from, WindowSeen window) =>
        string.Join('\n', from.Agent, from.Account, window.Window, window.Scope ?? "", from.Source,
            from.Seen.UtcTicks.ToString(CultureInfo.InvariantCulture));

    /// <summary>Whether this reading was kept already in this process.</summary>
    private static bool Remembered(string folder, string key) => Recent.TryGetValue(folder, out var recent) && recent.Keys.Contains(key);

    /// <summary>Remember a reading as kept.</summary>
    private static void Remember(string folder, string key)
    {
        if (!Recent.TryGetValue(folder, out var recent))
        {
            recent = (new HashSet<string>(StringComparer.OrdinalIgnoreCase), new Queue<string>());
            Recent[folder] = recent;
        }

        if (!recent.Keys.Add(key)) return;
        recent.Order.Enqueue(key);
        if (recent.Order.Count > RememberedMost) recent.Keys.Remove(recent.Order.Dequeue());
    }

}
