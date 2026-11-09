using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// What the Agent Client Protocol's frames say, read and written without a session: the capabilities this client asks for and the ones an
/// agent's <c>initialize</c> answer declares, the open's parameters, and the typed failure of an answer. Every reader takes the answer
/// without trusting its shape: a capability of the wrong kind is no capability.
/// </summary>
internal static class AcpProtocol
{
    /// <summary>JSON-RPC's <c>resource_not_found</c>, which the adapter answers for a conversation it no longer has.</summary>
    internal const int ResourceNotFound = -32002;

    /// <summary>
    /// <c>codex-acp</c>'s <c>data.reason</c> for a thread another Codex client holds, read from
    /// <c>@agentclientprotocol/codex-acp</c> 2.1.1's <c>dist/index.js</c>.
    /// </summary>
    internal const string ActiveWriter = "thread_active_writer";

    /// <summary>
    /// How long an adapter is asked to wait, before it answers the open, for the servers it is handed to start.
    /// Codex's adapter answers <c>session/new</c> as soon as its own session exists, and the first catalog could omit a connector
    /// still starting, which only the next turn then has. Read from codex-acp 2.1.1 (<c>request._meta.mcpStartupAwaitTimeoutMs</c>):
    /// it waits for the servers raced against this bound and ANSWERS when the time runs out, never failing. The connector host
    /// answers <c>initialize</c> in a few seconds cold; fifteen leaves room for a slow first start and stays far under the open's
    /// own ceiling, so a connector that never starts costs a session this long and no more. An agent without the key ignores it.
    /// </summary>
    internal static readonly TimeSpan McpStartupAwait = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Whether the agent is Claude Code's adapter, which stamps the origin of a turn it did not prompt on that turn's end
    /// (<see cref="AcpWake"/>): <c>agentCapabilities._meta.claudeCode</c> is an object. Another adapter, <c>codex-acp</c> among them,
    /// says no end for a turn its harness starts.
    /// </summary>
    internal static bool SaysOrigins(JsonElement initialized) =>
        initialized.ValueKind == JsonValueKind.Object
        && initialized.TryGetProperty("agentCapabilities", out var capabilities) && capabilities.ValueKind == JsonValueKind.Object
        && capabilities.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object
        && meta.TryGetProperty("claudeCode", out var claudeCode) && claudeCode.ValueKind == JsonValueKind.Object;

    /// <summary>
    /// Whether the agent says it takes a prompt while one runs: <c>agentCapabilities._meta.claudeCode
    /// .promptQueueing</c> as <c>true</c>, which <c>claude-agent-acp</c> declares, and whose words Claude Code folds into
    /// the running turn at its next step.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Its own word, and nothing else.</b> An agent that does not say so is never sent a second prompt during a turn:
    /// <c>codex-acp</c> resets its running turn on one, and a door's silence is never a licence to guess (the
    /// posture's rule). The steering extension (<c>_session/steering</c>) is not read as this: it interrupts the step
    /// in flight.
    /// </remarks>
    internal static bool TakesWordsMidTurn(JsonElement initialized) =>
        initialized.ValueKind == JsonValueKind.Object
        && initialized.TryGetProperty("agentCapabilities", out var capabilities) && capabilities.ValueKind == JsonValueKind.Object
        && capabilities.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object
        && meta.TryGetProperty("claudeCode", out var claudeCode) && claudeCode.ValueKind == JsonValueKind.Object
        && claudeCode.TryGetProperty("promptQueueing", out var queueing) && queueing.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Whether the person's *Send now* steers into the running turn: an agent that takes a
    /// prompt during its turn (<see cref="TakesWordsMidTurn"/>) and offers the steering extension, the top-level
    /// <c>_meta.steering.supported</c> as <c>true</c> (<c>claude-agent-acp</c> 0.84.0, <c>acp-agent.js:1291-1307</c>).
    /// </summary>
    /// <remarks>
    /// Each by its own <c>true</c>. <c>codex-acp</c> offers the extension and not the first, and its <c>turn/steer</c> waits for
    /// a measured turn: until then it is a turn-end door, where *Send now* stops the turn keeping the words.
    /// </remarks>
    internal static bool SteersNow(JsonElement initialized) =>
        TakesWordsMidTurn(initialized)
        && initialized.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object
        && meta.TryGetProperty("steering", out var steering) && steering.ValueKind == JsonValueKind.Object
        && steering.TryGetProperty("supported", out var supported) && supported.ValueKind == JsonValueKind.True;

