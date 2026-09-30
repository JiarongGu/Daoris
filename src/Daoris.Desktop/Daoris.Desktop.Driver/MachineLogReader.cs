using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>One line of the machine log, as a reader holds it (LOG1c): the four fields every line has, its data, and the line as written.</summary>
public sealed record LogLine(DateTimeOffset Time, string Source, string Level, string Event, JsonElement Data, string Raw)
{
    /// <summary>The time as the log writes it: UTC, with milliseconds.</summary>
    public string Stamp => Time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}

/// <summary>
/// What to read: lines at or after <see cref="Since"/>, from one <see cref="Source"/>, of one
/// <see cref="Event"/>, at or above one <see cref="Level"/>. Null is no filter.
/// </summary>
public sealed record LogFilter(DateTimeOffset? Since = null, string? Source = null, string? Event = null, string? Level = null)
{
    /// <summary>Whether the line passes every filter set.</summary>
    public bool Admits(LogLine line) =>
        (Since is not { } since || line.Time >= since)
        && (Source is null || line.Source == Source)
        && (Event is null || line.Event == Event)
        && (Level is null || MachineLogReader.Rank(line.Level) >= MachineLogReader.Rank(Level));
}

/// <summary>What a read found: the lines, oldest first, and how many could not be read.</summary>
public sealed record LogRead(IReadOnlyList<LogLine> Lines, int Skipped);

/// <summary>The terminal's flags as a filter, or the problem with them in a sentence.</summary>
public sealed record LogArguments(LogFilter Filter, bool Json, string? Problem);

/// <summary>
/// The machine log's reader (LOG1c, D94): every source's files under the home's <c>logs/</c>, merged by
/// time, filtered — one reading for both doors, <c>daoris-driver logs</c> and the Settings domain's
/// <c>DAORIS.LOG</c> · <c>LINES</c>.
/// </summary>
/// <remarks>
/// <para><b>A twin</b>: <c>tools/usage-report.mjs</c> reads the same lines with its own code, and the two
/// share none. The format is the contract (<c>docs/2026-09-30-machine-log-design.md</c> §3), and each
/// side's tests hold the same parse table: what is a line, and what is skipped.</para>
///
/// <para><b>A line that cannot be read is skipped and counted, never thrown.</b> The log is evidence; a
/// torn last line (a process ended mid-write) must not hide the lines around it. A field the reader does
/// not know is ignored, and a line's data that is no object is read as none.</para>
///
/// <para><b>Only the log's own files</b> — <c>&lt;date&gt;.&lt;source&gt;.jsonl</c> — are read: anything
/// else a person keeps in the folder is theirs. A file that vanishes or will not open between the listing
/// and the read (a prune, another process) is passed over.</para>
/// </remarks>
public static class MachineLogReader
{
    /// <summary>Every process kind that writes a file: the design's §2.</summary>
    public static readonly IReadOnlyList<string> Sources = ["desktop", "host", "mcp", "browser", "driver"];

    /// <summary>The levels a filter may name as its floor.</summary>
    public static readonly IReadOnlyList<string> Floors = ["warn", "error"];

    /// <summary>The terminal door's usage line.</summary>
    public const string Usage =
        "usage: daoris-driver logs [--since <30m|2h|3d>] [--source <desktop|host|mcp|browser|driver>] "
        + "[--event <name>] [--level <warn|error>] [--json]";

    private static readonly Regex FileName = new(
        @"^(?<day>\d{4}-\d{2}-\d{2})\.(?<source>[a-z]+)\.jsonl$", RegexOptions.CultureInvariant);

    // UTC, as the writers write it: a time with no zone would be read as UTC here and as local time by
    // the report's JavaScript, so neither twin reads one.
    private static readonly Regex TimeShape = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$", RegexOptions.CultureInvariant);

    private static readonly Regex Span = new(@"^(?<count>[1-9]\d{0,5})(?<unit>[mhd])$", RegexOptions.CultureInvariant);

    private static readonly JsonElement NoData = JsonDocument.Parse("{}").RootElement.Clone();

    private static readonly JsonSerializerOptions Quoting = new()
    {
        // 中文 stays readable in a terminal, as it does in the file.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>A level's place: info below warn below error. A level nobody knows ranks with info.</summary>
    public static int Rank(string level) => level switch
    {
        "error" => 2,
        "warn" => 1,
        _ => 0,
    };

    /// <summary>A span such as <c>30m</c>, <c>2h</c> or <c>3d</c>, or null for anything else.</summary>
    public static TimeSpan? ParseSince(string text)
    {
        var match = Span.Match(text);
        if (!match.Success) return null;
        var count = int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture);
        return match.Groups["unit"].Value switch
        {
            "m" => TimeSpan.FromMinutes(count),
            "h" => TimeSpan.FromHours(count),
            _ => TimeSpan.FromDays(count),
        };
    }

