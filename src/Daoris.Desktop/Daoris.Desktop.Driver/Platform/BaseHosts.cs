namespace Daoris.Driver;

/// <summary>
/// A development host the walking-up locators passed over for standing in the previous line's build base (the tree-pool design §38.2,
/// 5): where none is left to offer, the window's words for a missing host and the session's for a missing connector name the marked
/// build, workspace-relative, and the door that releases it (<c>tree.base-host</c>).
/// </summary>
public static class BaseHosts
{
    /// <summary>The window's reason for a missing host where its development build stands in a base.</summary>
    public const string Why = "base-host";

    /// <summary>
    /// The marked configuration folder of <paramref name="project"/>'s build in the workspace above <paramref name="baseDirectory"/>,
    /// workspace-relative; null where no workspace is above it or no marker stands there.
    /// </summary>
    public static string? PassedOver(string baseDirectory, string project)
    {
        var workspace = new DirectoryInfo(baseDirectory);
        while (workspace is not null && !File.Exists(Path.Combine(workspace.FullName, KnowledgeConnector.WorkspaceMarker))) workspace = workspace.Parent;
        if (workspace is null) return null;

        var bin = Path.Combine(workspace.FullName, "src", "Daoris.Service", project, "bin");
        if (!Directory.Exists(bin)) return null;
        return Directory.EnumerateDirectories(bin).Order(StringComparer.Ordinal)
            .Select(configuration => BaseMarker.Above(Path.Combine(configuration, "net10.0")))
            .OfType<BaseStanding>()
            .Select(standing => Path.GetRelativePath(workspace.FullName, standing.Folder).Replace('\\', '/'))
            .FirstOrDefault();
    }

    /// <summary>The words for a host passed over at <paramref name="path"/>.</summary>
    public static Noted Said(string path) => Noted.Of(
        StartNoteCodes.TreeBaseHost,
        $"The development build at `{path}` is the previous line's, so Daoris does not run it here; `daoris tree build {path}` builds it and releases it.",
        ("path", path));
}
