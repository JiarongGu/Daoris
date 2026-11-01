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
        + "work then waits until a role takes it; running work goes on), `default` gives a seeded role back its seed, and `default-all` every seeded role at once (its id is `*`; added roles are kept). Nothing changes "
        + "until the person presses Apply, whatever access you have: no setting you are granted reaches a role.")]
    public string ProposeRole(
        [Description("`set`, `remove`, `default` or `default-all`.")] string change,
        [Description("The role's id: lowercase letters, digits and hyphens, as the catalogue names it (`build`, `designer`); `*` for `default-all`, which names no one role.")] string id,
        [Description("Why: what the person asked. The person decides on this.")] string why,
        [Description("A set's name as the person wrote it, in whatever language; no language is required. Left out keeps the role's own names.")] string? name = null,
        [Description("A set's name in other languages, by language tag (`{ \"fr\": \"Relecteur\", \"zh\": \"审阅\" }`); with `name` that text is kept as `en`. Optional.")]
        Dictionary<string, string>? name_in = null,
        [Description("A set's work, one or more of the codes above; left out keeps the role's own.")] string[]? takes = null,
        [Description("A set's scope: `machine`, `workspace:<id>` or `repository:<id>`; left out keeps the role's own.")] string? scope = null,
        [Description("A set's choices in order, each one quoted `agent [model|-] [effort|-]`, at most four; left out keeps the role's own.")]
        string[]? choices = null,
        [Description("The revision of the role you read, so a change made since is refused rather than overwritten.")] long? revision = null,
        [Description(
            "A set's staffing policy, for a role with choices of its own: how its choices share new starts. A preset's name — `first` (the choices "
            + "in order, as before), `even` or `weighted` — or the record as JSON "
            + "(`{ \"rule\": \"weight\", \"choices\": [{ \"agent\": \"claude-code\", \"model\": \"sonnet\", \"effort\": \"high\", \"weight\": 3, \"share\": 60, "
            + "\"budget\": { \"starts\": 6 }, \"takes\": [\"design\"] }] }`), or `none` to go back to no policy. This build enforces the rule, weights, "
            + "shares, daily budgets of starts and works; a field it does not enforce yet is refused. A change of spending: say so. Left out keeps the role's own.")]
        string? policy = null,
        [Description("The policy revision you read (`ROLES`' `policies`), so a policy changed since is refused rather than overwritten.")] long? policy_revision = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeRole(change, id, name, name_in, takes, scope, choices, revision, why, intake?.Session, DateTimeOffset.UtcNow, policy, policy_revision).Message;
    }
}
