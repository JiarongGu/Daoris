using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CODEXUSE1 (D125's CODEXUSE1 note; the Codex usage evidence): a Codex account's windows, asked of Codex's own app server
/// with <c>account/rateLimits/read</c>, which spends no model usage, and read into the same reading Claude Code's frames fill.
/// Pure and in process: the reader's table, and the protocol against a stand-in server that speaks the measured lines.
/// </summary>
/// <remarks>
/// The answer is the evidence's (§2), its ids left out and each reset placed against this test's own moment: no account, no
/// credit and no machine time is in it. The real spawn under an account's <c>CODEX_HOME</c> is <c>CodexUsageProcessTests</c>,
/// in the <c>Process</c> half.
/// </remarks>
public sealed class CodexUsageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset In(double hours) => DateTimeOffset.FromUnixTimeSeconds((Now + TimeSpan.FromHours(hours)).ToUnixTimeSeconds());

    private static long Seconds(double hours) => In(hours).ToUnixTimeSeconds();

    /// <summary>The recorded answer, its resets (<c>&lt;reset&gt;</c>, in order) placed <paramref name="hours"/> after now.</summary>
    internal static string Recorded(params double[] hours)
    {
        var placed = CodexUsage.Question.Recorded[0].Frame;
        foreach (var each in hours)
        {
            var at = placed.IndexOf("<reset>", StringComparison.Ordinal);
            placed = placed[..at] + Seconds(each) + placed[(at + "<reset>".Length)..];
        }

        return placed;
    }

    private static IReadOnlyList<WindowReading> Read(string answer) => CodexUsage.Read(JsonDocument.Parse(answer).RootElement.Clone());

    /// <summary>One window as the answer writes it.</summary>
    private static string Window(object? used, object? minutes, object? resets) =>
        "{" + string.Join(",", new[]
        {
            used is null ? null : $"\"usedPercent\":{Json(used)}",
            minutes is null ? null : $"\"windowDurationMins\":{Json(minutes)}",
            resets is null ? null : $"\"resetsAt\":{Json(resets)}",
        }.Where(part => part is not null)) + "}";

    private static string Json(object value) => value is string text ? text : JsonSerializer.Serialize(value);

    private static string Answer(string? primary, string? secondary = null) =>
        """{"rateLimits":{"limitId":"codex","primary":""" + (primary ?? "null") + ""","secondary":""" + (secondary ?? "null")
        + ""","rateLimitReachedType":null}}""";

    // ——— The table: every window length the reader names stands on a recorded answer, as an entry of D125's tables does.

    [Fact]
    public void Every_window_length_the_reader_names_stands_on_a_recorded_answer()
    {
        var recorded = CodexUsage.Question.Recorded;
        Assert.NotEmpty(recorded);
        Assert.All(recorded, frame => Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", frame.Seen));

        var minutes = recorded
            .Select(frame => JsonDocument.Parse(frame.Frame.Replace("<reset>", "0", StringComparison.Ordinal)).RootElement.GetProperty("rateLimits"))
            .SelectMany(limits => new[] { "primary", "secondary" }.Select(name => limits.GetProperty(name).GetProperty("windowDurationMins").GetInt32()))
            .ToHashSet();
        Assert.Contains(CodexUsage.SessionMinutes, minutes);
        Assert.Contains(CodexUsage.WeekMinutes, minutes);
        // No id of an account or a credit is kept in the record (the evidence's own rule).
        Assert.All(recorded, frame => Assert.DoesNotContain("accountId", frame.Frame, StringComparison.Ordinal));
    }

    [Fact]
    public void Codex_s_door_declares_Codex_s_own_app_server_asked_by_its_owner_s_pin()
    {
        var question = new CodexAcpAdapter().Toolchain!.Usage;

        Assert.Same(CodexUsage.Question, question);
        Assert.Equal("codex", question!.Harness);
        Assert.Equal(["codex", "app-server"], question.Command);
        Assert.Equal("account/rateLimits/read", CodexUsage.Method);
    }

    // ——— The reading (evidence §2): `windowDurationMins` names the window, `usedPercent` its use, `resetsAt` its reset.

    [Fact]
    public void The_recorded_answer_says_its_five_hour_window_as_the_session_and_its_week_as_weekly()
    {
        Assert.Equal(
            [
                new WindowReading("session", 0.01, In(3.5)),
                new WindowReading("weekly", 0.15, In(100.25)),
            ],
            Read(Recorded(3.5, 100.25)));
    }

    public static TheoryData<int, string> Lengths => new()
    {
        { 300, "session" },
        { 10080, "weekly" },
        // Any other length is named by its minutes, never guessed to be one of the two.
        { 1440, "1440-minute" },
        { 60, "60-minute" },
        { 301, "301-minute" },
    };

    [Theory]
    [MemberData(nameof(Lengths))]
    public void A_window_is_named_by_its_length_and_only_the_two_measured_lengths_take_Daoris_s_words(int minutes, string named)
    {
        Assert.Equal(named, CodexUsage.WindowOf(minutes));
        Assert.Equal([new WindowReading(named, 0.42, In(2))], Read(Answer(Window(42, minutes, Seconds(2)))));
    }

    public static TheoryData<string, string> Unread => new()
    {
        { "no rate limits", """{"accountId":"…","rateLimits":null}""" },
        { "no answer at all", "null" },
        { "not an object", "[1,2]" },
        { "rate limits not an object", """{"rateLimits":"codex"}""" },
        { "both windows null", Answer(null, null) },
        { "a window with no reset", Answer(Window(15, 10080, null)) },
        { "a window with no use", Answer(Window(null, 10080, Seconds(2))) },
        { "a window with no length", Answer(Window(15, null, Seconds(2))) },
        { "a length of nothing", Answer(Window(15, 0, Seconds(2))) },
        { "a negative length", Answer(Window(15, -300, Seconds(2))) },
        { "a fractional length", Answer(Window(15, 300.5, Seconds(2))) },
        { "a length in words", Answer(Window(15, "\"300\"", Seconds(2))) },
        { "a negative use", Answer(Window(-1, 300, Seconds(2))) },
        { "a use in words", Answer(Window("\"15\"", 300, Seconds(2))) },
        { "a reset in words", Answer(Window(15, 300, "\"soon\"")) },
        { "a reset no calendar holds", Answer(Window(15, 300, 1e300)) },
    };

    /// <summary>Absent is never zero (D57): a window the answer does not say whole says nothing, and the account stays unknown.</summary>
    [Theory]
    [MemberData(nameof(Unread))]
    public void What_does_not_read_is_nothing_said(string why, string answer)
    {
        Assert.True(Read(answer).Count == 0, why);
    }

    [Fact]
    public void One_window_unread_leaves_the_other_said()
    {
        Assert.Equal([new WindowReading("weekly", 0.15, In(9))], Read(Answer(Window(1, 300, null), Window(15, 10080, Seconds(9)))));
    }

    // ——— The protocol (evidence §1): one JSON object per line over stdio, the measured lines in the measured order.

    /// <summary>A stand-in app server: a handler turns each line the client sends into the lines it answers with.</summary>
    private sealed class FakeServer(Func<JsonElement, FakeServer, IEnumerable<string>> handle)
    {
        private readonly Channel<string> _toClient = Channel.CreateUnbounded<string>();

        public List<string> Sent { get; } = [];

        public TextReader Incoming => new ChannelReader(_toClient);

        public TextWriter Outgoing => new HandlerWriter(line =>
        {
            Sent.Add(line);
            foreach (var reply in handle(JsonDocument.Parse(line).RootElement.Clone(), this)) _toClient.Writer.TryWrite(reply);
        });

        public void Close() => _toClient.Writer.TryComplete();

        private sealed class ChannelReader(Channel<string> channel) : TextReader
        {
            public override async ValueTask<string?> ReadLineAsync(CancellationToken ct) =>
                await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false) && channel.Reader.TryRead(out var line) ? line : null;
        }

        private sealed class HandlerWriter(Action<string> onLine) : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;

            public override Task WriteLineAsync(string? value)
            {
                if (value is not null) onLine(value);
                return Task.CompletedTask;
            }
        }
    }

    private static string? MethodOf(JsonElement frame) => frame.TryGetProperty("method", out var method) ? method.GetString() : null;

    /// <summary>The server as measured: it answers <c>initialize</c>, takes <c>initialized</c>, pushes an update, then answers the read.</summary>
    private static IEnumerable<string> AsMeasured(JsonElement frame, FakeServer _) => MethodOf(frame) switch
    {
        "initialize" => ["""{"id":1,"result":{"userAgent":"codex_cli_rs/0.160.0"}}"""],
        "initialized" => [],
        "account/rateLimits/read" =>
        [
            "a line that is not JSON",
            """{"method":"account/rateLimits/updated","params":{"rateLimits":{"primary":{"usedPercent":99,"windowDurationMins":300,"resetsAt":1}}}}""",
            """{"id":7,"result":{"rateLimits":null}}""",
            """{"id":2,"result":""" + Recorded(3.5, 100.25) + "}",
        ],
        _ => [],
    };

    [Fact]
    public async Task The_server_is_asked_the_measured_lines_in_order_and_its_answer_is_read_past_its_pushes()
    {
        var server = new FakeServer(AsMeasured);

        var readings = await CodexUsage.AskWithinAsync(server.Incoming, server.Outgoing, TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.Equal(
            [
                """{"id":1,"method":"initialize","params":{"clientInfo":{"name":"daoris-driver","version":"0"}}}""",
                """{"method":"initialized"}""",
                """{"id":2,"method":"account/rateLimits/read","params":null}""",
            ],
            server.Sent);
        Assert.Equal([new WindowReading("session", 0.01, In(3.5)), new WindowReading("weekly", 0.15, In(100.25))], readings);
    }

    [Fact]
    public async Task A_refused_read_is_unknown()
    {
        var server = new FakeServer((frame, _) => MethodOf(frame) switch
        {
            "initialize" => ["""{"id":1,"result":{}}"""],
            "account/rateLimits/read" => ["""{"id":2,"error":{"code":-32600,"message":"codex account authentication required to read rate limits"}}"""],
            _ => [],
        });

        Assert.Null(await CodexUsage.AskWithinAsync(server.Incoming, server.Outgoing, TimeSpan.FromSeconds(10), CancellationToken.None));
    }

    [Fact]
    public async Task A_refused_handshake_is_unknown_and_nothing_more_is_asked()
    {
        var server = new FakeServer((frame, _) => MethodOf(frame) == "initialize"
            ? ["""{"id":1,"error":{"code":-32603,"message":"Internal error"}}"""]
            : []);

        Assert.Null(await CodexUsage.AskWithinAsync(server.Incoming, server.Outgoing, TimeSpan.FromSeconds(10), CancellationToken.None));
        Assert.Single(server.Sent);
    }

    [Fact]
    public async Task A_server_that_never_answers_is_unknown_once_its_patience_is_spent()
    {
        var server = new FakeServer((_, _) => []);
        var timer = System.Diagnostics.Stopwatch.StartNew();

        var readings = await CodexUsage.AskWithinAsync(server.Incoming, server.Outgoing, TimeSpan.FromMilliseconds(200), CancellationToken.None);

        Assert.Null(readings);
        Assert.InRange(timer.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task A_server_that_ends_before_its_answer_is_unknown()
    {
        var server = new FakeServer((frame, self) =>
        {
            if (MethodOf(frame) == "initialize") return ["""{"id":1,"result":{}}"""];
            self.Close();
            return [];
        });

        Assert.Null(await CodexUsage.AskWithinAsync(server.Incoming, server.Outgoing, TimeSpan.FromSeconds(10), CancellationToken.None));
    }

    [Fact]
    public async Task An_answer_with_no_windows_is_nothing_said()
    {
        var server = new FakeServer((frame, _) => MethodOf(frame) switch
        {
            "initialize" => ["""{"id":1,"result":{}}"""],
            // An account the plan's windows do not cover (an API key's) answers with none.
            "account/rateLimits/read" => ["""{"id":2,"result":{"rateLimits":null}}"""],
            _ => [],
        });

        Assert.Equal([], await CodexUsage.AskWithinAsync(server.Incoming, server.Outgoing, TimeSpan.FromSeconds(10), CancellationToken.None) ?? []);
    }

    [Fact]
    public async Task The_caller_s_own_stop_is_its_own_and_not_an_unknown_reading()
    {
        var server = new FakeServer((_, _) => []);
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CodexUsage.AskWithinAsync(server.Incoming, server.Outgoing, TimeSpan.FromMinutes(1), stop.Token));
    }
}
