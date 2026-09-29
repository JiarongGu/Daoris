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

        Assert.Contains("NO_ROUTE", await RefusalAsync(new LogModule(log), "READ"));
    }
}
