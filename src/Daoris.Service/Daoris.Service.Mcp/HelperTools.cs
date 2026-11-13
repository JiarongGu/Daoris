using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>A helper's two tools: the working session asks another agent a question, and the helper the driver started answers it, each
/// for the session its connector names; the ask is a file the driver takes, and the person's rule for helpers is the driver's.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box reads.</param>
/// <param name="helpers">Where a working session's ask and a helper's answer are written — under the home.</param>
[McpServerToolType]
/// <param name="teams">The team's plan, which a scout's ask is judged against and a scout's pack is kept with.</param>
public sealed class HelperTools(IntakeScope? intake = null, SessionLedger? ledger = null, HelperBox? helpers = null, TeamDesk? teams = null)
{
    [McpServerTool(Name = "helper_ask")]
    [Description(
        "Ask another agent for a second reading of this repository. It reads your work as it stands when your turn ends "
        + "(uncommitted included), changes nothing and takes one turn; the person's rule for helpers decides if it starts. Its "
        + "answer comes back as your next turn, as another agent's claim: check it before relying on it. Say what you asked, then end your turn.")]
    public async Task<string> AskHelperAsync(
        [Description("The question, with what the helper should read, in at most 4000 characters.")] string? question,
        [Description("The agent to ask, as Settings lists it. Omit and Daoris chooses.")]
        string? agent = null,
        [Description("True when you go on working and want the answer at your next step; otherwise it is your next turn.")]
        bool? urgent = null,
        [Description("Team lead only: a plan part's id; a scout reads its repository and answers its pack, kept with the plan.")]
        string? part = null,
        CancellationToken ct = default)
    {
        var box = helpers ?? HelperBox.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        HelperScout? scout = null;
        if (!string.IsNullOrWhiteSpace(part))
        {
            if (teams is null) return "This host keeps no team plan, so there is no part to orient. Nothing was asked.";
            var judged = await teams.Scouts.JudgeAsync(record, part, ct).ConfigureAwait(false);
            if (judged.Scout is null) return $"{judged.Refusal} Nothing was asked.";
            scout = judged.Scout;
        }

        return box.Ask(record, session, question, agent, DateTimeOffset.UtcNow, urgent == true, scout).Message;
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
        var now = DateTimeOffset.UtcNow;
        var kept = box.Answer(record, session, answer, now);
        // A scout's answer is a pack: it is kept with the plan, and the scout is recorded as a member of the team.
        if (kept.Kept && teams is not null && record is not null && box.ScoutOf(record) is { } scout)
        {
            await teams.Scouts.KeepAsync(scout, record.Id, answer!.Trim(), now, ct).ConfigureAwait(false);
        }

        return kept.Message;
    }
}
