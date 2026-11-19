using System.Globalization;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's sent check the heavy executor has not started reads what it waits for: the machine's check slot and the run whose check holds
/// it since when, else its place in the slot's queue; a check whose gates run, or one not sent, waits on no slot.
/// </summary>
public sealed class CheckSlotWaitsTests
{
    private static readonly NodeDeclaration Check = new("check", NodeKinds.Check, 1, NodeReach.Performed);

    private static readonly NodeGraph Graph = new([Check], [], ["check"], "check");

    private static readonly DateTimeOffset Held = new(2026, 11, 20, 8, 15, 0, TimeSpan.Zero);

    private static NodeRecord Sent(string invocation, string state = NodeStates.Active) => new("run:mine")
    {
        Statuses = new Dictionary<string, NodeStatus> { ["check"] = new NodeStatus("run:mine", "check", state) { Attempt = 2 } },
        Attempts =
        [
            new NodeAttempt("check", 2, NodeKinds.Check, new WorkPerformCall("run:mine", NodeKinds.Check, 1, invocation, "k-2", "tip", [], DateTimeOffset.UnixEpoch), AttemptStates.Sent),
        ],
    };

    private static VerifyWaiter Waiter(string run, string invocation, int minutes) => new(1, 1, run, invocation, "Daoris", Held.AddMinutes(minutes), minutes);

    [Fact]
    public void A_check_waiting_for_the_slot_reads_the_run_whose_check_holds_it_and_since_when_never_ready()
    {
        var look = new VerifyLook(new VerifyHolder(1, 1, "theirs", "i-theirs", "Daoris", Held), [Waiter("third", "i-third", 1), Waiter("mine", "i-mine", 2)]);

        var wait = CheckSlotWaits.Of(Graph, Sent("i-mine"), look)["check"];

        Assert.Equal(CheckSlotWaits.HeldBy, wait.Code);
        Assert.Equal("theirs", wait.Values["run"]);
        Assert.Equal("2", wait.Values["position"]);
        Assert.Equal(Held.AddMinutes(2), wait.Since);
        var step = new WorkflowRunStep(WorkflowGraphRuns.Cell(Check, null), WorkflowRunStates.Working, WorkflowGraphDetails.Active) { Node = NodeKinds.Check, Wait = wait };
        var at = Held.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        Assert.Equal($"Waits for the machine's check slot, held by run theirs's check since {at}: 2 of 2 in its queue.",WorkflowRunWords.Said(step));
    }

    [Fact]
    public void A_check_waiting_where_no_check_holds_the_slot_reads_its_place_in_the_queue()
    {
        var look = new VerifyLook(null, [Waiter("first", "i-first", 1), Waiter("mine", "i-mine", 2), Waiter("last", "i-last", 3)]);

        var wait = CheckSlotWaits.Of(Graph, Sent("i-mine"), look)["check"];

        Assert.Equal(CheckSlotWaits.Queued, wait.Code);
        Assert.Equal("Waits for the machine's check slot: 2 of 3 in its queue.", CheckSlotWaits.Said(wait));
    }

    [Fact]
    public void A_check_whose_gates_run_or_that_is_not_under_way_waits_on_no_slot()
    {
        var look = new VerifyLook(new VerifyHolder(1, 1, "mine", "i-mine", "Daoris", Held), [Waiter("other", "i-other", 1)]);

        Assert.Empty(CheckSlotWaits.Of(Graph, Sent("i-mine"), look));
        Assert.Empty(CheckSlotWaits.Of(Graph, Sent("i-other", NodeStates.Ready), look));
        Assert.Empty(CheckSlotWaits.Of(Graph, null, look));
    }
}
