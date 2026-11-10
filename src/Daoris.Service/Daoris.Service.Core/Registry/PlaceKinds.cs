namespace Daoris.Knowledge;

/// <summary>
/// A department's kind on its registration: a git checkout's top, or any other folder, which is driven in place with a manifest in
/// place of git. Its twin is the driver's <c>PlaceKind</c>, which reads a registration's word and a root the same way.
/// </summary>
public static class PlaceKinds
{
    public const string Git = "git";
    public const string Folder = "folder";

    /// <summary>The kind a word names, or null where it names none this host keeps.</summary>
    public static string? Of(string? word) => word?.Trim().ToLowerInvariant() switch
    {
        Git => Git,
        Folder => Folder,
        _ => null,
    };

    /// <summary>The kind of the folder at <paramref name="root"/>: <c>git</c> where a <c>.git</c> stands at its top, <c>folder</c> otherwise.</summary>
    public static string At(string root) =>
        Directory.Exists(Path.Combine(root, ".git")) || File.Exists(Path.Combine(root, ".git")) ? Git : Folder;
}
