using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's sent check the heavy executor has not started reads what it waits for: a slot (how many run of how many), memory (free and
/// needed) or its repository's heavy run, with its place in the slots' queue; a check whose gates run, or one not sent, waits on no slot.
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

    private static VerifyWaiter Waiter(string run, string invocation, int minutes, CheckWait? wait = null) =>
        new(1, 1, run, invocation, "Daoris", Held.AddMinutes(minutes), minutes) { Wait = wait };

    private static readonly VerifyHolder Theirs = new(1, 1, "theirs", "i-theirs", "Daoris", Held);

    [Fact]
    public void A_check_waiting_for_a_slot_reads_how_many_run_of_how_many_never_ready()
    {
        var look = new VerifyLook([Theirs, Theirs with { Run = "other" }], [Waiter("third", "i-third", 1), Waiter("mine", "i-mine", 2, CheckWait.OfSlots(2, 2))]);

        var wait = CheckSlotWaits.Of(Graph, Sent("i-mine"), look)["check"];

        Assert.Equal(CheckSlotWaits.Full, wait.Code);
        Assert.Equal("2", wait.Values["running"]);
        Assert.Equal("2", wait.Values["position"]);
        Assert.Equal(Held.AddMinutes(2), wait.Since);
        var step = new WorkflowRunStep(WorkflowGraphRuns.Cell(Check, null), WorkflowRunStates.Working, WorkflowGraphDetails.Active) { Node = NodeKinds.Check, Wait = wait };
        Assert.Equal("Waits for a check slot: 2 of 2 run; 2 of 2 in its queue.", WorkflowRunWords.Said(step));
    }

    [Fact]
    public void A_check_waiting_for_memory_reads_the_memory_free_and_needed()
    {
        var memory = new CheckWait(CheckWait.Memory, new Dictionary<string, string> { ["free"] = "4.1", ["need"] = "6" });
        var look = new VerifyLook([Theirs], [Waiter("mine", "i-mine", 1, memory)]);

        var wait = CheckSlotWaits.Of(Graph, Sent("i-mine"), look)["check"];

        Assert.Equal(CheckSlotWaits.Memory, wait.Code);
        Assert.Equal("Waits for memory: 4.1 GB free, the next check needs 6 GB; 1 of 1 in its queue.", CheckSlotWaits.Said(wait));
    }

    [Fact]
    public void A_check_waiting_for_its_repository_s_heavy_run_names_the_repository()
    {
        var look = new VerifyLook([Theirs], [Waiter("mine", "i-mine", 1, CheckWait.OfRepository("Daoris", "the verification run theirs"))]);

        var wait = CheckSlotWaits.Of(Graph, Sent("i-mine"), look)["check"];

        Assert.Equal(CheckSlotWaits.Repository, wait.Code);
        Assert.Equal("Waits for Daoris's heavy-run slot, one heavy run of a repository at a time; 1 of 1 in its queue.", CheckSlotWaits.Said(wait));
    }

    [Fact]
    public void A_check_waiting_where_nothing_is_said_yet_reads_its_place_in_the_queue()
    {
        var look = new VerifyLook([], [Waiter("first", "i-first", 1), Waiter("mine", "i-mine", 2), Waiter("last", "i-last", 3)]);

        var wait = CheckSlotWaits.Of(Graph, Sent("i-mine"), look)["check"];

        Assert.Equal(CheckSlotWaits.Queued, wait.Code);
        Assert.Equal("Waits for a check slot: 2 of 3 in its queue.", CheckSlotWaits.Said(wait));
    }

    [Fact]
    public void A_check_whose_gates_run_or_that_is_not_under_way_waits_on_no_slot()
    {
        var look = new VerifyLook([new VerifyHolder(1, 1, "mine", "i-mine", "Daoris", Held)], [Waiter("other", "i-other", 1)]);

        Assert.Empty(CheckSlotWaits.Of(Graph, Sent("i-mine"), look));
        Assert.Empty(CheckSlotWaits.Of(Graph, Sent("i-other", NodeStates.Ready), look));
        Assert.Empty(CheckSlotWaits.Of(Graph, null, look));
    }
}
