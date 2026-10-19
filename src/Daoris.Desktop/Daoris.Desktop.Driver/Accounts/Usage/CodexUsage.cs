using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// How an agent is asked an account's windows where its door carries none (CODEXUSE1, D125's CODEXUSE1 note): its own
/// server, started under the account's configuration home, asked one question and stopped. Declared on the toolchain as
/// <see cref="HarnessToolchain.Usage"/>, beside <see cref="HarnessToolchain.Windows"/>, which reads a door's frame instead.
/// </summary>
/// <param name="Harness">
/// The harness whose binary <paramref name="Command"/> names, resolved as every spawn is (D57): <c>driver.json</c>'s command
/// for it, then its managed pin, then <c>PATH</c>.
/// </param>
/// <param name="Command">The binary and the arguments that start the server.</param>
/// <param name="Recorded">The answers the reader was written against: an entry grows only with a recorded answer.</param>
/// <param name="Kind">Which protocol the command speaks (USAGEREAD2a): Codex's JSON-RPC by default.</param>
public sealed record UsageQuestion(
    string Harness, IReadOnlyList<string> Command, IReadOnlyList<RecordedFrame> Recorded, UsageKind Kind = UsageKind.Codex)
{
    /// <summary>
    /// How long a reading stands before a start asks the account again: Codex's five minutes, and Claude's minute, a floor
    /// beside it (USAGEREAD2a) because its read is its only whole-account reading and a walk ranks on it.
    /// </summary>
    public TimeSpan Fresh => Kind == UsageKind.Claude ? ClaudeUsage.Floor : CodexUsage.Fresh;
}

/// <summary>The protocol a <see cref="UsageQuestion"/>'s command speaks (USAGEREAD2a).</summary>
public enum UsageKind
{
    /// <summary><c>codex app-server</c>: JSON-RPC 2.0 over stdio (<see cref="CodexUsage"/>).</summary>
    Codex,

    /// <summary>The native <c>claude</c>'s stream-json control requests (<see cref="ClaudeUsage"/>).</summary>
    Claude,
}

