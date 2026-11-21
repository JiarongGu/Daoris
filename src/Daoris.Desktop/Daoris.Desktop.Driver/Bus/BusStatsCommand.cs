using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris bus stats</c>: the bus log's measures by day, read from the home's store and changing nothing: entries by kind, the
/// flushes with their entries and time, the rows' bytes and retention's cut, the misses by reason, each kind's handling time and each
/// look stage's time. What the bus's byte budget and its obligations' deadlines are set from.
/// </summary>
/// <remarks>Exit codes: 0 read, or the home keeps no log yet, said · 2 a word it does not answer.</remarks>
[CommandGroup("bus", "stats")]
public static class BusStatsCommand
{
    public const string Usage =
        "usage: daoris bus stats [--days <1-90>]   the bus log's measures by day (UTC): events by kind, flushes and their time, bytes,\n"
        + "                                         misses, handling and look stage times; changes nothing";

    /// <summary>The days read where none are asked for.</summary>
    public const int Days = 7;

    /// <param name="args">The words after <c>bus</c>.</param>
    /// <param name="home">The Daoris home.</param>
    /// <param name="clock">What time it is, which day the days are read back from.</param>
    /// <param name="said">What the verb said, for the caller to print.</param>
    public static int Run(string[] args, string home, Func<DateTimeOffset> clock, out string said)
    {
        var output = new StringBuilder();
        var code = Run(args, home, clock, output);
        said = output.ToString();
        return code;
    }

    private static int Run(string[] args, string home, Func<DateTimeOffset> clock, StringBuilder output)
    {
        if (DaysOf(args) is not { } days)
        {
            output.AppendLine("bus: name what to read (stats), and at most --days <1-90>.");
            output.AppendLine(Usage);
            return 2;
        }

        using var store = DesktopBusLogStore.Reading(home);
        if (store is null)
        {
            output.AppendLine("bus stats: this home keeps no bus log yet: its store has not been opened by a build that keeps one, and nothing was read.");
            return 0;
        }

        var today = DateOnly.FromDateTime(clock().UtcDateTime);
        var from = today.AddDays(1 - days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var (rows, lowest, highest) = store.Kept();
        var measures = store.Measures(from);
        output.AppendLine(Invariant($"bus stats: the bus's log on this home, the last {days} day{(days == 1 ? "" : "s")} (UTC), as the home's lease holder measured it."));
        output.AppendLine(Invariant($"log: {rows} entr{(rows == 1 ? "y" : "ies")} kept, seq {lowest} to {highest}; the newest {DesktopBusLogStore.Cap} are kept."));
        if (measures.Count == 0)
        {
            output.AppendLine("No measures in these days: nothing was teed, and no look was timed.");
            return 0;
        }

        foreach (var day in measures.GroupBy(row => row.Day, StringComparer.Ordinal))
        {
            output.AppendLine(day.Key);
            Day(day.ToList(), output);
        }

        return 0;
    }

    private static void Day(IReadOnlyList<BusMeasureRow> rows, StringBuilder output)
    {
        IEnumerable<BusMeasureRow> Of(string measure) => rows.Where(row => row.Measure == measure).OrderBy(row => row.Name, StringComparer.Ordinal);
        BusMeasureRow? One(string measure, string name) => rows.FirstOrDefault(row => row.Measure == measure && row.Name == name);

        var kinds = Of(BusMeasures.Kind).ToList();
        output.AppendLine(Invariant($"  events {kinds.Sum(row => row.Count)}{(kinds.Count == 0 ? "" : ": ")}{string.Join(", ", kinds.Select(row => Invariant($"{row.Name} {row.Count}")))}"));

        var entries = One(BusMeasures.Flush, BusMeasures.Entries);
        var micros = One(BusMeasures.Flush, BusMeasures.Micros);
        var commit = One(BusMeasures.Flush, BusMeasures.CommitClass);
        var alone = One(BusMeasures.Flush, BusMeasures.MeasuresAlone);
        output.AppendLine(entries is null || micros is null
            ? Invariant($"  flushes 0{(alone is null ? "" : $"; the measures alone {alone.Count}")}")
            : Invariant($"  flushes {entries.Count}: {entries.Total} entries, {entries.Total / (double)entries.Count:0.0} a flush, most {entries.Most}; ")
              + Invariant($"mean {micros.Total / (double)micros.Count / 1000:0.00} ms, most {micros.Most / 1000.0:0.00} ms; commit class {commit?.Count ?? 0}")
              + (alone is null ? "" : Invariant($"; the measures alone {alone.Count}, mean {alone.Total / (double)alone.Count / 1000:0.00} ms")));

        var bytes = One(BusMeasures.Bytes, BusMeasures.Rows);
        var cut = One(BusMeasures.Cut, BusMeasures.Rows);
        output.AppendLine(bytes is null
            ? "  bytes 0"
            : Invariant($"  bytes {bytes.Total}: {bytes.Total / Math.Max(1, bytes.Count)} a row, most {bytes.Most}; cut {cut?.Total ?? 0} rows"));

        var missed = Of(BusMeasures.Miss).ToList();
        output.AppendLine(missed.Count == 0 ? "  missed none" : $"  missed {string.Join(", ", missed.Select(row => Invariant($"{row.Name} {row.Count}")))}");

        Timed("took", Of(BusMeasures.Took).ToList(), output);
        Timed("stages", Of(BusMeasures.Stage).ToList(), output);
    }

    private static void Timed(string named, IReadOnlyList<BusMeasureRow> rows, StringBuilder output)
    {
        if (rows.Count == 0) return;
        output.AppendLine($"  {named}: " + string.Join("; ", rows.Select(row => Invariant($"{row.Name} n={row.Count} mean {row.Total / Math.Max(1, row.Count)} ms most {row.Most} ms"))));
    }

    /// <summary>The days asked for, <see cref="Days"/> where none; null for words this does not answer.</summary>
    private static int? DaysOf(string[] args) => args switch
    {
        ["stats"] => Days,
        ["stats", "--days", var count] when int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out var days) && days is >= 1 and <= DesktopBusLogStore.MeasureDays => days,
        _ => null,
    };

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
