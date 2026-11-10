namespace Daoris.Knowledge;

/// <summary>
/// A department's kind on its registration: a git checkout's top, any other folder, driven in place with a manifest, or a system
/// reached through its console, which names its adapter. Its twin is the driver's <c>PlaceKind</c>.
/// </summary>
public static class PlaceKinds
{
    public const string Git = "git";
    public const string Folder = "folder";
    public const string System = "system";

    /// <summary>The kind a word names, or null where it names none this host keeps.</summary>
    public static string? Of(string? word) => word?.Trim().ToLowerInvariant() switch
    {
        Git => Git,
        Folder => Folder,
        System => System,
        _ => null,
    };

    /// <summary>A system's adapter, <c>&lt;plugin&gt;/&lt;system&gt;</c> trimmed, or null where the word is not two ids about one slash.</summary>
    public static string? SystemOf(string? word)
    {
        var trimmed = word?.Trim() ?? "";
        var parts = trimmed.Split('/');
        return parts.Length == 2 && parts.All(part => part.Length is > 0 and <= 64 && part.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            ? trimmed
            : null;
    }

    /// <summary>The kind of the folder at <paramref name="root"/>: <c>git</c> where a <c>.git</c> stands at its top, <c>folder</c> otherwise.</summary>
    public static string At(string root) =>
        Directory.Exists(Path.Combine(root, ".git")) || File.Exists(Path.Combine(root, ".git")) ? Git : Folder;
}
