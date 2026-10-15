using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>setup_propose</c> (LAYOUT8): the <c>setup</c> kind's tool, written by <see cref="HelpProposalBox.ProposeSetup"/>.</summary>
[McpServerToolType]
public sealed class HelpSetupTool(IntakeScope? intake = null, HelpProposalBox? help = null)
{
    [McpServerTool(Name = "setup_propose")]
    [Description(
        "Ask Daoris only: propose setting a repository registered on this machine up for every agent, for the person to apply — "
        + "`daoris-driver setup <repository>`, and the repository's page → Set up for agents. The press publishes one quest to "
        + "that repository, as the person's ask, carried by its own session on a branch of its own: the session takes up the "
        + "shared doctrine, writes down what the repository owns and its instructions, and lands by the workspace's rule. The "
        + "press also adds a rule to the repository's own rules, so its session may run the doctrine tool. Daoris never writes "
        + "into the repository. The card shows what was read on the repository's line and the quest's whole text; nothing is "
        + "published until the person presses Apply, and what the press would refuse (not registered here, not driven, no "
        + "tree of its own, no doctrine tool, already set up, a set-up already open) comes back to you in its words. Propose "
        + "it when the person asks to include, adopt or set up a repository registered here.")]
    public string ProposeSetup(
        [Description("The repository, as `registry` names it — never its folder.")]
        string repository,
        [Description("Why: what the person asked. The person decides on this.")]
        string why)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeSetup(repository, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
