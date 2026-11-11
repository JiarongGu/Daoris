namespace Daoris.Driver;

/// <summary>
/// What a session working in a folder that is no git repository is told of its place: it is the one session writing there and the
/// person may edit beside it, there is no git, its changes are reviewed file by file against what the folder held at its start, and what
/// changed there outside Daoris since the last session, or that the folder is too large for that to be followed.
/// </summary>
internal static class FolderPrompt
{
    /// <summary>The section; a folder always has one, since its session would otherwise take it for a repository.</summary>
    public static string Told(PlaceTold told)
    {
        var text = "\n\n## Your place\n\nYou work in this folder in place. It is not a git repository: there is no history or branch here, "
                   + "so do not start one, and run no git in it. You are the one session writing here, but the person may edit beside you. "
                   + "What you change is reviewed file by file against what the folder held when you started.\n";
        if (told.TooLarge)
        {
            return text + "\nThis folder is too large to follow, so what changed here since the last session is not known: read a file "
                        + "again before you change it, and keep what the person changed.\n";
        }

        return told.Outside.Count == 0
            ? text
            : text + $"\nSince the last session here these changed outside Daoris: {PlacePrompt.Listed(told.Outside)}. "
                   + "The person or another program changed them: read a file again before you change it, and keep what the person changed.\n";
    }
}
