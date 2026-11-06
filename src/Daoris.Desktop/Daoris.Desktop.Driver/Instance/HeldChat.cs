using System.Text.Json;
using System.Threading.Channels;

namespace Daoris.Driver;

/// <summary>
/// A terminal that holds a conversation of the running Daoris (RPC1g2, D238, the terminal-conversation design §3.3): stdin is the
/// person and stdout the console, as <c>chat</c> has always read; every line, attachment, stop and finish is a request on the held
/// connection, and every line the conversation writes, or the person says in the window, is forwarded to it. Nothing runs in this
/// process.
/// </summary>
/// <remarks>
/// <para>Everything that happens, a notification, a line typed, Ctrl+C, the end of input, the connection's loss, is one item in one
/// queue, taken in order, so a line typed before the end of input is said before the conversation is finished.</para>
/// <para>Printed console lines are numbered by the conversation (<c>sequence</c>): the backlog is read once the hold forwards, and a
/// forwarded line already printed is dropped; a <c>GAP</c> reads the backlog again from the last line printed.</para>
/// </remarks>
public sealed class HeldChat(InstanceWorld world)
{
    /// <summary>The line that attaches a file to the next message (CONV4c).</summary>
    public const string Attach = ":attach ";

    private enum Kind { Notice, Line, End, Interrupt, Lost }

    private readonly record struct Item(Kind Kind, HeldNotice? Notice = null, string? Line = null);

    private readonly Channel<Item> _queue = Channel.CreateUnbounded<Item>(new UnboundedChannelOptions { SingleReader = true });
    private readonly List<string> _attaching = [];
    private readonly List<string> _own = [];
    private readonly HashSet<string> _told = new(StringComparer.Ordinal);
    private HeldConversation _held = null!;
    private string _session = "";
    private long _printed;

    /// <summary>Where this terminal hears the connection: the notifications <see cref="HeldDoor.OpenAsync"/> hands on, from its first.</summary>
    public void Hear(HeldNotice notice) => _queue.Writer.TryWrite(new(Kind.Notice, notice));

    /// <summary>The person's Ctrl+C.</summary>
    public void Interrupt() => _queue.Writer.TryWrite(new(Kind.Interrupt));

    /// <summary>The wording of what a terminal says when it lets go of a conversation that goes on in Daoris.</summary>
    public static string GoesOn(string session) =>
        $"chat: the conversation goes on in Daoris; `daoris chat --session {session}` holds it again.";

    /// <summary>The conversation held on <paramref name="held"/>, until it ends or this terminal lets go; the exit code.</summary>
    public async Task<int> RunAsync(HeldConversation held, string session, CancellationToken ct = default)
    {
        _held = held;
        _session = session;
        held.Told = Hear;
        _ = held.Lost.ContinueWith(_ => _queue.Writer.TryWrite(new(Kind.Lost)), TaskScheduler.Default);
        _ = Task.Run(() => ReadInputAsync(ct), CancellationToken.None);
        await ReadBacklogAsync(ct).ConfigureAwait(false);

        await foreach (var item in _queue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            var exit = item.Kind switch
            {
                Kind.Notice => await OnNoticeAsync(item.Notice!, ct).ConfigureAwait(false),
                Kind.Line => await OnLineAsync(item.Line!, ct).ConfigureAwait(false),
                Kind.End => await OnEndAsync(ct).ConfigureAwait(false),
                Kind.Interrupt => await OnInterruptAsync(ct).ConfigureAwait(false),
                _ => await OnLostAsync().ConfigureAwait(false),
            };
            if (exit is { } code) return code;
        }

        return 0;
    }

    private async Task ReadInputAsync(CancellationToken ct)
    {
        try
        {
            while (await world.In.ReadLineAsync(ct).ConfigureAwait(false) is { } line) _queue.Writer.TryWrite(new(Kind.Line, Line: line));
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // Ending: an input that cannot be read is the end of it.
        }

        _queue.Writer.TryWrite(new(Kind.End));
    }

