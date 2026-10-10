using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>go_propose</c> (HELP6): the <c>go</c> kind's tool, written by <see cref="HelpProposalBox.ProposeGo"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "go_propose")]
    [Description(
        "Ask Daoris only: offer to take the person to a screen — a view, a domain of Settings, a part of it, "
        + "a step of the setup guide, one session on Sessions, or one quest or ask on Quests, or Repositories' Add repository or Import a folder "
        + "with the workspace filled. It changes nothing: the card's Go opens the screen, where the person does "
        + "what it is for. Use it when the answer is a place on the window; the room lists every place.")]
    public string ProposeGo(
        [Description("The view: overview, sessions, quests, projects, map, knowledge, agents, plugins or settings. Knowledge's search and convergence are its parts.")]
        string view,
        [Description("Why: what the person asked, and what they will find there.")]
        string why,
        [Description("For settings: the domain, as the room lists them (start, appearance, ai, driver, browser, logs).")]
        string? domain = null,
        [Description("A part of that domain or view, as the room lists them — a setup step under start, add, import, setup or one of a workspace's six parts under projects (workspace-details, workspace-branches, workspace-workflow, workspace-setup, workspace-defaults, workspace-remote: a workspace's Details, Branches, Workflow, Setup, Defaults, Remote and reach), search or convergence under knowledge, waiting (Waiting on you) or review (To review) under sessions, asks (Asks) or held (Waiting on you) under quests. A part brings that group into view and names no session or quest; to open the one that waits, name it as the item. Add and import may carry the workspace.")]
        string? part = null,
        [Description("For sessions: the one session to open, a session by its id — one the room lists as waiting on the person, or another the person names, never Ask Daoris's own conversation. For quests: the one quest or ask to open, as the room lists them — a quest by its id, an ask as ask:<id>. Name no part with it: the item opens on its own, and one this machine does not hold is refused.")]
        string? item = null,
        [Description("For projects' add or import only: the workspace the drawer opens with, by its name — one the room lists, or a new name, which registering there starts. Name it, never a folder: the person picks the folder in the drawer and registers it.")]
        string? workspace = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeGo(view, domain, part, item, why, intake?.Session, DateTimeOffset.UtcNow, workspace).Message;
    }
}
