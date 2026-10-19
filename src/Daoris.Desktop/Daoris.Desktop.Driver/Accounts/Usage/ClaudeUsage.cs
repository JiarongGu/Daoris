using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Claude Code's windows, asked of the native CLI (USAGEREAD2a, D165 point 1; the usage-read evidence): <c>claude</c> in
/// stream-json mode under the account's <c>CLAUDE_CONFIG_DIR</c> answers a <c>get_usage</c> control request with the plan's
/// whole use, with no prompt and no model request. Read into the same <see cref="WindowReading"/> a session's frame fills, so
/// the walk, the page and the rest read an account's real use, not the part a session of Daoris's last said.
/// </summary>
/// <remarks>
/// <para><b>Absent is never zero</b> (D57): a binary not there, a home not signed in (<c>rate_limits_available: false</c>), an
/// error reply, a CLI silent past <see cref="Patience"/> and an answer of another shape all leave the account unknown, and
/// nothing is kept. The request is declared experimental, so the answer is read defensively, field by field.</para>
/// <para><b>Never the server's judgement or the account's money</b>: <c>limits[].severity</c>, usage credits and the session's
/// own totals are not read (design §1.3: the maker's word is recorded, never acted on alone).</para>
/// <para><b>Whether a read disturbs a session already running on the account is unmeasured</b> (evidence, what this does not
/// establish), so <see cref="HarnessRoster"/> asks it only while none of Daoris's runs there, as it does Codex's.</para>
/// </remarks>
public static class ClaudeUsage
{
    /// <summary>How long one read may take, the start included: the evidence's answers took 1.5 to 2.6 s.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>The least time between two reads of one account: a floor, beside Codex's five-minute freshness.</summary>
    public static readonly TimeSpan Floor = TimeSpan.FromMinutes(1);

    /// <summary>The two windows' lengths: the soonest a window not yet opened could turn over.</summary>
    public static readonly TimeSpan SessionLength = TimeSpan.FromHours(5), WeekLength = TimeSpan.FromDays(7);

    private const string InitializeLine = """{"type":"control_request","request_id":"init","request":{"subtype":"initialize"}}""";
    private const string UsageLine = """{"type":"control_request","request_id":"usage","request":{"subtype":"get_usage","skip_behaviors":true}}""";