    private async Task<int?> OnLineAsync(string line, CancellationToken ct)
    {
        // A script has no Ctrl+C to press: a line that is only the character it stands for stops the turn, as Ctrl+C in one does.
        if (line == "\u0003")
        {
            await StopTurnAsync(ct).ConfigureAwait(false);
            return null;
        }

        // `/` is the harnesses' own command namespace and `@` is a mention, so neither is taken.
        if (line.StartsWith(Attach, StringComparison.Ordinal))
        {
            await AttachAsync(line[Attach.Length..].Trim().Trim('"')).ConfigureAwait(false);
            return null;
        }

        var body = new Dictionary<string, object?> { ["id"] = _session, ["text"] = line, ["said"] = true };
        var attached = _attaching.Count;
        if (attached > 0) body["paths"] = _attaching.ToArray();
        _attaching.Clear();
        _own.Add(line);
        var answer = await AskAsync("SESSION_INPUT", body, ct).ConfigureAwait(false);
        if (answer is not { } said)
        {
            _own.Remove(line);
            return null;
        }

        if (!InstanceDoor.Succeeded(said))
        {
            _own.Remove(line);
            var (_, message, _) = InstanceDoor.Refusal(said);
            await world.Error.WriteLineAsync($"chat: {message}{(attached > 0 ? " Nothing was sent; attach again." : "")}").ConfigureAwait(false);
            return null;
        }

        // The verb's own words, said only where the words were not taken: a line said is not answered with a line.
        if (said.TryGetProperty("data", out var data) && TreesSaid.Read(data) is { Code: not 0 } refused)
        {
            _own.Remove(line);
            foreach (var each in refused.Out.Concat(refused.Error))
            {
                await world.Error.WriteLineAsync($"chat: {(each.StartsWith("sessions: ", StringComparison.Ordinal) ? each["sessions: ".Length..] : each)}")
                    .ConfigureAwait(false);
            }
        }

        return null;
    }

    private async Task AttachAsync(string path)
    {
        if (!File.Exists(path))
        {
            await world.Error.WriteLineAsync($"chat: `{path}` is not a file on this machine, so nothing was attached.").ConfigureAwait(false);
            return;
        }

        // Judged from its size before the instance is asked to read it; the route's own ground judges what it reads.
        if (new FileInfo(path).Length > ChatFiles.MaxBytes)
        {
            await world.Error.WriteLineAsync(
                $"chat: `{Path.GetFileName(path)}` is larger than {ChatFiles.MaxBytes / (1024 * 1024)} MB, so nothing was attached.").ConfigureAwait(false);
            return;
        }

        _attaching.Add(Path.GetFullPath(path));
        await world.Error.WriteLineAsync($"chat: {Path.GetFileName(path)} goes with your next message.").ConfigureAwait(false);
    }

    /// <summary>Ctrl+C: it stops the turn that runs; at rest it lets go, and the conversation goes on in Daoris.</summary>
    private async Task<int?> OnInterruptAsync(CancellationToken ct)
    {
        if (await StopTurnAsync(ct, leaveAtRest: true).ConfigureAwait(false)) return null;
        return await LetGoAsync(ct).ConfigureAwait(false);
    }

    /// <summary>The turn stopped and what it handed back said; false where none ran or the stop was refused, and then nothing is said if <paramref name="leaveAtRest"/>.</summary>
    private async Task<bool> StopTurnAsync(CancellationToken ct, bool leaveAtRest = false)
    {
        var answer = await AskAsync("CANCEL_TURN", new { id = _session }, ct).ConfigureAwait(false);
        if (answer is not { } said) return false;
        if (!InstanceDoor.Succeeded(said))
        {
            if (!leaveAtRest) await world.Error.WriteLineAsync($"chat: {InstanceDoor.Refusal(said).Message}").ConfigureAwait(false);
            return false;
        }

        var data = said.TryGetProperty("data", out var each) ? each : default;
        var cancelled = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("cancelled", out var did) && did.ValueKind == JsonValueKind.True;
        var withdrawn = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("withdrawn", out var held) && held.ValueKind == JsonValueKind.Array
            ? held.EnumerateArray().ToArray()
            : [];
        if (leaveAtRest && !cancelled && withdrawn.Length == 0) return false;
        await world.Error.WriteLineAsync(
            cancelled ? "chat: the turn was asked to stop; the conversation goes on."
            : withdrawn.Length > 0 ? "chat: nothing had reached the agent yet; the conversation goes on."
            : "chat: no turn was running.").ConfigureAwait(false);
        foreach (var word in withdrawn)
        {
            var names = word.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array
                ? files.EnumerateArray().Select(file => file.GetString()).OfType<string>().ToArray()
                : [];
            var withFiles = names.Length > 0 ? $" (with {string.Join(", ", names)})" : "";
            await world.Error.WriteLineAsync($"chat: not sent: {InstanceClient.Text(word, "text")}{withFiles}").ConfigureAwait(false);
        }

        return cancelled || withdrawn.Length > 0;
    }

