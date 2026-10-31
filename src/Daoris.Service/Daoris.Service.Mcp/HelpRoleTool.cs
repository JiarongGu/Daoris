using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>role_propose</c> (FOUNDATION1y-i): the <c>role</c> kind's tool, written by <see cref="HelpProposalBox.ProposeRole"/>.</summary>
[McpServerToolType]
public sealed class HelpRoleTool(IntakeScope? intake = null, HelpProposalBox? help = null)
{
    [McpServerTool(Name = "role_propose")]
    [Description(
        "Ask Daoris only: propose a change of this machine's role catalogue, for the person to apply — Agents → Roles → "
        + "Working roles, or `daoris driver role`. A role takes one kind of work (build, design, chat, ask-daoris, intake, help, "
        + "partner, review, brainstorm) on this machine, in a workspace or in a repository, and runs on its ordered choices of agent, "
        + "model and effort. `set` adds a role or changes one (a field left out keeps the role's own), `remove` takes one out (its "
        + "work then waits until a role takes it; running work goes on), `default` gives a seeded role back its seed. Nothing changes "
        + "until the person presses Apply, whatever access you have: no setting you are granted reaches a role.")]
    public string ProposeRole(
        [Description("`set`, `remove` or `default`.")] string change,
        [Description("The role's id: lowercase letters, digits and hyphens, as the catalogue names it (`build`, `designer`).")] string id,
        [Description("Why: what the person asked. The person decides on this.")] string why,
        [Description("A set's name in English; left out keeps the role's own.")] string? name_en = null,
        [Description("A set's name in Chinese; left out keeps the role's own.")] string? name_zh = null,
        [Description("A set's work, one or more of the codes above; left out keeps the role's own.")] string[]? takes = null,
        [Description("A set's scope: `machine`, `workspace:<id>` or `repository:<id>`; left out keeps the role's own.")] string? scope = null,
        [Description("A set's choices in order, each one quoted `agent [model|-] [effort|-]`, at most four; left out keeps the role's own.")]
        string[]? choices = null,
        [Description("The revision of the role you read, so a change made since is refused rather than overwritten.")] long? revision = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeRole(change, id, name_en, name_zh, takes, scope, choices, revision, why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
