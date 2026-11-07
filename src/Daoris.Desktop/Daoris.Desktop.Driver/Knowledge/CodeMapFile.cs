namespace Daoris.Driver;

/// <summary>
/// Where a repository keeps its code map: the reader's own candidates, first match wins. The
/// driver shares no code with the service, so it restates them, and a test holds them to the reader's
/// source — the devkit's twin does the same.
/// </summary>
/// <remarks>
/// Read to frame the session's prompt, never to write: the map is the repository's own file,
/// kept by that repository's session in its own tree.
/// </remarks>
public static class CodeMapFile
{
    public static readonly IReadOnlyList<string> Candidates = ["docs/code-map.json", "code-map.json"];

    /// <summary>The repository-relative path of the map <paramref name="root"/> keeps, or null when it keeps none.</summary>
    public static string? Find(string root) =>
        Candidates.FirstOrDefault(candidate => File.Exists(Path.Combine(root, candidate)));
}
