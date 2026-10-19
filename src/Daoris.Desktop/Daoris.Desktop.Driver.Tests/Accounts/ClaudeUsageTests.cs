using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// USAGEREAD2a (D165 point 1; the usage-read evidence): a Claude Code account's windows, asked of the native CLI with the
/// <c>get_usage</c> control request, which spends no model usage, and read into the reading its frames fill. Pure and in
/// process: the reader's table, and the protocol against a stand-in CLI that speaks the measured lines. The real spawn under
/// an account's <c>CLAUDE_CONFIG_DIR</c> is <c>ClaudeUsageProcessTests</c>, in the <c>Process</c> half.
/// </summary>
public sealed class ClaudeUsageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    private static string At(double hours) => (Now + TimeSpan.FromHours(hours)).ToString("yyyy-MM-dd'T'HH:mm:ss.ffffffzzz");

    /// <summary>The evidence's answer body (shape only: no account, plan dollar or id), resets placed against this test's moment.</summary>
    private static string Body(string fiveHour, string sevenDay) =>
        $$$"""{"subscription_type":"team","rate_limits_available":true,"rate_limits":{"five_hour":{{{fiveHour}}},"seven_day":{{{sevenDay}}},"seven_day_opus":null,"seven_day_sonnet":null,"limits":[{"kind":"session","percent":23,"severity":"normal"}],"extra_usage":null},"session":{"total_cost_usd":0}}""";

    private static string Window(double percent, string? resets) =>
        $$"""{"utilization":{{percent.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"resets_at":{{(resets is null ? "null" : "\"" + resets + "\"")}},"limit_dollars":null,"used_dollars":null,"remaining_dollars":null,"locked_reason":null}""";

    private static IReadOnlyList<WindowReading> Read(string body) =>
        ClaudeUsage.Read(JsonDocument.Parse(body).RootElement.Clone(), Now);

    private static DateTimeOffset Parsed(double hours) => DateTimeOffset.Parse(At(hours), System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void Claude_s_native_door_declares_the_measured_command_asked_of_its_own_pin_and_a_recorded_answer()
    {
        var question = new ClaudeCodeAdapter().Toolchain!.Usage;

        Assert.Same(ClaudeUsage.Question, question);
        Assert.Equal("claude-code", question!.Harness);
        Assert.Equal(
            ["claude", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose", "--strict-mcp-config"],
            question.Command);
        Assert.NotEmpty(question.Recorded);
        Assert.Equal(TimeSpan.FromSeconds(10), ClaudeUsage.Patience);
        Assert.Equal(TimeSpan.FromMinutes(1), ClaudeUsage.Floor);
        Assert.Equal(ClaudeUsage.Floor, question.Fresh);
        Assert.Equal(CodexUsage.Fresh, CodexUsage.Question.Fresh);
    }

    [Fact]
    public void The_five_hour_window_is_the_session_and_the_seven_day_is_weekly_a_whole_percent_as_a_fraction()
    {
        var readings = Read(Body(Window(23, At(3.5)), Window(79, At(100.25))));

        Assert.Equal([new WindowReading("session", 0.23, Parsed(3.5)), new WindowReading("weekly", 0.79, Parsed(100.25))], readings);
    }

    [Fact]
    public void A_window_not_yet_opened_is_zero_used_and_resets_a_window_s_length_after_the_read()
    {
        var readings = Read(Body(Window(0, null), Window(0, null)));

        Assert.Equal(
            [new WindowReading("session", 0, Now + TimeSpan.FromHours(5)), new WindowReading("weekly", 0, Now + TimeSpan.FromDays(7))],
            readings);
    }

    [Fact]
    public void A_null_reset_on_a_window_that_is_used_says_nothing_of_it()
    {
        Assert.Equal([new WindowReading("weekly", 0.5, Parsed(9))], Read(Body(Window(10, null), Window(50, At(9)))));
    }

    public static TheoryData<string, string> Unread => new()
    {
        { "not available", """{"subscription_type":null,"rate_limits_available":false,"rate_limits":{"five_hour":null,"seven_day":null}}""" },
        { "available but no limits", """{"rate_limits_available":true}""" },
        { "limits not an object", """{"rate_limits_available":true,"rate_limits":"x"}""" },
        { "no availability said", """{"rate_limits":{"five_hour":{"utilization":3,"resets_at":"2026-10-12T15:00:00+00:00"}}}""" },
        { "not an object", "[1]" },
        { "both windows null", Body("null", "null") },
        { "a use in words", Body("{\"utilization\":\"23\",\"resets_at\":\"2026-10-12T15:00:00+00:00\"}", "null") },
        { "no use", Body("{\"resets_at\":\"2026-10-12T15:00:00+00:00\"}", "null") },
        { "a negative use", Body(Window(-1, "2026-10-12T15:00:00+00:00"), "null") },
        { "a use above a hundred", Body(Window(101, "2026-10-12T15:00:00+00:00"), "null") },
        { "a reset in words", Body(Window(5, "soon"), "null") },
    };

    /// <summary>Absent is never zero (D57): a window the answer does not say whole says nothing.</summary>
    [Theory]
    [MemberData(nameof(Unread))]
    public void What_does_not_read_is_nothing_said(string why, string body)
    {
        Assert.True(Read(body).Count == 0, why);
    }

    // ——— What the usage history keeps of an answer (USAGECAP1a1, D179 point 2): a parse beside the walk's, every window the
    // answer says whole or in part, the server's rows (the evidence's field table), the plan's word, and where each reset came from.

    private static UsageAnswer Answer(string body) => ClaudeUsage.Read(JsonDocument.Parse(body).RootElement.Clone(), Now);

    /// <summary>The recorded answer, its resets (<c>&lt;reset&gt;</c>, in order) placed <paramref name="hours"/> after now.</summary>
    private static string Recorded(params double[] hours)
    {
        var placed = ClaudeUsage.Question.Recorded[0].Frame;
        foreach (var each in hours)
        {
            var at = placed.IndexOf("<reset>", StringComparison.Ordinal);
            placed = placed[..at] + At(each) + placed[(at + "<reset>".Length)..];
        }

        return placed;
    }

    [Fact]
    public void The_recorded_answer_is_kept_for_the_history_with_its_plan_as_get_usage_read_it()
    {
        var answer = Answer(Recorded(3.5, 100.25));

        // The walk's readings are the ones it always had.
        Assert.Equal([new WindowReading("session", 0.23, Parsed(3.5)), new WindowReading("weekly", 0.79, Parsed(100.25))], answer);
        Assert.Equal(
            [
                new WindowSeen("five_hour", "session", null, 300, 0.23, 0.01, Parsed(3.5), UsageHistory.ResetSaid),
                new WindowSeen("seven_day", "weekly", null, 10080, 0.79, 0.01, Parsed(100.25), UsageHistory.ResetSaid),
            ],
            answer.Seen);
        Assert.Equal("team", answer.Plan);
        Assert.Equal("get_usage", answer.Source);
        Assert.Equal(1, answer.Reader);
    }

    [Fact]
    public void A_reset_made_up_for_a_window_not_yet_opened_is_marked_made_up()
    {
        var answer = Answer(Body(Window(0, null), Window(4, At(9))));

        Assert.Equal([new WindowReading("session", 0, Now + TimeSpan.FromHours(5)), new WindowReading("weekly", 0.04, Parsed(9))], answer);
        Assert.Equal(
            [
                new WindowSeen("five_hour", "session", null, 300, 0, 0.01, Now + TimeSpan.FromHours(5), UsageHistory.ResetMadeUp),
                new WindowSeen("seven_day", "weekly", null, 10080, 0.04, 0.01, Parsed(9), UsageHistory.ResetSaid),
                // The body's own server row, which names no reset: none is made up for it.
                new WindowSeen("limits:session", null, null, 300, 0.23, 0.01, null, null),
            ],
            answer.Seen);
    }

    [Fact]
    public void Scoped_weeks_and_the_server_s_rows_are_kept_for_the_history_while_the_walk_reads_its_two()
    {
        var body = $$$"""
            {"subscription_type":"max","rate_limits_available":true,"rate_limits":{
              "five_hour":{{{Window(23, At(3.5))}}},"seven_day":{{{Window(79, At(100.25))}}},
              "seven_day_opus":{{{Window(40.5, At(100.25))}}},"seven_day_sonnet":null,"seven_day_oauth_apps":{{{Window(10, null)}}},
              "limits":[
                {"kind":"session","group":"g","percent":23,"severity":"normal","resets_at":"{{{At(3.5)}}}","is_active":true},
                {"kind":"weekly_scoped","group":"g","percent":40,"severity":"warning","resets_at":"{{{At(100.25)}}}","scope":"opus","is_active":true},
                {"kind":"weekly_scoped","percent":"40"},
                "a row in words"],
              "extra_usage":{"utilization":50,"resets_at":"{{{At(1)}}}","is_enabled":true},
              "spend":{"utilization":5}},
             "session":{"total_cost_usd":0}}
            """;

        var answer = Answer(body);

        Assert.Equal([new WindowReading("session", 0.23, Parsed(3.5)), new WindowReading("weekly", 0.79, Parsed(100.25))], answer);
        Assert.Equal(
            [
                new WindowSeen("five_hour", "session", null, 300, 0.23, 0.01, Parsed(3.5), UsageHistory.ResetSaid),
                new WindowSeen("seven_day", "weekly", null, 10080, 0.79, 0.01, Parsed(100.25), UsageHistory.ResetSaid),
                new WindowSeen("seven_day_opus", null, "opus", 10080, 0.405, 0.001, Parsed(100.25), UsageHistory.ResetSaid),
                // A scoped week with a use and no reset is kept with none: no reset is made up beyond the walk's two.
                new WindowSeen("seven_day_oauth_apps", null, "oauth_apps", 10080, 0.1, 0.01, null, null),
                new WindowSeen("limits:session", null, null, 300, 0.23, 0.01, Parsed(3.5), UsageHistory.ResetSaid),
                new WindowSeen("limits:weekly_scoped", null, "opus", 10080, 0.4, 0.01, Parsed(100.25), UsageHistory.ResetSaid),
            ],
            answer.Seen);
        Assert.Equal("max", answer.Plan);
    }

    [Fact]
    public void A_not_signed_in_answer_keeps_nothing_for_the_history()
    {
        var answer = Answer("""{"subscription_type":null,"rate_limits_available":false,"rate_limits":{"five_hour":null,"seven_day":null}}""");

        Assert.Empty(answer);
        Assert.Empty(answer.Seen);
        Assert.Null(answer.Plan);
    }

    // ——— The protocol (evidence lines 38-39): two control requests, the second once the first's reply arrives.

    private sealed class FakeCli(Func<JsonElement, FakeCli, IEnumerable<string>> handle)
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

    private static string? Subtype(JsonElement frame) => frame.GetProperty("request").GetProperty("subtype").GetString();

    private static string Success(string id, string body) =>
        $$$"""{"type":"control_response","response":{"subtype":"success","request_id":"{{{id}}}","response":{{{body}}}}}""";

    private static IEnumerable<string> AsMeasured(JsonElement frame, FakeCli _) => Subtype(frame) switch
    {
        "initialize" => [Success("init", "{}")],
        "get_usage" =>
        [
            "a line that is not JSON",
            """{"type":"system","subtype":"init"}""",
            Success("other", """{"rate_limits_available":false}"""),
            Success("usage", Body(Window(23, At(3.5)), Window(79, At(100.25)))),
        ],
        _ => [],
    };

    private static Task<IReadOnlyList<WindowReading>?> Ask(FakeCli cli, TimeSpan? patience = null, CancellationToken ct = default) =>
        ClaudeUsage.AskWithinAsync(cli.Incoming, cli.Outgoing, patience ?? TimeSpan.FromSeconds(10), Now, ct);

    [Fact]
    public async Task The_measured_lines_are_written_in_order_and_the_answer_is_read_past_other_frames_and_other_ids()
    {
        var cli = new FakeCli(AsMeasured);

        var readings = await Ask(cli);

        Assert.Equal(
            [
                """{"type":"control_request","request_id":"init","request":{"subtype":"initialize"}}""",
                """{"type":"control_request","request_id":"usage","request":{"subtype":"get_usage","skip_behaviors":true}}""",
            ],
            cli.Sent);
        Assert.Equal([new WindowReading("session", 0.23, Parsed(3.5)), new WindowReading("weekly", 0.79, Parsed(100.25))], readings);
    }

    [Fact]
    public async Task A_not_signed_in_answer_is_nothing_said()
    {
        var cli = new FakeCli((frame, _) => Subtype(frame) switch
        {
            "initialize" => [Success("init", "{}")],
            "get_usage" => [Success("usage", """{"subscription_type":null,"rate_limits_available":false,"rate_limits":null}""")],
            _ => [],
        });

        Assert.Equal([], await Ask(cli) ?? []);
    }

    [Fact]
    public async Task An_error_reply_is_unknown()
    {
        var cli = new FakeCli((frame, _) => Subtype(frame) switch
        {
            "initialize" => [Success("init", "{}")],
            "get_usage" => ["""{"type":"control_response","response":{"subtype":"error","request_id":"usage","error":"not supported"}}"""],
            _ => [],
        });

        Assert.Null(await Ask(cli));
    }

    [Fact]
    public async Task A_refused_initialize_is_unknown_and_nothing_more_is_asked()
    {
        var cli = new FakeCli((frame, _) => Subtype(frame) == "initialize"
            ? ["""{"type":"control_response","response":{"subtype":"error","request_id":"init","error":"no"}}"""]
            : []);

        Assert.Null(await Ask(cli));
        Assert.Single(cli.Sent);
    }

    [Fact]
    public async Task A_cli_that_never_answers_is_unknown_once_its_patience_is_spent()
    {
        var cli = new FakeCli((_, _) => []);
        var timer = System.Diagnostics.Stopwatch.StartNew();

        Assert.Null(await Ask(cli, TimeSpan.FromMilliseconds(200)));
        Assert.InRange(timer.Elapsed, TimeSpan.Zero, Poll.Patience);
    }

    [Fact]
    public async Task A_cli_that_ends_before_its_answer_is_unknown()
    {
        var cli = new FakeCli((frame, self) =>
        {
            if (Subtype(frame) == "initialize") return [Success("init", "{}")];
            self.Close();
            return [];
        });

        Assert.Null(await Ask(cli));
    }

    [Fact]
    public async Task The_caller_s_own_stop_is_its_own_and_not_an_unknown_reading()
    {
        var cli = new FakeCli((_, _) => []);
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Ask(cli, TimeSpan.FromMinutes(1), stop.Token));
    }
}
