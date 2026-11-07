using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The review's answer to the reading the instance's leases ask of two verbs' own commands: of
/// <c>daoris place heavy …</c> and <c>daoris place checks …</c>, the words that change the driver's
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
