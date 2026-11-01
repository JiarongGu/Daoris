using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>setup_propose</c> (LAYOUT8): the <c>setup</c> kind's tool, written by <see cref="HelpProposalBox.ProposeSetup"/>.</summary>
[McpServerToolType]
public sealed class HelpSetupTool(IntakeScope? intake = null, HelpProposalBox? help = null)
{
    [McpServerTool(Name = "setup_propose")]
    [Description(
        "Ask Daoris only: propose setting a repository registered on this machine up for every agent, for the person to apply — "
        + "`daoris-driver setup <repository>`, and the repository's page → Set up for agents. The press publishes one quest to "
        + "that repository, as the person's ask, carried by its own session on a branch of its own: the session takes up the "
        + "shared doctrine, writes down what the repository owns and its instructions, and lands by the workspace's rule. The "
        + "press also adds a rule to the repository's own rules, so its session may run the doctrine tool. Daoris never writes "
        + "into the repository. The card shows what was read on the repository's line and the quest's whole text; nothing is "
        + "published until the person presses Apply, and what the press would refuse (not registered here, not driven, no "
        + "tree of its own, no doctrine tool, already set up, a set-up already open) comes back to you in its words. Propose "
        + "it when the person asks to include, adopt or set up a repository registered here. Give a `preset` instead to propose "
        + "a quick set-up of one workspace's repositories (`repository` and `repositories`): their delivery, a second opinion, "
        + "the person's look, adoption with what it builds, and general driving. The card shows the plan `daoris-driver place "
        + "<repository>… --preset <id> --plan` prints, and its Apply opens the Agents place's Setup with *Set up…* open on your ask, "
        + "where the person reads the plan and presses Apply there; your card writes nothing.")]
    public string ProposeSetup(
        [Description("The repository, as `registry` names it — never its folder. With a `preset`, the first repository of the set-up.")]
        string repository,
        [Description("Why: what the person asked. The person decides on this.")]
        string why,
        [Description("A place's delivery: `not-driven`, `merge-after-accept`, `branch-to-push`, `pull-request` or `integrated`. Leaving it out proposes the press above.")]
        string? preset = null,
        [Description("A place's other repositories, as `registry` names them, in the same workspace as `repository`.")]
        string[]? repositories = null,
        [Description("A place's second opinion on the delivery; not with `not-driven`.")]
        bool opinion = false,
        [Description("A place's look: the person's own review before it lands; not with `not-driven`.")]
        bool look = false,
        [Description("A place's adoption: ask each repository's own session to set up for agents.")]
        bool adoption = false,
        [Description("What the repositories build, which shapes the adoption quest's request alone: `library`, `service`, `desktop` or `tooling`.")]
        string? builds = null,
        [Description("A place's general driving, enabled with the delivery; not with `not-driven`.")]
        bool drive = false)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        var asked = preset is not null || repositories is { Length: > 0 } || opinion || look || adoption || builds is not null || drive;
        var place = asked ? new HelpSetupPlace(preset ?? "", repositories, opinion, look, adoption, builds, drive) : null;
        return box.ProposeSetup(repository, why, intake?.Session, DateTimeOffset.UtcNow, place).Message;
    }
}
