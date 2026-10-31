using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>INTEGRATE1h (the integration design §5.8): the Integration view's read and a unit's head, read from the integration record and never copied.</summary>
public sealed class IntegrationLooksTests : IDisposable
{
    private const string Line = "0000000000000000000000000000000000000000";
    private const string Tip = "1111111111111111111111111111111111111111";

    private static readonly DateTimeOffset At = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "integration-looks", Guid.NewGuid().ToString("N")[..8]);

    public IntegrationLooksTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private IntegrationStore Store() => new(_home, () => StoreSettled.Yes, _ => true, "run-a", StoreProcess.Shell);

    private static IntegrationRequest Request(string id, string branch, string? unit = null) =>
        new(id, "engine", "aurora", branch, Tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.Card, new LandingIntegrate().Resolved(), At) { Unit = unit };

    [Fact]
    public void A_queued_request_is_in_the_queue_and_its_unit_head_says_queued()
    {
        var store = Store();
        Assert.Null(store.Add(Request("r1", "feature/a", "unit-a")));

        var look = IntegrationLooks.Of(store, "engine");

        Assert.Equal(["r1"], look.Queue.Select(request => request.Request));
        Assert.Empty(look.Candidates);
        Assert.Equal(new IntegrationHead(IntegrationHeads.Queued, "main"), IntegrationLooks.HeadOf(store, "unit-a"));
    }

    [Fact]
    public void A_standing_candidate_lists_its_members_and_a_fixed_strip_of_steps()
    {
        var store = Store();
        Assert.Null(store.Add(Request("r1", "feature/a", "unit-a")));
        store.Form(new IntegrationForming("engine", "aurora", "main", Line, ["r1"]));

        var look = IntegrationLooks.Of(store, "engine");

        var candidate = Assert.Single(look.Candidates);
        Assert.Equal(["r1"], candidate.Members.Select(member => member.Request));
        Assert.Equal(IntegrationStepNames.All, candidate.Steps.Select(step => step.Step));
        Assert.Equal(IntegrationStepNames.Form, candidate.At);
        Assert.Empty(look.Queue);
        Assert.Equal(IntegrationHeads.InCandidate, IntegrationLooks.HeadOf(store, "unit-a")!.State);
    }

    /// <summary>A candidate of r1 replayed and held at its judge for a decide, by the words a hold verdict ends it with.</summary>
    private static IntegrationClaim HeldForDecide(IntegrationStore store)
    {
        Assert.Null(store.Add(Request("r1", "feature/a", "unit-a")));
        var claim = store.Form(new IntegrationForming("engine", "aurora", "main", Line, ["r1"])).Claim!;
        store.End(claim, store.OpenStep(claim.Candidate)!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Tip = Tip, Members = [new IntegrationMemberChange("r1", IntegrationMemberStates.Replayed) { Replayed = Tip }],
        });
        var judge = store.Begin(claim, IntegrationStepNames.Judge);
        store.End(claim, judge.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Said = "held for a decide (decide): an unconfirmed failure of unit." });
        return claim;
    }

    /// <summary>INTEGRATE1h: a candidate the policy held says it waits for the owner's decide with the judge's words; a decide recorded is said until its run acts.</summary>
    [Fact]
    public void A_candidate_held_for_a_decide_names_the_act_and_who_presses_it_and_a_recorded_decide_until_its_run_acts()
    {
        var store = Store();
        var claim = HeldForDecide(store);

        var held = Assert.Single(IntegrationLooks.Of(store, "engine").Candidates);

        Assert.Equal(("decide", "decide", "owner", (string?)null), (held.Holds, held.Release, held.By, held.Decided));
        Assert.Contains("an unconfirmed failure of unit", held.Why, StringComparison.Ordinal);
        Assert.Equal(IntegrationHeads.Held, IntegrationLooks.HeadOf(store, "unit-a")!.State);

        store.Decide(claim.Candidate, IntegrationDecides.Land, IntegrationRequester.Person);
        var decided = Assert.Single(IntegrationLooks.Of(store, "engine").Candidates);

        Assert.Equal(("decide", (string?)null, IntegrationDecides.Land), (decided.Holds, decided.Release, decided.Decided));
    }

    /// <summary>INTEGRATE1h: the queue is read in its order, each row saying where an order moved it; a member held for its conflict names no act none releases it by.</summary>
    [Fact]
    public void The_queue_is_read_in_its_order_with_where_each_was_moved_and_a_conflict_names_no_act_that_would_refuse()
    {
        var store = Store();
        Assert.Null(store.Add(Request("r1", "feature/a")));
        Assert.Null(store.Add(Request("r2", "feature/b") with { At = At.AddMinutes(1) }));
        Assert.Null(store.Add(Request("r3", "feature/c") with { At = At.AddMinutes(2) }));
        store.Order("r3", IntegrationOrders.First);
        store.Order("r1", IntegrationOrders.Last);

        var look = IntegrationLooks.Of(store, "engine");

        Assert.Equal([("r3", (string?)IntegrationOrders.First), ("r2", null), ("r1", IntegrationOrders.Last)], look.Queue.Select(row => (row.Request, row.Ordered)));

        var claim = store.Form(new IntegrationForming("engine", "aurora", "main", Line, ["r2"])).Claim!;
        store.End(claim, store.OpenStep(claim.Candidate)!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Members = [new IntegrationMemberChange("r2", IntegrationMemberStates.Held) { Said = "it conflicts in a.txt" }],
        });
        var conflict = Assert.Single(IntegrationLooks.Of(store, "engine").Candidates);
        Assert.Equal(("conflict", (string?)null, "owner"), (conflict.Holds, conflict.Release, conflict.By));
    }

    [Fact]
    public void A_dropped_and_a_settled_request_leave_the_queue_for_the_history_the_head_reads()
    {
        var store = Store();
        Assert.Null(store.Add(Request("r1", "feature/a", "unit-a")));
        Assert.True(store.Close("r1", IntegrationRequestCloses.Dropped));

        Assert.Empty(IntegrationLooks.Of(store, "engine").Queue);
        Assert.Equal(IntegrationHeads.Dropped, IntegrationLooks.HeadOf(store, "unit-a")!.State);
        Assert.Null(IntegrationLooks.HeadOf(store, "unit-none"));
    }
}
