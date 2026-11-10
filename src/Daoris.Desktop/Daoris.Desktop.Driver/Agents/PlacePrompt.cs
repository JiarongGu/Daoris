namespace Daoris.Driver;

/// <summary>
/// What a session working in its place is told of it at its start: what changed there outside Daoris since the last session, and in a
/// git place the uncommitted paths that are not its own, each list bounded and the rest counted. The driver's own words.
/// </summary>
internal static class PlacePrompt
{
    /// <summary>The most paths of each list named; the rest are counted.</summary>
    public const int Limit = 30;

    /// <summary>The section, or empty where the place found nothing to tell.</summary>
    public static string Told(PlaceTold? told)
    {
        if (told is { Kind: PlaceKind.Folder }) return FolderPrompt.Told(told);
        if (told is null || told.Outside.Count + told.Others.Count == 0) return "";
        var text = "\n\n## Your place\n";
        if (told.Outside.Count > 0)
        {
            text += $"\nSince the last session here these changed outside Daoris: {Listed(told.Outside)}. "
                    + "The person or another program changed them: read a file again before you change it, and keep what the person changed.\n";
        }

        if (told.Others.Count > 0)
        {
            text += $"\nThese uncommitted paths are not yours, but the person's or another line's: {Listed(told.Others)}. "
                    + "So leave them out of your commits; commit your own by path (`git add <path>`), never with `git add -A`, `git add .`, "
                    + "`git commit -a` or `git stash`.\n";
        }

        return text;
    }

    /// <summary>How many paths of both lists the bound left unnamed.</summary>
    public static int Left(PlaceTold? told) =>
        told is null ? 0 : Math.Max(0, told.Outside.Count - Limit) + Math.Max(0, told.Others.Count - Limit);

    internal static string Listed(IReadOnlyList<string> paths) =>
        string.Join(", ", paths.Take(Limit).Select(path => $"`{path}`")) + (paths.Count > Limit ? $" and {paths.Count - Limit} more" : "");
}
