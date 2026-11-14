namespace Daoris.Driver;

/// <summary>
/// The files stage as a start runs it after its record opens, a driven session's and a conversation's alike, each line it says kept on
/// the record; and the hand's own step, which takes the <c>unworked</c> mark off before anything is handed the tree.
/// </summary>
internal static class TreeFilesStep
{
    /// <summary>The stage in <paramref name="tree"/>, one of Daoris's trees of <paramref name="root"/>; a failure of git or the disk said, never thrown.</summary>
    internal static async Task<TreeFilesOutcome> RunAsync(
        string home, string root, string tree, string repository, Action<Noted> say, CancellationToken ct)
    {
        TreeFilesOutcome outcome;
        try
        {
            outcome = await new TreeFilesStage(home, root, tree, repository).RunAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            outcome = new([TreeFilesStage.Failed(error)], false, null);
        }

        foreach (var line in outcome.Said) say(line);
        return outcome;
    }

    /// <summary>The mark taken off in the hand's own step; a removal that fails hands nothing, so the start ends as a failed open does.</summary>
    /// <exception cref="DriverException">The record could not be written.</exception>
    internal static void Hand(string home, string tree)
    {
        if (!TreeFilesRecord.Unmark(home, tree))
        {
            throw new DriverException("Daoris could not record that this tree is handed on, so it handed it to nothing: its next start prepares it again.");
        }
    }
}
