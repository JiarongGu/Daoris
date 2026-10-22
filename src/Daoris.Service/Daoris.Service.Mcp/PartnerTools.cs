using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A partner's one tool (COAGENT1b, D199 point 8; the co-agent design §2.3 step 8): the partner the driver started for a piece says its
/// answer, once. It speaks only for the session its connector names; any other session is refused in a sentence. What the partner did
/// is the files it left, which Daoris reads itself: the answer is its claim beside them.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box reads.</param>
/// <param name="partners">Where a piece's answer is written — under the home.</param>
[McpServerToolType]
public sealed class PartnerTools(IntakeScope? intake = null, SessionLedger? ledger = null, PartnerBox? partners = null)
{
    [McpServerTool(Name = "partner_answer")]
    [Description(
        "Only for a session Daoris started to write a piece of another session's work: say your answer, once, before you end. Summarise "
        + "what you did, and say what you did not do and what you would need to finish it. The files you leave in your copy are your "
        + "piece, which Daoris reads and hands on with your answer; your answer is a claim the session that asked checks. Nobody can ask "
        + "you anything after.")]
    public async Task<string> AnswerPartnerAsync(
        [Description("What you did, in at most 20000 characters.")] string? summary,
        [Description("Optional: what you did not do or could not finish, and what you would need, in at most 4000 characters.")] string? left = null,
        CancellationToken ct = default)
    {
        var box = partners ?? PartnerBox.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        return box.Answer(record, session, summary, left, DateTimeOffset.UtcNow).Message;
    }
}
