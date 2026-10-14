using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// <c>permission_propose</c> (PERM2, D74; PART10a): a session's proposal to change the rules the agents Daoris starts work
/// under. How every tool's words are written is said on <see cref="KnowledgeServer"/>.
/// </summary>
/// <param name="proposals">Where a proposal to change the rules is written — under the home; the environment's when none is handed.</param>
/// <param name="intake">Which session and ask this connector speaks for: what a proposal records as its author.</param>
[McpServerToolType]
public sealed class RuleProposalTools(RuleProposalBox? proposals = null, IntakeScope? intake = null)
{
    [McpServerTool(Name = "permission_propose")]
    [Description(
        "Propose a change to what the agents Daoris starts may do on this machine — its permission rules, "
        + "in Claude Code's own shape. Use it when you were refused something you needed, or when a rule "
        + "lets agents do more than they should. A change that NARROWS (a new deny or ask, an allow "
        + "removed, an allowing default switched off) is applied at the driver's next tick; one that "
        + "WIDENS waits for the person's yes. Either way it records which session proposed it, and why. "
        + "Not for reaching into another repository — to read its code or change it, ask it with "
        + "quest_publish and wait on the answer.")]
    public string ProposePermission(
        [Description("add (a rule to a list), remove (a rule from a scope), or default (switch one of Daoris's defaults).")]
        string action,
        [Description("Where it reaches: machine (every session here), workspace (every session in one workspace), or repository (one repository). Prefer the narrowest that is enough.")]
        string scope,
        [Description("Why: what you were doing, and what the change would allow or stop. The person decides on this.")]
        string why,
        [Description("For add: allow, ask or deny. An ask is refused for anything Daoris starts — nobody is there to answer it.")]
        string? list = null,
        [Description("For add and remove: the rule, like `Bash(npm run test:*)`, `Edit(/docs/**)` or `mcp__daoris-knowledge__quest_list`.")]
        string? rule = null,
        [Description("For workspace and repository: which one.")]
        string? name = null,
        [Description("For default: the id it ships under — connector, commit, no-push or tree-guard.")]
        string? id = null,
        [Description("For default: true to switch it on, false to switch it off.")]
        bool? on = null)
    {
        var box = proposals ?? RuleProposalBox.FromEnvironment();
        var (_, message) = box.Propose(
            new RuleChange(action.Trim().ToLowerInvariant(), scope.Trim().ToLowerInvariant(), name, list?.Trim().ToLowerInvariant(), rule, id, on),
            why, intake?.Session, intake?.Ask, Directory.GetCurrentDirectory(), DateTimeOffset.UtcNow);
        return message;
    }
}
