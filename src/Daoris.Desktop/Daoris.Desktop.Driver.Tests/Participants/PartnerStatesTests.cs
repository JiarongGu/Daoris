using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (D199 points 2 and 8; the co-agent design §7.2, §7.6): a restart reconciles each piece's state with what it observes, by
/// each side effect's identity, never trusting the last state alone and never running an agent again unasked; the delivery receipt is
/// claimed before a hand, moved along <c>from</c> only while it waits, and an uncertain attempt waits for the person.
/// </summary>
public sealed class PartnerStatesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static PartnerObserved Seen(
        bool readable = true, bool copy = false, string? record = null, PartnerJob job = PartnerJob.Empty, string? at = null) =>
        new(readable, copy, record, job, at);

    private static PartnerState State(string state) => new("p1", state, null, Now.AddMinutes(-5)) { Copy = "p-p1" };

    [Fact]
    public void A_new_ask_is_judged_and_an_absent_state_beside_a_side_effect_is_uncertain()
    {
        Assert.Equal(new PartnerStep(PartnerAct.Judge, null), PartnerReconcile.Next("p1", null, Seen(), Now));
        foreach (var seen in new[] { Seen(copy: true), Seen(record: "s9"), Seen(at: "abc") })
        {
            var step = PartnerReconcile.Next("p1", null, seen, Now);
            Assert.Equal(PartnerAct.None, step.Act);
            Assert.Equal((PartnerStates.Uncertain, PartnerWhy.StateUnreadable), (step.Write!.State, step.Write.Code));
        }
    }

    [Fact]
    public void An_unreadable_state_file_is_uncertain_whatever_it_said()
    {
        var step = PartnerReconcile.Next("p1", State(PartnerStates.Started), Seen(readable: false, job: PartnerJob.Empty), Now);

        Assert.Equal(PartnerAct.None, step.Act);
        Assert.Equal((PartnerStates.Uncertain, PartnerWhy.StateUnreadable, "p-p1"), (step.Write!.State, step.Write.Code, step.Write.Copy));
    }

    [Fact]
    public void Each_state_before_the_turn_is_repaired_by_observing_never_by_doing_twice()
    {
        Assert.Equal(PartnerAct.Judge, PartnerReconcile.Next("p1", State(PartnerStates.Asked), Seen(), Now).Act);
        Assert.Equal(PartnerAct.Judge, PartnerReconcile.Next("p1", State(PartnerStates.Waiting), Seen(), Now).Act);
        Assert.Equal(PartnerAct.Prepare, PartnerReconcile.Next("p1", State(PartnerStates.Approved), Seen(), Now).Act);
        // A partial copy is Daoris's own work: removed and made again.
        Assert.Equal(new PartnerStep(PartnerAct.RemoveCopyThenPrepare, null), PartnerReconcile.Next("p1", State(PartnerStates.Preparing), Seen(copy: true), Now));
        Assert.Equal(new PartnerStep(PartnerAct.Prepare, null), PartnerReconcile.Next("p1", State(PartnerStates.Preparing), Seen(), Now));

        // A record already open for the piece is taken as this one, and `started` is written naming it.
        var opened = PartnerReconcile.Next("p1", State(PartnerStates.Preparing), Seen(copy: true, record: "s9"), Now);
        Assert.Equal(PartnerAct.Watch, opened.Act);
        Assert.Equal((PartnerStates.Started, "s9"), (opened.Write!.State, opened.Write.Partner));
    }

    [Fact]
    public void A_started_piece_keeps_only_once_its_job_is_known_empty_else_it_is_uncertain_and_its_copy_untouched()
    {
        var empty = PartnerReconcile.Next("p1", State(PartnerStates.Started), Seen(copy: true, job: PartnerJob.Empty), Now);
        Assert.Equal(PartnerAct.Keep, empty.Act);
        Assert.Equal((PartnerStates.Ending, (DateTimeOffset?)Now), (empty.Write!.State, empty.Write.Kept));

        Assert.Equal(new PartnerStep(PartnerAct.Watch, null), PartnerReconcile.Next("p1", State(PartnerStates.Started), Seen(job: PartnerJob.Running), Now));

        var unknown = PartnerReconcile.Next("p1", State(PartnerStates.Started), Seen(copy: true, job: PartnerJob.Unknown), Now);
        Assert.Equal(PartnerAct.None, unknown.Act);
        Assert.Equal((PartnerStates.Uncertain, PartnerWhy.JobUnknown), (unknown.Write!.State, unknown.Write.Code));
        Assert.True(PartnerStates.HoldsCopy(unknown.Write));
    }

    [Fact]
    public void After_the_turn_each_state_goes_on_from_its_own_side_effect_and_the_final_ones_and_the_persons_do_nothing()
    {
        // The keeper's moment is on file, so keeping again makes the same commit.
        Assert.Equal(new PartnerStep(PartnerAct.Keep, null), PartnerReconcile.Next("p1", State(PartnerStates.Ending), Seen(copy: true), Now));
        Assert.Equal(PartnerAct.Answer, PartnerReconcile.Next("p1", State(PartnerStates.Imported), Seen(), Now).Act);
        Assert.Equal(PartnerAct.Hand, PartnerReconcile.Next("p1", State(PartnerStates.Answered), Seen(), Now).Act);
        foreach (var state in new[]
        {
            PartnerStates.Held, PartnerStates.Uncertain, PartnerStates.Handed, PartnerStates.TakenIn, PartnerStates.NotTakenIn,
            PartnerStates.Discarded, PartnerStates.Refused,
        })
        {
            Assert.Equal(new PartnerStep(PartnerAct.None, null), PartnerReconcile.Next("p1", State(state), Seen(copy: true, job: PartnerJob.Running), Now));
        }
    }

    [Fact]
    public void The_create_only_ref_reads_absent_as_create_the_keepers_commit_as_imported_and_another_as_uncertain()
    {
        Assert.Null(PartnerReconcile.AfterRef(null, "abc"));
        Assert.Equal(PartnerStates.Imported, PartnerReconcile.AfterRef("ABC", "abc"));
        Assert.Equal(PartnerStates.Uncertain, PartnerReconcile.AfterRef("def", "abc"));
    }

    [Fact]
    public void A_reservation_counts_until_the_record_opens_open_pieces_from_approved_on_and_the_box_owns_its_copy_until_removed()
    {
        Assert.Equal([PartnerStates.Approved, PartnerStates.Preparing], PartnerStates.All.Where(PartnerStates.Reserves));
        Assert.Equal(
            [PartnerStates.Approved, PartnerStates.Preparing, PartnerStates.Started, PartnerStates.Ending, PartnerStates.Uncertain],
            PartnerStates.All.Where(PartnerStates.Open));
        Assert.True(PartnerStates.HoldsCopy(State(PartnerStates.Held)));
        Assert.True(PartnerStates.HoldsCopy(State(PartnerStates.Answered)));
        Assert.False(PartnerStates.HoldsCopy(State(PartnerStates.Answered) with { CopyRemoved = true }));
        Assert.False(PartnerStates.HoldsCopy(State(PartnerStates.Asked)));
        Assert.False(PartnerStates.HoldsCopy(State(PartnerStates.Discarded)));
    }

    [Fact]
    public void The_receipt_is_claimed_before_the_hand_and_moved_along_from_only_while_it_waits()
    {
        var claimed = PartnerDelivery.Claim(null, "p1", "lead", Now);
        Assert.Equal(new PartnerReceipt("p1", "lead", "partner:p1", PartnerDelivery.Waiting, Now), claimed);
        Assert.True(PartnerDelivery.MayHand(claimed));

        // The lead handed over before the answer was taken: the claim moves to its successor, explicitly.
        var moved = PartnerDelivery.Claim(claimed, "p1", "successor", Now.AddMinutes(1));
        Assert.Equal(("successor", "partner:p1", PartnerDelivery.Waiting), (moved.To, moved.Key, moved.Outcome));

        // Once attempted, the claim stays where it went, and nothing hands it again.
        var attempted = moved with { Outcome = PartnerDelivery.Attempted };
        Assert.Same(attempted, PartnerDelivery.Claim(attempted, "p1", "third", Now.AddMinutes(2)));
        Assert.False(PartnerDelivery.MayHand(attempted));
    }

    [Fact]
    public void An_attempt_a_restart_settles_is_taken_or_uncertain_and_an_uncertain_one_waits_for_the_person()
    {
        var attempted = new PartnerReceipt("p1", "lead", "partner:p1", PartnerDelivery.Attempted, Now);

        Assert.Equal(PartnerDelivery.Taken, PartnerDelivery.Settle(attempted, taken: true, Now).Outcome);
        var uncertain = PartnerDelivery.Settle(attempted, taken: false, Now);
        Assert.Equal(PartnerDelivery.Uncertain, uncertain.Outcome);
        Assert.False(PartnerDelivery.MayHand(uncertain));
        Assert.Same(uncertain, PartnerDelivery.Claim(uncertain, "p1", "successor", Now));

        // A key already taken stays taken: the hand door answers it *handed*.
        var taken = attempted with { Outcome = PartnerDelivery.Taken };
        Assert.Same(taken, PartnerDelivery.Settle(taken, taken: false, Now));
    }
}
