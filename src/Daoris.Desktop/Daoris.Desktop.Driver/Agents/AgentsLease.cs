using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The agents' answer to the reading the instance's leases ask of a verb's own command (FOUNDATION1 mod-vi, the design §14.3): which of
/// <c>daoris-driver agent …</c>'s words change a home, which of its second words are its own, and which act on a wiring file's home alone.
/// Installed when the driver's assembly loads, before any host reads a lease, so every verb holds exactly what it always did and no host
/// hands the leases anything.
/// </summary>
internal static class AgentsLease
{
    [ModuleInitializer]
    internal static void Install() => VerbLeases.Answer("agent", new(AgentsCommand.Changes, AgentsCommand.Verbs, AgentsCommand.Isolated));
}
