using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class WorkplaceEventRecordOperations : IWorkplaceEventRecords
{
    [ModuleInitializer]
    internal static void Install() => WorkplaceEventRecords.Current = new WorkplaceEventRecordOperations();

    public IWorkplaceEventHistory Open(string directory) => new SessionEvents(directory);
}
