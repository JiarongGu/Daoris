using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>go_propose</c> (HELP6): the <c>go</c> kind's tool, written by <see cref="HelpProposalBox.ProposeGo"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "go_propose")]
    [Description(
        "Ask Daoris only: offer to take the person to a screen — a view, a domain of Settings, a part of it, or "
        + "a step of the setup guide. It changes nothing: the card's Go opens the screen, where the person does "
        + "what it is for. Use it when the answer is a place on the window; the room lists every place.")]
    public string ProposeGo(
        [Description("The view: overview, sessions, quests, projects, map, knowledge, agents, plugins or settings. Knowledge's search and convergence are its parts.")]
        string view,
        [Description("Why: what the person asked, and what they will find there.")]
        string why,
        [Description("For settings: the domain, as the room lists them (start, appearance, ai, driver, browser, logs).")]
        string? domain = null,
        [Description("A part of that domain or view, as the room lists them — a setup step under start, add, import, setup or one of a workspace's six parts under projects (workspace-details, workspace-branches, workspace-workflow, workspace-setup, workspace-defaults, workspace-remote: a workspace's Details, Branches, Workflow, Setup, Defaults, Remote and reach), search or convergence under knowledge.")]
        string? part = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeGo(view, domain, part, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
