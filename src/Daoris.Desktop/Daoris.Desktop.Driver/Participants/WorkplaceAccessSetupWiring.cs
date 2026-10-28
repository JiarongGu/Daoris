using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class WorkplaceAccessSetupOperations : IWorkplaceAccessSetup
{
    [ModuleInitializer]
    internal static void Install() => WorkplaceAccessSetup.Current = new WorkplaceAccessSetupOperations();

    public IWorkplaceResolvedAccess Resolve(DriverConfig config, string repository, string? workspace) =>
        AccessRules.Resolve(config, repository, workspace);

    public string Whose(string source) => AccessRules.Whose(source);

    public string DefaultSource => AccessRules.DefaultSource;
}