    /// <summary>
    /// How this agent resumes, as its <c>initialize</c> answered: <c>session/resume</c>, <c>session/load</c>, or null for neither.
    /// </summary>
    internal static string? ResumeMethod(JsonElement initialized)
    {
        if (initialized.ValueKind != JsonValueKind.Object
            || !initialized.TryGetProperty("agentCapabilities", out var capabilities)
            || capabilities.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (capabilities.TryGetProperty("sessionCapabilities", out var session) && session.ValueKind == JsonValueKind.Object
            && session.TryGetProperty("resume", out var resume) && resume.ValueKind == JsonValueKind.Object)
        {
            return "session/resume";
        }

        return capabilities.TryGetProperty("loadSession", out var load) && load.ValueKind == JsonValueKind.True
            ? "session/load"
            : null;
    }

    /// <summary>
    /// The <c>_meta</c> an open or a resume carries: the adapter's rules as composed, and, where servers are
    /// handed, the wait for their start. Null when neither, and then no <c>_meta</c> goes at all.
    /// </summary>
    internal static JsonNode? OpenMeta(object? rules, object[] offered)
    {
        if (offered.Length == 0) return rules is null ? null : JsonSerializer.SerializeToNode(rules);
        var merged = rules is null ? new JsonObject() : JsonSerializer.SerializeToNode(rules) as JsonObject ?? new JsonObject();
        merged["mcpStartupAwaitTimeoutMs"] = (long)McpStartupAwait.TotalMilliseconds;
        return merged;
    }

    /// <summary>The servers a session is handed, in the wire's own shape: on a new session and on a resumed one alike.</summary>
    internal static object[] Offer(ConnectorPlan? plan)
    {
        var servers = plan?.Servers;
        // 🔴 The session's VOICE. The composed target tells every session to claim and
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
        return offered;
    }

    /// <summary>What this client asks of the agent at <c>initialize</c>.</summary>
    /// <remarks>
    /// <para>Declared honestly: this client offers the agent no filesystem and no terminal of its own.
    /// The session works in `cwd` with the harness's own tools, under the repository's own checked-in
    /// configuration — the adapter's obligation.</para>
    ///
    /// <para><b>A session whose streams are kept asks for them</b>, by the spelling that
    /// arrives: AIR's <c>nativeSubagentSessions</c> and <c>asyncTasks</c>. 🔴 The protocol's own
    /// <c>subagents</c> alone switches nothing on at ACP SDK 1.4.0, whose schema drops the key before
    /// the adapter reads it. It is sent too, for when the schema carries it. <c>terminal_output</c> is
    /// never asked for: it streams nothing, and it moves a command's output out of the field the record
    /// reads.</para>
    ///
    /// <para><b>Every session asks for its background tasks</b>: their frames are what a turn that ends on its own
    /// background work waits by, and the headless loop's sessions, which keep no streams, would carry none, so their turns
    /// would park on work still running. Where no stream takes the frames, the record and the console keep none of them
    /// (<see cref="AcpRender.Map"/>, <see cref="AcpRender.Render"/>). A subagent's own session is asked for only where streams route it.</para>
    /// </remarks>
    internal static object Capabilities(bool streams, bool typedFailures = false)
    {
        // Asked for only by a door declared to mark a terminal failure for such a client alone.
        var asked = new List<string>();
        if (streams) asked.Add("nativeSubagentSessions");
        asked.Add("asyncTasks");
        if (typedFailures) asked.Add("sessionFailure");
        var meta = new { jetbrains = new { air = new { version = 1, capabilities = asked } } };
        if (streams) return new { fs = new { readTextFile = false, writeTextFile = false }, terminal = false, subagents = new { }, _meta = meta };
        return new { fs = new { readTextFile = false, writeTextFile = false }, terminal = false, _meta = meta };
    }

    /// <summary>
    /// The terminal failure a typed client's prompt is answered with: <c>end_turn</c> with
    /// <c>_meta.jetbrains.air.sessionFailure</c> at <c>error</c> severity, written as the error frame a refused prompt is, its
    /// <c>category</c> the error's kind and its <c>title</c> the provider's words, as written. Null for any other answer: a warning
    /// is an attempt that failed and another follows, and the agent's own message is never read.
    /// </summary>
    internal static JsonElement? TypedFailure(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("_meta", out var meta)
            || meta.ValueKind != JsonValueKind.Object || !meta.TryGetProperty("jetbrains", out var jetbrains) || jetbrains.ValueKind != JsonValueKind.Object
            || !jetbrains.TryGetProperty("air", out var air) || air.ValueKind != JsonValueKind.Object
            || !air.TryGetProperty("sessionFailure", out var failure) || failure.ValueKind != JsonValueKind.Object
            || Json.Text(failure, "severity") != "error" || Json.Text(failure, "category") is not { Length: > 0 } category)
        {
            return null;
        }

        var frame = new JsonObject
        {
            ["error"] = new JsonObject
            {
                ["message"] = Json.Text(failure, "title") ?? "the agent's turn failed",
                ["data"] = new JsonObject { ["errorKind"] = category },
            },
        };
        using var document = JsonDocument.Parse(frame.ToJsonString());
        return document.RootElement.Clone();
    }
}
