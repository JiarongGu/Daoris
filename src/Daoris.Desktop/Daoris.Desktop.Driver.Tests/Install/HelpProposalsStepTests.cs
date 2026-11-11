using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Ask Daoris's proposal box is gone: a place to open is a link in its answer and every change a press, so the folder an older build
/// kept its proposals in is removed once, each file copied to the salvage folder first and counted in the migration's line.
/// </summary>
public sealed class HelpProposalsStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "help-proposals-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private string Folder => DaorisHome.Entry(_home, HomeEntry.HelpProposals);

    [Fact]
    public void The_folder_goes_its_files_kept_in_the_salvage_folder_and_counted()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "p1a2b3c4.json"), """{ "id": "p1a2b3c4", "kind": "go", "state": "proposed" }""");
        File.WriteAllText(Path.Combine(Folder, "g5h6i7j8.json"), """{ "id": "g5h6i7j8", "kind": "go", "state": "applied" }""");
        File.WriteAllText(Path.Combine(Folder, "g5h6i7j8.claim"), "");
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new HelpProposalsStep().Run(run);

        Assert.False(Directory.Exists(Folder));
        Assert.Equal(2, run.Changed.Count);
        Assert.All(run.Changed, each => Assert.True(File.Exists(Path.Combine(run.Copies, each)), each));
    }

    [Fact]
    public void A_home_with_no_folder_is_left_as_it_is()
    {
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new HelpProposalsStep().Run(run);
        new HelpProposalsStep().Run(run);

        Assert.Empty(run.Changed);
        Assert.False(Directory.Exists(Folder));
    }

    [Fact]
    public void The_migration_carries_the_step()
    {
        Assert.Contains(HomeMigration.Steps, step => step is HelpProposalsStep && step.Name == "help-proposals");
    }
}
