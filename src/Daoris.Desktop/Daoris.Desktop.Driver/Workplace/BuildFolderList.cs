namespace Daoris.Driver;

/// <summary>
/// The one list of folders a build or an install writes, and the subset each use of it names: what a removal lets go of unasked (the
/// legacy trees' own thirteen, byte for byte), the folders a slim drops early, and the whole list the kept slots reason over. A name
/// authorises no deletion by itself: a path is dealt with only where it is ignored and under the list.
/// </summary>
internal static class BuildFolderList
{
    /// <summary>What a tree's removal lets go of: the thirteen names that were always the removal's.</summary>
    internal static readonly HashSet<string> Removal = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", "dist", "build", "out", ".angular", ".next", "coverage", "target", "__pycache__", ".venv",
        ".gradle",
    };

    /// <summary>The web root a build writes whole each time, which no removal's list holds.</summary>
    internal const string WebRoot = "wwwroot";

    /// <summary>The package folder a slim drops early beside a tracked <c>package.json</c>.</summary>
    internal const string Packages = TreeSlims.PackageFolder;

    /// <summary>The whole list: the removal's thirteen and the web root.</summary>
    internal static IReadOnlySet<string> All { get; } = new HashSet<string>([.. Removal, WebRoot], StringComparer.OrdinalIgnoreCase);
}
