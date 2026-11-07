using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>session_propose</c> (SESSUX1h2a, COWORKUX1): the <c>session</c> kind's tool, written by <see cref="HelpProposalBox.ProposeSession"/>.</summary>
[McpServerToolType]
public sealed class HelpSessionTool(IntakeScope? intake = null, HelpProposalBox? help = null)
{
    [McpServerTool(Name = "session_propose")]
    [Description(
        "Ask Daoris only: propose a change to sessions, for the person to apply. Door `archive` or `unarchive`: what Sessions' Archive "
        + "and Unarchive do, and the terminal's `sessions archive`; an archived session leaves the rail and keeps its record. Name "
        + "the sessions by id, or say `ended` to archive every session that has ended (only with `archive`). Door `helper`: ask "
        + "another agent, read-only, one question about one session's work, as the session's Ask another agent… does; its answer "
        + "comes back to that session, or to the person alone with `toMe`, who passes it on. Door `pass`: send an answer kept for "
        + "the person to the session that asked, naming the ask. Nothing changes until the person presses Apply. Never for "
        + "deleting a session: that is `delete_propose` with a session.")]
    public string ProposeSession(
        [Description("`archive`, `unarchive`, `helper` or `pass`.")]
        string door,
        [Description("Why: what the person asked. The person decides on this.")]
        string why,
        [Description("The session ids, comma-separated, each one word; exactly one for `helper` and `pass`. Name this or `ended`, not both.")]
        string? sessions = null,
        [Description("True to archive every ended session; only with door `archive`. Name this or `sessions`.")]
        bool ended = false,
        [Description("Door `helper`: the question, in the words the helper reads.")]
        string? question = null,
        [Description("Door `helper`: the agent to ask, one word as listed in Settings; omit it to let Daoris choose.")]
        string? agent = null,
        [Description("Door `helper`: the answered ask this asks again after, so its question and answer are handed quoted. Door `pass`: the ask whose kept answer is passed.")]
        string? ask = null,
        [Description("Door `helper`: true to keep the answer for the person, reaching the session only when they pass it.")]
        bool toMe = false)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        var helper = door is "helper" or "pass" || question is not null || agent is not null || ask is not null || toMe
            ? new SessionHelperProposal(question, agent, ask, toMe)
            : null;
        return box.ProposeSession(door, sessions, ended, why, intake?.Session, DateTimeOffset.UtcNow, helper).Message;
    }
}
