using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>agent_propose</c> (HELP6): the <c>agent</c> kind's tool, written by <see cref="HelpProposalBox.ProposeAgent"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "agent_propose")]
    [Description(
        "Ask Daoris only: propose updating an agent, pinning it to one exact version, or making one of its accounts "
        + "the one it runs as by default, for the person to apply — the Agents screen's Update, Pin and Make "
        + "default. Update is offered only where that screen offers it: a pinned agent moves its pin to the newest "
        + "release, and an unpinned one runs its own updater. A pin names one exact release, like 2.1.300, never "
        + "`latest`. A default names an account the room lists under that agent, for the machine or one workspace. The "
        + "person sees a card with Apply and Not now; nothing runs until they press Apply, and their answer comes back "
        + "as their next message.")]
    public string ProposeAgent(
        [Description("update, pin, or default.")]
        string action,
        [Description("The agent, as `daoris agent` spells it: claude-code, claude-code-acp, codex-acp or dsh, or one a plugin declares.")]
        string agent,
        [Description("Why: what the person asked, and what the change would do. The person decides on this.")]
        string why,
        [Description("For pin only: the exact version, such as 2.1.300.")]
        string? version = null,
        [Description("For default only: the account the agent runs as, as the room lists it.")]
        string? account = null,
        [Description("For default only: the one workspace it is the default for. Omit for the whole machine.")]
        string? workspace = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeAgent(action, agent, version, why, intake?.Session, DateTimeOffset.UtcNow, account, workspace).Message;
    }
}
