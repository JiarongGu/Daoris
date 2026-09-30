using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>agent_settings_propose</c> (HELP6): the <c>account</c> kind's tool, written by <see cref="HelpProposalBox.ProposeAgentSettings"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "agent_settings_propose")]
    [Description(
        "Ask Daoris only: propose an account's own model and effort, for the person to apply — the Agents & "
        + "accounts screen's Model & effort, written to that tool's own settings for that account. Only for a tool "
        + "whose settings Daoris knows, and only for one of Daoris's accounts, never the tool's own sign-in. The "
        + "values are the tool's own: a model alias it names or a full model id, and an effort of low, medium, "
        + "high or xhigh — `max` is for one conversation, never an account's default. `unset` returns either to "
        + "the tool's own default. Nothing changes until the person presses Apply.")]
    public string ProposeAgentSettings(
        [Description("The agent, as `daoris agent` spells it; a door runs as its owner's accounts.")]
        string agent,
        [Description("The account's name, as the room lists it.")]
        string account,
        [Description("Why: what the person asked, and what the change would do. The person decides on this.")]
        string why,
        [Description("The model: one of the tool's aliases, a full model id, or `unset`. Omit to leave it.")]
        string? model = null,
        [Description("The effort: low, medium, high, xhigh, or `unset`. Omit to leave it.")]
        string? effort = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeAgentSettings(agent, account, model, effort, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
