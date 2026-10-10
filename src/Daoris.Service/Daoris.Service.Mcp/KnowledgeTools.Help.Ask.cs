using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Ask Daoris's <c>ask_propose</c> (HELP1c): the <c>ask</c> kind's tool, written by <see cref="HelpProposalBox.ProposeAsk"/>, with
/// the review choice the person said and their words (ENTRY1c), as the composer and <c>daoris-driver ask --review</c> send them.
/// An ask's kind and workflow are the person's (D157 point 10), so the tool takes neither.
/// </summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "ask_propose")]
    [Description(
        "Ask Daoris only: propose starting something — an ask made at a workspace, which the driver's loop "
        + "then answers as it answers any ask. The person sees it as a card and presses Apply to make the ask; "
        + "nothing is asked until they do. Never publishes a quest itself. Carries the person's review choice only when "
        + "they said one; their kind or workflow for it is theirs to choose on the ask.")]
    public string ProposeAsk(
        [Description("The ask's words: what is to be done, as the person would say it, with any ticket or link in them.")]
        string sentence,
        [Description("The workspace it is asked at.")]
        string workspace,
        [Description("Why: what the person asked for. The person decides on this.")]
        string why,
        [Description(
            "Only when the person said how its work is reviewed before it lands: `on` to review it in the default environment, "
            + "an environment's name the workspace's review rules declare to review it there, or `off` for no review. Leave it "
            + "out, or `rule`, and each repository's rule decides, as the ask composer's default does.")]
        string? review = null,
        [Description("The person's own words with that review choice, why they chose it; only with a choice.")]
        string? reviewWords = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeAsk(sentence, workspace, why, intake?.Session, DateTimeOffset.UtcNow, review, reviewWords).Message;
    }
}
