namespace Daoris.Driver;

/// <summary>
/// A workspace's name as the settings key and compare it (D48 §2): trimmed, and the default one where it is unstated. The settings'
/// own reading of a name, which the service's workspace map answers through (<see cref="RemoteTarget.Workspace"/>), so a name is
/// spelled the same in both files.
/// </summary>
public static class WorkspaceNames
{
    /// <summary>The workspace a repository is in when no workspace names it.</summary>
    public const string Default = "default";

    /// <summary>A workspace name as it is stored and compared: trimmed, and the default when unstated.</summary>
    public static string Of(string? name) =>
        string.IsNullOrWhiteSpace(name) ? Default : name.Trim();
}
