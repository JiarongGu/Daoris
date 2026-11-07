using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TERMTRUST1 (D201's note of 2026-11-07): the person's standing confirmation grant is gone, so the record an older build stored for it is
/// removed at the start as one step of the home's migration; the data beside it stays, and a home with no such record is left alone.
/// </summary>
public sealed class StandingGrantStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "standing-grant-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Put(HomeEntry entry, string text)
    {
        var path = DaorisHome.Entry(_home, entry);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void A_stored_grant_is_removed_and_the_data_beside_it_stays()
    {
        var grant = Put(HomeEntry.StandingConfirmation, """{ "schemaVersion": 1, "presses": [ "stop" ] }""");
        var beside = Put(HomeEntry.GitIdentities, "{}");

        new StandingGrantStep().Run(new HomeMigrationRun(_home, HomeMigration.Name));

        Assert.False(File.Exists(grant));
        Assert.True(File.Exists(beside));
    }

    [Fact]
    public void A_home_with_no_stored_grant_is_left_alone()
    {
        var beside = Put(HomeEntry.GitIdentities, "{}");

        new StandingGrantStep().Run(new HomeMigrationRun(_home, HomeMigration.Name));

        Assert.True(File.Exists(beside));
    }

    [Fact]
    public void The_migration_carries_the_step()
    {
        Assert.Contains(HomeMigration.Steps, step => step is StandingGrantStep && step.Name == "standing-grant");
    }
}
