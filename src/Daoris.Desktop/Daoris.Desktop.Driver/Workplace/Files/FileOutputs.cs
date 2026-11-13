using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The paths a repository's own rule file (<c>daoris.files.json</c>) says its tools write again, whose loss with a tree loses no one's
/// work: an output, an intermediate, a cache or scratch. What a removal takes with a tree where the checkout holds none, or holds
/// another build's bytes.
/// </summary>
/// <remarks>
/// Read from the line's tip, never the tree's copy, so a session cannot declare its own files disposable. A dependency's path is
/// not here (its install makes it again, a build folder already goes), nor a machine-local one, which is the person's. An entry that
/// takes a path back with <c>!</c>, or is kept in the first form, declares nothing: its reach is not a list of globs.
/// </remarks>
internal static class FileOutputs
{
    private static readonly FileKind[] Remade = [FileKind.Output, FileKind.Intermediate, FileKind.Cache, FileKind.Scratch];

    /// <summary>The patterns <paramref name="text"/>'s rule entries of a remade kind cover; none where the text does not read.</summary>
    internal static IReadOnlyList<Regex> Declared(string? text) =>
        [.. RepositoryFilesReader.Read(text).Rules.Where(rule => Remade.Contains(rule.Kind)).SelectMany(GitPattern.RegexesOf)];

    /// <summary>The patterns of the rule file at <paramref name="line"/>'s tip in the checkout at <paramref name="root"/>; none where it has none.</summary>
    internal static async Task<IReadOnlyList<Regex>> AtAsync(string root, string line, CancellationToken ct)
    {
        if (!BranchName.IsValid(line)) return [];
        return Declared(await FileGit.ShowAsync(root, $"refs/heads/{line}", DeclarationNames.Rules, ct).ConfigureAwait(false));
    }
}
