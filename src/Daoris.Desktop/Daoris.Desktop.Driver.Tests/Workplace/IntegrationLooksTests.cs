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
