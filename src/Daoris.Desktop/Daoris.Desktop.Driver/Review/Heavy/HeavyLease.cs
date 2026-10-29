using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The review's answer to the reading the instance's leases ask of two verbs' own commands (FOUNDATION1 mod-vi, the design §14.3): of
/// <c>daoris-driver heavy …</c> (HEAVYRUN1b2a) and <c>daoris-driver checks …</c> (FABRIC-PIPELINE1b), the words that change the driver's
/// file. Naming no change lists or says the gates or the rule, which holds nothing; a set or a clear writes the file. Installed when the
/// driver's assembly loads, before any host reads a lease.
/// </summary>
internal static class HeavyLease
{
    [ModuleInitializer]
    internal static void Install()
    {
        VerbLeases.Answer("heavy", new(words => HeavyMappingCommand.Read(words, out _)?.Changes == true));
        VerbLeases.Answer("checks", new(words => SessionChecksCommand.Read(words, out _)?.Changes == true));
    }
}