    private async Task<int?> LetGoAsync(CancellationToken ct)
    {
        await AskAsync(InstanceClient.PipeModule, "LET_GO", new { }, ct).ConfigureAwait(false);
        await world.Error.WriteLineAsync(GoesOn(_session)).ConfigureAwait(false);
        return 0;
    }

    /// <summary>The end of input: the conversation is finished through the hold, and its end waited for.</summary>
    private async Task<int?> OnEndAsync(CancellationToken ct)
    {
        var answer = await AskAsync("END_CHAT", new { id = _session }, ct).ConfigureAwait(false);
        if (answer is not { } said || InstanceDoor.Succeeded(said)) return null;
        await world.Error.WriteLineAsync($"chat: {InstanceDoor.Refusal(said).Message}").ConfigureAwait(false);
        return 1;
    }

    private async Task<int?> OnLostAsync()
    {
        await world.Error.WriteLineAsync(
            "chat: Daoris is closing; the conversation is kept; hold it again once it is back "
            + $"(`daoris chat --session {_session}`).").ConfigureAwait(false);
        return 2;
    }

    private async Task<int?> OnNoticeAsync(HeldNotice notice, CancellationToken ct)
    {
        if (notice.Module == InstanceClient.PipeModule)
        {
            switch (notice.Type)
            {
                case "GAP":
                    await ReadBacklogAsync(ct).ConfigureAwait(false);
                    return null;
                case "HELD" when InstanceClient.Text(notice.Payload, "held") == "ended"
                                 && InstanceClient.Text(notice.Payload, "reason") == "window":
                    await world.Error.WriteLineAsync("chat: Daoris's window took this conversation back.").ConfigureAwait(false);
                    return 0;
                default:
                    return null;
            }
        }

        if (InstanceClient.Text(notice.Payload, "session") != _session) return null;
        switch (notice.Type)
        {
            case "SESSION_OUTPUT":
                await PrintLinesAsync(notice.Payload).ConfigureAwait(false);
                return null;
            case "SESSION_EVENTS":
                await PrintSaidInWindowAsync(notice.Payload).ConfigureAwait(false);
                return null;
            case "SESSION_ENDED":
                await world.Error.WriteLineAsync($"chat: session {_session} is {InstanceClient.Text(notice.Payload, "state")}.").ConfigureAwait(false);
                return 0;
            default:
                return null;
        }
    }

    private async Task PrintLinesAsync(JsonElement batch)
    {
        if (!batch.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array) return;
        foreach (var line in lines.EnumerateArray())
        {
            if (line.TryGetProperty("sequence", out var sequence) && sequence.TryGetInt64(out var at))
            {
                if (at <= _printed) continue;
                _printed = at;
            }

            await world.Out.WriteLineAsync(InstanceClient.Text(line, "text") ?? "").ConfigureAwait(false);
        }
    }

    /// <summary>The person's words said in the window, so this terminal's transcript is whole; its own words are not said back.</summary>
    private async Task PrintSaidInWindowAsync(JsonElement batch)
    {
        if (!batch.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array) return;
        foreach (var each in events.EnumerateArray())
        {
            if (InstanceClient.Text(each, "kind") != "user" || InstanceClient.Text(each, "origin") != "person") continue;
            if (InstanceClient.Text(each, "text") is not { Length: > 0 } text) continue;
            if (_own.Remove(text)) continue;
            // The same words are told once while they wait and once where they were taken: paired by the id the two carry.
            if (InstanceClient.Text(each, "id") is { } id && !_told.Add(id)) continue;
            await world.Error.WriteLineAsync($"chat: said in Daoris: {text}").ConfigureAwait(false);
        }
    }

    private async Task ReadBacklogAsync(CancellationToken ct)
    {
        var answer = await AskAsync("TAIL_SESSION", new { id = _session, after = _printed }, ct).ConfigureAwait(false);
        if (answer is { } said && InstanceDoor.Succeeded(said) && said.TryGetProperty("data", out var data)) await PrintLinesAsync(data).ConfigureAwait(false);
    }

    private Task<JsonElement?> AskAsync(string type, object payload, CancellationToken ct) => AskAsync(TreesRoutes.DriverModule, type, payload, ct);

    private Task<JsonElement?> AskAsync(string module, string type, object payload, CancellationToken ct) =>
        _held.RequestAsync(Guid.NewGuid().ToString(), module, type, payload, Guid.NewGuid().ToString("N"), hold: false, world.Within, ct);
}
