namespace Daoris.Driver;

/// <summary>
/// The classes of a repository's ignored content (the tree-pool design's R5.5), as its gates file's <c>trees</c> declares them:
/// dependencies a package manager installs from a lockfile, intermediates a build tool checks itself and valid only at the path it
/// made them, runnable outputs never carried, and the commands that warm a tree. The local class is <c>localInputs</c>
/// (<see cref="TreeLocalInputs"/>), never declared here; everything ignored that no class names is scratch. Nothing acts on them yet.
/// </summary>
internal sealed record TreeClasses(
    IReadOnlyList<TreeDependency> Dependencies, IReadOnlyList<string> Intermediates, IReadOnlyList<string> Outputs, IReadOnlyList<string> Prepare)
{
    /// <summary>No declaration: nothing ignored is carried, which is never wrong, only not faster.</summary>
    internal static readonly TreeClasses None = new([], [], [], []);

    internal bool Declares => Dependencies.Count + Intermediates.Count + Outputs.Count + Prepare.Count > 0;

    /// <summary>Every path a class names, with its class's key, in the order declared.</summary>
    internal IEnumerable<(string Class, string Path)> Paths =>
        Dependencies.Select(each => ("dependencies", each.Path))
            .Concat(Intermediates.Select(each => ("intermediates", each)))
            .Concat(Outputs.Select(each => ("outputs", each)));
}

/// <summary>
/// One dependency folder: where it is placed, the tracked files whose bytes key it (its lockfile and the package manager's own
/// configuration), and the command whose output keys it with them; null for none.
/// </summary>
internal sealed record TreeDependency(string Path, IReadOnlyList<string> Key, string? Toolchain);
