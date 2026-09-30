using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>agent_propose</c> (HELP6): the <c>agent</c> kind's tool, written by <see cref="HelpProposalBox.ProposeAgent"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "agent_propose")]
    [Description(
        "Ask Daoris only: propose updating an agent, or pinning it to one exact version, for the person to "
        + "apply — the Agents & accounts screen's Update and Pin. Update is offered only where that screen "
        + "offers it: a pinned agent moves its pin to the newest release, and an unpinned one runs its own "
        + "updater. A pin names one exact release, like 2.1.300, never `latest`. The person sees a card with "
        + "Apply and Not now; nothing runs until they press Apply, and their answer comes back as their next message.")]
    public string ProposeAgent(
        [Description("update, or pin.")]
        string action,
        [Description("The agent, as `daoris agent` spells it: claude-code, claude-code-acp, codex-acp or dsh, or one a plugin declares.")]
        string agent,
        [Description("Why: what the person asked, and what the change would do. The person decides on this.")]
        string why,
        [Description("For pin only: the exact version, such as 2.1.300.")]
        string? version = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeAgent(action, agent, version, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
