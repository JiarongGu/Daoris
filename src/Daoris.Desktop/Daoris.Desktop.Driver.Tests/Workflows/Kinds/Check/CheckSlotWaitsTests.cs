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
        Assert.Equal("Waits for a check slot: 2 of 2 run; 2 of 2 in its queue; ahead: run third, normal.", WorkflowRunWords.Said(step));
    }

    [Fact]
    public void A_check_waiting_for_memory_reads_the_memory_free_and_needed()
    {
        var memory = new CheckWait(CheckWait.Memory, new Dictionary<string, string> { ["free"] = "4.1", ["need"] = "6", ["source"] = "learned" });
        var look = new VerifyLook([Theirs], [Waiter("mine", "i-mine", 1, memory)]);

        var wait = CheckSlotWaits.Of(Graph, Sent("i-mine"), look)["check"];

        Assert.Equal(CheckSlotWaits.Memory, wait.Code);
        Assert.Equal("Waits for memory: 4.1 GB free, the next check needs 6 GB (learned); 1 of 1 in its queue.", CheckSlotWaits.Said(wait));
    }

    [Fact]
    public void A_memory_wait_says_how_many_checks_still_starting_its_number_counts_and_leaves_out_a_basis_it_was_not_given()
    {
        var counted = new CheckWait(CheckWait.Memory, new Dictionary<string, string> { ["free"] = "22.6", ["need"] = "31.3", ["source"] = "learned", ["starting"] = "2" });
        var kept = new CheckWait(CheckWait.Memory, new Dictionary<string, string> { ["free"] = "4.1", ["need"] = "8" });

        var said = CheckSlotWaits.Of(Graph, Sent("i-mine"), new VerifyLook([Theirs], [Waiter("mine", "i-mine", 1, counted)]))["check"];
        Assert.Equal("2", said.Values["starting"]);
        Assert.Equal("Waits for memory: 22.6 GB free, the next check needs 31.3 GB (learned) with 2 still starting; 1 of 1 in its queue.", CheckSlotWaits.Said(said));

        var bare = CheckSlotWaits.Of(Graph, Sent("i-mine"), new VerifyLook([Theirs], [Waiter("mine", "i-mine", 1, kept)]))["check"];
        Assert.DoesNotContain("basis", bare.Values.Keys);
        Assert.Equal("Waits for memory: 4.1 GB free, the next check needs 8 GB; 1 of 1 in its queue.", CheckSlotWaits.Said(bare));
    }

    [Fact]
    public void A_check_waiting_for_the_reserve_reads_the_memory_free_needed_and_kept_for_the_person()
    {
        var reserve = new CheckWait(CheckWait.Reserve, new Dictionary<string, string> { ["free"] = "24.0", ["need"] = "8", ["source"] = "declared", ["reserve"] = "21" });
        var look = new VerifyLook([Theirs], [Waiter("mine", "i-mine", 1, reserve)]);

        var wait = CheckSlotWaits.Of(Graph, Sent("i-mine"), look)["check"];

        Assert.Equal(CheckSlotWaits.Reserve, wait.Code);
        Assert.Equal("Waits for the reserve kept for your own work: 24.0 GB free, the next check needs 8 GB (declared) and 21 GB stays free; 1 of 1 in its queue.", CheckSlotWaits.Said(wait));
    }

    [Fact]
    public void A_check_waiting_for_its_repository_s_heavy_run_names_the_repository()
    {
        var look = new VerifyLook([Theirs], [Waiter("mine", "i-mine", 1, CheckWait.OfRepository("Daoris", "the verification run theirs"))]);

        var wait = CheckSlotWaits.Of(Graph, Sent("i-mine"), look)["check"];

        Assert.Equal(CheckSlotWaits.Repository, wait.Code);
        Assert.Equal("Waits for a heavy-run slot of Daoris: its other heavy runs hold every one it has; 1 of 1 in its queue.", CheckSlotWaits.Said(wait));
    }

    [Fact]
    public void A_check_waiting_for_the_admission_s_lock_says_so_never_a_slot_count()
    {
        var look = new VerifyLook([], [Waiter("mine", "i-mine", 1, CheckWait.Held)]);

        var wait = CheckSlotWaits.Of(Graph, Sent("i-mine"), look)["check"];

        Assert.Equal(CheckSlotWaits.Admitting, wait.Code);
        Assert.Contains(CheckSlotWaits.Admitting, CheckSlotWaits.Codes);
        Assert.Equal("Waits for the admission's lock, which another start holds; 1 of 1 in its queue.", CheckSlotWaits.Said(wait));
    }

    [Fact]
    public void A_check_waiting_where_nothing_is_said_yet_reads_its_place_in_the_queue()
    {
        var look = new VerifyLook([], [Waiter("first", "i-first", 1), Waiter("mine", "i-mine", 2), Waiter("last", "i-last", 3)]);

        var wait = CheckSlotWaits.Of(Graph, Sent("i-mine"), look)["check"];

        Assert.Equal(CheckSlotWaits.Queued, wait.Code);
        Assert.Equal("Waits for a check slot: 2 of 3 in its queue; ahead: run first, normal.", CheckSlotWaits.Said(wait));
    }

    [Fact]
    public void A_check_s_wait_line_names_its_class_and_each_item_ahead_with_its_class_and_reason()
    {
        var urgent = Waiter("fix", "i-fix", 4) with { Class = PriorityClasses.Urgent, Source = PriorityBook.Quest, By = "the person", Why = "it slows every session" };
        var fromAsk = Waiter("feature", "i-feature", 1) with { Class = PriorityClasses.High, Source = PriorityBook.Ask, By = "session s-9", Why = "the release" };
        var look = new VerifyLook([Theirs], [urgent, fromAsk, Waiter("mine", "i-mine", 2, CheckWait.OfSlots(1, 1)) with { Class = PriorityClasses.Low, Source = PriorityBook.Quest, By = "the person", Why = "can wait" }]);

        var wait = CheckSlotWaits.Of(Graph, Sent("i-mine"), look)["check"];

        Assert.Equal(("3", "3", PriorityClasses.Low, "2"), (wait.Values["position"], wait.Values["of"], wait.Values["class"], wait.Values["ahead"]));
        Assert.Equal(("fix", PriorityClasses.Urgent, PriorityBook.Quest), (wait.Values["ahead.1.run"], wait.Values["ahead.1.class"], wait.Values["ahead.1.source"]));
        Assert.Equal(
            "Waits for a check slot: 1 of 1 run; 3 of 3 in its queue, as low (set by the person: can wait); ahead: run fix, urgent (set by the person: it slows every session); "
            + "run feature, high (from its ask, set by session s-9: the release).",
            CheckSlotWaits.Said(wait));
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
