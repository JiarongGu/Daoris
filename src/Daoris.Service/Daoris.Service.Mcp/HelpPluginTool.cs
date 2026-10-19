using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>plugin_propose</c> (PLUG9): the <c>plugin</c> kind's tool, written by <see cref="HelpProposalBox.ProposePlugin"/>.</summary>
[McpServerToolType]
public sealed class HelpPluginTool(IntakeScope? intake = null, HelpProposalBox? help = null)
{
    [McpServerTool(Name = "plugin_propose")]
    [Description(
        "Ask Daoris only: propose adding a plugin that has landed or one of Daoris's own, switching one installed here "
        + "on or off, or updating one, for the person to apply — `daoris plugin add <folder>`, `daoris plugin add --offer "
        + "<id>`, the Plugins place's switch (Turn on / Turn off) and `daoris plugin update <id>`. `add` copies the plugin's folder into "
        + "Daoris's home under its manifest's id: name the repository whose checkout holds it and the folder there, or "
        + "name one of the install's own plugins by its id in `offer`, as the room lists them. `update` takes a newer copy "
        + "from where an installed plugin came from. The card shows what the plugin runs (and for an update what changes) "
        + "before the person presses Apply, and nothing is copied, switched, replaced or started until they do. Never for "
        + "a plugin that has not landed: making one is work, proposed with ask_propose at the workspace of the repository "
        + "that holds plugins. `approve` proposes the person's approval of what an installed plugin's server offers you, "
        + "the plugin's page's Approve (`daoris-driver plugins approve <id> <server>`): name the plugin's `id`, its `server`, "
        + "and in `writes` only the writes outside Daoris the person said they accept; the card names every tool, its reads "
        + "and its writes apart, and you are handed it from your next conversation once they press Apply. `withdraw` takes "
        + "an approval back.")]
    public string ProposePlugin(
        [Description("add, enable, disable, update, approve, or withdraw.")]
        string action,
        [Description("Why: what the person asked, and what the plugin does. The person decides on this.")]
        string why,
        [Description("For add: the repository whose checkout holds the plugin, as the registry names it.")]
        string? repository = null,
        [Description("For add: the plugin's folder from that checkout's root, like `quiet-hours` or `plugins/quiet-hours`; a whole path only when the person gave one, with no repository.")]
        string? folder = null,
        [Description("For enable, disable, update, approve and withdraw: the plugin's id, as the room lists the plugins installed here.")]
        string? id = null,
        [Description("For add, instead of a repository and folder: one of Daoris's own plugins this install offers, by its id as the room lists them.")]
        string? offer = null,
        [Description("For approve and withdraw: the server the plugin offers you, as the room names it; omit it where the plugin offers you one only.")]
        string? server = null,
        [Description("For approve: the writes outside Daoris the person accepts, each a tool's name as the room lists it; none accepts its reads only.")]
        string[]? writes = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposePlugin(action, id, folder, repository, why, intake?.Session, DateTimeOffset.UtcNow, offer, server, writes).Message;
    }
}
