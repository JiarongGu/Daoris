namespace Daoris.Driver;

/// <summary>
/// The person's standing confirmation grant an older build stored (D201's note of 2026-11-07): the grant is gone, the four presses it
/// named being the steward's acts, so its record is removed. Nothing in it is copied, since it held presses and a moment, never a word
/// of the person's. It ends when the owner's install finds none, and the row that deletes it, with <see cref="HomeEntry.StandingConfirmation"/>
/// in the three home tables, is TERMTRUST1's.
/// </summary>
public sealed class StandingGrantStep : IHomeMigrationStep
{
    public string Name => "standing-grant";

    public void Run(HomeMigrationRun run)
    {
        var path = DaorisHome.Entry(run.Home, HomeEntry.StandingConfirmation);
        if (!File.Exists(path)) return;
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left for the next start: a record nothing reads harms nothing.
        }
    }
}
