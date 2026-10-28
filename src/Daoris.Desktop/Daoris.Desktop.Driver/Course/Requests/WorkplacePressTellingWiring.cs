using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class WorkplacePressTellingOperation : IWorkplacePressTelling
{
    [ModuleInitializer]
    internal static void Install() => WorkplacePressTelling.Current = new WorkplacePressTellingOperation();

    public bool Tell(string home, string route, string? session, DateTimeOffset at) =>
        new PressesTold(home).Tell(new PressTold(route, session, at));
}
