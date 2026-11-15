using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// The quests a completed run's end closes Done: each standing at its submission (<see cref="QuestResults.Submitted"/>), in its chain's
/// order. One held for the person's yes closes only where the person passed over the run's <c>quest.holds</c> with words, the pass its row
/// names the way past the holds, so that pass is their yes to a departure or to its evidence, and the end names it passed for the service to
/// keep the yes before the close. Any other held quest is left as the person left it, and the run's sessions end all the same.
/// </summary>
public static class RunEndQuests
{
    /// <summary>The quests <paramref name="ended"/> closes; none where it ended any other way than at its graph's end.</summary>
    /// <param name="passed">Whether the person passed over the run's holds (<see cref="Passed"/>), which read its newest quest.</param>
    public static IReadOnlyList<RunEndQuest> Of(RunEnd ended, IEnumerable<QuestView> quests, bool passed) =>
        ended.Outcome != RunOutcomes.Completed
            ? []
            : [.. quests.Where(quest => QuestResults.Submitted(quest) && (!quest.Held || (passed && quest.Id == ended.Quest && YesTakes(quest.Hold))))
                .Select(quest => new RunEndQuest(quest.Id, quest.Submission!.Machine, quest.Submission.Sequence, quest.Held))];

    /// <summary>Whether a <c>quest.holds</c> of <paramref name="graph"/> stands skipped on <paramref name="record"/> by the person, with their words.</summary>
    public static bool Passed(NodeGraph graph, NodeRecord record) =>
        graph.Nodes.Any(node => node.Kind == NodeKinds.QuestHolds
            && WorkPerformer.StatusOf(record, node.Id) is { State: NodeStates.Skipped, By: NodeCause.Person, Words: { } words } && !string.IsNullOrWhiteSpace(words));

    // A review's hold is lifted by the person's reviewed, never by a yes.
    private static bool YesTakes(string? hold) => hold is EvidenceCodes.Departed or EvidenceCodes.Unread or EvidenceCodes.MissingHold;
}
