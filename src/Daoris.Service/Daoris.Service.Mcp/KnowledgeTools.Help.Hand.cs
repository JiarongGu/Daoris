using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>hand_propose</c> (WSR5b): the <c>hand</c> kind's tool, written by <see cref="HelpProposalBox.ProposeHand"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "hand_propose")]
    [Description(
        "Ask Daoris only: propose handing a branch a landing made to a landing plugin, for the person to apply — the "
        + "review's *hand it to* and `daoris-driver trees hand`. The plugin pushes the branch and opens the pull request, "
        + "signed in as the person: for a ticket landed before its workspace named a plugin, or one whose plugin failed. "
        + "Only a branch a landing made and recorded on this machine, as the room lists them; nothing is pushed until the "
        + "person presses Apply.")]
    public string ProposeHand(
        [Description("The session that landed the branch, or the branch's name, as the room lists them.")]
        string target,
        [Description("Why: what the person asked. The person decides on this.")]
        string why,
        [Description("The repository the branch is in, only where a branch's name alone is in several.")]
        string? repository = null,
        [Description("The plugin to hand it to, only where the repository's landing rule names none; its id, as the room lists them.")]
        string? plugin = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeHand(target, repository, plugin, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
