namespace Daoris.Desktop.Driver.Tests;

/// <summary>The Process tests' own stand-in for the <c>event-relay</c> plugin, which Daoris.Plugins holds.</summary>
internal static class RelayStandIn
{
    public static string Folder => Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "Plugins", "RelayStandIn");

    public static string File => Path.Combine(Folder, "plugin.mjs");
}
