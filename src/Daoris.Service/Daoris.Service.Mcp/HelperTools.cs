using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A helper's two tools: the working session asks another agent a question, and the helper the driver started answers it, each
/// for the session its connector names; the ask is a file the driver takes, and the person's rule for helpers is the driver's.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box reads.</param>
/// <param name="helpers">Where a working session's ask and a helper's answer are written — under the home.</param>
[McpServerToolType]
public sealed class HelperTools(IntakeScope? intake = null, SessionLedger? ledger = null, HelperBox? helpers = null)
{
    [McpServerTool(Name = "helper_ask")]
    [Description(
        "Ask another agent for a second reading of this repository: an explanation, a check of your reasoning, a place to look. "
        + "It reads your work as it stands when your turn ends (uncommitted included), changes nothing, takes one turn and asks no "
        + "one; the person's rule for helpers decides if it starts. Its answer comes back as your next turn, marked as another agent's claim: check it before "
        + "relying on it. Say what you asked, then end your turn. Asked urgent, an answer reaches a later turn at its next step.")]
    public async Task<string> AskHelperAsync(
        [Description("The question, with what the helper should read, in at most 4000 characters.")] string? question,
        [Description("Optional: the agent to ask, as Settings lists it. Left out, Daoris chooses another maker's first.")]
        string? agent = null,
        [Description(
            "Optional: true when you go on working and want the answer at your next step, where your door takes words then. Left out, "
            + "the answer comes as your next turn.")]
        bool? urgent = null,
        CancellationToken ct = default)
    {
        var box = helpers ?? HelperBox.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        return box.Ask(record, session, question, agent, DateTimeOffset.UtcNow, urgent == true).Message;
    }

    [McpServerTool(Name = "helper_answer")]
    [Description(
        "Only for a session Daoris started to answer another session's question: say your answer, once, before you end. Say what "
        + "you found or concluded, where you looked, and what you did not read or could not tell. Your answer is a claim: the "
        + "session that asked checks it, and the person sees it. You change nothing, and nobody can ask you anything after.")]
    public async Task<string> AnswerHelperAsync(
        [Description("Your answer, in at most 20000 characters.")] string? answer,
        CancellationToken ct = default)
    {
        var box = helpers ?? HelperBox.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        return box.Answer(record, session, answer, DateTimeOffset.UtcNow).Message;
    }
}
