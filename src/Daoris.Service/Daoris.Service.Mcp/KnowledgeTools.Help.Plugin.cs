using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>plugin_propose</c> (PLUG9): the <c>plugin</c> kind's tool, written by <see cref="HelpProposalBox.ProposePlugin"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "plugin_propose")]
    [Description(
        "Ask Daoris only: propose adding a plugin that has landed or one of Daoris's own, switching one installed here "
        + "on or off, or updating one, for the person to apply — `daoris plugin add <folder>`, `daoris plugin add --offer "
        + "<id>`, Settings → Plugins' switch and `daoris plugin update <id>`. `add` copies the plugin's folder into "
        + "Daoris's home under its manifest's id: name the repository whose checkout holds it and the folder there, or "
        + "name one of the install's own plugins by its id in `offer`, as the room lists them. `update` takes a newer copy "
        + "from where an installed plugin came from. The card shows what the plugin runs (and for an update what changes) "
        + "before the person presses Apply, and nothing is copied, switched, replaced or started until they do. Never for "
        + "a plugin that has not landed: making one is work, proposed with ask_propose at the workspace of the repository "
        + "that holds plugins.")]
    public string ProposePlugin(
        [Description("add, enable, disable, or update.")]
        string action,
        [Description("Why: what the person asked, and what the plugin does. The person decides on this.")]
        string why,
        [Description("For add: the repository whose checkout holds the plugin, as the registry names it.")]
        string? repository = null,
        [Description("For add: the plugin's folder from that checkout's root, like `quiet-hours` or `plugins/quiet-hours`; a whole path only when the person gave one, with no repository.")]
        string? folder = null,
        [Description("For enable, disable and update: the plugin's id, as the room lists the plugins installed here.")]
        string? id = null,
        [Description("For add, instead of a repository and folder: one of Daoris's own plugins this install offers, by its id as the room lists them.")]
        string? offer = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposePlugin(action, id, folder, repository, why, intake?.Session, DateTimeOffset.UtcNow, offer).Message;
    }
}
