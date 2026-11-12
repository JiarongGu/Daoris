namespace Daoris.Ports.Ambient;

/// <summary>
/// A service locator, named as one: the ports a root set once, read by a static funnel that has no wiring to carry them. It is a
/// bridge and not a seam. A part with a wiring interface takes its ports through the wiring; each read of this class is counted per
/// subdomain by the layers census's ratchet, which only tightens. In a process whose root set nothing, as a unit test's, a read
/// throws, because nothing real is behind it.
/// </summary>
public static class AmbientPorts
{
    private static PortSet? _set;

    /// <summary>Whether a root has set the ports.</summary>
    public static bool IsSet => Volatile.Read(ref _set) is not null;

    /// <summary>The ports the root set; throws where none were.</summary>
    public static PortSet Read() =>
        Volatile.Read(ref _set) ?? throw new InvalidOperationException("no ports were set: this process has no root to choose them.");

    /// <summary>Sets the ports once; a second set throws, since two roots in one process would each be read as the root.</summary>
    public static void Set(PortSet ports)
    {
        ArgumentNullException.ThrowIfNull(ports);
        if (Interlocked.CompareExchange(ref _set, ports, null) is not null)
            throw new InvalidOperationException("the ports were already set by this process's root.");
    }
}
