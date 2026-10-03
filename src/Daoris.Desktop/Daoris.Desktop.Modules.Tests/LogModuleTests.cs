using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The page's report into the machine log (LOG1b, D94): one request, <c>DAORIS.LOG</c> · <c>EVENT</c>,
/// and the module that takes it is where D94's list of what is never logged is enforced.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The page is the one writer that could pass a word through by mistake</b>, so these tests
/// send it the mistakes: a field nobody declared, the message's text beside its length, a sentence where
/// a name goes, a list where a count goes. Each is dropped, and the rest of the line survives.</para>
/// </remarks>
public sealed class LogModuleTests : Bridge
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 7, 10, 0, 123, TimeSpan.Zero);

    private MachineLog Log() => new(Home, "desktop", () => Now);

    private string[] Lines()
    {
        var folder = Path.Combine(Home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder).SelectMany(path =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }).ToArray();
    }

    private static Task<JsonElement> Report(LogModule module, object payload) => AnswerAsync(module, "EVENT", payload);

    /// <summary>Each of the page's events, with each of its fields, is one line — and nothing else is added.</summary>
    public static TheoryData<string, object, string, string> Catalogue => new()
    {
        { "view.opened", new { view = "sessions" }, "info", """{"view":"sessions"}""" },
        { "command.run", new { command = "go.quests" }, "info", """{"command":"go.quests"}""" },
        { "panel.moved", new { view = "console", region = "right" }, "info", """{"view":"console","region":"right"}""" },
        {
            "message.sent", new { session = "76cdd5db", kind = "chat", length = 42, files = 2 }, "info",
            """{"session":"76cdd5db","kind":"chat","length":42,"files":2}"""
        },
        {
            // MSG1d (D137 §3.3): where the words reach the session, the answer SESSION_INPUT gave.
            "message.sent", new { session = "76cdd5db", kind = "steer", length = 18, files = 0, reach = "resume" }, "info",
            """{"session":"76cdd5db","kind":"steer","length":18,"files":0,"reach":"resume"}"""
        },
        { "proposal.settled", new { applied = true }, "info", """{"applied":true}""" },
        {
            "page.error", new { where = "window", message = "Cannot read properties of undefined (reading 'id')" }, "error",
            """{"where":"window","message":"Cannot read properties of undefined (reading 'id')"}"""
        },
    };

    [Theory]
    [MemberData(nameof(Catalogue))]
    public async Task Each_page_event_in_the_catalogue_is_one_line_with_its_fields(string name, object data, string level, string fields)
    {
        using var log = Log();

        await Report(new LogModule(log), new { @event = name, data });

        Assert.Equal(
            "{\"time\":\"2026-09-30T07:10:00.123Z\",\"source\":\"desktop\",\"level\":\"" + level
            + "\",\"event\":\"" + name + "\",\"data\":" + fields + "}",
            Assert.Single(Lines()));
    }

    /// <summary>An event the catalogue does not name is dropped, silently: the page is not told it tried.</summary>
    [Theory]
    [InlineData("search.typed")]
    [InlineData("session.started")]
    [InlineData("error")]
    [InlineData("")]
    public async Task An_event_outside_the_catalogue_is_dropped_silently(string name)
    {
        using var log = Log();

        var answer = await Report(new LogModule(log), new { @event = name, data = new { text = "find my private plan", view = "sessions" } });

        Assert.Equal(JsonValueKind.Null, answer.ValueKind);
        Assert.Empty(Lines());
    }

    [Fact]
    public async Task A_field_the_event_does_not_declare_is_dropped_and_the_rest_is_kept()
    {
        using var log = Log();

        await Report(new LogModule(log), new { @event = "view.opened", data = new { view = "quests", query = "find my private plan", url = "https://example.com/?token=abc" } });

        var line = Assert.Single(Lines());
        Assert.EndsWith("""
            "event":"view.opened","data":{"view":"quests"}}
            """, line);
    }

    /// <summary>
    /// 🔴 A message is counted, never kept: its length and how many files it carried. The text, the file
    /// names and anything else the page might have put beside them are dropped.
    /// </summary>
    [Fact]
    public async Task A_message_sent_keeps_its_length_and_file_count_and_never_its_words()
    {
        using var log = Log();

        await Report(new LogModule(log), new
        {
            @event = "message.sent",
            data = new
            {
                session = "76cdd5db", kind = "help", length = 25, files = 1,
                text = "rename the secret module", names = new[] { "plan.docx" }, file = "plan.docx",
            },
        });

        var line = Assert.Single(Lines());
        Assert.EndsWith("""
            "data":{"session":"76cdd5db","kind":"help","length":25,"files":1}}
            """, line);
        Assert.DoesNotContain("secret", line);
        Assert.DoesNotContain("plan.docx", line);
    }

    /// <summary>A value of the wrong kind is dropped: a sentence where a name goes, words or a list where a count goes.</summary>
    [Fact]
    public async Task A_value_of_the_wrong_kind_is_dropped()
    {
        using var log = Log();
        var module = new LogModule(log);

        await Report(module, new
        {
            @event = "message.sent",
            data = new { session = "rename the secret module", kind = "chat", length = "rename the secret module", files = new[] { "plan.docx" } },
        });
        await Report(module, new { @event = "message.sent", data = new { session = "s1", kind = "chat", length = -3, files = 1.5 } });
        await Report(module, new { @event = "proposal.settled", data = new { applied = "yes, apply the plan" } });
        await Report(module, new { @event = "view.opened", data = new { view = new { nested = "sessions" } } });

        var lines = Lines();
        Assert.Equal(4, lines.Length);
        Assert.EndsWith("""
            "data":{"kind":"chat"}}
            """, lines[0]);
        Assert.EndsWith("""
            "data":{"session":"s1","kind":"chat"}}
            """, lines[1]);
        Assert.EndsWith("""
            "data":{}}
            """, lines[2]);
        Assert.EndsWith("""
            "data":{}}
            """, lines[3]);
        Assert.DoesNotContain(lines, line => line.Contains("secret", StringComparison.Ordinal) || line.Contains("plan", StringComparison.Ordinal));
    }

    /// <summary>A long string is cut short, and says it was: the log keeps a clue, not a paragraph.</summary>
    [Fact]
    public async Task A_long_string_is_capped()
    {
        using var log = Log();
        var message = string.Concat(Enumerable.Repeat("a failure the page caught, ", 20));

        await Report(new LogModule(log), new { @event = "page.error", data = new { where = "promise", message } });

        using var line = JsonDocument.Parse(Assert.Single(Lines()));
        var kept = line.RootElement.GetProperty("data").GetProperty("message").GetString()!;
        Assert.Equal(LogModule.MaxText + 1, kept.Length);
        Assert.Equal(message[..LogModule.MaxText] + "…", kept);
    }

    /// <summary>A payload that is no event is no line and no failure: the page's report never breaks the page.</summary>
    [Fact]
    public async Task A_payload_that_is_no_event_writes_nothing_and_refuses_nothing()
    {
        using var log = Log();
        var module = new LogModule(log);

        await AnswerAsync(module, "EVENT");
        await Report(module, new { @event = 5, data = new { view = "sessions" } });
        await Report(module, new { data = new { view = "sessions" } });
        await Report(module, new { @event = "view.opened", data = "sessions" });

        // The last is the catalogue's event with no fields it could keep.
        Assert.EndsWith("""
            "event":"view.opened","data":{}}
            """, Assert.Single(Lines()));
    }

    [Fact]
    public async Task A_request_type_the_module_does_not_have_is_refused_as_every_module_refuses_one()
    {
        using var log = Log();

        Assert.Contains("NO_ROUTE", await RefusalAsync(new LogModule(log), "WRITE"));
    }

    // ---- LOG1c: the screen's door to reading the log, and to its folder ----

    private static readonly DateTimeOffset Noon = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private LogModule Reader(MachineLog log, OpenFolder? open = null) => new(log, open, () => Noon);

    private void Logged(string name, params string[] lines)
    {
        var folder = Path.Combine(Home, MachineLog.Folder);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name), string.Join("\n", lines) + "\n");
    }

    private static string Line(string time, string source, string level, string @event, string data = "{}") =>
        $$"""{"time":"{{time}}","source":"{{source}}","level":"{{level}}","event":"{{@event}}","data":{{data}}}""";

    /// <summary>A morning on this machine, across three sources and two days, with one torn line.</summary>
    private void Morning()
    {
        Logged("2026-09-29.desktop.jsonl", Line("2026-09-29T20:00:00.000Z", "desktop", "info", "app.started"));
        Logged("2026-09-30.desktop.jsonl",
            Line("2026-09-30T09:00:00.000Z", "desktop", "info", "view.opened", """{"view":"sessions"}"""),
            Line("2026-09-30T10:00:00.000Z", "desktop", "info", "refused", """{"code":"DRIVER_REFUSED","request":"DAORIS.DRIVER.START_CHAT"}"""),
            "{\"time\":\"2026-09-30T10:1",
            Line("2026-09-30T11:30:00.000Z", "desktop", "error", "page.error", """{"where":"window","message":"x is undefined"}"""));
        Logged("2026-09-30.host.jsonl", Line("2026-09-30T11:00:00.000Z", "host", "warn", "request.failed", """{"method":"GET","status":500}"""));
        Logged("2026-09-30.browser.jsonl", Line("2026-09-30T10:30:00.000Z", "browser", "info", "app.started"));
    }

    private static string[] Events(JsonElement answer) =>
        answer.GetProperty("lines").EnumerateArray().Select(line => line.GetProperty("event").GetString()!).ToArray();

    /// <summary>
    /// The recent lines, newest first, from every source — each with its time as written, its source,
    /// level, event and data — and the folder they are read from, what each level counts, the events the
    /// period holds, and how many lines could not be read.
    /// </summary>
    [Fact]
    public async Task Lines_are_every_sources_newest_first_with_counts_events_and_what_was_skipped()
    {
        Morning();
        using var log = Log();

        var answer = await AnswerAsync(Reader(log), "LINES");

        Assert.Equal(Path.Combine(Home, MachineLog.Folder), answer.GetProperty("folder").GetString());
        Assert.Equal(["page.error", "request.failed", "app.started", "refused", "view.opened", "app.started"], Events(answer));
        var first = answer.GetProperty("lines")[0];
        Assert.Equal("2026-09-30T11:30:00.000Z", first.GetProperty("time").GetString());
        Assert.Equal("desktop", first.GetProperty("source").GetString());
        Assert.Equal("error", first.GetProperty("level").GetString());
        Assert.Equal("window", first.GetProperty("data").GetProperty("where").GetString());
        Assert.Equal(6, answer.GetProperty("total").GetInt32());
        Assert.Equal(1, answer.GetProperty("skipped").GetInt32());
        var counts = answer.GetProperty("counts");
        Assert.Equal((4, 1, 1), (counts.GetProperty("info").GetInt32(), counts.GetProperty("warn").GetInt32(), counts.GetProperty("error").GetInt32()));
        Assert.Equal(
            ["app.started", "page.error", "refused", "request.failed", "view.opened"],
            answer.GetProperty("events").EnumerateArray().Select(name => name.GetString()));
    }

    /// <summary>
    /// The filters are the terminal's, applied here and not on the page: a span back from now, a source,
    /// an event, a level as a floor. The counts are the period's before the level, so a person filtering
    /// to errors still sees how many warnings there were; the events are the period's before the event.
    /// </summary>
    [Fact]
    public async Task The_filters_are_applied_here_as_the_terminal_applies_them()
    {
        Morning();
        using var log = Log();
        var module = Reader(log);

        Assert.Equal(["page.error", "request.failed", "app.started"],
            Events(await AnswerAsync(module, "LINES", new { since = "90m" })));
        Assert.Equal(["request.failed"], Events(await AnswerAsync(module, "LINES", new { source = "host" })));
        Assert.Equal(["app.started", "app.started"], Events(await AnswerAsync(module, "LINES", new { @event = "app.started" })));

        var errors = await AnswerAsync(module, "LINES", new { since = "12h", level = "error" });
        Assert.Equal(["page.error"], Events(errors));
        Assert.Equal(1, errors.GetProperty("total").GetInt32());
        Assert.Equal(3, errors.GetProperty("counts").GetProperty("info").GetInt32());
        Assert.Equal(1, errors.GetProperty("counts").GetProperty("warn").GetInt32());

        var warnings = await AnswerAsync(module, "LINES", new { level = "warn", @event = "request.failed" });
        Assert.Equal(["request.failed"], Events(warnings));
        Assert.Equal(5, warnings.GetProperty("events").GetArrayLength());
    }

    /// <summary>At most the cap, the newest; the total says how many there were, so the page can say it showed part.</summary>
    [Fact]
    public async Task At_most_the_limit_is_answered_and_never_more_than_the_cap()
    {
        using var log = Log();
        Logged("2026-09-30.desktop.jsonl", Enumerable.Range(0, LogModule.MaxLines + 20)
            .Select(i => Line(Noon.AddSeconds(-i - 1).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture), "desktop", "info", "view.opened", $$"""{"view":"v{{i}}"}"""))
            .Reverse().ToArray());
        var module = Reader(log);

        var two = await AnswerAsync(module, "LINES", new { limit = 2 });
        Assert.Equal(2, two.GetProperty("lines").GetArrayLength());
        Assert.Equal("v0", two.GetProperty("lines")[0].GetProperty("data").GetProperty("view").GetString());
        Assert.Equal(LogModule.MaxLines + 20, two.GetProperty("total").GetInt32());

        Assert.Equal(LogModule.DefaultLines, (await AnswerAsync(module, "LINES")).GetProperty("lines").GetArrayLength());
        Assert.Equal(LogModule.MaxLines, (await AnswerAsync(module, "LINES", new { limit = 100_000 })).GetProperty("lines").GetArrayLength());
    }

    /// <summary>A filter the reader cannot use is refused by name, never read as no filter.</summary>
    [Theory]
    [InlineData("since", "yesterday")]
    [InlineData("source", "hots")]
    [InlineData("level", "info")]
    public async Task A_filter_the_reader_cannot_use_is_refused(string filter, string value)
    {
        using var log = Log();

        var refusal = await RefusalAsync(Reader(log), "LINES", new Dictionary<string, string> { [filter] = value });

        Assert.Contains(Refusals.LogFilterUnknown, refusal);
        Assert.Contains($"filter={filter}", refusal);
        Assert.Contains($"value={value}", refusal);
    }

    [Fact]
    public async Task With_no_folder_yet_the_answer_is_empty_not_a_refusal()
    {
        using var log = Log();

        var answer = await AnswerAsync(Reader(log), "LINES");

        Assert.Empty(Events(answer));
        Assert.Equal(0, answer.GetProperty("total").GetInt32());
    }

    /// <summary>
    /// Open the folder: the log's own, made if it is not there yet. The page names no path — a page that
    /// chose what the shell opens would be a page choosing a folder on this machine.
    /// </summary>
    [Fact]
    public async Task Open_the_folder_opens_the_logs_own_folder_and_no_path_the_page_names()
    {
        using var log = Log();
        var opened = new List<string>();

        var answer = await AnswerAsync(Reader(log, opened.Add), "OPEN_FOLDER", new { path = "C:/elsewhere" });

        var folder = Path.Combine(Home, MachineLog.Folder);
        Assert.Equal([folder], opened);
        Assert.True(Directory.Exists(folder));
        Assert.True(answer.GetProperty("opened").GetBoolean());
        Assert.Equal(folder, answer.GetProperty("folder").GetString());
    }

    [Fact]
    public async Task With_no_way_to_open_a_folder_it_says_it_did_not()
    {
        using var log = Log();

        Assert.False((await AnswerAsync(Reader(log), "OPEN_FOLDER")).GetProperty("opened").GetBoolean());
    }

    [Fact]
    public async Task A_folder_the_system_would_not_open_is_refused_with_its_reason()
    {
        using var log = Log();

        var refusal = await RefusalAsync(
            Reader(log, _ => throw new System.ComponentModel.Win32Exception("the file manager is not there")), "OPEN_FOLDER");

        Assert.Contains(Refusals.LogFolderNotOpened, refusal);
        Assert.Contains("the file manager is not there", refusal);
    }
}
