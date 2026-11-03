using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>setup_propose</c> (LAYOUT8; WORKFLOWCLEAN1 1f): the <c>setup</c> kind's tool, a place's set-up, written by <see cref="HelpProposalBox.ProposeSetup"/>.</summary>
[McpServerToolType]
public sealed class HelpSetupTool(IntakeScope? intake = null, HelpProposalBox? help = null)
{
    [McpServerTool(Name = "setup_propose")]
    [Description(
        "Ask Daoris only: propose a quick set-up of one workspace's repositories registered on this machine, for the person to "
        + "apply (`repository` and `repositories`; the one door that adopts a repository is the place's, `daoris-driver place "
        + "<repository>… --preset keep --adoption`, and its page's *Set up…*). A `preset` is always given: how their work lands "
        + "(`keep` where they follow a saved workflow or the person only wants to adopt, or to turn driving or a tick on), a "
        + "second opinion, the person's look, adoption with what it builds, and general driving. Adoption publishes one quest to "
        + "each repository, as the person's ask, carried by its own session on a branch of its own: the session takes up the "
        + "shared doctrine, writes down what the repository owns and its instructions, and lands by the workspace's rule, and a "
        + "rule is added to the repository's own rules so its session may run the doctrine tool. Daoris never writes into the "
        + "repository. Propose it with `preset` `keep` and `adoption` when the person asks to include, adopt or set up a "
        + "repository registered here. The card shows the plan `daoris-driver place <repository>… --preset <id> --plan` prints, "
        + "every refusal in its words (not registered here, no tree of its own, no doctrine tool, a set-up already open), and its "
        + "Apply opens the Agents place's Setup with *Set up…* open on your ask, where the person reads the plan and presses "
        + "Apply there; your card writes nothing.")]
    public string ProposeSetup(
        [Description("The repository, as `registry` names it — never its folder. The first repository of the set-up.")]
        string repository,
        [Description("Why: what the person asked. The person decides on this.")]
        string why,
        [Description(
            "How a place's work lands: `keep` (keep how it lands: writes no landing and no workflow choice, so a saved workflow it "
            + "follows survives, and driving, the ticks and adoption stay open), or a delivery: `not-driven`, `merge-after-accept`, "
            + "`branch-to-push`, `pull-request` or `integrated`. Required.")]
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
        var place = new HelpSetupPlace(preset ?? "", repositories, opinion, look, adoption, builds, drive);
        return box.ProposeSetup(repository, why, intake?.Session, DateTimeOffset.UtcNow, place).Message;
    }
}