/// <summary>
/// What a <see cref="UsageQuestion"/> answered (USAGECAP1a1, D179 point 2): the walk's readings, as every caller always had
/// them, and beside them what the usage history keeps: every window the answer said (<see cref="Seen"/>), the plan's word where
/// the answer names it, the question asked and its reader's version. A caller that reads the walk's alone is unchanged.
/// </summary>
public sealed class UsageAnswer(
    IReadOnlyList<WindowReading> walk, IReadOnlyList<WindowSeen> seen, string? plan, string source, int reader)
    : IReadOnlyList<WindowReading>
{
    /// <summary>An answer that said nothing: no reading, nothing for the history.</summary>
    public static UsageAnswer None(string source, int reader) => new([], [], null, source, reader);

    /// <summary>Every window the answer said, read beside the walk's own (<see cref="WindowSeen"/>).</summary>
    public IReadOnlyList<WindowSeen> Seen => seen;

    /// <summary>The plan's word as the answer said it (Claude's <c>subscription_type</c>), or null: never who, nor the organisation.</summary>
    public string? Plan => plan;

    /// <summary>The question asked: <c>get_usage</c>, or Codex's <see cref="CodexUsage.Method"/>.</summary>
    public string Source => source;

    /// <summary>The version of the reader that parsed it.</summary>
    public int Reader => reader;

    public WindowReading this[int index] => walk[index];

    public int Count => walk.Count;

    public IEnumerator<WindowReading> GetEnumerator() => walk.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Codex's windows, asked of its own app server (CODEXUSE1; the Codex usage evidence): <c>codex app-server</c> speaks
/// JSON-RPC 2.0 over stdio, one object per line, and <c>account/rateLimits/read</c> answers the account's windows and spends
/// no model usage. Read into the same <see cref="WindowReading"/> Claude Code's frames fill, so near, pace, the roster and the
/// page read a Codex account as they read a Claude Code one.
/// </summary>
/// <remarks>
/// <para><b>Asked, because no door carries it</b>: <c>codex-acp</c> keeps <c>account/rateLimits/updated</c> for its own
/// <c>/status</c> text and forwards none of it (limit-signals evidence §4), and <c>codex exec --json</c> carries only a turn's
/// tokens (usage evidence §3). The question is the agent's own binary's, under the account's own sign-in: Daoris holds no
/// credential and calls no provider, as with the status question it already asks there (D49 §4).</para>
/// <para><b>Absent is never zero</b> (D57): a binary not there, a refused read, a server that ends or is silent past
/// <see cref="Patience"/>, and an answer with no window read whole all leave the account unknown, and nothing is kept.</para>
/// <para><b>Never the agent's words, an account's id or a credit</b>: the answer's account id, plan, credits and reset credits
/// are not read, and no reset credit is ever redeemed. A limit's own id (<c>limitId</c>) is read as its windows' scope for the
/// usage history (USAGECAP1a1).</para>
/// </remarks>
public static class CodexUsage
{
    /// <summary>The request that reads an account's windows (usage evidence §1).</summary>
    public const string Method = "account/rateLimits/read";

    /// <summary>The two window lengths the evidence recorded, in minutes: five hours, and a week.</summary>
    public const int SessionMinutes = 300, WeekMinutes = 10080;

    /// <summary>
    /// How long one read may take, the server's start included, before it is no answer: under a status question's 20
    /// seconds, since it is asked under the same account lock (<see cref="ProbeLock"/>), whose stale age is three of those.
    /// </summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How long a reading stands before a start asks the account again, and how long one that could not be had waits: a
    /// floor as of when it was said, as a door's frame is, so asked at most once in this long per account, never at every
    /// start.
    /// </summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromMinutes(5);

    /// <summary>The question Codex's door declares for its owner: Codex's own app server (usage evidence §1, §2).</summary>
    public static UsageQuestion Question { get; } = new(
        "codex",
        ["codex", "app-server"],
        [
            new(
                """{"ordinaryUsageAllowed":true,"rateLimits":{"limitId":"codex","primary":{"usedPercent":1,"windowDurationMins":300,"resetsAt":<reset>},"secondary":{"usedPercent":15,"windowDurationMins":10080,"resetsAt":<reset>},"spendControlReached":false,"rateLimitReachedType":null}}""",
                "2026-10-07",
                "codex app-server's answer to account/rateLimits/read, over stdio outside Daoris's driver (the Codex usage evidence §2); the account id, the plan, the credits, the reset credits and the upsell left out, each reset written <reset>",
                "0.160.0"),
        ]);

    /// <summary>
    /// Daoris's name for a window of <paramref name="minutes"/>: <c>session</c> for five hours and <c>weekly</c> for a week,
    /// the two the evidence recorded; any other length by its minutes (<c>1440-minute</c>), never guessed to be either.
    /// </summary>
    public static string WindowOf(int minutes) => minutes switch
    {
        SessionMinutes => "session",
        WeekMinutes => AccountWindows.Weekly,
        _ => $"{minutes.ToString(CultureInfo.InvariantCulture)}-minute",
    };

    /// <summary>
    /// What the read's answer says (usage evidence §2): its <c>rateLimits</c>' <c>primary</c> and <c>secondary</c> windows,
    /// each named by its length, its use (<c>usedPercent</c>, a percent) as a fraction, and its reset (<c>resetsAt</c>, Unix
    /// seconds). A window not said whole (its length, its use and its reset) says nothing. No standing and no credits: the
    /// answer's words for those were not recorded with any value but none.
    /// </summary>
    /// <remarks>
    /// <para>Only <c>rateLimits</c>, the account's own limit (<c>limitId: codex</c>): <c>rateLimitsByLimitId</c> repeats it, and
    /// any other limit there would name windows of the same lengths.</para>
    /// <para>Beside the walk's, what the usage history keeps (USAGECAP1a1, D179 point 8): each window said in part or whole,
    /// named by its length and its limit's scope (<c>limitId</c>), never its slot, the other limits of <c>rateLimitsByLimitId</c>
    /// among them. The plan (<c>planType</c>) is still not read.</para>
    /// </remarks>
    public static UsageAnswer Read(JsonElement answer)
    {
        if (answer.ValueKind != JsonValueKind.Object
            || !answer.TryGetProperty("rateLimits", out var limits) || limits.ValueKind != JsonValueKind.Object)
        {
            return UsageAnswer.None(Method, HistoryReader);
        }

        var readings = new List<WindowReading>();
        foreach (var name in new[] { "primary", "secondary" })
        {
            if (!limits.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object) continue;
            if (Whole(window, "windowDurationMins") is not { } minutes || minutes <= 0) continue;
            if (Json.Number(window, "usedPercent") is not { } percent || percent < 0) continue;
            if (Moment(window, "resetsAt") is not { } reset) continue;

            var reading = new WindowReading(WindowOf(minutes), percent / 100, reset);
            readings.RemoveAll(each => each.Window == reading.Window);
            readings.Add(reading);
        }

        var seen = new List<WindowSeen>();
        var own = Json.NonEmpty(limits, "limitId");
        Seen(limits, own, walked: true);
        if (answer.TryGetProperty("rateLimitsByLimitId", out var byId) && byId.ValueKind == JsonValueKind.Object)
        {
            foreach (var other in byId.EnumerateObject())
            {
                if (other.Value.ValueKind != JsonValueKind.Object) continue;
                var id = Json.NonEmpty(other.Value, "limitId") ?? other.Name;
                if (!string.Equals(id, own, StringComparison.Ordinal)) Seen(other.Value, id, walked: false);
            }
        }

        return new UsageAnswer(readings, seen, plan: null, Method, HistoryReader);

        // A limit's windows as the history keeps them: a window with no length has only its slot to name it, and says nothing.
        void Seen(JsonElement limit, string? scope, bool walked)
        {
            foreach (var name in new[] { "primary", "secondary" })
            {
                if (!limit.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object) continue;
                if (Whole(window, "windowDurationMins") is not { } minutes || minutes <= 0) continue;
                var used = Json.Number(window, "usedPercent") is { } percent && percent >= 0 ? percent / 100 : (double?)null;
                var reset = Moment(window, "resetsAt");
                if (used is null && reset is null) continue;

                var key = $"{minutes.ToString(CultureInfo.InvariantCulture)}-minute";
                seen.RemoveAll(each => each.Window == key && each.Scope == scope);
                seen.Add(new WindowSeen(
                    key, walked ? WindowOf(minutes) : null, scope, minutes, used,
                    used is null ? null : UsageHistory.Precision(window.GetProperty("usedPercent"), 100),
                    reset, reset is null ? null : UsageHistory.ResetSaid));
            }
        }
    }

    /// <summary>The version of <see cref="Read"/>'s history parse, which each reading it keeps carries (USAGECAP1a1).</summary>
    public const int HistoryReader = 1;

    /// <summary>
    /// The read over a server's lines (usage evidence §1): <c>initialize</c>, its answer, <c>initialized</c>, then the read,
    /// whose answer is read past whatever the server pushes first. Unknown (null) where the server refuses either, ends, or
    /// is silent past <paramref name="patience"/>; <paramref name="ct"/> is the caller's own stop, and throws.
    /// </summary>
    internal static async Task<IReadOnlyList<WindowReading>?> AskWithinAsync(
        TextReader incoming, TextWriter outgoing, TimeSpan patience, CancellationToken ct)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(patience);
        // Waited on apart from the read itself: a pipe's read may not honour a token, and is ended by the server's stop.
        var asking = AskAsync(incoming, outgoing, bounded.Token);
        try
        {
            return await asking.WaitAsync(bounded.Token).ConfigureAwait(false) is { } answer ? Read(answer) : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (IOException)
        {
            // The server's end of the pipe closed under a write or a read: it answered nothing.
            return null;
        }
        finally
        {
            // A read left behind ends when the server does; whatever it ends in is observed, never thrown at nobody.
            _ = asking.ContinueWith(left => _ = left.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }

    private static async Task<JsonElement?> AskAsync(TextReader incoming, TextWriter outgoing, CancellationToken ct)
    {
        // The measured lines, as measured: no `jsonrpc` member, which the server's own lines leave out too.
        await SendAsync(outgoing, """{"id":1,"method":"initialize","params":{"clientInfo":{"name":"daoris-driver","version":"0"}}}""")
            .ConfigureAwait(false);
        if (await AnswerAsync(incoming, 1, ct).ConfigureAwait(false) is null) return null;

        await SendAsync(outgoing, """{"method":"initialized"}""").ConfigureAwait(false);
        await SendAsync(outgoing, $$"""{"id":2,"method":"{{Method}}","params":null}""").ConfigureAwait(false);
        return await AnswerAsync(incoming, 2, ct).ConfigureAwait(false);
    }

    private static async Task SendAsync(TextWriter outgoing, string line)
    {
        await outgoing.WriteLineAsync(line).ConfigureAwait(false);
        await outgoing.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The answer to request <paramref name="id"/>: its <c>result</c>, or null where it is an error or the server ended first.
    /// A line that is not a JSON object, a notification, a request of the server's own and an answer to another id are read
    /// past.
    /// </summary>
    private static async Task<JsonElement?> AnswerAsync(TextReader incoming, int id, CancellationToken ct)
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

            if (frame.ValueKind != JsonValueKind.Object || frame.TryGetProperty("method", out _)) continue;
            if (Whole(frame, "id") != id) continue;
            return frame.TryGetProperty("error", out _) || !frame.TryGetProperty("result", out var result) ? null : result;
        }

        return null;
    }

    /// <summary>
    /// Start <paramref name="command"/> as a harness's reporting command starts (D49 §4): on the PATH a session would get
    /// (TOOLS5), its account's home in the toolchain's variable, the pin's binary where one is managed, through the one seam
    /// both doors take (<see cref="HarnessProbe.Apply"/>); its three streams redirected as UTF-8 with no mark, and no window.
    /// </summary>
    /// <param name="profileHome">The account's configuration home, or null for the tool's own, the variable then unset.</param>
    /// <param name="managed">The managed binary the command's first word resolved to, or null for the command as named.</param>
    /// <param name="workingDirectory">Where the process runs, or null for the caller's (Claude's read runs in an empty folder).</param>
    internal static ProcessStartInfo Prepare(
        IReadOnlyList<string> command, HarnessToolchain toolchain, string? profileHome, string? managed = null,
        string? workingDirectory = null)
    {
        var info = new ProcessStartInfo
        {
            FileName = command[0],
            WorkingDirectory = workingDirectory ?? "",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var part in command.Skip(1)) info.ArgumentList.Add(part);
        Tools.Hand(info);
        HarnessProbe.Apply(info, toolchain, profileHome, binary: managed);
        return info;
    }

    /// <summary>
    /// Start the server, read, and stop it (CODEXUSE1): the readings, or null where none could be had. The server does not
    /// end by itself, so it is stopped, its whole tree, once the answer or the patience is spent.
    /// </summary>
    /// <param name="ask">
    /// The protocol over the process's streams (USAGEREAD2a); null is Codex's own, <see cref="AskWithinAsync"/>.
    /// </param>
    internal static async Task<IReadOnlyList<WindowReading>?> ReadAsync(
        ProcessStartInfo info, TimeSpan patience, CancellationToken ct,
        Func<TextReader, TextWriter, TimeSpan, CancellationToken, Task<IReadOnlyList<WindowReading>?>>? ask = null)
    {
        Process? process;
        try
        {
            // Through the start gate (PROCSTART2): the usage page's read reaches here. A probe, so the background's
            // (PARALLEL1d); its caller's token ends its wait for a place as it ends the question (PARALLEL1e).
            process = await ProcessStarts.Shared.StartAsync(info, StartClass.Background, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException)
        {
            // No binary to start is nobody to ask.
            return null;
        }

        if (process is null) return null;
        using (process)
        {
            // Drained, so a server that writes its logs to stderr never stalls on a full pipe.
            var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
            try
            {
                return await (ask ?? AskWithinAsync)(process.StandardOutput, process.StandardInput, patience, ct).ConfigureAwait(false);
            }
            finally
            {
                Stop(process);
                await stderr.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Close the server's input and end its tree, whatever state it is in.</summary>
    private static void Stop(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // Its end is already closed.
        }

        try
        {
            if (!process.HasExited) ChildProcess.KillTree(process);
            process.WaitForExit(TimeSpan.FromSeconds(5));
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // It ended on its own between the look and the kill.
        }
    }

    /// <summary>A whole number; null where it is not one.</summary>
    private static int? Whole(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var whole)
            ? whole
            : null;

    /// <summary>A moment written as Unix seconds; null where it is not one a calendar holds.</summary>
    private static DateTimeOffset? Moment(JsonElement element, string name)
    {
        if (Json.Number(element, name) is not { } seconds) return null;
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(seconds * 1000));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
