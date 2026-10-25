using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The participants' answer to the reading the instance's leases ask of a verb's own command (FOUNDATION1 mod-vi, the design §14.3): of
/// <c>daoris-driver access …</c>, the words that change the driver's file. <c>access</c> naming no change says the setup, which holds
/// nothing; a change writes the file (AUTONOMY1a). Installed when the driver's assembly loads, before any host reads a lease.
/// </summary>
internal static class AccessLease
{
    [ModuleInitializer]
    internal static void Install() => VerbLeases.Answer("access", new(words => AccessCommand.Read(words, out _)?.Edit is not null));
}
