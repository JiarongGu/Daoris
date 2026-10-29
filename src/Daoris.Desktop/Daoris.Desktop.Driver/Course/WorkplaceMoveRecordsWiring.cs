using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>The Course's side of the workplace's delete port (FOUNDATION1f-i): the move store over a home, the one production path that reaches it until its rows adopt it.</summary>
internal sealed class WorkplaceMoveRecordStoreOperations : IWorkplaceMoveRecordStores
{
    [ModuleInitializer]
    internal static void Install() => WorkplaceMoveRecords.Current = new WorkplaceMoveRecordStoreOperations();

    public IWorkplaceMoveRecords Open(string home) => new MoveStore(home);
}
