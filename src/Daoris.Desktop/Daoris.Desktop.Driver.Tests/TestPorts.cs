using System.Diagnostics;
using System.Runtime.CompilerServices;
using Daoris.Driver;
using Daoris.Ports.Ambient;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The root of a unit test process: it chooses the ports the library reads, once, before any test runs, and chooses fakes. The desktop
/// store's engine is <see cref="MemoryUnitOfWork"/> over the scratch files a test's home holds; the machine is <see cref="Processes"/>,
/// four places and this test process the one that runs, and its seal <see cref="ScriptedSeal"/>, so the unit tier names no adapter; the
/// admission of each start is the application's own git policy, which these tests prove.
/// </summary>
internal static class TestPorts
{
    /// <summary>
    /// The scripted machine every test of this process shares. A process it did not make is answered by the machine's own liveness,
    /// compiled in: a bridge while the Process classes, whose sessions and shells are real processes, still run in this project; it
    /// goes when they leave it for the rehearsal tier.
    /// </summary>
    internal static ScriptedProcesses Processes { get; } = new ScriptedProcesses(4)
        .Live(Environment.ProcessId, Created())
        .Beyond((id, created) => Daoris.Adapters.Machine.ProcessLiveness.Alive(id, created.UtcTicks));

    [ModuleInitializer]
    internal static void Choose() => AmbientPorts.Set(new PortSet
    {
        Clock = new MovedClock(),
        Store = MemoryUnitOfWork.Engines,
        Admission = Processes,
        Liveness = Processes,
        StartPolicy = GitStartAdmission.Admit,
        Seal = ScriptedSeal.Port,
    });

    private static DateTimeOffset Created()
    {
        using var self = Process.GetCurrentProcess();
        return new DateTimeOffset(self.StartTime.ToUniversalTime(), TimeSpan.Zero);
    }
}
