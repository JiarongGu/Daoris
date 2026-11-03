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
        + "until the person presses Apply, whatever access you have: no setting you are granted reaches a role. The supervisor's rules "
        + "are a `set` of the id `supervisor` naming `place`, `rules` and `rules_revision` alone: when a line's count reaches a number "
        + "there, Daoris waits for the person, asks before the next move, waits for a reset or moves to another choice. The card shows the "
        + "place's rules before and after: say what holds earlier or later.")]
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
            + "\"budget\": { \"starts\": 6 }, \"takes\": [\"design\"] }], \"moves\": { \"limit\": \"weighted\", \"transient\": { \"do\": \"next\", \"after\": 2 } }, "
            + "\"most\": 3 }`), or `none` to go back to no policy. This build enforces the rule, weights, shares, daily budgets of starts, works, "
            + "the moves on a limit and on a service that does not answer, and `most`; a field it does not enforce yet is refused. The person's card "
            + "shows the policy before and after, in the words the room's Roles section says it in: a change of spending, so say so. Left out keeps "
            + "the role's own.")]
        string? policy = null,
        [Description("The policy revision you read (`ROLES`' `policies`), so a policy changed since is refused rather than overwritten.")] long? policy_revision = null,
        [Description("The supervisor's rules only: the place they are for, `machine`, `workspace:<id>`, `repository:<id>` or `line:<key>`.")] string? place = null,
        [Description(
            "The supervisor's rules only: the place's whole list as JSON, `[]` for none, each `{ \"count\": \"answers-alone\", \"at\": 3, \"do\": \"hold\" }` "
            + "with `upTo` minutes for a `wait` and `roles` (`[\"build\"]`) where it watches some roles alone. Counts: `answers-alone` (go-ons by answers "
            + "alone in a row), `failures` (failed sessions on a quest), `failures-on-choice`, `tries` (a service that did not answer), `quiet`, "
            + "`limits` and `moves` (moves to another choice a day). Actions: `hold`, `ask`, `wait`, `next`, `weighted` and `off` (none here); a "
            + "move needs a `moves` rule, and `tries` cannot be removed.")]
        string? rules = null,
        [Description("The supervisor's rules only: the revision of the place's rules you read (`ROLES`' `supervisor`), so a change made since is refused rather than overwritten.")]
        long? rules_revision = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeRole(change, id, name, name_in, takes, scope, choices, revision, why, intake?.Session, DateTimeOffset.UtcNow, policy, policy_revision, place, rules, rules_revision).Message;
    }
}