    /// <summary>The question Claude's native door declares for its owner (usage-read evidence, the request and its answer).</summary>
    public static UsageQuestion Question { get; } = new(
        "claude-code",
        ["claude", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose", "--strict-mcp-config"],
        [
            new(
                """{"subscription_type":"team","rate_limits_available":true,"rate_limits":{"five_hour":{"utilization":23,"resets_at":"<reset>","limit_dollars":null,"used_dollars":null,"remaining_dollars":null,"locked_reason":null},"seven_day":{"utilization":79,"resets_at":"<reset>","limit_dollars":null,"used_dollars":null,"remaining_dollars":null,"locked_reason":null},"seven_day_opus":null,"seven_day_sonnet":null,"limits":[]},"session":{"total_cost_usd":0}}""",
                "2026-10-12",
                "claude 2.1.296's response to get_usage with skip_behaviors, over stream-json outside Daoris's driver (the usage-read evidence); the server's limit rows, usage credits and the account's plan words left out, each reset written <reset>",
                "2.1.296"),
        ],
        UsageKind.Claude);

    /// <summary>The question's name as the usage history writes a reading's source (USAGECAP1a1).</summary>
    public const string Source = "get_usage";

    /// <summary>The version of <see cref="Read"/>'s history parse, which each reading it keeps carries (USAGECAP1a1).</summary>
    public const int HistoryReader = 1;

    /// <summary>
    /// What the answer's body says (evidence's field table): <c>rate_limits.five_hour</c> as <c>session</c> and
    /// <c>.seven_day</c> as <c>weekly</c>, each <c>utilization</c> (a whole percent) as a fraction and each <c>resets_at</c>
    /// (ISO 8601). A window not yet opened answers a null reset at 0%: it is 0 used and resets a window's length after
    /// <paramref name="now"/>, the soonest it could turn over, so it is dropped then like any reading. Nothing where the home is
    /// not signed in, or a window is not said whole.
    /// </summary>
    /// <remarks>
    /// Beside the walk's, what the usage history keeps (USAGECAP1a1, D179 point 2): every window of <c>rate_limits</c> said in
    /// part or whole, the scoped weeks among them (<c>seven_day_opus</c>), each reset marked said or made up; the server's
    /// rows (<c>limits[]</c>: <c>kind</c>, <c>scope</c>, <c>percent</c>, <c>resets_at</c>, the evidence's field table) by their
    /// kind and scope, never their severity; and the plan's word (<c>subscription_type</c>). Usage credits and spend
    /// (<c>extra_usage</c>, <c>spend</c>) stay unread.
    /// </remarks>
    public static UsageAnswer Read(JsonElement body, DateTimeOffset now)
    {
        if (body.ValueKind != JsonValueKind.Object
            || !body.TryGetProperty("rate_limits_available", out var available) || available.ValueKind != JsonValueKind.True
            || !body.TryGetProperty("rate_limits", out var limits) || limits.ValueKind != JsonValueKind.Object)
        {
            return UsageAnswer.None(Source, HistoryReader);
        }

        var readings = new List<WindowReading>();
        foreach (var (name, window, length) in new[] { ("five_hour", "session", SessionLength), ("seven_day", AccountWindows.Weekly, WeekLength) })
        {
            if (!limits.TryGetProperty(name, out var each) || each.ValueKind != JsonValueKind.Object) continue;
            if (!each.TryGetProperty("utilization", out var used) || used.ValueKind != JsonValueKind.Number
                || !used.TryGetDouble(out var percent) || !double.IsFinite(percent) || percent < 0 || percent > 100)
            {
                continue;
            }

            DateTimeOffset reset;
            if (!each.TryGetProperty("resets_at", out var at) || at.ValueKind == JsonValueKind.Null)
            {
                // Not yet opened: a window with no reset and any use is not one this read can place.
                if (percent != 0) continue;
                reset = now + length;
            }
            else if (Moments.Read(each, "resets_at") is { } read)
            {
                reset = read;
            }
            else
            {
                continue;
            }

            readings.Add(new WindowReading(window, percent / 100, reset));
        }

        return new UsageAnswer(readings, Seen(limits, now), Json.NonEmpty(body, "subscription_type"), Source, HistoryReader);
    }

    /// <summary>The answer's windows and the server's rows, as the usage history keeps them (<see cref="Read"/>'s remarks).</summary>
    private static List<WindowSeen> Seen(JsonElement limits, DateTimeOffset now)
    {
        var seen = new List<WindowSeen>();
        foreach (var each in limits.EnumerateObject())
        {
            if (each.Value.ValueKind != JsonValueKind.Object || each.Name is "extra_usage" or "spend") continue;
            var (used, precision) = Percent(each.Value, "utilization");
            var (minutes, scope) = ClaudeWindows.Shape(each.Name);
            var walk = ClaudeWindows.Words.Windows.GetValueOrDefault(each.Name);
            DateTimeOffset? reset = Moments.Read(each.Value, "resets_at");
            var from = reset is null ? null : UsageHistory.ResetSaid;
            if (reset is null && walk is not null && used == 0
                && (!each.Value.TryGetProperty("resets_at", out var at) || at.ValueKind == JsonValueKind.Null))
            {
                // As the walk reads it: a window not yet opened turns over a window's length from now at the soonest.
                reset = now + (minutes == SessionLength.TotalMinutes ? SessionLength : WeekLength);
                from = UsageHistory.ResetMadeUp;
            }

            Add(new WindowSeen(each.Name, walk, scope, minutes, used, precision, reset, from));
        }

        if (limits.TryGetProperty("limits", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object || Json.NonEmpty(row, "kind") is not { } kind) continue;
                var (used, precision) = Percent(row, "percent");
                var reset = Moments.Read(row, "resets_at");
                var minutes = kind == "session" ? (int)SessionLength.TotalMinutes
                    : kind.StartsWith("weekly", StringComparison.Ordinal) ? (int)WeekLength.TotalMinutes : (int?)null;
                Add(new WindowSeen(
                    $"limits:{kind}", null, Json.NonEmpty(row, "scope"), minutes, used, precision, reset,
                    reset is null ? null : UsageHistory.ResetSaid));
            }
        }

        return seen;

        // A window says something with a use or a reset; one said twice is its last.
        void Add(WindowSeen window)
        {
            if (window.Used is null && window.Reset is null) return;
            seen.RemoveAll(each => each.Window == window.Window && each.Scope == window.Scope);
            seen.Add(window);
        }
    }

