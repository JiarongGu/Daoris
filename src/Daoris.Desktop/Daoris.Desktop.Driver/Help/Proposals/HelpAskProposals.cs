using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>ask</c> proposal (HELP1c, D89): something to start — its sentence the words, its workspace
/// where it is asked — which the ordinary loop then answers as it answers any ask (D65). The helper never runs
/// a session's work, and never publishes a quest itself. Applied through the local host's ask door.
/// </summary>
internal sealed class HelpAskProposals : IHelpProposalKind
{
    public string Kind => "ask";

    public string Tool => "ask_propose";

    public IReadOnlyList<string> Doors { get; } = ["ask"];

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var workspace = proposal.Workspace?.Trim() ?? "";
        var sentence = proposal.Sentence?.Trim() ?? "";
        var terminal = $"daoris-driver ask --workspace {workspace} \"{sentence.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        var describe = $"Ask at workspace `{workspace}`: “{sentence}”";
        if (sentence.Length == 0) return new HelpPlan("an ask needs its words.", describe, terminal, null);
        if (!facts.Workspaces.Contains(workspace, StringComparer.OrdinalIgnoreCase))
        {
            return new HelpPlan($"there is no workspace `{workspace}` on this machine — one of {Names(facts.Workspaces)}.", describe, terminal, null);
        }

        return new HelpPlan(null, describe, terminal, null);
    }

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var proposal = applying.Proposal;
        var answer = await applying.Doors.AskAsync(proposal.Workspace!.Trim(), proposal.Sentence!.Trim(), ct).ConfigureAwait(false);
        return applying.Settled(answer.Ok, $"{(answer.Ok ? "Applied" : "Not applied")}: `#{applying.Id}` (`{applying.Plan.Terminal}`) — {answer.Message}", answer.Message);
    }
}

public partial interface IHelpDoors
{
    /// <summary>An ask, through the local host's ask door.</summary>
    Task<AskAnswer> AskAsync(string workspace, string sentence, CancellationToken ct);
}
