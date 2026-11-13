using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What one walk of a tree read beyond its answer, handed back so the kept read holds it without asking git again: the repository's declared
/// output the proof read, and the paths of the one <c>status</c> that proof made (set only where the tree is held by content).
/// </summary>
internal sealed class TreeWalkBack
{
    /// <summary>The declared output the walk read at the line's tip; null where it read none.</summary>
    internal IReadOnlyList<Regex>? Declared { get; set; }

    /// <summary>The tree's paths no commit holds, as the proof's status listed them; null where it made none.</summary>
    internal int? Paths { get; set; }
}
