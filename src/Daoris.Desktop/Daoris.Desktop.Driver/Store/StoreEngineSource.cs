using System.Data.Common;
using Daoris.Ports.Ambient;
using Daoris.Ports.Store;

namespace Daoris.Driver;

/// <summary>
/// The one place the desktop store reads the engines its root set: the store and its reader both ask here, so
/// the ambient ports are read once for the whole store family.
/// </summary>
internal static class StoreEngineSource
{
    /// <summary>The engines the root handed; throws where it handed none, as a process with no root has nothing real behind the store.</summary>
    internal static DesktopStoreEngines Current =>
        AmbientPorts.Read().Store ?? throw new InvalidOperationException("the root handed no engine for the desktop's store.");

    /// <summary>A connection to <paramref name="path"/>, for a one-off read of a store file (<see cref="DesktopStoreReader"/>).</summary>
    internal static DbConnection Connect(string path, DesktopOpen mode) => Current.Connect(path, mode);
}
