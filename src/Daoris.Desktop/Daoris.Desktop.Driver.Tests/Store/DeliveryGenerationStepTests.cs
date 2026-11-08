using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The step that keys a delivery by its generation as well: every row written before it is generation 1 with its key unchanged, the
/// unique index holds one row per unit, tip, method, target and generation, the writer stamp stays where it is, and a reader of a store
/// before the step reads every delivery as the first.
/// </summary>
public sealed class DeliveryGenerationStepTests : IDisposable
{
    private const string At = "2026-11-01T00:00:00.0000000+00:00";

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "delivery-generation-step", Guid.NewGuid().ToString("N")[..8]);

    private readonly List<IDisposable> _open = [];

    public DeliveryGenerationStepTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        foreach (var each in _open) each.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static DesktopStoreOptions Before() =>
        new() { Writer = DesktopStore.PolicyWriter, Steps = [.. DesktopStore.Steps.Take(DesktopStore.GenerationVersion - 1)], Settled = () => StoreSettled.Yes };

    private DesktopStore Open(DesktopStoreOptions? options = null)
    {
        var store = DesktopStore.Open(DesktopStore.LocationIn(_home), options ?? new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        _open.Add(store);
        return store;
    }

    private static void Row(DesktopStore store, string key, bool withGeneration = false, long generation = 1, string? settled = null)
    {
        var sql =
            "INSERT INTO deliveries (key, format, unit, repository, tip, method, target, line, message, epoch, authority, accept, revision, graph, options, "
            + "receipts, claim_run, claim_generation, at, settled, settled_at" + (withGeneration ? ", generation" : "") + ") "
            + "VALUES ($key, 1, 'unit-1', 'engine', 'abc', 'branch', 'main', 'main', 'Tidy', 'epoch-1', 'person', 'accept-1', 'r1', 'g', '{}', '[]', $run, 1, $at, "
            + "$settled, $settledAt" + (withGeneration ? ", $generation" : "") + ")";
        (string, object?)[] values = withGeneration
            ? [("$key", key), ("$run", store.Run), ("$at", At), ("$settled", settled), ("$settledAt", settled is null ? null : At), ("$generation", generation)]
            : [("$key", key), ("$run", store.Run), ("$at", At), ("$settled", settled), ("$settledAt", settled is null ? null : At)];
        store.Write(DeliveryStore.Family, write => write.Execute(sql, values));
    }

    [Fact]
    public void A_row_written_before_the_step_is_generation_1_under_its_own_key_and_the_stamp_stays_at_3()
    {
        var step = Assert.Single(DesktopStore.Steps, each => each.Version == DesktopStore.GenerationVersion);
        Assert.Equal(DesktopStore.PolicyWriter, step.Writer);
        var key = DeliveryStore.KeyOf("unit-1", "abc", "branch", "main");
        using (var before = Open(Before()))
        {
            Row(before, key, settled: "withdrawn");
            Assert.Equal(DesktopStore.GenerationVersion - 1, before.Stamp.Version);
        }

        SqliteConnection.ClearAllPools();
        var store = Open();

        Assert.Equal((DesktopStore.Steps.Count, DesktopStore.WriterStamp), store.Stamp);
        Assert.Equal([(key, 1L)], store.Query("SELECT key, generation FROM deliveries", row => (row.GetString(0), row.GetInt64(1))));
        Assert.Equal(key, DeliveryStore.KeyOf("unit-1", "abc", "branch", "main", generation: 1));
        var kept = new DeliveryStore(_home).Delivery(key)!;
        Assert.Equal((1L, DeliverySettlements.Withdrawn), (kept.Generation, kept.Settled));
    }

    [Fact]
    public void The_unique_index_holds_one_row_per_unit_tip_method_target_and_generation_and_a_write_without_a_generation_is_the_first()
    {
        var store = Open();
        Row(store, "k1", settled: "withdrawn");

        // 2067 is SQLite's unique refusal: the same four parts at the same generation, with or without it named.
        Assert.Equal(2067, Assert.Throws<SqliteException>(() => Row(store, "k2")).SqliteExtendedErrorCode);
        Assert.Equal(2067, Assert.Throws<SqliteException>(() => Row(store, "k3", withGeneration: true, generation: 1)).SqliteExtendedErrorCode);
        Row(store, "k4", withGeneration: true, generation: 2);

        Assert.Equal([("k1", 1L), ("k4", 2L)], store.Query("SELECT key, generation FROM deliveries ORDER BY generation", row => (row.GetString(0), row.GetInt64(1))));
        Assert.Contains(
            "CREATE UNIQUE INDEX deliveries_generation ON deliveries (unit, tip, method, target, generation)",
            store.Query(
                "SELECT sql FROM sqlite_master WHERE type = 'index' AND tbl_name = 'deliveries' AND sql IS NOT NULL", row => row.GetString(0)));
    }

    [Fact]
    public void A_reader_of_a_store_before_the_step_reads_every_delivery_as_the_first()
    {
        var key = DeliveryStore.KeyOf("unit-1", "abc", "branch", "main");
        using (var before = Open(Before()))
        {
            Row(before, key);
        }

        SqliteConnection.ClearAllPools();
        var store = new DeliveryStore(_home);

        Assert.Equal(1L, store.Delivery(key)!.Generation);
        Assert.Equal([1L], store.OfUnit("unit-1").Select(each => each.Generation));
        Assert.Equal(1L, store.UnresolvedFor("unit-1")!.Generation);
    }
}
