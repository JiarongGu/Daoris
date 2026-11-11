using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Configured access's housekeeping and chosen settings were lists of their own an older build kept beside the families of acts; the
/// home's migration converts them into the families, so a grant the person made keeps holding as the one shape.
/// </summary>
public sealed class ConfiguredAccessStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "configured-access-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Put(string text)
    {
        var path = ConfiguredAccess.PathOf(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void The_older_lists_become_families_and_the_other_grants_stay()
    {
        var path = Put("""
            { "housekeeping": [ "inspect", "clean-up", "discard-branch", "drop-recovery" ], "settings": [ "cap", "notify" ],
              "acts": [ "pause" ], "checkouts-off": [ "engine" ], "kept": 1 }
            """);
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new ConfiguredAccessStep().Run(run);

        var read = ConfiguredAccess.Read(_home);
        Assert.Equal([ActFamilies.Pause, ActFamilies.CleanUp, ActFamilies.Discard, ActFamilies.Settings], read.Acts);
        Assert.Equal(["engine"], read.CheckoutsOff);
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("housekeeping", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"settings\":", text, StringComparison.Ordinal);
        Assert.Contains("\"kept\"", text, StringComparison.Ordinal);
        Assert.Single(run.Changed);
    }

    [Fact]
    public void A_look_alone_grants_no_family_since_a_look_is_open()
    {
        Put("""{ "housekeeping": [ "inspect" ], "settings": [] }""");

        new ConfiguredAccessStep().Run(new HomeMigrationRun(_home, HomeMigration.Name));

        Assert.Empty(ConfiguredAccess.Read(_home).Acts);
        Assert.DoesNotContain("housekeeping", File.ReadAllText(ConfiguredAccess.PathOf(_home)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_record_holding_no_older_list_is_left_as_it_is()
    {
        Put("""{ "acts": [ "settings" ] }""");
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new ConfiguredAccessStep().Run(run);
        new ConfiguredAccessStep().Run(new HomeMigrationRun(_home, HomeMigration.Name));

        Assert.Empty(run.Changed);
        Assert.Equal([ActFamilies.Settings], ConfiguredAccess.Read(_home).Acts);
    }

    [Fact]
    public void The_migration_carries_the_step()
    {
        Assert.Contains(HomeMigration.Steps, step => step is ConfiguredAccessStep && step.Name == "configured-access");
    }
}
