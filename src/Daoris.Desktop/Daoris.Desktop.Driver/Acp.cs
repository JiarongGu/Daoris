using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <param name="StopReason">What the agent said ended the turn, verbatim from the wire.</param>
/// <param name="SessionId">The id the agent gave the session, for the cancel verb and the record.</param>
/// <param name="Updates">How many `session/update` notifications arrived — the timeline's raw count.</param>
/// <remarks>
/// <b>None of this moves a session record.</b> The record moves on the exit code and the quest's own
/// state (D46 §4), because those are the two signals outside work also produces — and because the ACP
/// wire flattens an aborted, blocked or errored turn to `end_turn`, so its stop reason is a
/// self-report that cannot be told apart from an ordinary ending. What this carries ENRICHES the
/// console and the transcript; it never replaces an observation.
/// </remarks>
/// <summary>
/// What a session consumed, as its harness reported it (TOOL3/D57 §4).
/// </summary>
/// <param name="Used">Context the session was holding, in the harness's own units.</param>
/// <param name="Size">The window it was holding it against.</param>
/// <remarks>
/// <para><b>Reported, never computed.</b> Daoris makes no model calls and knows nothing about
/// tokenization — this is a number somebody else's tool volunteered, carried as it was given. No
/// price is attached to it either: what a token costs is the deployment's business (D24), and a
/// price table per model per provider maintained here would be wrong within a month.</para>
///
/// <para><b>The high-water mark, not the last reading.</b> Context drops when a session compacts, so
/// the final number would say a session that nearly filled its window used very little.</para>
/// </remarks>
public sealed record AcpUsage(long Used, long Size);

/// <summary>
/// One MCP server a session is handed on `session/new` (ACP4) — a local program the agent spawns.
/// </summary>
/// <remarks>
/// <b>This is how a driven session gets a voice without anything being written into its repository.</b>
/// The pipe door depends on the repository's own `.mcp.json` wiring the knowledge tools; the protocol
/// carries them itself, which is both simpler and the only shape compatible with `reaching-in` for a
/// repository that has not wired one.
/// </remarks>
/// <param name="Name">What the agent calls it — tools arrive as `mcp__&lt;name&gt;__&lt;tool&gt;`.</param>
/// <param name="Command">The program, resolved by this side before it is named.</param>
/// <param name="Arguments">Its arguments, as given.</param>
/// <param name="Environment">
/// What the server needs to find the same store this driver is using. Passed through rather than
/// invented: a scratch run overrides these, and a session writing to the machine's real store because
/// the overrides did not travel is the failure that would be hardest to see.
/// </param>
public sealed record AcpMcpServer(
    string Name,
    string Command,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment);

/// <param name="Usage">
/// The context pressure the agent reported, or <b>null when it reported none</b> — absent, never
/// zero. "Nothing was measured" and "it used nothing" are different claims (TOOL3).
/// </param>
public sealed record AcpOutcome(
    string StopReason, string SessionId, int Updates, AcpUsage? Usage = null);

