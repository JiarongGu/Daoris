using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A delivery whose landing made nothing, every entry a preparation it withdrew (a replay's conflict refused it), is settled withdrawn at
/// the reconciler's look, with its sentence and with no checkout to read, so it no longer holds an update back. Its unit's own next landing
/// still goes on from it, and a preparation the look reads withdrawn after a stop goes on, read with no checkout too.
/// </summary>
public sealed class WithdrawnPreparationsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "withdrawn-preparations", Guid.NewGuid().ToString("N")[..8]);

    public WithdrawnPreparationsTests() => Directory.CreateDirectory(_home);

    [Fact]
    public async Task A_delivery_whose_every_entry_is_a_withdrawn_preparation_is_settled_withdrawn_at_the_look_with_no_checkout()
    {
        var claim = Started();
        var store = Store("run-a");
        foreach (var _ in new[] { 1, 2 })
        {
            store.Begin(claim, DeliveryPhases.Prepared, "replayed");
            store.End(claim, DeliveryPhases.Prepared, DeliveryOutcomes.Withdrawn, null, "Nothing was landed: the replay met a conflict.");
        }

        Assert.False(store.Rollback().Allowed);

        var settled = Assert.Single(await Reconciler("run-b").StartAsync(CancellationToken.None));

        Assert.Equal(DeliveryStandings.Settled, settled.Standing);
        Assert.Contains("nothing of it stands beyond a preparation its landing withdrew", settled.Said);
        Assert.Equal(DeliverySettlements.Withdrawn, Store("run-b").Delivery(claim.Key)?.Settled);
        Assert.True(Store("run-b").Rollback().Allowed);
        Assert.Empty(await Reconciler("run-b").StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Its_unit_s_own_next_landing_goes_on_from_it_rather_than_finding_it_settled()
    {
        var claim = Started();
        Store("run-a").Begin(claim, DeliveryPhases.Prepared, "replayed");
        Store("run-a").End(claim, DeliveryPhases.Prepared, DeliveryOutcomes.Withdrawn, null, "Nothing was landed.");

        var before = await Reconciler("run-b").BeforeStartAsync("unit-1", CancellationToken.None);

        Assert.Equal((DeliveryStandings.GoesOn, DeliveryPhases.Prepared), (before?.Standing, before?.Next));
        Assert.Null(Store("run-b").Delivery(claim.Key)?.Settled);
    }

    [Fact]
    public async Task A_preparation_cut_by_a_stop_is_read_withdrawn_with_no_checkout_goes_on_and_is_settled_at_the_next_look()
    {
        var claim = Started();
        Store("run-a").Begin(claim, DeliveryPhases.Prepared, "replaying");

        var cut = Assert.Single(await Reconciler("run-b").StartAsync(CancellationToken.None));

        Assert.Equal((DeliveryStandings.GoesOn, DeliveryPhases.Prepared), (cut.Standing, cut.Next));
        var phase = Assert.Single(Store("run-b").Phases(claim.Key));
        Assert.Equal(DeliveryOutcomes.Withdrawn, phase.Outcome);

        var next = Assert.Single(await Reconciler("run-c", "run-b").StartAsync(CancellationToken.None));
        Assert.Equal(DeliveryStandings.Settled, next.Standing);
        Assert.Equal(DeliverySettlements.Withdrawn, Store("run-c").Delivery(claim.Key)?.Settled);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private DeliveryClaim Started()
    {
        var start = Store("run-a").Start(
            new DeliveryIntent("unit-1", "engine", "a1b2c3d4e5", DeliveryMethods.Branch, "main", "main", "feature/tidy", null, "Tidy", null, "epoch-1"),
            DeliveryAuthorization.Person("accept-1", "r1", "graph-digest", """{"method":"branch"}""", "[]"));
        Assert.True(start.Started, start.Refused ?? start.Held);
        return start.Claim!;
    }

    private DeliveryStore Store(string run, params string[] dead) =>
        new(_home, () => StoreSettled.Yes, each => !dead.Append("run-a").Contains(each.Run), run, StoreProcess.Shell);

    // No checkout of `engine` is here: what these read needs none.
    private DeliveryReconciler Reconciler(string run, params string[] dead) => new(Store(run, dead), _home, _ => null);
}
