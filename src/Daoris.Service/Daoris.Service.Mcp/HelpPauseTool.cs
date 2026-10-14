using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>pause_propose</c> (PAUSE1f-a): the <c>pause</c> kind's tool, written by <see cref="HelpProposalBox.ProposePause"/>.</summary>
[McpServerToolType]
public sealed class HelpPauseTool(IntakeScope? intake = null, HelpProposalBox? help = null)
{
    [McpServerTool(Name = "pause_propose")]
    [Description(
        "Ask Daoris only: propose pausing or resuming one ask's or one quest's work, for the person to apply. Pausing stops "
        + "the work's running sessions and starts nothing until it is resumed; resuming lets it start again. Nothing changes "
        + "until the person presses Apply. Name exactly one ask or quest, by its id. Never for abandoning: abandon is the "
        + "person's own, on the ask's or quest's page.")]
    public string ProposePause(
        [Description("`pause` or `resume`.")]
        string door,
        [Description("Why: what the person asked. The person decides on this.")]
        string why,
        [Description("The quest's id. Name this or `ask`, not both.")]
        string? quest = null,
        [Description("The ask's id. Name this or `quest`, not both.")]
        string? ask = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposePause(door, quest, ask, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
