using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The conversion rows: a home whose person switched <c>no-push</c> off, with Write unset, is converted
/// once so a session pushes as before and <c>no-publish</c> is off; a home with Write set keeps it; after that the judge reads git access
/// alone, and the rules hand the push forms as git access opens them.
/// </summary>
public sealed class GitPushStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-push-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private string Put(HomeEntry entry, string text)
    {
        var path = DaorisHome.Entry(_home, entry);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private JsonObject Read(HomeEntry entry) => (JsonObject)JsonNode.Parse(File.ReadAllText(DaorisHome.Entry(_home, entry)))!;

    private HomeMigrationRun Convert()
    {
        var run = new HomeMigrationRun(_home, HomeMigration.Name);
        new GitPushStep().Run(run);
        return run;
    }

    private GitVerdict SessionPush() =>
        GitAccessRules.Judge(GitAct.Write, GitAccessKeys.Resolve(_home, "engine", null).Of(GitAccessKeys.Write).Value, granted: false);

    [Fact]
    public void The_step_runs_in_the_homes_migration()
    {
        Assert.Contains(HomeMigration.Steps, step => step is GitPushStep);
    }

    [Fact]
    public void No_push_off_and_Write_unset_converts_so_a_session_pushes_as_before_and_no_publish_is_off()
    {
        Put(HomeEntry.Permissions, """{"defaultsOff":["no-push","commit"]}""");
        Put(HomeEntry.DriverConfig, """{"adapter":"claude-code"}""");
        Assert.Equal("denied", SessionPush().Code);

        var run = Convert();

        Assert.Equal("allow", (string?)Read(HomeEntry.DriverConfig)["machineGitAccess"]!["write"]);
        Assert.Equal("claude-code", (string?)Read(HomeEntry.DriverConfig)["adapter"]);
        var permissions = PermissionRules.Load(_home);
        Assert.Equal(["commit", "no-publish"], permissions.DefaultsOff.Order(StringComparer.Ordinal));
        Assert.Equal(("access", true), (SessionPush().Code, SessionPush().Allowed));
        // The session's rules open the plain push, and the classifier is told git access's sentence and nothing of publishing.
        var write = GitAccessKeys.Resolve(_home, "engine", null).Of(GitAccessKeys.Write).Value;
        var rules = GitAccessPolicy.Compose(PermissionRules.Compose(permissions, "default", "engine"), write);
        Assert.Contains("Bash(git push:*)", rules.Allow);
        Assert.Equal([GitAccessPolicy.Judged], GitAccessPolicy.HardDeny(PermissionRules.HardDeny(permissions), write));
        Assert.Equal(2, run.Changed.Count);
    }

    [Theory]
    [InlineData("deny")]
    [InlineData("ask")]
    [InlineData("allow")]
    public void A_Write_the_person_set_is_left_untouched(string value)
    {
        Put(HomeEntry.Permissions, """{"defaultsOff":["no-push"]}""");
        var config = Put(HomeEntry.DriverConfig, $$$"""{"machineGitAccess":{"write":"{{{value}}}","dangerous":"deny"}}""");
        var before = File.ReadAllText(config);

        Convert();

        Assert.Equal(before, File.ReadAllText(config));
        Assert.Equal(["no-publish"], PermissionRules.Load(_home).DefaultsOff);
    }

    [Fact]
    public void A_home_with_no_driver_file_gets_the_machines_Write_allow()
    {
        Put(HomeEntry.Permissions, """{"defaultsOff":["no-push"]}""");

        Convert();

        Assert.Equal("allow", (string?)Read(HomeEntry.DriverConfig)["machineGitAccess"]!["write"]);
    }

    [Fact]
    public void A_home_that_never_switched_no_push_off_is_left_as_it_is_and_its_sessions_still_push_nothing()
    {
        var permissions = Put(HomeEntry.Permissions, """{"defaultsOff":["tree-guard"]}""");
        var config = Put(HomeEntry.DriverConfig, """{"adapter":"claude-code"}""");

        var run = Convert();

        Assert.Empty(run.Changed);
        Assert.Equal("""{"defaultsOff":["tree-guard"]}""", File.ReadAllText(permissions));
        Assert.Equal("""{"adapter":"claude-code"}""", File.ReadAllText(config));
        Assert.Equal("denied", SessionPush().Code);
    }

    [Fact]
    public void Run_again_over_a_converted_home_it_changes_nothing()
    {
        Put(HomeEntry.Permissions, """{"defaultsOff":["no-push"]}""");
        Convert();

        var again = Convert();

        Assert.Empty(again.Changed);
    }
}
