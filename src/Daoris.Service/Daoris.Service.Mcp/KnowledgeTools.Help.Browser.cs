using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>browser_propose</c> (HELP10): the <c>browser</c> kind's tool, written by <see cref="HelpProposalBox.ProposeBrowser"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "browser_propose")]
    [Description(
        "Ask Daoris only: propose a change to Daoris's browser's settings, for the person to apply — Settings → Browser, "
        + "and `daoris browser`: which browser sessions and the person use, where links on Daoris's page open, whether other "
        + "software's Chrome extensions are offered or refused, or a favorite kept on its bookmarks bar or dropped. Each but "
        + "the links holds from the browser's next start. Nothing changes until the person presses Apply, and their answer "
        + "comes back as their next message.")]
    public string ProposeBrowser(
        [Description("The setting, as `daoris browser` spells it: use, links, extensions or favorite.")]
        string door,
        [Description("Why: what the person asked, and what the change would do. The person decides on this.")]
        string why,
        [Description("What it is set to: `daoris` or `edge` (use), `system` or `daoris` (links), `offer` or `refuse` (extensions), `add` or `remove` (favorite).")]
        string? value = null,
        [Description("For favorite only: the page's address; to remove, one the room lists as kept.")]
        string? address = null,
        [Description("For favorite add only: its title. Omit for the page's host.")]
        string? title = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeBrowser(door, value, address, title, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
