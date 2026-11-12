using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The former fixed defaults are opt-in now: a home that had them on has them off and the log counts each by id; one that had switched
/// one off keeps it off and the id leaves <c>defaultsOff</c>; nothing else in the file moves, and a file naming none is left as it is.
/// </summary>
public sealed class AccessDefaultsOffStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "access-defaults-off", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private string Put(string text)
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Permissions);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void The_step_runs_in_the_homes_migration_under_a_name_of_its_own()
    {
        Assert.Contains(HomeMigration.Steps, step => step is AccessDefaultsOffStep && step.Name == "access-defaults-off");
    }

    [Fact]
    public void A_home_with_the_old_ids_has_them_off_and_the_ones_it_switched_off_leave_the_list()
    {
        Put("""{"machine":{"deny":["Bash(rm:*)"]},"defaultsOff":["commit","no-publish","tree-guard"]}""");
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new AccessDefaultsOffStep().Run(run);

        var file = PermissionRules.Load(_home);
        Assert.Equal(["commit"], file.DefaultsOff);
        Assert.Empty(file.DefaultsOn);
        Assert.Equal(["Bash(rm:*)"], file.Machine.Deny);
        Assert.False(PermissionRules.IsOn(file, PermissionRules.Defaults.First(shipped => shipped.Id == PermissionRules.NoPushId)));
        Assert.Equal(1, run.Counts["access-defaults-off.no-push.turned-off"]);
        Assert.Equal(1, run.Counts["access-defaults-off.no-publish.kept-off"]);
        Assert.Equal(1, run.Counts["access-defaults-off.tree-guard.kept-off"]);
    }

    [Fact]
    public void A_home_with_no_rules_file_is_counted_and_gets_none_written()
    {
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new AccessDefaultsOffStep().Run(run);

        Assert.False(File.Exists(DaorisHome.Entry(_home, HomeEntry.Permissions)));
        Assert.Equal(3, run.Counts.Count(each => each.Key.EndsWith(".turned-off", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_file_that_lists_none_of_them_is_left_byte_for_byte()
    {
        var path = Put("""{"defaultsOff":["commit"]}""");

        new AccessDefaultsOffStep().Run(new HomeMigrationRun(_home, HomeMigration.Name));

        Assert.Equal("""{"defaultsOff":["commit"]}""", File.ReadAllText(path));
    }
}
