using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The machine log's reader (LOG1c, D94): every source's files under the home, merged by time, with the
/// same filters at both doors — `daoris-driver logs` at a terminal, the Settings domain on the screen.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The parse table is a twin's</b> (<c>docs/2026-09-30-machine-log-design.md</c> §3): the
/// usage report (<c>tools/usage-report.mjs</c>) reads the same lines with its own code, and its test
/// (<c>usage-report.test.ts</c>) holds the same cases with the same answers. Change one table and the
/// other changes with it.</para>
///
/// <para><b>A line that cannot be read is skipped and counted, never thrown</b>: the log is evidence, and a
/// reader that stopped at a torn line would hide every line after it.</para>
/// </remarks>
public sealed class MachineLogReaderTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-log-reader-" + Guid.NewGuid().ToString("N")[..8]);

    public MachineLogReaderTests() => Directory.CreateDirectory(Logs);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Logs => Path.Combine(_home, MachineLog.Folder);

    private void File(string name, params string[] lines) =>
        System.IO.File.WriteAllText(Path.Combine(Logs, name), string.Join("\n", lines) + "\n");

    private static string Line(string time, string source, string level, string @event, string data = "{}") =>
        $$"""{"time":"{{time}}","source":"{{source}}","level":"{{level}}","event":"{{@event}}","data":{{data}}}""";

    /// <summary>
    /// The twin's table: each line, and whether a reader keeps it. The report's test holds the same rows.
    /// A blank line is neither kept nor counted: it is the file's last newline, not a line.
    /// </summary>
    public static TheoryData<string, string, bool> Parsing => new()
    {
        { "a whole line", """{"time":"2026-09-30T07:10:00.123Z","source":"desktop","level":"info","event":"session.opened","data":{"session":"76cdd5db","openMs":5840}}""", true },
        { "no data", """{"time":"2026-09-30T07:10:00.123Z","source":"host","level":"info","event":"app.started"}""", true },
        { "data that is no object", """{"time":"2026-09-30T07:10:00.123Z","source":"host","level":"info","event":"app.started","data":"x"}""", true },
        { "a field nobody knows", """{"time":"2026-09-30T07:10:00.123Z","source":"mcp","level":"warn","event":"log","data":{},"extra":1}""", true },
        { "a time with no milliseconds", """{"time":"2026-09-30T07:10:00Z","source":"desktop","level":"info","event":"app.started","data":{}}""", true },
        { "not JSON", "not json at all", false },
        { "a torn line", """{"time":"2026-09-30T07:10:00.123Z","source":"desk""", false },
        { "an array", """["time","source"]""", false },
        { "no time", """{"source":"desktop","level":"info","event":"app.started","data":{}}""", false },
        { "a time that is no time", """{"time":"yesterday","source":"desktop","level":"info","event":"app.started","data":{}}""", false },
        { "a time with no zone", """{"time":"2026-09-30T07:10:00.123","source":"desktop","level":"info","event":"app.started","data":{}}""", false },
        { "no source", """{"time":"2026-09-30T07:10:00.123Z","level":"info","event":"app.started","data":{}}""", false },
        { "no level", """{"time":"2026-09-30T07:10:00.123Z","source":"desktop","event":"app.started","data":{}}""", false },
        { "no event", """{"time":"2026-09-30T07:10:00.123Z","source":"desktop","level":"info","data":{}}""", false },
        { "an event that is no string", """{"time":"2026-09-30T07:10:00.123Z","source":"desktop","level":"info","event":5,"data":{}}""", false },
    };

    [Theory]
    [MemberData(nameof(Parsing))]
    public void Each_line_of_the_twin_table_is_kept_or_skipped(string why, string text, bool kept)
    {
        Assert.True(MachineLogReader.TryParse(text, out var line) == kept, why);
        if (kept) Assert.Equal(text, line!.Raw);
    }

    [Fact]
    public void A_kept_line_carries_its_time_source_level_event_and_data()
    {
        Assert.True(MachineLogReader.TryParse(
            """{"time":"2026-09-30T07:10:00.123Z","source":"desktop","level":"info","event":"session.opened","data":{"session":"76cdd5db","openMs":5840}}""",
            out var line));

        Assert.Equal(new DateTimeOffset(2026, 9, 30, 7, 10, 0, 123, TimeSpan.Zero), line!.Time);
        Assert.Equal(("desktop", "info", "session.opened"), (line.Source, line.Level, line.Event));
        Assert.Equal("76cdd5db", line.Data.GetProperty("session").GetString());
        Assert.Equal(5840, line.Data.GetProperty("openMs").GetInt32());
        Assert.Equal("2026-09-30T07:10:00.123Z", line.Stamp);
    }

    /// <summary>Every source's files, across days, come back as one list in time order.</summary>
    [Fact]
    public void Lines_from_every_source_and_day_are_merged_by_time()
    {
        File("2026-09-29.desktop.jsonl",
            Line("2026-09-29T23:00:00.000Z", "desktop", "info", "app.started"),
            Line("2026-09-29T23:30:00.000Z", "desktop", "info", "view.opened", """{"view":"sessions"}"""));
        File("2026-09-29.host.jsonl", Line("2026-09-29T23:10:00.000Z", "host", "info", "app.started"));
        File("2026-09-30.desktop.jsonl", Line("2026-09-30T01:00:00.000Z", "desktop", "info", "app.stopped"));
        File("2026-09-30.browser.jsonl", Line("2026-09-30T00:15:00.000Z", "browser", "error", "error"));

        var read = MachineLogReader.Read(Logs, new LogFilter());

        Assert.Equal(
            ["desktop app.started", "host app.started", "desktop view.opened", "browser error", "desktop app.stopped"],
            read.Lines.Select(line => $"{line.Source} {line.Event}"));
        Assert.Equal(0, read.Skipped);
    }

    /// <summary>A line that cannot be read is skipped and counted; the lines after it are still read.</summary>
    [Fact]
    public void A_malformed_line_is_skipped_and_counted_and_the_rest_are_read()
    {
        File("2026-09-30.desktop.jsonl",
            Line("2026-09-30T01:00:00.000Z", "desktop", "info", "app.started"),
            "{\"time\":\"2026-09-30T01:0",
            "",
            Line("2026-09-30T02:00:00.000Z", "desktop", "info", "app.stopped"));
        File("2026-09-30.host.jsonl", "not json");

        var read = MachineLogReader.Read(Logs, new LogFilter());

        Assert.Equal(["app.started", "app.stopped"], read.Lines.Select(line => line.Event));
        Assert.Equal(2, read.Skipped);
    }

    /// <summary>Only the log's own files are read: a person's note in the folder is not a line gone wrong.</summary>
    [Fact]
    public void Files_that_are_not_the_logs_own_are_not_read()
    {
        File("2026-09-30.desktop.jsonl", Line("2026-09-30T01:00:00.000Z", "desktop", "info", "app.started"));
        File("notes.txt", "the person's own note");
        File("2026-13-45.desktop.jsonl", Line("2026-09-30T01:00:00.000Z", "desktop", "info", "app.stopped"));
        File("export.jsonl", Line("2026-09-30T01:00:00.000Z", "desktop", "info", "app.stopped"));

        var read = MachineLogReader.Read(Logs, new LogFilter());

        Assert.Equal("app.started", Assert.Single(read.Lines).Event);
        Assert.Equal(0, read.Skipped);
    }

    [Fact]
    public void A_folder_that_does_not_exist_is_no_lines_not_a_failure()
    {
        var read = MachineLogReader.Read(Path.Combine(_home, "nowhere"), new LogFilter());

        Assert.Empty(read.Lines);
        Assert.Equal(0, read.Skipped);
    }

    private void Day()
    {
        File("2026-09-28.desktop.jsonl", Line("2026-09-28T09:00:00.000Z", "desktop", "warn", "log", """{"category":"Old"}"""));
        File("2026-09-30.desktop.jsonl",
            Line("2026-09-30T09:00:00.000Z", "desktop", "info", "view.opened", """{"view":"sessions"}"""),
            Line("2026-09-30T10:00:00.000Z", "desktop", "warn", "refused", """{"code":"NO_ROUTE"}"""),
            Line("2026-09-30T11:30:00.000Z", "desktop", "error", "page.error", """{"where":"window"}"""));
        File("2026-09-30.host.jsonl", Line("2026-09-30T11:00:00.000Z", "host", "warn", "request.failed", """{"status":500}"""));
    }

    [Fact]
    public void Since_keeps_the_lines_at_or_after_it_and_skips_older_days_unread()
    {
        Day();
        // An older day's file that could not be read would be counted, if it were opened at all.
        File("2026-09-27.host.jsonl", "not json");

        var read = MachineLogReader.Read(Logs, new LogFilter(Since: Now.AddHours(-2)));

        Assert.Equal(["refused", "request.failed", "page.error"], read.Lines.Select(line => line.Event));
        Assert.Equal(0, read.Skipped);
    }

    [Fact]
    public void Source_keeps_one_process_kinds_lines()
    {
        Day();

        var read = MachineLogReader.Read(Logs, new LogFilter(Source: "host"));

        Assert.Equal("request.failed", Assert.Single(read.Lines).Event);
    }

    [Fact]
    public void Event_keeps_one_events_lines()
    {
        Day();

        Assert.Equal("page.error", Assert.Single(MachineLogReader.Read(Logs, new LogFilter(Event: "page.error")).Lines).Event);
    }

    /// <summary>A level is a floor: warnings mean warnings and errors, errors mean errors alone.</summary>
    [Fact]
    public void Level_is_a_floor()
    {
        Day();

        Assert.Equal(
            ["log", "refused", "request.failed", "page.error"],
            MachineLogReader.Read(Logs, new LogFilter(Level: "warn")).Lines.Select(line => line.Event));
        Assert.Equal(
            ["page.error"],
            MachineLogReader.Read(Logs, new LogFilter(Level: "error")).Lines.Select(line => line.Event));
    }

    [Theory]
    [InlineData("30m", 30)]
    [InlineData("2h", 120)]
    [InlineData("3d", 3 * 24 * 60)]
    [InlineData("1d", 24 * 60)]
    public void A_span_is_minutes_hours_or_days(string text, int minutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutes), MachineLogReader.ParseSince(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0h")]
    [InlineData("2")]
    [InlineData("h")]
    [InlineData("2w")]
    [InlineData("-3d")]
    [InlineData("2 h")]
    [InlineData("yesterday")]
    public void Anything_else_is_no_span(string text)
    {
        Assert.Null(MachineLogReader.ParseSince(text));
    }

    /// <summary>The terminal's flags, read into one filter; the same filter the screen's request builds.</summary>
    [Fact]
    public void The_terminals_flags_become_the_filter()
    {
        var parsed = MachineLogReader.Arguments(
            ["--since", "2h", "--source", "host", "--event", "request.failed", "--level", "warn", "--json"], Now);

        Assert.Null(parsed.Problem);
        Assert.Equal(new LogFilter(Now.AddHours(-2), "host", "request.failed", "warn"), parsed.Filter);
        Assert.True(parsed.Json);

        var none = MachineLogReader.Arguments([], Now);
        Assert.Null(none.Problem);
        Assert.Equal(new LogFilter(), none.Filter);
        Assert.False(none.Json);
    }

    /// <summary>A flag the reader cannot use is said, naming what it would take — never read as no filter.</summary>
    [Theory]
    [InlineData(new[] { "--since", "yesterday" }, "`--since` takes a span such as 30m, 2h or 3d")]
    [InlineData(new[] { "--since" }, "`--since` takes a span such as 30m, 2h or 3d")]
    [InlineData(new[] { "--source", "hots" }, "`hots` is not a source; the sources are desktop, host, mcp, browser and driver")]
    [InlineData(new[] { "--level", "info" }, "`--level` takes warn or error")]
    [InlineData(new[] { "--event" }, "`--event` takes an event's name, such as session.opened")]
    [InlineData(new[] { "--tail" }, "`--tail` is not an option of logs")]
    public void A_flag_the_reader_cannot_use_is_a_problem_that_says_why(string[] args, string problem)
    {
        Assert.Equal(problem, MachineLogReader.Arguments(args, Now).Problem);
    }

    /// <summary>One readable line an event: its time, source, level and event, then its data as key=value.</summary>
    [Fact]
    public void A_line_reads_as_its_time_source_level_event_and_data()
    {
        Assert.True(MachineLogReader.TryParse(
            """{"time":"2026-09-30T07:10:00.123Z","source":"host","level":"warn","event":"request.failed","data":{"method":"GET","route":"/api/search","status":500,"slow":true,"note":null}}""",
            out var line));

        Assert.Equal(
            "2026-09-30 07:10:00.123Z  host     warn   request.failed  method=GET route=/api/search status=500 slow=true note=null",
            MachineLogReader.Format(line!));
    }

    /// <summary>A value with a space, a quote, an equals sign or a line break is quoted, so the line stays one line.</summary>
    [Fact]
    public void A_value_that_would_break_the_line_is_quoted()
    {
        Assert.True(MachineLogReader.TryParse(
            """{"time":"2026-09-30T07:10:00.123Z","source":"desktop","level":"error","event":"error","data":{"where":"the driver loop","message":"盘满了 \"full\"","stack":"at A\nat B","empty":""}}""",
            out var line));

        Assert.Equal(
            "2026-09-30 07:10:00.123Z  desktop  error  error  where=\"the driver loop\" message=\"盘满了 \\\"full\\\"\" stack=\"at A\\nat B\" empty=\"\"",
            MachineLogReader.Format(line!));
    }

    /// <summary>What the terminal says after the lines: nothing found, and what could not be read.</summary>
    [Fact]
    public void The_closing_says_when_nothing_matched_and_how_many_lines_were_skipped()
    {
        Assert.Empty(MachineLogReader.Closing(new LogRead([Parsed()], 0), "logs"));
        Assert.Equal(
            ["logs: nothing in `logs` for that filter."],
            MachineLogReader.Closing(new LogRead([], 0), "logs"));
        Assert.Equal(
            ["logs: 3 line(s) could not be read and were skipped."],
            MachineLogReader.Closing(new LogRead([Parsed()], 3), "logs"));
    }

    private static LogLine Parsed()
    {
        Assert.True(MachineLogReader.TryParse(Line("2026-09-30T01:00:00.000Z", "desktop", "info", "app.started"), out var line));
        return line!;
    }
}
