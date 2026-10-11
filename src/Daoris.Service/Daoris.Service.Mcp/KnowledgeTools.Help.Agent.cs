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
        + "as their next message. Three more doors change how Daoris spends an account (a change that widens what it may "
        + "spend is the person's Apply, never yours): `use` sets how a scope's list of accounts is used (`use` goal or "
        + "order, `keep` an account or `no_keep`, `early` to switch before the limit, `near` a percent from 50 to 99), "
        + "`order` sets the walk's list of accounts (`accounts`) or, with `clear` and a workspace, sends that workspace "
        + "back to the machine's accounts, and `ready` ends an account's cool-off now (`account`, or `own` for the "
        + "tool's own sign-in). Use and order are for the machine or one `workspace`.")]
    public string ProposeAgent(
        [Description("update, pin, default, use, order, or ready.")]
        string action,
        [Description("The agent, as `daoris agent` spells it: claude-code, claude-code-acp, codex-acp or dsh, or one a plugin declares.")]
        string agent,
        [Description("Why: what the person asked, and what the change would do. The person decides on this.")]
        string why,
        [Description("For pin only: the exact version, such as 2.1.300.")]
        string? version = null,
        [Description("For default: the account the agent runs as, as the room lists it. For ready: the account whose cool-off ends, or `own`.")]
        string? account = null,
        [Description("For default, use and order: the one workspace it is for. Omit for the whole machine.")]
        string? workspace = null,
        [Description("For use only: goal or order, the way the scope chooses an account.")]
        string? use = null,
        [Description("For use only: the account to keep. Omit to leave it as it is; see no_keep.")]
        string? keep = null,
        [Description("For use only: true keeps no account.")]
        bool noKeep = false,
        [Description("For use only: true switches before the limit, false does not. Omit to leave it.")]
        bool? early = null,
        [Description("For use only: the percent, 50 to 99, at which an account counts as near its limit.")]
        int? near = null,
        [Description("For order only: the accounts in walk order.")]
        string[]? accounts = null,
        [Description("For order only: with a workspace, that workspace goes back to the machine's accounts. Not with accounts.")]
        bool clear = false)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeAgent(action, agent, version, why, intake?.Session, DateTimeOffset.UtcNow, account, workspace,
            use, keep, noKeep, early, near, accounts, clear).Message;
    }
}
