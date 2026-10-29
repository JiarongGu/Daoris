using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class WorkplaceHandedQuestStoreOperations : IWorkplaceHandedQuestStores
{
    [ModuleInitializer]
    internal static void Install() => WorkplaceHandedQuests.Current = new WorkplaceHandedQuestStoreOperations();

    public IWorkplaceHandedQuests Open(string home) => new HandedQuests(home);
}