    /// <summary>A whole percent from 0 to 100 as a fraction, with the precision it was written to; nulls where it is not one.</summary>
    private static (double? Used, double? Precision) Percent(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var percent) && double.IsFinite(percent) && percent is >= 0 and <= 100
            ? (percent / 100, UsageHistory.Precision(value, 100))
            : (null, null);

    /// <summary>
    /// The read over the CLI's lines (evidence, the request): <c>initialize</c>, its reply, then <c>get_usage</c>, whose reply
    /// is read past whatever else the CLI writes. Unknown (null) where either is an error reply, the CLI ends, or is silent
    /// past <paramref name="patience"/>; <paramref name="ct"/> is the caller's own stop, and throws.
    /// </summary>
    internal static async Task<IReadOnlyList<WindowReading>?> AskWithinAsync(
        TextReader incoming, TextWriter outgoing, TimeSpan patience, DateTimeOffset now, CancellationToken ct)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(patience);
        // Waited on apart from the read itself: a pipe's read may not honour a token, and is ended by the CLI's stop.
        var asking = AskAsync(incoming, outgoing, bounded.Token);
        try
        {
            return await asking.WaitAsync(bounded.Token).ConfigureAwait(false) is { } body ? Read(body, now) : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (IOException)
        {
            // The CLI's end of the pipe closed under a write or a read: it answered nothing.
            return null;
        }
        finally
        {
            _ = asking.ContinueWith(left => _ = left.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }

    private static async Task<JsonElement?> AskAsync(TextReader incoming, TextWriter outgoing, CancellationToken ct)
    {
        await SendAsync(outgoing, InitializeLine).ConfigureAwait(false);
        if (await AnswerAsync(incoming, "init", ct).ConfigureAwait(false) is null) return null;

        await SendAsync(outgoing, UsageLine).ConfigureAwait(false);
        return await AnswerAsync(incoming, "usage", ct).ConfigureAwait(false);
    }

    private static async Task SendAsync(TextWriter outgoing, string line)
    {
        await outgoing.WriteLineAsync(line).ConfigureAwait(false);
        await outgoing.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The <c>response</c> of the <c>control_response</c> to <paramref name="requestId"/>: null where it is an error or the CLI
    /// ended first. Any other line (not JSON, a session frame, a reply to another id) is read past.
    /// </summary>
    private static async Task<JsonElement?> AnswerAsync(TextReader incoming, string requestId, CancellationToken ct)
    {
        while (await incoming.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            JsonElement frame;
            try
            {
                using var document = JsonDocument.Parse(line);
                frame = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }

            if (frame.ValueKind != JsonValueKind.Object
                || !frame.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "control_response"
                || !frame.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object
                || !response.TryGetProperty("request_id", out var id) || id.ValueKind != JsonValueKind.String || id.GetString() != requestId)
            {
                continue;
            }

            if (!response.TryGetProperty("subtype", out var subtype) || subtype.ValueKind != JsonValueKind.String || subtype.GetString() != "success") return null;
            return response.TryGetProperty("response", out var body) ? body : null;
        }

        return null;
    }
}
