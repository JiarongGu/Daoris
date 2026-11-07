using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>A repository's integration over the bridge (`IntegrateRoutes.cs`): the read the view draws and the owner's acts.</summary>
public sealed class DriverModuleIntegrateTests : DriverModuleBridge
{
    private const string Tip = "1111111111111111111111111111111111111111";

    private static readonly DateTimeOffset At = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private IntegrationStore Store() => new(Home);

    private void Queue(string id, string branch, string unit, int minutes = 0, string? session = null) =>
        Assert.Null(Store().Add(new IntegrationRequest(
            id, "engine", "aurora", branch, Tip, IntegrationRoutes.Recorded, "main",
            IntegrationRequester.Card, new LandingIntegrate().Resolved(), At.AddMinutes(minutes)) { Unit = unit, Session = session }));

    /// <summary>A candidate of <paramref name="request"/> replayed and held at its judge for a decide; its id.</summary>
    private string HeldForDecide(string request)
    {
        var store = Store();
        var claim = store.Form(new IntegrationForming("engine", "aurora", "main", "0000000000000000000000000000000000000000", [request])).Claim!;
        store.End(claim, store.OpenStep(claim.Candidate)!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Tip = Tip, Members = [new IntegrationMemberChange(request, IntegrationMemberStates.Replayed) { Replayed = Tip }],
        });
        store.End(claim, store.Begin(claim, IntegrationStepNames.Judge).Id, new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Said = "an unconfirmed failure" });
        return claim.Candidate;
    }

    private static (bool Done, string? Why) Of(System.Text.Json.JsonElement answer) =>
        (answer.GetProperty("done").GetBoolean(), answer.TryGetProperty("why", out var why) ? why.GetString() : null);

    [Fact]
    public async Task The_view_reads_a_repositorys_queue_and_a_unit_reads_its_head()
    {
        Queue("r1", "feature/a", "unit-a");

        var look = await AnswerAsync(Module(), "INTEGRATION", new { repository = "engine" });
        var head = await AnswerAsync(Module(), "INTEGRATION", new { unit = "unit-a" });

        Assert.Equal("r1", look.GetProperty("look").GetProperty("queue")[0].GetProperty("request").GetString());
        Assert.Equal(0, look.GetProperty("look").GetProperty("waiting").GetArrayLength());
        Assert.Equal("queued", head.GetProperty("head").GetProperty("state").GetString());
    }

    [Fact]
    public async Task A_drop_closes_a_request_in_no_candidate_and_an_unknown_act_changes_nothing()
    {
        Queue("r1", "feature/a", "unit-a");
        var module = Module();

        var refused = await AnswerAsync(module, "INTEGRATE", new { repository = "engine", act = "merge" });
        Assert.Equal((false, "act"), Of(refused));
        Assert.Single(Store().Open());

        var dropped = await AnswerAsync(module, "INTEGRATE", new { repository = "engine", act = "drop", request = "r1" });
        Assert.True(dropped.GetProperty("done").GetBoolean());
        Assert.Empty(Store().Open());
    }

    /// <summary>The owner's decide is recorded on a candidate held for one, once; a second, a word that is none and another repository's candidate change nothing.</summary>
    [Fact]
    public async Task A_decide_is_recorded_on_a_held_candidate_once_and_refused_as_data_otherwise()
    {
        Queue("r1", "feature/a", "unit-a");
        var candidate = HeldForDecide("r1");
        var module = Module();

        Assert.Equal((false, "decide"), Of(await AnswerAsync(module, "INTEGRATE", new { repository = "engine", act = "decide", candidate, decide = "merge" })));
        Assert.Equal((false, "gone"), Of(await AnswerAsync(module, "INTEGRATE", new { repository = "portal", act = "decide", candidate, decide = "land" })));
        Assert.Equal((true, (string?)null), Of(await AnswerAsync(module, "INTEGRATE", new { repository = "engine", act = "decide", candidate, decide = "land" })));
        Assert.Equal((false, "decided"), Of(await AnswerAsync(module, "INTEGRATE", new { repository = "engine", act = "decide", candidate, decide = "split" })));

        Assert.Equal(IntegrationDecides.Land, Store().DecideOf(candidate)!.Decide);
        var look = await AnswerAsync(module, "INTEGRATION", new { repository = "engine" });
        Assert.Equal("land", look.GetProperty("look").GetProperty("candidates")[0].GetProperty("decided").GetString());
    }

    /// <summary>An order moves a queued request first or last, read back in the look's order; one in a candidate keeps its place.</summary>
    [Fact]
    public async Task An_order_moves_a_queued_request_and_the_look_reads_the_queue_in_its_order()
    {
        Queue("r1", "feature/a", "unit-a");
        Queue("r2", "feature/b", "unit-b", minutes: 1);
        var module = Module();

        Assert.Equal((true, (string?)null), Of(await AnswerAsync(module, "INTEGRATE", new { repository = "engine", act = "order", request = "r2", place = "first" })));
        Assert.Equal((false, "place"), Of(await AnswerAsync(module, "INTEGRATE", new { repository = "engine", act = "order", request = "r1", place = "middle" })));

        var queue = (await AnswerAsync(module, "INTEGRATION", new { repository = "engine" })).GetProperty("look").GetProperty("queue");
        Assert.Equal(["r2", "r1"], queue.EnumerateArray().Select(row => row.GetProperty("request").GetString()));
        Assert.Equal("first", queue[0].GetProperty("ordered").GetString());

        HeldForDecide("r1");
        Assert.Equal((false, "stands"), Of(await AnswerAsync(module, "INTEGRATE", new { repository = "engine", act = "order", request = "r1", place = "last" })));
    }

    /// <summary>A superseded commit is answered by its word; a rebase of a request with no session is refused, nothing answered.</summary>
    [Fact]
    public async Task A_superseded_commit_is_answered_and_a_rebase_with_no_session_answers_nothing()
    {
        Queue("r1", "feature/a", "unit-a");
        var store = Store();
        var claim = store.Form(new IntegrationForming("engine", "aurora", "main", "0000000000000000000000000000000000000000", ["r1"])).Claim!;
        store.End(claim, store.OpenStep(claim.Candidate)!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Tip = Tip });
        store.End(claim, store.Begin(claim, IntegrationStepNames.Promote, lineCommit: "0000000000000000000000000000000000000000").Id,
            new IntegrationStepEnd(IntegrationStepOutcomes.Promoted) { Promoted = true });
        store.End(claim, store.Begin(claim, IntegrationStepNames.Settle).Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Members = [new IntegrationMemberChange("r1", IntegrationMemberStates.Settled) { Ends = true }],
            Closes = [new IntegrationRequestClose("r1", IntegrationRequestCloses.Settled)],
            Dispositions = [new IntegrationDisposition("r1", "c1", "refs/daoris/discarded/feature/a", "b1", ["a.txt"], "later work")],
        });
        var module = Module();

        Assert.Equal((false, "no-session"), Of(await AnswerAsync(module, "INTEGRATE", new { repository = "engine", act = "answer", request = "r1", commit = "c1", answer = "rebase" })));
        Assert.Single(store.Pending());
        Assert.Equal((true, (string?)null), Of(await AnswerAsync(module, "INTEGRATE", new { repository = "engine", act = "answer", request = "r1", commit = "c1", answer = "superseded" })));
        Assert.Empty(store.Pending());
    }
}
