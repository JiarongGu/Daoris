using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class WorkplaceHarnessBindingStoreOperations : IWorkplaceHarnessBindingStores
{
    [ModuleInitializer]
    internal static void Install() => WorkplaceHarnessBindings.Current = new WorkplaceHarnessBindingStoreOperations();

    public IWorkplaceHarnessBindings Open(string home) => new HarnessConversations(home);
}
