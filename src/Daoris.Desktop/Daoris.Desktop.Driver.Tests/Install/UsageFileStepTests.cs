using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1t (D232): the usage file's entries name the door a session ran on by <c>door</c> after the home's migration; an older
/// entry's <c>harness</c> is that door.
/// </summary>
public sealed class UsageFileStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "usage-file-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private string UsageFile => DaorisHome.Entry(_home, HomeEntry.Usage);

    [Fact]
    public void An_entry_names_its_door_by_today_s_word_and_a_second_run_changes_nothing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UsageFile)!);
        File.WriteAllText(UsageFile, """[{"session":"s1","repository":"engine","harness":"claude-code-acp","profile":"work","used":5,"size":9,"when":"2026-10-19T10:00:00+00:00"},{"session":"s2","repository":"","door":"codex","used":1,"size":2,"when":"2026-10-19T10:00:00+00:00"}]""");
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new UsageFileStep().Run(run);

        var entries = (JsonArray)JsonNode.Parse(File.ReadAllText(UsageFile))!;
        Assert.Equal(("claude-code-acp", null), (entries[0]!["door"]!.GetValue<string>(), (string?)entries[0]!["harness"]));
        Assert.Equal("codex", entries[1]!["door"]!.GetValue<string>());
        Assert.Single(run.Changed);

        var again = new HomeMigrationRun(_home, HomeMigration.Name);
        new UsageFileStep().Run(again);
        Assert.Empty(again.Changed);
    }

    [Fact]
    public void A_file_that_does_not_read_is_left_as_it_is_and_the_migration_carries_the_step()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UsageFile)!);
        File.WriteAllText(UsageFile, "{not json");
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new UsageFileStep().Run(run);

        Assert.Empty(run.Changed);
        Assert.Equal("{not json", File.ReadAllText(UsageFile));
        Assert.Contains(HomeMigration.Steps, step => step is UsageFileStep && step.Name == "usage-file");
    }
}
