using System.Runtime.CompilerServices;
using Daoris.Adapters.DesktopStore.Sqlite;
using Daoris.Adapters.Machine;
using Daoris.Driver;
using Daoris.Ports.Ambient;

namespace Daoris.Desktop.Driver.Rehearsals;

/// <summary>
/// The root of a rehearsal process: it hands the library the machine's ports, the store's engine and the clock the shell and
/// daoris-driver hand, once, before any class runs, so what a real host it starts keeps (a key under the user's seal) reads back here.
/// </summary>
internal static class RealPorts
{
    [ModuleInitializer]
    internal static void Choose() => AmbientPorts.Set(new PortSet
    {
        Clock = TimeProvider.System,
        Store = SqliteDesktopStores.Engines,
        Admission = StartPlaces.Shared,
        Liveness = MachineLiveness.Shared,
        StartPolicy = GitStartAdmission.Admit,
        Seal = KeySeal.Port,
        Git = GitProcessRuns.Shared,
    });
}
