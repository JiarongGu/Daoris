using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.ObservedFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary><c>claimed</c> is the agent's done on the quest's record, read by the ledger and nothing else's evidence.</summary>
public sealed class ClaimedNodeTests
{
    /// <summary><c>claimed</c> is the agent's done on the quest's record, held or not: a claim, by the ledger, and never anything else's evidence.</summary>
    [Fact]
    public void Claimed_is_the_quests_done_read_from_its_record()
    {
        QuestView Quest(string status, bool held = false) => new("q1", "game", "engine", "The work", "Body.", status) { Updated = Then, Held = held };
        WorkUnit QuestUnit(QuestView quest) => Assert.Single(QuestUnits.Read([quest], []));

        foreach (var held in new[] { false, true })
        {
            var done = QuestUnit(Quest("Done", held));
            var moved = Applied(done, NodeKinds.Claimed, tip: done.Subject.Revision);
            Assert.Equal((NodeStates.Reached, NodeSource.Ledger), (moved.Status.State, moved.Status.By));
            Assert.Equal(("quest:q1", "quests#q1"), (moved.Status.Current?.Unit, moved.Status.Current?.Evidence?.Ref));
        }

        Assert.Null(NodeObserving.Observe(QuestUnit(Quest("Taken")), Node(NodeKinds.Claimed), Graph, 1).Outcome);
        Assert.Null(NodeObserving.Observe(QuestUnit(Quest("Declined")), Node(NodeKinds.Claimed), Graph, 1).Outcome);
        Assert.Equal(NodeStates.Unknown, Applied(QuestUnit(Quest("Paused")), NodeKinds.Claimed).Status.State);
    }

    /// <summary>
    /// On a run's subject, <c>claimed</c> reaches on its quest's done only where that quest's steps
    /// name none still to come in its repository, as the close and the opinion's gate read a chain; a look's set-up step is no step of the
    /// work. On a quest unit bound to a graph it reads as today.
    /// </summary>
    [Fact]
    public void Claimed_on_a_runs_subject_waits_for_its_chains_last_step_here()
    {
        WorkUnit QuestUnit(params QuestStepView[] then) =>
            Assert.Single(QuestUnits.Read([new QuestView("q1", "game", "engine", "The work", "Body.", "Done") { Updated = Then, Then = then }], []));
        NodeAcceptance OnRun(WorkUnit quest) => new("run:r", quest.Subject.Revision, Graph, NodeKinds.Claimed, 1, 1) { Subject = quest.Id };

        var more = QuestUnit(new QuestStepView("engine", "Next", "More of it."));
        var waits = NodeObserving.Observe(more, Node(NodeKinds.Claimed), OnRun(more));
        Assert.Equal(((string?)null, (string?)null, (NodeMove?)null), (waits.Refused, waits.Outcome, waits.Move));
        Assert.Contains("engine", waits.Said, StringComparison.Ordinal);
        Assert.Equal(NodeStates.Reached, NodeObserving.Observe(more, Node(NodeKinds.Claimed), Graph, 1).Outcome);

        foreach (var last in new[]
        {
            QuestUnit(),
            QuestUnit(new QuestStepView("game", "Then there", "Elsewhere.")),
            QuestUnit(new QuestStepView("engine", "Look", "At it.") { SetUpIn = "preview" }),
        })
        {
            var seen = NodeObserving.Observe(last, Node(NodeKinds.Claimed), OnRun(last));
            Assert.Equal((NodeStates.Reached, "quest:q1"), (seen.Outcome, seen.Acceptance?.Subject));
        }
    }
}
