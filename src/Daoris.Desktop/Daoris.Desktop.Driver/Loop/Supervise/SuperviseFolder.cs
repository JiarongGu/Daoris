namespace Daoris.Driver;

/// <summary>
/// Which folder a supervisor's turn reads in: a copy of its subject's git work tree, or an empty folder of its own. A packet's reader
/// always takes its own, and so does a subject whose record names no git work tree (Ask Daoris's room among them): git is never asked
/// to capture a folder that is no repository.
/// </summary>
internal static class SuperviseFolder
{
    /// <summary>The tree a turn copies, or null where it takes an empty folder of its own.</summary>
    public static string? Copied(string? tree, bool packetOnly) =>
        !packetOnly && tree is { Length: > 0 } && Directory.Exists(tree)
        && (Directory.Exists(Path.Combine(tree, ".git")) || File.Exists(Path.Combine(tree, ".git")))
            ? tree
            : null;
}
