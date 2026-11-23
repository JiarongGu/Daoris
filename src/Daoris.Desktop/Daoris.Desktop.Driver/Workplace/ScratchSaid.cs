namespace Daoris.Driver;

/// <summary>What a removal says of a session's scratch, and the ignored folder a file git lists alone lies under.</summary>
internal static class ScratchSaid
{
    /// <summary>
    /// The sentence (with its leading space) naming the scratch that went with the tree the press removed; empty for the look, whose
    /// move aside names it in its record, and for a tree that stays.
    /// </summary>
    internal static string Went(bool byItself, SweepItem now, IReadOnlyList<ContentHolds.TreeHolds> seen) =>
        !byItself && now.TreeOn is null && seen.FirstOrDefault()?.ScratchWent is { } said ? $" {said}" : "";

    /// <summary>
    /// The topmost folder above <paramref name="path"/> that an ignore rule names, as git names it (with its slash); null where none, or git
    /// cannot say. <c>--no-index</c>: a folder that holds a tracked path reads as not ignored without it.
    /// </summary>
    internal static async Task<string?> IgnoredFolderAboveAsync(string tree, string path, Dictionary<string, string?> asked, CancellationToken ct)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var length = 1; length < segments.Length; length++)
        {
            var folder = string.Join('/', segments.Take(length)) + "/";
            if (!asked.TryGetValue(folder, out var found))
            {
                var (code, _, _) = await WorkingTree.GitAsync(tree, ["check-ignore", "--no-index", "--quiet", "--", folder], ct).ConfigureAwait(false);
                asked[folder] = found = code == 0 ? folder : null;
            }

            if (found is not null) return found;
        }

        return null;
    }
}
