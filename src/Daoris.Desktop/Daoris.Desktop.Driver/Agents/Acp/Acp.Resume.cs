using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The agent would not resume the conversation an answer continues (ANSWER1a, D131 §2): nothing was prompted, and the
/// answer is carried on in a new session, saying <see cref="Why"/>.
/// </summary>
/// <remarks>
/// A <see cref="DriverException"/> like every failure of this door, so a caller that does not continue anything reads it
/// as one; the continuation catches it first, because a refused resume is not a turn that failed.
/// </remarks>
public sealed class AcpResumeRefused(ContinueReason why, string? words)
    : DriverException(words is { Length: > 0 } ? $"{why.Sentence}: {words}" : why.Sentence)
{
    /// <summary>Why, by a code the log writes and a line the note says, which never quotes the agent.</summary>
    public ContinueReason Why { get; } = why;

    /// <summary>The agent's own words, where it refused in any: for the record's conversation on this machine, never a note.</summary>
    public string? Words { get; } = words;
}

public sealed partial class AcpSession
{
    /// <summary>JSON-RPC's <c>resource_not_found</c>, which the adapter answers for a conversation it no longer has.</summary>
    internal const int ResourceNotFound = -32002;

    /// <summary>
    /// <c>codex-acp</c>'s <c>data.reason</c> for a thread another Codex client holds (MSG1b, D137 §1.1), read from
    /// <c>@agentclientprotocol/codex-acp</c> 2.1.1's <c>dist/index.js</c>.
    /// </summary>
    internal const string ActiveWriter = "thread_active_writer";

    /// <summary>The conversation whose replay is arriving while a <c>session/load</c> is answered, or null.</summary>
    private volatile string? _replaying;

    /// <summary>
    /// Resume a conversation instead of opening one (ANSWER1a, D131 §1): the reader started, the handshake, then
    /// <c>session/resume</c> where the agent advertises it, else <c>session/load</c> where it advertises that, on the same
    /// tree, with the servers and the rules a new session would be handed, and the posture set on its answer.
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
    public async Task ResumeAsync(string cwd, string conversation, CancellationToken ct, IReadOnlyList<AcpMcpServer>? servers = null)
    {
        var initialized = await HandshakeAsync(ct).ConfigureAwait(false);
        var method = ResumeMethod(initialized)
            ?? throw new AcpResumeRefused(ContinueWhy.Of(ContinueWhy.Offered), words: null);

        var offered = Offer(servers);
        object parameters = meta is null
            ? new { sessionId = conversation, cwd, mcpServers = offered }
            : new { sessionId = conversation, cwd, mcpServers = offered, _meta = meta };

        JsonElement resumed;
        _replaying = method == "session/load" ? conversation : null;
        try
        {
            resumed = await RequestAsync(method, parameters, ct).ConfigureAwait(false);
        }
        catch (AcpRefusal refused)
        {
            // By the error's structure, never its words: another client holding the thread is its data's reason (MSG1b).
            var why = refused.Code == ResourceNotFound ? ContinueWhy.Gone
                : string.Equals(refused.Reason, ActiveWriter, StringComparison.Ordinal) ? ContinueWhy.Elsewhere
                : ContinueWhy.Refused;
            throw new AcpResumeRefused(ContinueWhy.Of(why), refused.Words);
        }
        finally
        {
            _replaying = null;
        }

        _sessionId = conversation;
        KeepOptions(resumed.ValueKind == JsonValueKind.Object && resumed.TryGetProperty("configOptions", out var options)
            ? AcpConfigOption.Read(options)
            : []);
        if (resumed.ValueKind == JsonValueKind.Object) await SetPostureAsync(resumed, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// How this agent resumes, as its <c>initialize</c> answered: <c>session/resume</c>, <c>session/load</c>, or null for
    /// neither. Read without trusting the shape: a capability of the wrong kind is no capability.
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

    /// <summary>Whether an update is a load's replay of the conversation being resumed.</summary>
    private bool Replayed(JsonElement parameters) =>
        _replaying is { } replaying
        && parameters.TryGetProperty("sessionId", out var id) && id.ValueKind == JsonValueKind.String
        && string.Equals(id.GetString(), replaying, StringComparison.Ordinal);

    /// <summary>Tell whoever keeps it the id the agent named (ANSWER1a); a listener's failure costs a console line, never the session.</summary>
    private void Told(string conversation)
    {
        try
        {
            onConversation?.Invoke(conversation);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            onLine($"[the session's conversation id could not be kept: {error.Message}]");
        }
    }
}
