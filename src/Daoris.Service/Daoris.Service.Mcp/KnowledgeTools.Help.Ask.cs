using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>ask_propose</c> (HELP1c): the <c>ask</c> kind's tool, written by <see cref="HelpProposalBox.ProposeAsk"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "ask_propose")]
    [Description(
        "Ask Daoris only: propose starting something — an ask made at a workspace, which the driver's loop "
        + "then answers as it answers any ask. The person sees it as a card and presses Apply to make the ask; "
        + "nothing is asked until they do. Never publishes a quest itself.")]
    public string ProposeAsk(
        [Description("The ask's words: what is to be done, as the person would say it, with any ticket or link in them.")]
        string sentence,
        [Description("The workspace it is asked at.")]
        string workspace,
        [Description("Why: what the person asked for. The person decides on this.")]
        string why)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeAsk(sentence, workspace, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
