using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A working session's ask to go on with another agent (the mixed agents design §2 and §5): the agent
/// writes where its work stands, kept as its own claim, and the person's <c>handOver</c> rule, which the driver applies, settles the ask.</summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box reads.</param>
/// <param name="box">Where the ask is written, under the home's sessions folder.</param>
[McpServerToolType]
public sealed class HandOverTools(IntakeScope? intake = null, SessionLedger? ledger = null, HandOverAskBox? box = null)
{
    [McpServerTool(Name = "handover_propose")]
    [Description(
        "Ask to hand this session over to another agent, when you cannot go on yourself (your account is near its limit, or "
        + "another agent suits what is left). Write a note: where the work stands, what is left, what to read first, what you are "
        + "unsure of. It is kept as your own claim beside Daoris's record of the session, never in place of it. The person's rule "
        + "for hand-overs settles the ask. It never stops your turn: commit what you have and end your turn. One ask at a time.")]
    public async Task<string> ProposeAsync(
        [Description("Your note for the next agent, in at most 4000 characters.")] string? note,
        CancellationToken ct = default)
    {
        var asks = box ?? HandOverAskBox.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        return asks.Ask(record, session, note, DateTimeOffset.UtcNow).Message;
    }
}