/// <summary>
/// One session held over the Agent Client Protocol (D53): JSON-RPC 2.0 in newline-delimited frames,
/// over a spawned process's stdio.
/// </summary>
/// <remarks>
/// <para><b>Why a protocol door at all.</b> The pipe door gives the driver a wall of text and an exit
/// code. This one gives tool boundaries with ids, inputs and outcomes, turn boundaries, thoughts and
/// context pressure — by contract, with nothing parsed out of another program's stdout, which is the
/// coupling D23/D24 exist to prevent and which D52 rejected by name. One wire reaches dsh natively
/// and claude-code and codex through the ACP project's adapters.</para>
///
/// <para><b>It takes streams, not a process.</b> Spawning stays the driver's (D46 §5: the driver owns
/// process lifetime, because observing it is its half of the contract), and a class that owned a
/// process could not be tested without one. Every rule below is provable against two in-memory
/// streams and a fake agent.</para>
///
/// <para><b>Permission requests are refused, always</b> — see <see cref="AnswerPermission"/>.</para>
/// </remarks>
/// <param name="closeTimeout">
/// How long to wait for the agent to acknowledge `session/close`. The turn is already over by then,
/// so this is courtesy with a bound: an agent that wedges after answering the prompt must not be able
/// to hang a run that has finished. Found by a test that never returned.
/// </param>
/// <param name="posture">
/// The mode id that expresses D37 on THIS agent's wire, or null when the agent carries no posture
/// there (ACP3).
/// </param>
/// <remarks>
/// 🔴 <b>The posture belongs to the adapter, not to this class.</b> It was a constant here while one
/// harness rode the door, and that was wrong the moment a second arrived: Claude Code names the same
/// boundary <c>acceptEdits</c>, Codex names it <c>agent</c>, and dsh does not express it on the wire
/// at all. A constant would have driven two of the three at whatever mode they happened to start in,
/// silently. Null asks for nothing, which leaves the agent at its own default — the safe direction,
/// since every default observed is equal to or stricter than what Daoris would set.
/// </remarks>
/// <param name="meta">
/// What <c>session/new</c> carries in <c>_meta</c> — the adapter's own vocabulary for the rules Daoris
/// composed (PERM1, D72) — or null, and then no <c>_meta</c> is sent at all.
/// </param>
/// <param name="onEvent">
/// Where the wire's STRUCTURE goes (D76 §1, CONV1): every update as a <see cref="SessionEvent"/>, beside
/// the line the console gets. Null where nobody keeps the record.
/// </param>
/// <param name="quiet">
/// How long the agent's words may go quiet before the console shows the line they left open
/// (<see cref="AcpConsole"/>); <see cref="AcpConsole.Quiet"/> when not given.
/// </param>
public sealed class AcpSession(
    TextReader incoming,
    TextWriter outgoing,
    Action<string> onLine,
    TimeSpan? closeTimeout = null,
    string? posture = null,
    object? meta = null,
    Action<SessionEvent>? onEvent = null,
    TimeSpan? quiet = null)
{
    /// <summary>The protocol version this client speaks. Stated, never negotiated downward silently.</summary>
    private const int ProtocolVersion = 1;

    private readonly TimeSpan _closeTimeout = closeTimeout ?? TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private int _nextId;
    private int _updates;

    /// <summary>The largest context reading seen (TOOL3), and the lock that guards it.</summary>
    private readonly object _measured = new();
    private AcpUsage? _usage;
    private string? _sessionId;

    /// <summary>The reader, which lives as long as the conversation does, and what ends it.</summary>
    private readonly CancellationTokenSource _pumpStop = new();
    private Task? _pump;

    /// <summary>The console's lines, joined from the agent's streamed words (UX5 U3).</summary>
    private readonly AcpConsole _console = new(onLine, quiet ?? AcpConsole.Quiet);

    /// <summary>
    /// The largest context reading this session has reported (TOOL3), or null when it reported none —
    /// what a conversation, which never runs <see cref="RunAsync"/>, counts toward its account (USAGE1).
    /// </summary>
    public AcpUsage? Usage
    {
        get { lock (_measured) return _usage; }
    }

    /// <summary>
    /// Run one turn end to end: handshake, a session on the tree, the target as a prompt, and every
    /// update rendered as it arrives — a driven session's whole life on this wire.
    /// </summary>
    /// <param name="cwd">The working tree this session runs in — the registered root, or a session tree (D51).</param>
    /// <param name="prompt">The composed target, exactly as the pipe door delivers it.</param>
    /// <param name="servers">
    /// The MCP servers this session is handed (ACP4) — in practice the machine's own knowledge host,
    /// which is what makes the composed target's "respond to `#id` with `take`" a thing the session
    /// can actually do.
    /// </param>
    public async Task<AcpOutcome> RunAsync(
        string cwd, string prompt, CancellationToken ct, IReadOnlyList<AcpMcpServer>? servers = null)
    {
        try
        {
            await OpenAsync(cwd, ct, servers).ConfigureAwait(false);
            var stopReason = await PromptAsync(prompt, ct).ConfigureAwait(false);
            await CloseAsync(ct).ConfigureAwait(false);
            lock (_measured) return new AcpOutcome(stopReason, _sessionId!, _updates, _usage);
        }
        finally
        {
            Release();
        }
    }

    /// <summary>
    /// Open the session a conversation lives in (CONV3b): the reader started, the handshake, a session
    /// on the tree, and the posture — everything before the first prompt, done once.
    /// </summary>
    /// <remarks>
    /// The reader runs until the agent's stream ends or <see cref="Release"/> — NOT until
    /// <paramref name="ct"/>, which bounds only the opening: a conversation outlives the request that
    /// started it, and the updates of every later turn arrive on this same reader.
    /// </remarks>
    public async Task OpenAsync(string cwd, CancellationToken ct, IReadOnlyList<AcpMcpServer>? servers = null)
    {
        if (_pump is not null) throw new DriverException("this ACP session is already open.");
        _pump = PumpAsync(_pumpStop.Token);

        await RequestAsync(
            "initialize",
            new
            {
                protocolVersion = ProtocolVersion,
                // Declared honestly: this client offers the agent no filesystem and no terminal of
                // its own. The session works in `cwd` with the harness's own tools, under the
                // repository's own checked-in configuration — the adapter obligation D46 §5 states.
                clientCapabilities = new { fs = new { readTextFile = false, writeTextFile = false }, terminal = false },
                clientInfo = new { name = "daoris-driver", version = "0" },
            },
            ct).ConfigureAwait(false);

        // 🔴 The session's VOICE (ACP4). The composed target tells every session to claim and
        // close its quest over its own connector, and the pipe door only manages that because the
        // repository's own `.mcp.json` wires it — which an adopted repository may not have, and
        // which the driver may never reach in and write. The protocol carries the wiring instead,
        // so this hands the session what it needs with nothing written anywhere.
        //
        // An empty list rather than an absent field when there is nothing to offer: a machine
        // with no host found still drives, and an agent reading `mcpServers.length` must not meet
        // `undefined`.
        // Named in lower case explicitly: this serialiser writes property names as they are
        // spelled, so `server.Name` would go on the wire as `Name` and the agent would read nothing.
        var offered = (servers ?? []).Select(server => new
        {
            name = server.Name,
            command = server.Command,
            args = server.Arguments,
            // An ARRAY of {name,value}, not an object — read from the adapter's own source, which
            // does `Object.fromEntries(env.map(e => [e.name, e.value]))`.
            env = server.Environment
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new { name = pair.Key, value = pair.Value })
                .ToArray(),
        }).ToArray();

        // The rules composed for this session ride here when the adapter takes them (PERM1) —
        // and a session given none sends exactly what it sent before they existed.
        var created = await RequestAsync(
            "session/new",
            meta is null
                ? new { cwd, mcpServers = offered }
                : (object)new { cwd, mcpServers = offered, _meta = meta },
            ct).ConfigureAwait(false);
        // Read without trusting the shape (REV3): an id of the wrong kind threw past every catch that
        // names this client's own failures, and a conversation waited for a session that never came.
        _sessionId = created.ValueKind == JsonValueKind.Object && created.TryGetProperty("sessionId", out var id)
                     && id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
        if (string.IsNullOrEmpty(_sessionId))
        {
            throw new DriverException("the ACP agent created a session without an id — nothing can be sent to it.");
        }

        await SetPostureAsync(created, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// One turn: the text as a prompt on the open session, answered by the agent's stop reason — which
    /// is also what the record's turn event carries, as the wire's word and nothing more.
    /// </summary>
    /// <remarks>
    /// One at a time: ACP takes a session's prompts in turn, so the caller queues a person's messages
    /// rather than sending one into a turn still running.
    /// </remarks>
    /// <param name="sent">
    /// Told once the prompt is on the wire (CONV4a): a stop asked before that moment must wait for it,
    /// or its <c>session/cancel</c> overtakes the prompt it meant to stop and stops nothing.
    /// </param>
    /// <param name="files">
    /// What the person attached (CONV4c), each a <c>resource_link</c> to where it is kept — the
    /// protocol's baseline block, which every agent takes and <c>claude-code-acp</c> reads without a tool
    /// call (docs/2026-09-25-message-content-evidence.md).
    /// </param>
    public async Task<string> PromptAsync(
        string text, CancellationToken ct, Action? sent = null, IReadOnlyList<KeptFile>? files = null)
    {
        if (_sessionId is null) throw new DriverException("this ACP session is not open — nothing can be prompted on it.");

        object[] prompt =
        [
            new { type = "text", text },
            .. (files ?? []).Select(file => (object)new { type = "resource_link", uri = new Uri(file.Path).AbsoluteUri, name = file.Name }),
        ];

        JsonElement result;
        try
        {
            result = await RequestAsync(
                "session/prompt",
                new { sessionId = _sessionId, prompt },
                ct, sent).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The person's stop is a VERB on this wire (D49's two endings): cancel the turn in
            // flight rather than killing the process, so the agent winds up its own work. Sent
            // here rather than from a cancellation callback, because a callback fires on whatever
            // thread cancelled — including, in the worst case, one already inside the write lock.
            await CancelTurnAsync().ConfigureAwait(false);
            throw;
        }

        var stopReason = result.TryGetProperty("stopReason", out var reason)
            ? reason.GetString() ?? "unknown"
            : "unknown";
        Emit(new SessionEvent
        {
            Kind = SessionEventKind.Turn,
            StopReason = stopReason,
            // The turn's own counts, where the agent reported them (CONV5): the response's `usage`.
            Tokens = result.TryGetProperty("usage", out var usage)
                ? TurnTokens.Read(usage, "inputTokens", "outputTokens", "cachedReadTokens", "cachedWriteTokens")
                : null,
        });
        return stopReason;
    }

    /// <summary>
    /// Stop the turn in flight and keep the session (D49's interrupt; CONV4's "stop the turn"): the agent
    /// winds its work up and answers the prompt with its own stop reason. A notification, so there is
    /// nothing to wait for here.
    /// </summary>
    public Task CancelTurnAsync() =>
        _sessionId is null ? Task.CompletedTask : NotifyAsync("session/cancel", new { sessionId = _sessionId });

    /// <summary>
    /// Close the session politely so the agent can flush and persist; its exit is still what the driver
    /// observes, and a close that fails changes nothing about what already happened.
    /// </summary>
    /// <remarks>
    /// BOUNDED, because "best effort" without a bound is an unbounded wait: an agent that stops
    /// answering after its last turn would otherwise hang a run that is already finished.
    /// </remarks>
    public async Task CloseAsync(CancellationToken ct)
    {
        if (_sessionId is null) return;

        using var closing = CancellationTokenSource.CreateLinkedTokenSource(ct);
        closing.CancelAfter(_closeTimeout);
        try
        {
            await RequestAsync("session/close", new { sessionId = _sessionId }, closing.Token)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or OperationCanceledException)
        {
            // Best-effort by construction: the conversation is over either way.
        }
    }

    /// <summary>
    /// Stop reading. Observed rather than awaited: an agent may never send another byte, and waiting on
    /// its reader would turn a stop into a hang.
    /// </summary>
    public void Release()
    {
        _pumpStop.Cancel();
        _ = _pump?.ContinueWith(static t => t.Exception, TaskScheduler.Default);
    }

    /// <summary>
    /// Completes when the agent's stream has ended and every line it sent has been handled — what a
    /// conversation's transcript waits on before it closes.
    /// </summary>
    public Task Ended => _pump ?? Task.CompletedTask;

    /// <summary>
    /// Read frames until the stream ends, dispatching each: a response completes its request, a
    /// notification is rendered, and a request from the agent is answered.
    /// </summary>
    private async Task PumpAsync(CancellationToken ct)
    {
        try
        {
            while (await incoming.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                JsonElement frame;
                try
                {
                    frame = JsonDocument.Parse(line).RootElement.Clone();
                }
                catch (JsonException)
                {
                    // Not a frame. A real agent writes diagnostics to stderr, but stdout purity is its
                    // promise and not this client's guarantee — so the line is shown rather than
                    // dropped, and the run continues.
                    _console.End();
                    onLine(line);
                    Emit(new SessionEvent { Kind = SessionEventKind.Raw, Raw = line });
                    continue;
                }

                try
                {
                    await DispatchAsync(frame).ConfigureAwait(false);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // 🔴 One frame this client could not read is shown, and the reading goes on. A
                    // throw here used to end the reader, and its `finally` then said the agent's
                    // stream had ended — a false sentence over a lost exception, which is how the
                    // first real run died at its first tool call (ACP2, 2026-09-24).
                    _console.End();
                    onLine($"[unreadable frame: {error.Message}] {Compact(frame)}");
                    Emit(new SessionEvent
                    {
                        Kind = SessionEventKind.Raw, Title = "unreadable frame", Text = error.Message, Raw = Compact(frame),
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The run ended; the reader is nobody's business now.
        }
        finally
        {
            // An agent's last words before its stream ended are the ones a person reads the console for.
            _console.Dispose();

            // Whatever was still awaited will never be answered. Faulting it here is what turns an
            // agent that died mid-handshake into a sentence rather than a hang.
            Fail(new DriverException(
                "the ACP agent's stream ended before it answered — the agent exited, or it is not "
                + "speaking ACP on stdout."));
        }
    }

    private async Task DispatchAsync(JsonElement frame)
    {
        var hasMethod = frame.TryGetProperty("method", out var method);
        var hasId = frame.TryGetProperty("id", out var id);
        var name = hasMethod && method.ValueKind == JsonValueKind.String ? method.GetString() : null;

        // Anything but an update ends the line the agent's words left open: a turn's answer, a
        // refusal, a request. An update decides for itself (UX5 U3).
        if (name != "session/update") _console.End();

        if (!hasMethod && hasId)
        {
            Complete(id, frame);
            return;
        }

        if (!hasMethod) return; // neither a call nor an answer; nothing to do with it

        if (hasId)
        {
            try
            {
                await AnswerRequestAsync(name, id, frame).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // 🔴 A request is ANSWERED, whatever went wrong reading it (REV3): an unanswered one is
                // a turn hung for ever, which is worse than any error the agent can be told.
                await SendAsync(new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = JsonNode.Parse(id.GetRawText()),
                    ["error"] = new JsonObject { ["code"] = -32603, ["message"] = $"daoris-driver could not read {name}: {error.Message}" },
                }).ConfigureAwait(false);
                throw;
            }

            return;
        }

        if (name == "session/update" && frame.TryGetProperty("params", out var p)
            && p.TryGetProperty("update", out var update))
        {
            Interlocked.Increment(ref _updates);
            Measure(update);
            var structured = Map(update);
            _console.Update(update, structured);
            if (structured is not null) Emit(structured);
        }
    }

    /// <summary>
    /// An event to whoever keeps the record — and a record that fails costs a line on the console,
    /// never the turn: the conversation enriches the session, it does not run it.
    /// </summary>
    private void Emit(SessionEvent e)
    {
        if (onEvent is null) return;
        try
        {
            onEvent(e);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            onLine($"[the conversation record could not keep an event: {error.Message}]");
        }
    }

    private void Complete(JsonElement id, JsonElement frame)
    {
        if (!id.TryGetInt32(out var key) || !_pending.TryRemove(key, out var waiting)) return;

        if (frame.TryGetProperty("error", out var error))
        {
            // Read without trusting the shape (REV3): a throw here left the call it answers waiting.
            var message = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var m)
                          && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : error.GetRawText();
            waiting.TrySetException(new DriverException($"the ACP agent refused the call: {message}"));
            return;
        }

        waiting.TrySetResult(frame.TryGetProperty("result", out var result) ? result.Clone() : default);
    }

    /// <summary>
    /// The option that refuses, read without trusting the frame's shape — null when none can be read.
    /// </summary>
    /// <remarks>
    /// Null answers <c>cancelled</c>, which is also a refusal. A read that threw on an unexpected shape
    /// used to leave the request unanswered, and an unanswered request is a turn hung for ever (REV3).
    /// </remarks>
    private static string? RejectOption(JsonElement frame)
    {
        if (!frame.TryGetProperty("params", out var p) || p.ValueKind != JsonValueKind.Object
            || !p.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var option in options.EnumerateArray())
        {
            if (option.ValueKind != JsonValueKind.Object) continue;
            if (!option.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String) continue;
            if (kind.GetString() is not ("reject_once" or "reject_always")) continue;
            if (option.TryGetProperty("optionId", out var o) && o.ValueKind == JsonValueKind.String) return o.GetString();
        }

        return null;
    }

    private async Task AnswerRequestAsync(string? method, JsonElement id, JsonElement frame)
    {
        if (method == "session/request_permission")
        {
            await AnswerPermissionAsync(id, frame).ConfigureAwait(false);
            return;
        }

        // Anything else the agent asks of the client, this client does not implement — and says so in
        // the protocol's own vocabulary rather than leaving the agent waiting. An unanswered request
        // is a hung turn, which is the worst available outcome.
        await SendAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = JsonNode.Parse(id.GetRawText()),
            ["error"] = new JsonObject
            {
                ["code"] = -32601,
                ["message"] = $"daoris-driver does not implement {method}",
            },
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// A permission request is refused — always, and by the option's KIND rather than its position.
    /// </summary>
    /// <remarks>
    /// <para><b>This is D37 and D52 on the wire.</b> A request reaching the driver at all means the
    /// repository's own checked-in permission configuration did not already cover the action; the
    /// driver is a component, not a party to the work, and widening the posture at runtime is
    /// precisely what "a better approval surface must not widen autonomy" forbids. The harness's
    /// standing posture — `acceptEdits` and its equivalents — is set where it belongs, at session
    /// creation, so ordinary reversible work never reaches this path.</para>
    ///
    /// <para><b>With no refusal offered, the answer is `cancelled`</b> — never the first option that
    /// happens to be present. Fail closed means closed even when the menu is unhelpful.</para>
    /// </remarks>
    private async Task AnswerPermissionAsync(JsonElement id, JsonElement frame)
    {
        var rejectId = RejectOption(frame);
        var outcome = rejectId is null
            ? new JsonObject { ["outcome"] = "cancelled" }
            : new JsonObject { ["outcome"] = "selected", ["optionId"] = rejectId };

        var tool = frame.TryGetProperty("params", out var q) && q.ValueKind == JsonValueKind.Object
                   && q.TryGetProperty("toolCall", out var call)
            ? Compact(call) : "a tool call";
        var refused = $"permission refused: {tool} — the repository's own configuration governs, and the "
                      + "driver may not widen it";
        onLine($"  {refused}");
        Emit(new SessionEvent { Kind = SessionEventKind.Note, Text = refused });

        await SendAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = JsonNode.Parse(id.GetRawText()),
            ["result"] = new JsonObject { ["outcome"] = outcome },
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// The permission posture Daoris drives under, expressed as this wire's own mode (ACP2).
    /// </summary>
    /// <remarks>
    /// <para><b>The posture is unchanged and is D37's</b> — edits auto-accept because they are
    /// reversible and in-repository, and everything else runs under the repository's own checked-in
    /// configuration. Only its EXPRESSION moved: the pipe door passes
    /// <c>--permission-mode acceptEdits</c>, and this door sets a mode, which is what the evaluation
    /// observed Claude Code offering on `session/new` (§1a).</para>
    ///
    /// <para>🔴 <b>Never a mode the agent did not offer, and never a wider one.</b> The wire also
    /// offers <c>bypassPermissions</c>; a driver that reached for a neighbouring mode when its own
    /// was missing is how a permission boundary widens without a decision. An agent that offers no
    /// modes is left exactly alone — its permissions are its own business, which is the stub's
    /// shape and any future harness's right.</para>
    /// </remarks>
    private async Task SetPostureAsync(JsonElement created, CancellationToken ct)
    {
        // An adapter that names no posture asks for none — dsh, whose wire carries no modes, and the
        // stub, whose permissions are its own business (ACP3).
        if (posture is not { Length: > 0 }) return;

        if (!created.TryGetProperty("modes", out var modes)
            || !modes.TryGetProperty("availableModes", out var available)
            || available.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var offered = false;
        foreach (var mode in available.EnumerateArray())
        {
            if (mode.TryGetProperty("id", out var id) && id.GetString() == posture) offered = true;
        }

        if (!offered) return;

        // Already there is nothing to ask for — and an agent that started in the posture is a fact
        // worth not overwriting with an identical call.
        if (modes.TryGetProperty("currentModeId", out var current) && current.GetString() == posture)
        {
            return;
        }

        await RequestAsync("session/set_mode", new { sessionId = _sessionId, modeId = posture }, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Keep the largest context reading this session reported (TOOL3/D57 §4).
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>The high-water mark, not the last reading.</b> Context drops when a session
    /// compacts, so the final number would report a session that nearly filled its window as having
    /// used very little — which is exactly backwards for the person deciding whether to split work.</para>
    ///
    /// <para><b>A frame that is not a measurement is ignored, never believed.</b> Somebody else's
    /// protocol version is free to change its shape, and a malformed update must become an absence
    /// rather than a zero on a person's screen — nor may it take the turn down.</para>
    /// </remarks>
    private void Measure(JsonElement update)
    {
        if (Kind(update) != "usage_update"
            || Number(update, "used") is not { } used
            || Number(update, "size") is not { } size)
        {
            return;
        }

        lock (_measured)
        {
            if (_usage is null || used > _usage.Used) _usage = new AcpUsage(used, size);
        }
    }

    private static long? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var number)
            ? number
            : null;

    /// <summary>
    /// One update as a console line — the structured source rendered for a transcript a person reads.
    /// </summary>
    /// <remarks>
    /// <para>An update shape this build has never seen is rendered as ITSELF rather than dropped. The
    /// wire belongs to somebody else and it grows; a console that silently omitted the one update type
    /// it did not recognise would be a transcript with a hole in it that nothing reports.</para>
    ///
    /// <para>The agent's words are not rendered here: they arrive in chunks, and
    /// <see cref="AcpConsole"/> joins them into lines from the event <see cref="Map"/> made (UX5 U3).</para>
    /// </remarks>
    internal static string? Render(JsonElement update)
    {
        var kind = Kind(update);

        return kind switch
        {
            "agent_message_chunk" or "agent_thought_chunk" => null,
            "tool_call" => $"→ {Field(update, "title") ?? Field(update, "toolCallId") ?? "tool"}"
                           + (Field(update, "status") is { } s ? $" [{s}]" : ""),
            "tool_call_update" => $"  {Field(update, "toolCallId") ?? "tool"} → {Field(update, "status") ?? "?"}",
            "usage_update" => $"  context {Field(update, "used") ?? "?"}/{Field(update, "size") ?? "?"}",
            null => $"[update] {Compact(update)}",
            _ => $"[{kind}] {Compact(update)}",
        };
    }

    /// <summary>
    /// One update as an event in Daoris's vocabulary (D76 §1) — the structure <see cref="Render"/>
    /// flattens for the console, kept.
    /// </summary>
    /// <remarks>
    /// <para><b>The protocol's own fields, renamed and nothing more.</b> A tool call's kind, title,
    /// status, places and content are what the wire says they are; nothing is inferred from a title or
    /// parsed out of text, which is the line D52 drew and D76 keeps.</para>
    ///
    /// <para>🔴 <b>Every read is shape-checked.</b> The wire is somebody else's and it grows: a real
    /// <c>tool_call</c> carries <c>content</c> as a list where a stub sent an object, and reading one as
    /// the other took a whole turn down (ACP2). A field of an unexpected shape is absent here, never a
    /// throw.</para>
    ///
    /// <para><b>An update this build does not know is kept raw</b>, with its kind as the title, for
    /// the same reason the console shows it: a record with a silent hole in it reports nothing.</para>
    /// </remarks>
    internal static SessionEvent? Map(JsonElement update)
    {
        var kind = Kind(update);
        return kind switch
        {
            "agent_message_chunk" => Text(update) is { } message
                ? new SessionEvent { Kind = SessionEventKind.Message, Text = message }
                : null,
            "agent_thought_chunk" => Text(update) is { } thought
                ? new SessionEvent { Kind = SessionEventKind.Thought, Text = thought }
                : null,
            "user_message_chunk" => Text(update) is { } said
                ? new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = said }
                : null,
            "tool_call" or "tool_call_update" => new SessionEvent
            {
                Kind = SessionEventKind.Tool,
                Id = StringField(update, "toolCallId"),
                Title = StringField(update, "title"),
                ToolKind = StringField(update, "kind"),
                Status = StringField(update, "status"),
                Locations = Locations(update),
                Content = Contents(update),
                Input = update.TryGetProperty("rawInput", out var input) ? Compact(input) : null,
                Output = update.TryGetProperty("rawOutput", out var output) ? Compact(output) : null,
            },
            "plan" => new SessionEvent { Kind = SessionEventKind.Plan, Entries = Entries(update) },
            "usage_update" => new SessionEvent
            {
                Kind = SessionEventKind.Usage, Used = Number(update, "used"), Size = Number(update, "size"),
            },
            // Known, and deliberately not the conversation: the session's own settings — the commands it
            // offers, its mode, its config options (the model catalogue among them, which D24 keeps Daoris
            // out of). The console shows them; in the record they were rows over a chat nobody had spoken
            // in yet (CONV3b), as the native door's `system` frames would have been (CONV3a).
            "available_commands_update" or "current_mode_update" or "config_option_update" or "session_info_update" => null,
            _ => new SessionEvent { Kind = SessionEventKind.Raw, Title = kind ?? "update", Raw = Compact(update) },
        };
    }

    /// <summary>A string field, or null when absent or not a string.</summary>
    private static string? StringField(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>The paths a tool call names in <c>locations</c>, when it is a list of objects with one.</summary>
    private static IReadOnlyList<string>? Locations(JsonElement update)
    {
        if (!update.TryGetProperty("locations", out var locations) || locations.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var paths = locations.EnumerateArray()
            .Where(location => location.ValueKind == JsonValueKind.Object)
            .Select(location => StringField(location, "path"))
            .OfType<string>()
            .ToList();
        return paths.Count == 0 ? null : paths;
    }

    /// <summary>
    /// A tool call's <c>content</c> in ACP's three shapes — a content block (its text), a diff, a
    /// terminal — when it is a list. Anything else in the list is kept as its raw JSON, as text.
    /// </summary>
    private static IReadOnlyList<ToolContent>? Contents(JsonElement update)
    {
        if (!update.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = new List<ToolContent>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            switch (StringField(item, "type"))
            {
                case "diff":
                    items.Add(new ToolContent("diff", Path: StringField(item, "path"),
                        OldText: StringField(item, "oldText"), NewText: StringField(item, "newText")));
                    break;
                case "terminal":
                    items.Add(new ToolContent("terminal", Text: StringField(item, "terminalId")));
                    break;
                case "content" when item.TryGetProperty("content", out var block) && block.ValueKind == JsonValueKind.Object:
                    items.Add(StringField(block, "type") == "text"
                        ? new ToolContent("text", Text: StringField(block, "text"))
                        : new ToolContent(StringField(block, "type") ?? "content", Text: Compact(block)));
                    break;
                default:
                    items.Add(new ToolContent("raw", Text: Compact(item)));
                    break;
            }
        }

        return items.Count == 0 ? null : items;
    }

    /// <summary>A plan's entries, each with its content; one without content is skipped.</summary>
    private static IReadOnlyList<PlanEntry>? Entries(JsonElement update)
    {
        if (!update.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return [.. entries.EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.Object && StringField(entry, "content") is not null)
            .Select(entry => new PlanEntry(StringField(entry, "content")!, StringField(entry, "status"), StringField(entry, "priority")))];
    }

    /// <summary>
    /// `content.text`, which is where every textual update carries its words.
    /// </summary>
    /// <remarks>
    /// 🔴 Only when `content` is an OBJECT. A real `tool_call` carries it as a list, and reading a list
    /// as an object throws — which, in the reader, took the whole turn down at the first tool call of
    /// the first real run (ACP2, 2026-09-24).
    /// </remarks>
    private static string? Text(JsonElement update) =>
        update.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Object
            && content.TryGetProperty("text", out var t)
            && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;

    /// <summary>Which update this is, or null when the wire did not say in a string.</summary>
    private static string? Kind(JsonElement update) =>
        update.TryGetProperty("sessionUpdate", out var kind) && kind.ValueKind == JsonValueKind.String
            ? kind.GetString()
            : null;

    private static string? Field(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()
            : null;

    /// <summary>Bounded raw JSON: enough to recognise, never a log file on one line.</summary>
    private static string Compact(JsonElement element)
    {
        var raw = element.GetRawText();
        return raw.Length <= 400 ? raw : $"{raw[..400]}… ({raw.Length} chars)";
    }

    /// <param name="sent">Told once the request is on the wire, before its answer is awaited.</param>
    private async Task<JsonElement> RequestAsync(string method, object parameters, CancellationToken ct, Action? sent = null)
    {
        var id = Interlocked.Increment(ref _nextId);
        var waiting = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = waiting;

        await SendAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = JsonSerializer.SerializeToNode(parameters),
        }).ConfigureAwait(false);
        sent?.Invoke();

        return await waiting.Task.WaitAsync(ct).ConfigureAwait(false);
    }

    private Task NotifyAsync(string method, object parameters) => SendAsync(new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["method"] = method,
        ["params"] = JsonSerializer.SerializeToNode(parameters),
    });

    /// <summary>
    /// One frame, one line. Serialized under a lock because the answer to an agent's request is
    /// written from the read pump while the caller may be writing a request of its own, and two
    /// interleaved lines are two frames nobody can parse.
    /// </summary>
    /// <remarks>
    /// Asynchronous throughout, deliberately. The first version blocked on the write and took the
    /// lock synchronously; a cancellation callback then re-entered it on the very thread that held
    /// it, and the whole run deadlocked. `SemaphoreSlim` is not reentrant, and the fix that lasts is
    /// to have no path that can re-enter rather than a lock that tolerates it.
    /// </remarks>
    private async Task SendAsync(JsonNode frame)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await outgoing.WriteLineAsync(frame.ToJsonString()).ConfigureAwait(false);
            await outgoing.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Fault everything still awaiting an answer that will never come.</summary>
    private void Fail(Exception error)
    {
        foreach (var key in _pending.Keys)
        {
            if (_pending.TryRemove(key, out var waiting)) waiting.TrySetException(error);
        }
    }
}

/// <summary>
/// The protocol door's console lines (UX5 U3): the agent's words as the lines it wrote, never as the
/// chunks the wire carried them in.
/// </summary>
/// <remarks>
/// <para>A message streams in pieces that break wherever the agent flushed, inside a word as often as
/// not, and a line per piece was a raw view that broke words across lines. So a piece joins the line
/// it continues, a newline in the words ends a line, and anything else the wire says (a tool call, a
/// refusal, the turn's answer, the stream's end) ends the open line before it is shown. The native
/// door writes its console from the whole message; this wire never sends one. And a line the words
/// leave open is shown once they go quiet, because an agent that says something and then waits sends
/// nothing that would end it.</para>
///
/// <para>The words are read from the event <see cref="AcpSession.Map"/> made, so the console and the
/// record take them from one place. The reader drives it and the quiet timer ends a line, so one
/// lock holds both, and every line leaves in the order it was said.</para>
/// </remarks>
internal sealed class AcpConsole : IDisposable
{
    /// <summary>How long the words may go quiet before the line they left open is shown anyway.</summary>
    /// <remarks>
    /// 🔴 <b>Measured, not chosen</b> (UX5 U3, 2026-09-26). Half a second broke real lines on the
    /// window (<c>al</c> / <c>pha: …</c>), because a real stream pauses that long mid-word. Across 13
    /// kept conversations, 2,968 gaps between one chunk and the next: median 48ms, p99 557ms, the
    /// longest 1,161ms, none over two seconds. So two seconds breaks no line that sample holds, and a
    /// held turn's words still show within two seconds.
    /// </remarks>
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(2);

    private readonly Action<string> _onLine;
    private readonly TimeSpan _quiet;
    private readonly Timer _idle;
    private readonly object _gate = new();
    private readonly StringBuilder _open = new();
    private string? _openKind;
    private bool _disposed;

    /// <param name="quiet">
    /// 🔴 How long before an open line is shown with nothing after it. Joining chunks alone held a
    /// line until the next update, and an agent that says something and then waits sends none: the
    /// words sat unseen for as long as it waited. A pause longer than this breaks the line there, which
    /// is rare and still readable; infinite turns the flush off.
    /// </param>
    public AcpConsole(Action<string> onLine, TimeSpan quiet)
    {
        _onLine = onLine;
        _quiet = quiet;
        _idle = new Timer(_ => End());
    }

    /// <summary>One update: its words joined to the open line, or the open line ended and the update shown.</summary>
    public void Update(JsonElement update, SessionEvent? mapped)
    {
        lock (_gate)
        {
            if (mapped is { Kind: SessionEventKind.Message or SessionEventKind.Thought, Text: { } words })
            {
                if (_openKind != mapped.Kind) End();
                _openKind = mapped.Kind;
                _open.Append(words);

                var text = _open.ToString();
                var cut = text.LastIndexOf('\n');
                if (cut >= 0)
                {
                    foreach (var line in text[..cut].Split('\n')) Say(line);
                    _open.Clear().Append(text[(cut + 1)..]);
                }

                if (_open.Length > 0) _idle.Change(_quiet, Timeout.InfiniteTimeSpan);
                return;
            }

            End();
            if (AcpSession.Render(update) is { } rendered) _onLine(rendered);
        }
    }

    /// <summary>End the open line, if the agent's words left one.</summary>
    public void End()
    {
        lock (_gate)
        {
            // 🔴 A quiet flush already queued when the reader ended runs after the timer is gone, on a
            // pool thread, where a throw would take the process with it.
            if (_disposed) return;
            _idle.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            if (_open.Length > 0) Say(_open.ToString());
            _open.Clear();
            _openKind = null;
        }
    }

    /// <summary>The last line said, and the timer stopped: the reader has ended.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            End();
            _disposed = true;
            _idle.Dispose();
        }
    }

    private void Say(string line)
    {
        line = line.TrimEnd('\r');
        if (_openKind != SessionEventKind.Thought)
        {
            _onLine(line);
        }
        else if (line.Length > 0)
        {
            // A thought is marked on every line it runs to; a blank line in one marks nothing.
            _onLine($"· {line}");
        }
    }
}
