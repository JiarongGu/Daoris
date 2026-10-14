namespace Daoris.Driver;

/// <summary>
/// What the disk says of a session's tree folder, read from the folder alone (PART10k): public, since the terminal and the
/// bridge ask it too. It was <c>SessionTrees.TreeGone</c>, which nothing of the front's state was read by.
/// </summary>
public static class TreeFolders
{
    /// <summary>
    /// Whether a session's tree is gone from this machine (REVIEW2): its record names none, its folder is not there, or
    /// the folder is empty — what a removal leaves where something held the folder open (D109 §5).
    /// </summary>
    public static bool TreeGone(string? tree)
    {
        if (string.IsNullOrWhiteSpace(tree) || !Directory.Exists(tree)) return true;
        try
        {
            return !Directory.EnumerateFileSystemEntries(tree).Any();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A folder nobody may list is not a tree a review can read.
            return true;
        }
    }
}
