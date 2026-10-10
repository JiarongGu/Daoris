using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>repository_propose</c> (ENTRY1d1): the <c>repository</c> kind's tool, written by <see cref="HelpProposalBox.ProposeRepository"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "repository_propose")]
    [Description(
        "Ask Daoris only: propose moving a repository registered on this machine to a workspace, for the person to apply — "
        + "the repository's page → Manage → Move to workspace. It changes one row of this machine's registry, at once and only "
        + "here, and touches no file; nothing moves until the person presses Apply. Name the repository as `registry` lists it, "
        + "and the workspace: one this machine has, or a new name, which the move starts. Never for adding or importing a "
        + "repository: that needs a folder, which stays the person's to pick in Repositories.")]
    public string ProposeRepository(
        [Description("The repository, as `registry` names it — never its folder.")]
        string repository,
        [Description("The workspace to move it to: one this machine has, or a new name.")]
        string workspace,
        [Description("Why: what the person asked. The person decides on this.")]
        string why)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeRepository(repository, workspace, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
