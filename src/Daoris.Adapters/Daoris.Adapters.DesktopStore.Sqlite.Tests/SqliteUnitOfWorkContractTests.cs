using Daoris.Ports.Store;
using Daoris.Testing;
using Xunit;

namespace Daoris.Adapters.DesktopStore.Sqlite.Tests;

[Trait("Category", "Needs")]
[Trait("Needs", "filesystem")]
public sealed class SqliteUnitOfWorkContractTests : UnitOfWorkContract
{
    private static readonly string Scratch = Path.Combine(AppContext.BaseDirectory, "store-contract");

    protected override string Place(string name) => Path.Combine(Scratch, name, "desktop.db");

    protected override IDesktopStoreEngine Open(string place, DesktopStoreEngineOptions options) =>
        SqliteDesktopStores.Engines.Open(place, options);

    protected override bool Woken(string place) => File.Exists(place + ".wake");

    protected override void Release(string place)
    {
        SqliteDesktopStores.Engines.ReleaseConnections();
        try
        {
            Directory.Delete(Path.GetDirectoryName(place)!, recursive: true);
        }
        catch (IOException)
        {
            // A scratch folder the next run finds again; nothing reads it.
        }
    }
}
