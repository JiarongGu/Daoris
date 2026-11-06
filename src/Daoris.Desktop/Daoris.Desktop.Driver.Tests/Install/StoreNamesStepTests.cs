using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1t (D232): the desktop store's names are today's after the home's migration, a copy of the store kept first. A binding's
/// door is <c>bindings.door</c>, and the staffing's projection of <c>driver.json</c>'s old keys, which no code reads, is gone.
/// </summary>
public sealed class StoreNamesStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "store-names-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Db => DesktopStore.LocationIn(_home);

    /// <summary>A store as the build before the step left it, holding one binding by its old column.</summary>
    private void OlderStore()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Db)!);
        using var store = DesktopStore.Open(Db, new DesktopStoreOptions
        {
            Steps = [.. DesktopStore.Steps.Where(step => step.Version < StoreNamesStep.Version)],
            Settled = () => StoreSettled.Yes,
        });
        store.Write(BindingStore.Family, write => write.Execute(
            "INSERT INTO bindings (session, adapter, conversation, kept) VALUES ('s1', 'codex-acp', 'c-1', '2026-11-01T00:00:00.0000000Z')"));
    }

    private static IReadOnlyList<string> Columns(DesktopStoreReader store, string table) =>
        store.Query($"SELECT name FROM pragma_table_info('{table}') ORDER BY cid", row => row.GetString(0));

    private static bool HasTable(DesktopStoreReader store, string table) =>
        store.Query("SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name", row => row.GetInt32(0), ("$name", table)).Count > 0;

    [Fact]
    public void A_binding_s_door_is_read_by_its_name_and_the_staffing_projection_is_gone()
    {
        OlderStore();
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new StoreNamesStep().Run(run);

        using var store = DesktopStoreReader.Open(Db)!;
        Assert.Equal(["session", "door", "conversation", "kept"], Columns(store, "bindings").Take(4));
        Assert.Equal([("codex-acp", "c-1")], store.Query("SELECT door, conversation FROM bindings WHERE session = 's1'", row => (row.GetString(0), row.GetString(1))));
        Assert.False(HasTable(store, "role_staffing_projection"));
        Assert.False(HasTable(store, "role_staffing_found"));
        Assert.Equal(["data/desktop.db"], run.Changed);
        Assert.True(File.Exists(Path.Combine(run.Copies, "data", "desktop.db")));
        Assert.Equal(("codex-acp", "c-1"), new BindingStore(_home, () => StoreSettled.Yes, _ => null).Read("s1"));
    }

    [Fact]
    public void A_home_with_no_store_is_left_alone()
    {
        Directory.CreateDirectory(_home);

        new StoreNamesStep().Run(new HomeMigrationRun(_home, HomeMigration.Name));

        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void The_migration_carries_the_step()
    {
        Assert.Contains(HomeMigration.Steps, step => step is StoreNamesStep && step.Name == "store-names");
    }
}
