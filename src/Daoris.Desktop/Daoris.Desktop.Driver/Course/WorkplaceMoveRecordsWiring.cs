using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>The Course's side of the workplace's delete port: the move store over a home.</summary>
internal sealed class WorkplaceMoveRecordStoreOperations : IWorkplaceMoveRecordStores
{
    [ModuleInitializer]
    internal static void Install() => WorkplaceMoveRecords.Current = new WorkplaceMoveRecordStoreOperations();

    public IWorkplaceMoveRecords Open(string home) => new MoveStore(home);
}
