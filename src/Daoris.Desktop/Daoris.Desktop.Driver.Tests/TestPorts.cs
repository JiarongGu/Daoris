using System.Runtime.CompilerServices;
using Daoris.Driver;
using Daoris.Ports.Ambient;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The root of a unit test process: it chooses the ports the library reads, once, before any test runs, and chooses fakes. The
/// machine's places are <see cref="ScriptedProcesses"/>' four and its seal <see cref="ScriptedSeal"/>, so the unit tier names no
/// adapter; the admission of each start is the application's own git policy, which these tests prove.
/// </summary>
internal static class TestPorts
{
    [ModuleInitializer]
    internal static void Choose() => AmbientPorts.Set(new PortSet
    {
        Clock = new MovedClock(),
        Admission = new ScriptedProcesses(4),
        StartPolicy = GitStartAdmission.Admit,
        Seal = ScriptedSeal.Port,
    });
}
