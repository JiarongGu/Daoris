using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>session_propose</c> (SESSUX1h2a): the <c>session</c> kind's tool, written by <see cref="HelpProposalBox.ProposeSession"/>.</summary>
[McpServerToolType]
public sealed class HelpSessionTool(IntakeScope? intake = null, HelpProposalBox? help = null)
{
    [McpServerTool(Name = "session_propose")]
    [Description(
        "Ask Daoris only: propose archiving or unarchiving sessions, for the person to apply — what Sessions' Archive and "
        + "Unarchive do, and the terminal's `sessions archive`. An archived session leaves the rail and keeps its record; "
        + "nothing is lost, and nothing changes until the person presses Apply. Name the sessions by id, or say `ended` to "
        + "archive every session that has ended (only with door `archive`). Never for deleting a session: that is "
        + "`delete_propose` with a session.")]
    public string ProposeSession(
        [Description("`archive` or `unarchive`.")]
        string door,
        [Description("Why: what the person asked. The person decides on this.")]
        string why,
        [Description("The session ids, comma-separated, each one word. Name this or `ended`, not both.")]
        string? sessions = null,
        [Description("True to archive every ended session; only with door `archive`. Name this or `sessions`.")]
        bool ended = false)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeSession(door, sessions, ended, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