    /// <summary>One line of a file, or false when it is not one the format describes.</summary>
    public static bool TryParse(string text, out LogLine? line)
    {
        line = null;
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || Named(root, "time") is not { } time || !TimeShape.IsMatch(time)
                || !DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var at)
                || Named(root, "source") is not { } source
                || Named(root, "level") is not { } level
                || Named(root, "event") is not { } name)
            {
                return false;
            }

            var data = root.TryGetProperty("data", out var given) && given.ValueKind == JsonValueKind.Object
                ? given.Clone()
                : NoData;
            line = new LogLine(at, source, level, name, data, text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Every line in <paramref name="folder"/> that <paramref name="filter"/> admits, oldest first, and a
    /// count of the lines that could not be read. A day's file older than the filter's since is not opened.
    /// </summary>
    public static LogRead Read(string folder, LogFilter filter)
    {
        if (!Directory.Exists(folder)) return new LogRead([], 0);

        var oldest = filter.Since?.UtcDateTime.Date;
        var files = new List<(string Path, DateTime Day, string Source)>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.jsonl"))
            {
                var match = FileName.Match(Path.GetFileName(path));
                if (!match.Success
                    || !DateTime.TryParseExact(match.Groups["day"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                    || (oldest is { } floor && day < floor)
                    || (filter.Source is { } wanted && match.Groups["source"].Value != wanted))
                {
                    continue;
                }

                files.Add((path, day, match.Groups["source"].Value));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new LogRead([], 0);
        }

        var lines = new List<LogLine>();
        var skipped = 0;
        foreach (var file in files.OrderBy(file => file.Day).ThenBy(file => file.Source, StringComparer.Ordinal))
        {
            foreach (var text in LinesOf(file.Path))
            {
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (!TryParse(text, out var line))
                {
                    skipped++;
                    continue;
                }

                if (filter.Admits(line!)) lines.Add(line!);
            }
        }

        // A stable sort: lines of one moment keep their day-then-source order.
        return new LogRead([.. lines.OrderBy(line => line.Time)], skipped);
    }

    /// <summary>
    /// The terminal's flags as a filter, <paramref name="now"/> anchoring a span. A flag the reader cannot
    /// use is a problem in a sentence, never read as no filter.
    /// </summary>
    public static LogArguments Arguments(IReadOnlyList<string> args, DateTimeOffset now)
    {
        var filter = new LogFilter();
        var json = false;
        for (var i = 0; i < args.Count; i++)
        {
            var value = i + 1 < args.Count ? args[i + 1] : null;
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;

                case "--since":
                    if (value is null || ParseSince(value) is not { } span) return Problem("`--since` takes a span such as 30m, 2h or 3d");
                    filter = filter with { Since = now - span };
                    i++;
                    break;

                case "--source":
                    if (value is null || !Sources.Contains(value))
                    {
                        var sources = $"{string.Join(", ", Sources.Take(Sources.Count - 1))} and {Sources[^1]}";
                        return Problem(value is null
                            ? $"`--source` takes a source; the sources are {sources}"
                            : $"`{value}` is not a source; the sources are {sources}");
                    }

                    filter = filter with { Source = value };
                    i++;
                    break;

                case "--event":
                    if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
                    {
                        return Problem("`--event` takes an event's name, such as session.opened");
                    }

                    filter = filter with { Event = value };
                    i++;
                    break;

                case "--level":
                    if (value is null || !Floors.Contains(value)) return Problem("`--level` takes warn or error");
                    filter = filter with { Level = value };
                    i++;
                    break;

                default:
                    return Problem($"`{args[i]}` is not an option of logs");
            }
        }

        return new LogArguments(filter, json, null);

        LogArguments Problem(string problem) => new(new LogFilter(), false, problem);
    }

    /// <summary>
    /// One readable line: the time, the source, the level, the event, then the data as <c>key=value</c>. A
    /// string that would break the line (a space, a quote, an equals sign, a line break) is quoted.
    /// </summary>
    public static string Format(LogLine line)
    {
        var text = new StringBuilder()
            .Append(line.Time.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
            .Append("  ").Append(line.Source.PadRight(7))
            .Append("  ").Append(line.Level.PadRight(5))
            .Append("  ").Append(line.Event);
        var first = true;
        foreach (var field in line.Data.EnumerateObject())
        {
            text.Append(first ? "  " : " ").Append(field.Name).Append('=').Append(Value(field.Value));
            first = false;
        }

        return text.ToString();
    }

    /// <summary>What the terminal says after the lines: that nothing matched, and how many could not be read.</summary>
    public static IEnumerable<string> Closing(LogRead read, string folder)
    {
        if (read.Lines.Count == 0) yield return $"logs: nothing in `{folder}` for that filter.";
        if (read.Skipped > 0) yield return $"logs: {read.Skipped} line(s) could not be read and were skipped.";
    }

    private static string? Named(JsonElement root, string field) =>
        root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    private static string Value(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String) return value.GetRawText();
        var text = value.GetString()!;
        return text.Length > 0 && !text.Any(c => char.IsWhiteSpace(c) || c is '"' or '=')
            ? text
            : JsonSerializer.Serialize(text, Quoting);
    }

    private static IEnumerable<string> LinesOf(string path)
    {
        FileStream stream;
        try
        {
            // Shared for writing and deleting: the writer holds today's file open, and a prune may remove one.
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        using (stream)
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            while (true)
            {
                string? text;
                try
                {
                    text = reader.ReadLine();
                }
                catch (IOException)
                {
                    yield break;
                }

                if (text is null) yield break;
                yield return text;
            }
        }
    }
}
