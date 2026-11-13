using System.Runtime.CompilerServices;
using Daoris.Ports.Ambient;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The root of a unit test process: it chooses the ports the library reads, once, before any test runs, and chooses fakes. The desktop
/// store's engine is <see cref="MemoryUnitOfWork"/> over the scratch files a test's home holds, so the unit tier names no adapter.
/// </summary>
internal static class TestPorts
{
    [ModuleInitializer]
    internal static void Choose() => AmbientPorts.Set(new PortSet { Clock = new MovedClock(), Store = MemoryUnitOfWork.Engines });
}
