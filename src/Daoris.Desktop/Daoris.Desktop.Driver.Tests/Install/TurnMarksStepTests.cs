using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// RPC1g4 (D238): a `*.turn` mark an older build's terminal left is removed at the start, as one step of the home's migration; the
/// requests beside it stay, and a home with no request folder is left alone.
/// </summary>
public sealed class TurnMarksStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "turn-marks-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Put(string name)
    {
        var path = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Requests), name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        return path;
    }

    [Fact]
    public void A_leftover_turn_mark_is_removed_and_a_request_beside_it_stays()
    {
        var mark = Put("a1b2c3d4.turn");
        var request = Put("a1b2c3d4-1.json");

        new TurnMarksStep().Run(new HomeMigrationRun(_home, HomeMigration.Name));

        Assert.False(File.Exists(mark));
        Assert.True(File.Exists(request));
    }

    [Fact]
    public void A_home_with_no_request_folder_is_left_alone()
    {
        new TurnMarksStep().Run(new HomeMigrationRun(_home, HomeMigration.Name));

        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Requests)));
    }

    [Fact]
    public void The_migration_carries_the_step()
    {
        Assert.Contains(HomeMigration.Steps, step => step is TurnMarksStep && step.Name == "turn-marks");
    }
}
