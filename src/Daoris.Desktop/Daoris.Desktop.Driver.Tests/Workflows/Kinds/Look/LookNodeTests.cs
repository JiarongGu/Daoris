using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GateFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-NODE1b (the fabric events design §E4, §E2): the <c>look</c> observed from its gate's records alone and through the one table. It is
/// reached by the person's <i>reviewed</i> at the bound commit, rejected by their <i>not yet</i> and passed over by their skip, in their words; a
/// landing at the bound commit keeps the review that let it go. Anything else waits.
/// </summary>
public sealed class LookNodeTests
{
    [Fact]
    public void A_look_is_reached_by_the_persons_reviewed_at_the_bound_commit_and_rejected_by_their_not_yet()
    {
        var look = Node(NodeKinds.Look);

        var reviewed = Applied(look, Reads.Look(Branch(), look, Binding(NodeKinds.Look),
            Gate(ReviewStates.Reviewed, new QuestReviewVerdictView(ReviewVerdicts.Reviewed) { Commit = Tip, At = Then })));
        Assert.Null(reviewed.Refused);
        Assert.Equal((NodeStates.Reached, NodeSource.Person), (reviewed.Status.State, reviewed.Status.By));
        Assert.Equal(Binding(NodeKinds.Look).Id, reviewed.Status.Acceptance!.Id);

        var notYet = Applied(look, Reads.Look(Branch(), look, Binding(NodeKinds.Look),
            Gate(ReviewStates.NotYet, new QuestReviewVerdictView(ReviewVerdicts.NotYet) { Words = "the chart is cut off", At = Then })));
        Assert.Null(notYet.Refused);
        Assert.Equal((NodeStates.Rejected, NodeSource.Person, "the chart is cut off"), (notYet.Status.State, notYet.Status.By, notYet.Status.Words));
    }

    [Fact]
    public void A_look_the_person_skipped_is_passed_over_in_their_words_and_never_without_them()
    {
        var look = Node(NodeKinds.Look);

        var skipped = Applied(look, Reads.Look(Branch(), look, Binding(NodeKinds.Look), Gate(ReviewStates.Skipped, skipped: "a copy edit")));
        Assert.Null(skipped.Refused);
        Assert.Equal((NodeStates.Skipped, NodeSource.Person, "a copy edit", Scope), (skipped.Status.State, skipped.Status.By, skipped.Status.Words, skipped.Status.Scope));

        // A skip kept with no words is not the person's word here: the look waits rather than pass over on words nobody gave.
        Assert.Null(Reads.Look(Branch(), look, Binding(NodeKinds.Look), Gate(ReviewStates.Skipped, skipped: "")).Move);
    }

    [Fact]
    public void A_look_waits_on_every_other_state_and_on_a_judgement_of_another_commit_or_repository()
    {
        var look = Node(NodeKinds.Look);
        var reviewed = new QuestReviewVerdictView(ReviewVerdicts.Reviewed) { Commit = Tip, At = Then };
        foreach (var (why, gate) in new (string, ReviewGateState?)[]
        {
            ("no read", null),
            ("not shown", Gate(ReviewStates.NotShown)),
            ("being set up", Gate(ReviewStates.BeingSetUp)),
            ("shown", Gate(ReviewStates.Shown)),
            ("not held", Gate(ReviewStates.NotHeld, reviewed)),
            ("unread", Gate(ReviewStates.Unread)),
            ("cannot start", Gate(ReviewStates.CannotStart)),
            ("a not yet with no words", Gate(ReviewStates.NotYet, new QuestReviewVerdictView(ReviewVerdicts.NotYet) { At = Then })),
            ("reviewed at another commit", Gate(ReviewStates.Reviewed, reviewed, tip: "tip-2")),
            ("reviewed with no commit read", Gate(ReviewStates.Reviewed, reviewed, tip: null)),
            ("reviewed for another repository", Gate(ReviewStates.Reviewed, reviewed, repository: "other")),
        })
        {
            var seen = Reads.Look(Branch(), look, Binding(NodeKinds.Look), gate);
            Assert.Equal((why, (string?)null, (NodeMove?)null), (why, seen.Outcome, seen.Move));
        }
    }

    [Fact]
    public void A_landing_recorded_at_the_bound_commit_keeps_the_review_that_let_it_go()
    {
        var look = Node(NodeKinds.Look);
        NodeObservation Seen(params LandedBranch[] landings) => Reads.Look(Branch(landings), look, Binding(NodeKinds.Look), Gate(ReviewStates.Shown));

        var reviewed = Applied(look, Seen(Landed(review: new LandingReview(ReviewVerdicts.Reviewed, "preview", "q2") { Commit = Tip, At = Then })));
        Assert.Equal((NodeStates.Reached, NodeSource.Person), (reviewed.Status.State, reviewed.Status.By));

        var skipped = Applied(look, Seen(Landed(review: new LandingReview(ReviewVerdicts.Skipped, null, "q1") { Words = "a copy edit", At = Then })));
        Assert.Equal((NodeStates.Skipped, NodeSource.Person, "a copy edit"), (skipped.Status.State, skipped.Status.By, skipped.Status.Words));

        // A landing that kept no review had none asked of it by the review's gate, whose outcome the landing keeps: that record passes the
        // look over, never in anyone's words.
        var none = Applied(look, Seen(Landed()));
        Assert.Equal((NodeStates.Skipped, NodeSource.Review, (string?)null), (none.Status.State, none.Status.By, none.Status.Words));

        // A landing at another commit says nothing of this one: the gate's own reading stands, and it waits.
        Assert.Null(Seen(Landed(tip: "tip-0", review: new LandingReview(ReviewVerdicts.Reviewed, "preview", "q2") { Commit = "tip-0" })).Move);
    }

    /// <summary>The person's words a run's look was skipped with, or their skip waits to apply, are read from the run's record (CHATFLOW1d2).</summary>
    [Fact]
    public void A_skipped_looks_words_are_read_from_the_record_or_the_skip_waiting_on_it()
    {
        var look = Node(NodeKinds.Look);
        var skipped = Applied(look, Reads.Look(Branch(), look, Binding(NodeKinds.Look), Gate(ReviewStates.Skipped, skipped: "a copy edit"))).Status;
        var record = new NodeRecord(Unit) { Statuses = new Dictionary<string, NodeStatus> { [look.Id] = skipped with { By = NodeCause.Person } } };

        Assert.Equal("a copy edit", LookReading.SkippedWords(record, look.Id));
        Assert.Null(LookReading.SkippedWords(record, "other"));
        Assert.Null(LookReading.SkippedWords(null, look.Id));
        Assert.Null(LookReading.SkippedWords(record, null));
    }
}
