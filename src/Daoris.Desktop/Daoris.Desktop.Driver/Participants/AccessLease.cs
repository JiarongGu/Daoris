using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The participants' answer to the reading the instance's leases ask of a verb's own command: of
/// <c>daoris place access …</c>, the words that change the driver's file. <c>access</c> naming no change says the setup, which holds
/// nothing; a change writes the file. Installed when the driver's assembly loads, before any host reads a lease.
/// </summary>
internal static class AccessLease
{
    [ModuleInitializer]
    internal static void Install() => VerbLeases.Answer("access", new(words => AccessCommand.Read(words, out _)?.Edit is not null));
}
