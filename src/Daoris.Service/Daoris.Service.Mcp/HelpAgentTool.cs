using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Ask Daoris's <c>agent_propose</c> (HELP6): the <c>agent</c> kind's tool, written by <see cref="HelpProposalBox.ProposeAgent"/>. Since
/// POOL1b (D197 point 6, the account-pool design §5.3) an agent's accounts are one set: <c>use</c> and <c>order</c> name no workspace, and
/// the <c>default</c> door left, in the same change as the driver's doors.
/// </summary>
[McpServerToolType]
public sealed class HelpAgentTool(IntakeScope? intake = null, HelpProposalBox? help = null)
{
    [McpServerTool(Name = "agent_propose")]
    [Description(
        "Ask Daoris only: propose updating an agent or pinning it to one exact version, for the person to apply — the Agents "
        + "screen's Update and Pin. Update is offered only where that screen offers it: a pinned agent moves its pin to the newest "
        + "release, and an unpinned one runs its own updater. A pin names one exact release, like 2.1.300, never "
        + "`latest`. The person sees a card with Apply and Not now; nothing runs until they press Apply, and their answer comes back "
        + "as their next message. Three more doors change how Daoris spends an account (a change that widens what it may "
        + "spend is the person's Apply, never yours). Daoris's accounts for an agent are one set that serves every workspace, in one "
        + "order, so none names a workspace: `use` sets how they are used (`use` goal or "
        + "order, `keep` an account or `no_keep`, and `switch`: smart switches accounts by Daoris's usage curve, curve by "
        + "the person's own `curve`, off not while a session runs; a curve says, per window, the most an account should "
        + "have used by each point of the window's time, and a session over it goes on with an account that has room at "
        + "its turn's end), `order` puts the accounts named (`accounts`) first, in that order, the rest after, or with `clear` "
        + "returns to the ids' order, and `ready` ends an account's cool-off now (`account`, or `own` "
        + "for the tool's own sign-in).")]
    public string ProposeAgent(
        [Description("update, pin, use, order, or ready.")]
        string action,
        [Description("The agent, as `daoris agent` spells it: claude-code, claude-code-acp, codex-acp or dsh, or one a plugin declares.")]
        string agent,
        [Description("Why: what the person asked, and what the change would do. The person decides on this.")]
        string why,
        [Description("For pin only: the exact version, such as 2.1.300.")]
        string? version = null,
        [Description("For ready only: the account whose cool-off ends, or `own`.")]
        string? account = null,
        [Description("For use only: goal or order, the way an account is chosen.")]
        string? use = null,
        [Description("For use only: the account to keep. Omit to leave it as it is; see no_keep.")]
        string? keep = null,
        [Description("For use only: true keeps no account.")]
        bool noKeep = false,
        [Description("For use only: smart (Daoris's usage curve), curve (the person's own) or off (no switch while a session runs). Omit to leave it.")]
        string? @switch = null,
        [Description("For use only: the person's own curve, one window each as `<window>=<elapsed>:<used>,…` with whole percents, "
            + "the windows `session` (five hours) and `weekly`, such as `weekly=0:30,50:70,100:100`. A window left out keeps its "
            + "points, or Daoris's curve where it has none.")]
        string[]? curve = null,
        [Description("For use only: true takes the person's own curve out, every window back to Daoris's. Not with curve.")]
        bool noCurve = false,
        [Description("For order only: the accounts to put first, in that order; the rest follow in theirs.")]
        string[]? accounts = null,
        [Description("For order only: true returns the order to the accounts' ids. Not with accounts.")]
        bool clear = false)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeAgent(action, agent, version, why, intake?.Session, DateTimeOffset.UtcNow, account,
            use, keep, noKeep, accounts: accounts, clear: clear, switched: @switch, curve: curve, noCurve: noCurve).Message;
    }
}
