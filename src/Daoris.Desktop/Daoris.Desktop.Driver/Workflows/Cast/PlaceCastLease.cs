using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The cast's answer to the reading the settings' command asks of a verb's own words: which <c>daoris settings cast …</c> words change the
/// home. Installed when the driver's assembly loads, before any host reads a lease, so the settings name nothing of the workflows.
/// </summary>
internal static class PlaceCastLease
{
    [ModuleInitializer]
    internal static void Install() => DriverChoicesCommand.Answer("cast", PlaceCastVerb.Changes);
}
