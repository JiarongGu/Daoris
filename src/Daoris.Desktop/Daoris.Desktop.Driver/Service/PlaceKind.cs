namespace Daoris.Driver;

/// <summary>
/// A department's kind, as its registration names it and its manifests record it: a git checkout's top, or any other folder. Its twin
/// is the service's <c>PlaceKinds</c>, which a registration's <c>kind</c> is read by.
/// </summary>
public static class PlaceKind
{
    public const string Git = "git";
    public const string Folder = "folder";
    public const string System = "system";

    /// <summary>The kind a registration's word names, where it names one this side reads; null otherwise.</summary>
    public static string? Of(string? word) => word?.Trim().ToLowerInvariant() switch
    {
        Git => Git,
        Folder => Folder,
        System => System,
        _ => null,
    };

    /// <summary>The kind the folder at <paramref name="root"/> is: <c>git</c> where it is a checkout's top, <c>folder</c> otherwise.</summary>
    public static string At(string root) =>
        Directory.Exists(Path.Combine(root, ".git")) || File.Exists(Path.Combine(root, ".git")) ? Git : Folder;
}
