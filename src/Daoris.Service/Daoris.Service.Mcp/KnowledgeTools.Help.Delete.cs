using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>delete_propose</c> (HELP6): the <c>delete</c> kind's tool, written by <see cref="HelpProposalBox.ProposeDelete"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "delete_propose")]
    [Description(
        "Ask Daoris only: propose deleting a quest or an ask made by mistake — a duplicate or a test — for the "
        + "person to apply. Only an open quest nobody has started on can go, and an ask goes with every quest it "
        + "became, or not at all. Never a taken, done or declined quest: its record stays, and declining it with "
        + "the reason is the way instead. The card says what goes; nothing is deleted until the person presses Apply.")]
    public string ProposeDelete(
        [Description("Why: what the person asked, and why the record was a mistake. The person decides on this.")]
        string why,
        [Description("The quest's id, for a quest. Name this or ask, not both.")]
        string? quest = null,
        [Description("The ask's id, for an ask — it goes with every quest asked by it.")]
        string? ask = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeDelete(quest, ask, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
