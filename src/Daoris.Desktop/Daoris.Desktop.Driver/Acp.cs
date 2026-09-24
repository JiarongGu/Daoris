using System.Collections.Concurrent;
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
public sealed class AcpSession(
    TextReader incoming,
    TextWriter outgoing,
    Action<string> onLine,
    TimeSpan? closeTimeout = null,
    string? posture = null,
    object? meta = null)
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

    /// <summary>
    /// Run one turn end to end: handshake, a session on the tree, the target as a prompt, and every
    /// update rendered as it arrives.
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
        using var pumpStopped = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pump = PumpAsync(pumpStopped.Token);

        try
        {
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
            _sessionId = created.TryGetProperty("sessionId", out var id) ? id.GetString() : null;
            if (string.IsNullOrEmpty(_sessionId))
            {
                throw new DriverException("the ACP agent created a session without an id — nothing can be sent to it.");
            }

            await SetPostureAsync(created, ct).ConfigureAwait(false);

            JsonElement result;
            try
            {
                result = await RequestAsync(
                    "session/prompt",
                    new { sessionId = _sessionId, prompt = new[] { new { type = "text", text = prompt } } },
                    ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The person's stop is a VERB on this wire (D49's two endings): cancel the turn in
                // flight rather than killing the process, so the agent winds up its own work. Sent
                // here rather than from a cancellation callback, because a callback fires on whatever
                // thread cancelled — including, in the worst case, one already inside the write lock.
                await NotifyAsync("session/cancel", new { sessionId = _sessionId }).ConfigureAwait(false);
                throw;
            }

            var stopReason = result.TryGetProperty("stopReason", out var reason)
                ? reason.GetString() ?? "unknown"
                : "unknown";

            // Closed politely so the agent can flush and persist; its exit is still what the driver
            // observes, and a close that fails changes nothing about the run that already happened.
            // BOUNDED, because "best effort" without a bound is an unbounded wait: an agent that
            // stops answering after the prompt would otherwise hang a run that is already finished.
            using var closing = CancellationTokenSource.CreateLinkedTokenSource(ct);
            closing.CancelAfter(_closeTimeout);
            try
            {
                await RequestAsync("session/close", new { sessionId = _sessionId }, closing.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or OperationCanceledException)
            {
                // Best-effort by construction: the turn is over either way.
            }

            lock (_measured) return new AcpOutcome(stopReason, _sessionId!, _updates, _usage);
        }
        finally
        {
            pumpStopped.Cancel();
            // Observed rather than awaited: a cancelled run's agent may never send another byte, and
            // waiting on its reader would turn the person's stop into a hang.
            _ = pump.ContinueWith(static t => t.Exception, TaskScheduler.Default);
        }
    }

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
                    onLine(line);
                    continue;
                }

                await DispatchAsync(frame).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The run ended; the reader is nobody's business now.
        }
        finally
        {
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

        if (!hasMethod && hasId)
        {
            Complete(id, frame);
            return;
        }

        if (!hasMethod) return; // neither a call nor an answer; nothing to do with it

        var name = method.GetString();
        if (hasId)
        {
            await AnswerRequestAsync(name, id, frame).ConfigureAwait(false);
            return;
        }

        if (name == "session/update" && frame.TryGetProperty("params", out var p)
            && p.TryGetProperty("update", out var update))
        {
            Interlocked.Increment(ref _updates);
            Measure(update);
            if (Render(update) is { } rendered) onLine(rendered);
        }
    }

    private void Complete(JsonElement id, JsonElement frame)
    {
        if (!id.TryGetInt32(out var key) || !_pending.TryRemove(key, out var waiting)) return;

        if (frame.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("message", out var m) ? m.GetString() : error.GetRawText();
            waiting.TrySetException(new DriverException($"the ACP agent refused the call: {message}"));
            return;
        }

        waiting.TrySetResult(frame.TryGetProperty("result", out var result) ? result.Clone() : default);
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
        string? rejectId = null;
        if (frame.TryGetProperty("params", out var p) && p.TryGetProperty("options", out var options)
            && options.ValueKind == JsonValueKind.Array)
        {
            foreach (var option in options.EnumerateArray())
            {
                var kind = option.TryGetProperty("kind", out var k) ? k.GetString() : null;
                if (kind is not ("reject_once" or "reject_always")) continue;
                rejectId = option.TryGetProperty("optionId", out var o) ? o.GetString() : null;
                if (rejectId is not null) break;
            }
        }

        var outcome = rejectId is null
            ? new JsonObject { ["outcome"] = "cancelled" }
            : new JsonObject { ["outcome"] = "selected", ["optionId"] = rejectId };

        var tool = frame.TryGetProperty("params", out var q) && q.TryGetProperty("toolCall", out var call)
            ? Compact(call) : "a tool call";
        onLine($"  permission refused: {tool} — the repository's own configuration governs, and the "
               + "driver may not widen it");

        await SendAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = JsonNode.Parse(id.GetRawText()),
            ["result"] = new JsonObject { ["outcome"] = outcome },
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// One update as a console line — the structured source rendered for a transcript a person reads.
    /// </summary>
    /// <remarks>
    /// An update shape this build has never seen is rendered as ITSELF rather than dropped. The wire
    /// belongs to somebody else and it grows; a console that silently omitted the one update type it
    /// did not recognise would be a transcript with a hole in it that nothing reports.
    /// </remarks>
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
        if (!update.TryGetProperty("sessionUpdate", out var kind)
            || kind.GetString() != "usage_update"
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

    internal static string? Render(JsonElement update)
    {
        var kind = update.TryGetProperty("sessionUpdate", out var k) ? k.GetString() : null;
        var text = Text(update);

        return kind switch
        {
            "agent_message_chunk" => text,
            "agent_thought_chunk" => text is null ? null : $"· {text}",
            "tool_call" => $"→ {Field(update, "title") ?? Field(update, "toolCallId") ?? "tool"}"
                           + (Field(update, "status") is { } s ? $" [{s}]" : ""),
            "tool_call_update" => $"  {Field(update, "toolCallId") ?? "tool"} → {Field(update, "status") ?? "?"}",
            "usage_update" => $"  context {Field(update, "used") ?? "?"}/{Field(update, "size") ?? "?"}",
            null => $"[update] {Compact(update)}",
            _ => $"[{kind}] {Compact(update)}",
        };
    }

    /// <summary>`content.text`, which is where every textual update carries its words.</summary>
    private static string? Text(JsonElement update) =>
        update.TryGetProperty("content", out var content) && content.TryGetProperty("text", out var t)
            ? t.GetString()
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

    private async Task<JsonElement> RequestAsync(string method, object parameters, CancellationToken ct)
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
