using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>accept_propose</c> (DRIFT1d2): the <c>accept</c> kind's tool, written by <see cref="HelpProposalBox.ProposeAccept"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "accept_propose")]
    [Description(
        "Ask Daoris only: propose the person's yes to a quest a done's departure holds: it closed done departing from what "
        + "the person required, so the chain's next step and any quest waiting on it wait for that yes (`quest_list` marks it "
        + "held for the person's yes). Propose it only when the person asks to accept the departure: the yes is theirs, "
        + "never yours to reach on your own reading of it. The card shows each departure with the person's words it relied "
        + "on; nothing goes on until they press Accept.")]
    public string ProposeAccept(
        [Description("The quest's id, as `quest_list` shows it held for the person's yes.")]
        string quest,
        [Description("Why: what the person asked, and what accepting would let go on. The person decides on this.")]
        string why)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeAccept(quest, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
