using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The plugins' answer to the reading the instance's leases ask of a verb's own command: of
/// <c>daoris plugin …</c>, which words are a short change that holds the lease from the top (an add, an update with
/// <c>--yes</c>, a remove and a switch) and which second words are its own. Installed when the driver's assembly loads, before any host
/// reads a lease.
/// </summary>
internal static class PluginLease
{
    [ModuleInitializer]
    internal static void Install() => VerbLeases.Answer("plugin", new(PluginCommand.Changes));
}
