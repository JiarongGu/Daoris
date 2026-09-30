using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>sync_propose</c> (HELP10): the <c>sync</c> kind's tool, written by <see cref="HelpProposalBox.ProposeSync"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "sync_propose")]
    [Description(
        "Ask Daoris only: propose bringing repositories up to date after a pull request merged, for the person to apply — "
        + "Settings → Workspace → Session branches → Updates, and `daoris-driver trees sync`: each line fetched from "
        + "origin and fast-forwarded, the branches still at work replayed onto it, the landed branches whose work reached "
        + "it deleted. Its card asks the person to look first, which fetches as them, then lists what the press would do; "
        + "Apply acts on those rows only. Daoris never pushes.")]
    public string ProposeSync(
        [Description("Why: what the person asked, such as a pull request that merged. The person decides on this.")]
        string why,
        [Description("The one repository to bring up to date, as Repositories lists it. Omit for every repository with a checkout here.")]
        string? repository = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeSync(repository, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
