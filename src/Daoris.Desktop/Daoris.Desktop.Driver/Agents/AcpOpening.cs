using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// How a session begins: a new one on the tree (CONV3b), or a conversation resumed (ANSWER1a, D131). Each runs the handshake once, asks
/// the agent for the session with the servers and rules a new one is handed, keeps the options the agent offers, carries the actor's
/// choice and sets the posture. The session hands it the doors it opens through.
/// </summary>
internal sealed class AcpOpening(
    object? meta,
    AcpOptions options,
    Func<CancellationToken, Task<JsonElement>> handshake,
    Func<Func<Task<JsonElement>>, CancellationToken, Task<JsonElement>> opening,
    Func<string, object, CancellationToken, Task<JsonElement>> request,
    Func<JsonElement, CancellationToken, bool, Task> setPosture,
    Action<string?> opened,
    Action<string?> replaying,
    Action<string> told)
{
    /// <summary>
    /// A new session: the handshake, a session on the tree, and the posture — everything before the first prompt, done once.
    /// </summary>
    public async Task OpenAsync(string cwd, CancellationToken ct, ConnectorPlan? plan)
    {
        await handshake(ct).ConfigureAwait(false);
        var offered = AcpProtocol.Offer(plan);

        // The rules composed for this session ride here when the adapter takes them (PERM1) —
        // and a session given none sends exactly what it sent before they existed.
        // AGENTERRORS1a2 (§5.6): a transient refusal of the raw operation is tried again on the schedule, never the handshake.
        var openMeta = AcpProtocol.OpenMeta(meta, offered);
        var created = await opening(
            () => request(
                "session/new",
                openMeta is null
                    ? new { cwd, mcpServers = offered }
                    : (object)new { cwd, mcpServers = offered, _meta = openMeta },
                ct),
            ct).ConfigureAwait(false);
        // Read without trusting the shape (REV3): an id of the wrong kind threw past every catch that
        // names this client's own failures, and a conversation waited for a session that never came.
        var id = created.ValueKind == JsonValueKind.Object && created.TryGetProperty("sessionId", out var named)
                 && named.ValueKind == JsonValueKind.String
            ? named.GetString()
            : null;
        opened(id);
        if (string.IsNullOrEmpty(id))
        {
            throw new DriverException("the ACP agent created a session without an id — nothing can be sent to it.");
        }

        // What an answer to a park resumes (ANSWER1a), told before the first turn can fail or park.
        told(id);

        // What the agent offers to change about this session — its mode, its model, its effort (AGT6b):
        // kept as it said them, so a person may be offered the ones that are theirs.
        options.Keep(created.TryGetProperty("configOptions", out var offeredOptions)
            ? AcpConfigOption.Read(offeredOptions)
            : []);
        // The opening's own snapshot and the agent's offer reading, before the posture or any set (D205 §3.3, §9.2).
        options.KeepOpening();

        // What its actor asked, before the posture, which a model change can move, and before the first prompt (D205 §3.2).
        var carried = await options.CarryAsync(ct).ConfigureAwait(false);
        await setPosture(created, ct, carried).ConfigureAwait(false);
    }

    /// <summary>
    /// Resume a conversation instead of opening one (ANSWER1a, D131 §1): the handshake, then <c>session/resume</c> where the agent
    /// advertises it, else <c>session/load</c> where it advertises that, on the same tree, with the servers and the rules a new session
    /// would be handed, and the posture set on its answer.
    /// </summary>
    /// <remarks>
    /// <para><b><c>session/resume</c> first</b>: it resumes without replaying, and the record already holds the
    /// conversation. A load replays every message as an update before it answers; those are dropped here, so nothing is
    /// kept twice, and what the agent says after the answer is the conversation going on.</para>
    ///
    /// <para><b>Read from the adapter's own source</b> (<c>claude-agent-acp</c> 0.84.0): <c>initialize</c> answers
    /// <c>agentCapabilities.loadSession</c> and <c>agentCapabilities.sessionCapabilities.resume</c>; both requests answer
    /// <c>modes</c> and <c>configOptions</c> and no id, the id being the one asked for; a conversation not in the
    /// account's home is refused <c>resource_not_found</c>.</para>
    /// </remarks>
    /// <exception cref="AcpResumeRefused">The agent offers neither, or refused this one.</exception>
    public async Task ResumeAsync(string cwd, string conversation, CancellationToken ct, ConnectorPlan? plan)
    {
        var initialized = await handshake(ct).ConfigureAwait(false);
        var method = AcpProtocol.ResumeMethod(initialized)
            ?? throw new AcpResumeRefused(ContinueWhy.Of(ContinueWhy.Offered), words: null);

        var offered = AcpProtocol.Offer(plan);
        var openMeta = AcpProtocol.OpenMeta(meta, offered);
        object parameters = openMeta is null
            ? new { sessionId = conversation, cwd, mcpServers = offered }
            : new { sessionId = conversation, cwd, mcpServers = offered, _meta = openMeta };

        JsonElement resumed;
        replaying(method == "session/load" ? conversation : null);
        try
        {
            // AGENTERRORS1a2 (§5.6): a transient refusal is tried again here, the raw operation alone; past the bound it ends exhausted.
            resumed = await opening(() => request(method, parameters, ct), ct).ConfigureAwait(false);
        }
        catch (AcpRefusal refused)
        {
            // By the error's structure, never its words: another client holding the thread is its data's reason (MSG1b).
            var why = refused.Code == AcpProtocol.ResourceNotFound ? ContinueWhy.Gone
                : string.Equals(refused.Reason, AcpProtocol.ActiveWriter, StringComparison.Ordinal) ? ContinueWhy.Elsewhere
                : ContinueWhy.Refused;
            throw new AcpResumeRefused(ContinueWhy.Of(why), refused.Words);
        }
        finally
        {
            replaying(null);
        }

        opened(conversation);
        options.Keep(resumed.ValueKind == JsonValueKind.Object && resumed.TryGetProperty("configOptions", out var offeredOptions)
            ? AcpConfigOption.Read(offeredOptions)
            : []);
        options.KeepOpening();
        // A resume carries its record's asked choice again: a resumed conversation keeps no model of its own (D205 §4.2; MODELROLE1a).
        var carried = await options.CarryAsync(ct).ConfigureAwait(false);
        // 🔴 HELPMODE1a4 (D183 point 3): set on every answer, an object or not, so a required posture is never skipped here.
        await setPosture(resumed, ct, carried).ConfigureAwait(false);
    }
}
