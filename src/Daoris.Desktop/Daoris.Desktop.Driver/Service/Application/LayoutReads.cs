namespace Daoris.Driver;

/// <summary>
/// A repository's layout read from its line on this machine (LAYOUT7): named on <see cref="LayoutReader"/>, where the set-up
/// press always named it, as a static member of an extension block (STRUCT3a's pattern, D166's STRUCT3d note). The rules stay
/// the Domain's; git is <see cref="LayoutGit"/>'s.
/// </summary>
public static class LayoutReads
{
    extension(LayoutReader)
    {
        /// <summary>
        /// The facts of <paramref name="line"/> in the checkout at <paramref name="root"/>, read as git objects; the working
        /// files and the index are never asked.
        /// </summary>
        /// <returns>The facts, or why the line could not be read: no repository there, or a line that names no commit.</returns>
        public static Task<LineReading> ReadAsync(string root, string line, CancellationToken ct = default) =>
            LayoutGit.ReadAsync(root, line, ct);
    }
}
