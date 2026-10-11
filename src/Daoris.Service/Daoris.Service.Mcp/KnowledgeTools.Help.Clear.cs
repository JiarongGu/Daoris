using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>clear_propose</c> (HIST1f-a): the <c>clear</c> kind's tool, written by <see cref="HelpProposalBox.ProposeClear"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "clear_propose")]
    [Description(
        "Ask Daoris only: propose clearing the finished history of one quest, one ask or one workspace, for the person to "
        + "apply. The card lists what would be cleared and what is kept, and why; Apply clears exactly that. A quest's, an "
        + "ask's or a workspace's history is gone once cleared, so say so in why. Nothing changes until the person presses "
        + "Apply. Name exactly one, by the door: `quest` and its id, `ask` and its id, `failed` and the id of a closed quest "
        + "whose failed sessions go, or `workspace` and its name.")]
    public string ProposeClear(
        [Description("`quest`, `ask`, `failed` or `workspace`: which one is named.")]
        string door,
        [Description("Why: what the person asked. The person decides on this.")]
        string why,
        [Description("The quest's id, for the `quest` and `failed` doors.")]
        string? quest = null,
        [Description("The ask's id, for the `ask` door.")]
        string? ask = null,
        [Description("The workspace's name, for the `workspace` door.")]
        string? workspace = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeClear(door, quest, ask, workspace, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
