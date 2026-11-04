using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// What a protocol session hands its turn's stages (<see cref="ITurnStage"/>): its console and record, its one writer to the agent,
/// and the parts of its state a stage reads or keeps. The session implements it; a stage holds nothing of the session's but this.
/// </summary>
internal interface ITurnWiring
{
    /// <summary>A line on the console, the raw view.</summary>
    void Say(string line);

    /// <summary>An event to whoever keeps the record: a record that fails costs a console line, never the turn.</summary>
    void Emit(SessionEvent e);

    /// <summary>A frame to the agent, through the writer every frame goes through, so two never interleave.</summary>
    Task SendAsync(JsonNode frame);

    /// <summary>The console's lines, joined from the agent's streamed words.</summary>
    AcpConsole Console { get; }

    /// <summary>The turns the harness starts by itself, and how long a turn waiting on the person stays not silent.</summary>
    AcpWake Wake { get; }

    /// <summary>Whether a prompt is on the wire and not yet answered: what the agent does meanwhile is its turn's.</summary>
    bool Prompted();

    /// <summary>Where the session's permission requests are held for the person; null refuses each as it comes.</summary>
    PermissionHolds? Permissions { get; }

    /// <summary>Whether the session's posture says the person answers its permission requests.</summary>
    bool PersonAnswers { get; }

    /// <summary>Whether the agent's stream is over: an answer now lands nowhere.</summary>
    bool StreamOver { get; }

    /// <summary>The calls this client refused in the session.</summary>
    RefusedCalls Refused { get; }

    /// <summary>A call announced or updated, as the record maps it: open, finished, or no longer cut by a steer.</summary>
    void Track(JsonElement update, SessionEvent? mapped);

    /// <summary>What the agent said about its account's windows, told to whoever reads them; a listener's failure is thrown back.</summary>
    void SayLimits(JsonElement limits);

    /// <summary>The session's config options as the agent says them now, kept and told.</summary>
    void KeepOptions(IReadOnlyList<AcpConfigOption> options);
}
