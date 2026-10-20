using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A working session's ask to go on with another agent (MIXAGENT1c, D163 point 3; the mixed agents design §2 and §5): the agent
/// writes where its work stands, kept as its own claim, and the person's <c>handOver</c> rule, which the driver applies, settles the
/// ask. Not Ask Daoris's <c>hand_propose</c> (<see cref="HelpHandTool"/>), which hands a landed branch to a plugin.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box reads.</param>
/// <param name="box">Where the ask is written, under the home's sessions folder.</param>
[McpServerToolType]
public sealed class HandOverTools(IntakeScope? intake = null, SessionLedger? ledger = null, HandOverAskBox? box = null)
{
    [McpServerTool(Name = "handover_propose")]
    [Description(
        "Ask to hand this session over to another agent, when you should not or cannot go on with it yourself: your account is near "
        + "its limit, or another agent suits what is left. Write a note for the next agent: where the work stands, what is left, "
        + "what it should read first and what you are unsure of. Your note is kept as your own claim, beside Daoris's own record of "
        + "the session (its plan, last words and what is uncommitted), never in place of it. The person's rule for hand-overs settles "
        + "the ask: it waits for their yes, goes on with the next agent in their order once your turn ends, or is refused. It never "
        + "stops your turn: finish it as you would, commit what you have, and end your turn. One ask at a time.")]
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
