using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDNODE1c2 (the land node design §4 and §7): the doors that read the delivery record. The update's door back to an older build is refused
/// while a delivery is unresolved, so an older door never lands, or calls a plugin without the key, behind a newer delivery; a newer build, or
/// one whose version does not read, is no going back. The landing's plan reads a unit's newest delivery: none, running at its phase, delivered.
/// </summary>
public sealed class DeliveryDoorsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "delivery-doors", Guid.NewGuid().ToString("N")[..8]);

    public DeliveryDoorsTests() => Directory.CreateDirectory(_home);

    [Fact]
    public void The_plan_reads_a_units_newest_delivery_running_at_its_phase_then_delivered()
    {
        Assert.Null(LandingDelivery.Newest(_home, "unit-1"));

        var store = new DeliveryStore(_home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell);
        var started = store.Start(
            new DeliveryIntent("unit-1", "engine", "a1b2c3d4e5", DeliveryMethods.Branch, "main", "main", "feature/tidy", null, "Tidy", null, "epoch-1"),
            DeliveryAuthorization.Automatic("repository:engine", "r1", "graph-digest", """{"method":"branch"}""", "[]"));
        store.Begin(started.Claim!, DeliveryPhases.Prepared, "made");

        var running = LandingDelivery.Newest(_home, "unit-1")!;
        Assert.Equal((DeliveryAuthorities.Automatic, DeliveryPhases.Prepared, true), (running.Record.Authorization.Authority, running.Phase, running.Record.Unresolved));

        store.End(started.Claim!, DeliveryPhases.Prepared, DeliveryOutcomes.Done, "a1b2c3d4e5", "made");
        foreach (var phase in new[] { DeliveryPhases.RefWritten, DeliveryPhases.Recorded })
        {
            store.Begin(started.Claim!, phase, phase);
            store.End(started.Claim!, phase, DeliveryOutcomes.Done, phase, phase);
        }

        store.Settle(started.Claim!, DeliverySettlements.Delivered, "delivered");
        var delivered = LandingDelivery.Newest(_home, "unit-1")!;
        Assert.Equal((DeliverySettlements.Delivered, (string?)null), (delivered.Record.Settled, delivered.Phase));
        Assert.NotNull(delivered.Record.SettledAt);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    [Fact]
    public void A_downgrade_is_refused_while_a_delivery_is_open_and_allowed_once_it_settles()
    {
        Assert.Null(InstallRollback.Refusal(_home, "0.41.0", "0.42.0"));

        var store = new DeliveryStore(_home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell);
        var started = store.Start(
            new DeliveryIntent("unit-1", "engine", "a1b2c3d4e5", DeliveryMethods.Branch, "main", "main", "feature/tidy", null, "Tidy", null, "epoch-1"),
            DeliveryAuthorization.Person("accept-1", "r1", "graph-digest", """{"method":"branch"}""", "[]"));
        Assert.True(started.Started, started.Refused ?? started.Held);

        Assert.Contains("1 delivery is unresolved", InstallRollback.Refusal(_home, "0.41.0", "0.42.0"));
        Assert.Null(InstallRollback.Refusal(_home, "0.42.1", "0.42.0"));
        Assert.Null(InstallRollback.Refusal(_home, "0.42.0-dev.4", "0.42.0+abc"));
        Assert.Null(InstallRollback.Refusal(_home, "a build", "0.42.0"));

        store.Settle(started.Claim!, DeliverySettlements.Stopped, "stopped by the person");
        Assert.Null(InstallRollback.Refusal(_home, "0.41.0", "0.42.0"));
    }
}
