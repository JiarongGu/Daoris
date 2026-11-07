using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.AccountFileNodes;

namespace Daoris.Driver;

/// <summary>
/// Reading the kept usage history back: a month's readings and turns, each line parsed, the gaps and the unread counted.
/// </summary>
internal static class UsageHistoryReads
{
    /// <summary>
    /// What the history holds, oldest month first and each in the order written: its readings, the gaps a reader
    /// meets, and how many lines did not read, which are counted and never guessed at. A record of a kind this reader does not
    /// know is passed over. Never throws: what could not be read is not there.
    /// </summary>
    public static HistoryRead Read(string home)
    {
        var (readings, gaps, unread) = UsageHistory.ReadLog(home, UsageHistory.ReadingsLog, "reading", ReadingOf);
        return new(readings, gaps, unread);
    }

    /// <summary>
    /// What the turns' log holds, oldest month first and each in the order written: its turns, the gaps a reader meets (the
    /// readings' rules), and how many lines did not read. Never throws.
    /// </summary>
    public static TurnsRead ReadTurns(string home)
    {
        var (turns, gaps, unread) = UsageHistory.ReadLog(home, UsageHistory.TurnsLog, "turn", TurnOf);
        return new(turns, gaps, unread);
    }

    /// <summary>
    /// The turn lines of <paramref name="sessions"/>, by session, in the order written: what a commit point hands the service,
    /// so a memory row names the model of the turn its instant falls in. A session with no line is left out. Never throws.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<KeptTurn>> TurnLinesOf(string home, IReadOnlyCollection<string> sessions)
    {
        var wanted = sessions.ToHashSet(StringComparer.Ordinal);
        return ReadTurns(home).Turns
            .Where(turn => wanted.Contains(turn.Session))
            .GroupBy(turn => turn.Session, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<KeptTurn>)[.. group], StringComparer.Ordinal);
    }

    /// <summary>The readings' month files now, oldest first; none where the history has none or its folder does not read.</summary>
    internal static IReadOnlyList<HistoryMonth> ReadingMonths(string home) => MonthsOf(home, UsageHistory.ReadingsLog);

    /// <summary>The turns' month files now, oldest first; none where the history has none or its folder does not read.</summary>
    internal static IReadOnlyList<HistoryMonth> TurnMonths(string home) => MonthsOf(home, UsageHistory.TurnsLog);

    /// <summary>
    /// The readings a month's file holds from <paramref name="offset"/> on, added to <paramref name="into"/>: the offset read to
    /// (the end of its last whole line), or -1 where the file is shorter than the offset, begun again since. Never throws.
    /// </summary>
    internal static long ReadingsOn(string home, HistoryMonth month, long offset, List<KeptReading> into) =>
        ReadOn(home, UsageHistory.ReadingsLog, month, offset, "reading", ReadingOf, into);

    /// <summary>The turns a month's file holds from <paramref name="offset"/> on, as <see cref="ReadingsOn"/> reads its readings.</summary>
    internal static long TurnsOn(string home, HistoryMonth month, long offset, List<KeptTurn> into) =>
        ReadOn(home, UsageHistory.TurnsLog, month, offset, "turn", TurnOf, into);

    /// <summary>A log's month files as <see cref="HistoryMonth"/>s, oldest first; none where its folder is not there or does not read.</summary>
    private static IReadOnlyList<HistoryMonth> MonthsOf(string home, string log)
    {
        try
        {
            return Directory.Exists(UsageHistory.FolderOf(home))
                ? [.. UsageHistory.Segments(home, log).Select(segment => new HistoryMonth(segment.Name, segment.Month, segment.Length))]
                : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Every whole line of a month's file from <paramref name="offset"/> on, each record of <paramref name="kind"/> added to
    /// <paramref name="into"/>: only the bytes appended since the last read are read, a line still being written
    /// is left for the next. The offset read to, -1 where the file is shorter than the offset, and the offset itself where the
    /// file does not read.
    /// </summary>
    private static long ReadOn<T>(
        string home, string log, HistoryMonth month, long offset, string kind, Func<JsonObject, T?> parse, List<T> into)
        where T : class
    {
        try
        {
            using var stream = new FileStream(
                DaorisHome.Entry(home, HomeEntry.UsageHistory, month.Name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = stream.Length;
            if (length < offset) return -1;
            if (length == offset) return offset;

            stream.Seek(offset, SeekOrigin.Begin);
            var bytes = new byte[length - offset];
            stream.ReadExactly(bytes);
            var end = Array.LastIndexOf(bytes, (byte)'\n');
            if (end < 0) return offset;

            var segment = new UsageHistory.Segment(log, month.Name, month.Month, length);
            foreach (var line in UsageHistory.Utf8.GetString(bytes, 0, end + 1).Split('\n'))
            {
                if (line.Length > 0 && Record(line, segment, kind, parse) is T record) into.Add(record);
            }

            return offset + end + 1;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Read again from here next time: what could not be read is not there yet.
            return offset;
        }
    }

    /// <summary>A reading read back whole, or null where it does not read.</summary>
    private static KeptReading? ReadingOf(JsonObject record)
    {
        if (Json.Text(record, "agent") is not { } agent || Json.Text(record, "account") is not { } account
            || Json.NonEmpty(record, "door") is not { } door || Json.NonEmpty(record, "window") is not { } window
            || Json.NonEmpty(record, "source") is not { } source || Moments.ReadStrict(Json.Text(record, "seen")) is not { } seen
            || Whole(record, "reader") is not { } reader)
        {
            return null;
        }

        var reset = Moments.ReadStrict(Json.Text(record, "reset"));
        return new KeptReading(
            agent, account, Whole(record, "epoch"), door,
            new WindowSeen(
                window, Json.NonEmpty(record, "walk"), Json.NonEmpty(record, "scope"), Whole(record, "minutes"),
                Fraction(record, "used"), Fraction(record, "precision"), reset, reset is null ? null : Json.NonEmpty(record, "resetFrom")),
            source, seen, Json.NonEmpty(record, "session"), Json.NonEmpty(record, "plan"), Json.NonEmpty(record, "planFrom"), reader);
    }

    /// <summary>
    /// A line read back: a record of the log's kind, a gap (a <c>full</c> record read as the rest of its month), null for a
    /// kind this reader does not know, or false where it does not read.
    /// </summary>
    internal static object? Record<T>(string line, UsageHistory.Segment segment, string kind, Func<JsonObject, T?> parse)
        where T : class
    {
        JsonObject record;
        try
        {
            if (JsonNode.Parse(line) is not JsonObject parsed) return false;
            record = parsed;
        }
        catch (JsonException)
        {
            return false;
        }

        var said = Json.Text(record, "kind");
        if (said == kind) return (object?)parse(record) ?? false;
        switch (said)
        {
            case "gap":
                return Moments.ReadStrict(Json.Text(record, "from")) is { } from && Moments.ReadStrict(Json.Text(record, "to")) is { } to
                    ? new HistoryGap(from, to, Json.NonEmpty(record, "why") ?? "pruned", Json.TrimmedStrings(record["segments"]))
                    : false;
            case "full":
                return Moments.ReadStrict(Json.Text(record, "written")) is { } written
                    ? new HistoryGap(written, segment.Month.AddMonths(1), "full", [segment.Name])
                    : false;
            default:
                return null;
        }
    }

    /// <summary>A turn read back whole, or null where it does not read.</summary>
    private static KeptTurn? TurnOf(JsonObject record)
    {
        if (Json.NonEmpty(record, "session") is not { } session || Count(record, "seq") is not { } seq
            || Moments.ReadStrict(Json.Text(record, "ended")) is not { } ended || Json.NonEmpty(record, "model") is not { } model
            || Json.NonEmpty(record, "meaning") is not { } meaning || Whole(record, "reader") is not { } reader
            || Flag(record, "cancelled") is not { } cancelled)
        {
            return null;
        }

        IReadOnlyList<ModelTokens>? split = null;
        if (record["split"] is JsonArray shares)
        {
            var read = new List<ModelTokens>();
            foreach (var node in shares)
            {
                if (node is not JsonObject share || Json.NonEmpty(share, "model") is not { } name) return null;
                read.Add(new(name, TokensOf(share)));
            }

            split = read;
        }

        var agent = Json.NonEmpty(record, "agent");
        return new KeptTurn(
            agent, agent is null ? null : Json.Text(record, "account"), Whole(record, "epoch"), Json.NonEmpty(record, "door"), session, seq,
            Moments.ReadStrict(Json.Text(record, "started")), ended, TokensOf(record), model, split, Json.NonEmpty(record, "attribution"),
            Flag(record, "whole"), cancelled, meaning, reader) { Actor = Json.NonEmpty(record, "actor"), Brainstorm = Json.NonEmpty(record, "brainstorm"), Choice = Json.NonEmpty(record, "choice") };
    }

    private static TurnTokens TokensOf(JsonObject record) =>
        new(Count(record, "input"), Count(record, "output"), Count(record, "cacheRead"), Count(record, "cacheWrite"));

    private static long? Count(JsonObject record, string name) =>
        record[name] is JsonValue value && value.TryGetValue<long>(out var count) ? count : null;

    private static bool? Flag(JsonObject record, string name) =>
        record[name] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    private static int? Whole(JsonObject record, string name) =>
        record[name] is JsonValue value && value.TryGetValue<int>(out var whole) ? whole : null;

    private static double? Fraction(JsonObject record, string name) =>
        record[name] is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number) ? number : null;

    // ——— An exact duplicate, once.
}
