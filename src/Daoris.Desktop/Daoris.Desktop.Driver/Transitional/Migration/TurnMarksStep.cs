namespace Daoris.Driver;

/// <summary>
/// The turn marks an older build's terminal left under the request folder: a terminal's conversation is the running
/// Daoris's now, so no process tells its turns through a file, and a <c>*.turn</c> mark is removed. A mark carries state only, never a
/// word or a request, so nothing is copied first. It ends when the owner's install finds none, and the row that deletes it is RPC1g5's
/// follow-up.
/// </summary>
public sealed class TurnMarksStep : IHomeMigrationStep
{
    /// <summary>The ending of a mark the retired terminal runner wrote beside the requests' <c>.json</c>.</summary>
    public const string Suffix = ".turn";

    public string Name => "turn-marks";

    public void Run(HomeMigrationRun run)
    {
        var folder = DaorisHome.Entry(run.Home, HomeEntry.Requests);
        if (!Directory.Exists(folder)) return;
        foreach (var path in Directory.EnumerateFiles(folder, "*" + Suffix))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Left for the next start: a mark nothing reads harms nothing.
            }
        }
    }
}
