using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A build's version is the release's (D233 choice 6): <c>canon/canon.json</c>'s, which the publish writes into a stage's
/// <c>build.json</c> and every desktop assembly carries. The rollback's running build, the launcher's product version and a stage's
/// manifest read one number, so a stage of the same release is no going back while a delivery is unresolved, and a lower one is.
/// </summary>
public sealed class BuildVersionTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "build-version", Guid.NewGuid().ToString("N")[..8]);

    public BuildVersionTests() => Directory.CreateDirectory(_home);

    /// <summary>The version the publish writes into a stage's manifest: the canon's, read as the publish reads it.</summary>
    private static string Release =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "canon", "canon.json")))!["version"]!.GetValue<string>();

    [Fact]
    public void The_running_build_and_the_launcher_s_read_of_it_carry_the_version_a_stage_s_manifest_names()
    {
        Assert.Equal(HomeLastBuild.Numbered(Release), HomeLastBuild.Numbered(UpdateRefusal.Running));
        var library = FileVersionInfo.GetVersionInfo(typeof(UpdateRefusal).Assembly.Location).ProductVersion;
        Assert.Equal(HomeLastBuild.Numbered(Release), HomeLastBuild.Numbered(library));
    }

    [Fact]
    public void A_stage_of_the_running_release_is_no_going_back_while_a_delivery_is_unresolved_and_a_lower_one_is()
    {
        var store = new DeliveryStore(_home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell);
        var started = store.Start(
            new DeliveryIntent("unit-1", "engine", "a1b2c3d4e5", DeliveryMethods.Branch, "main", "main", "feature/tidy", null, "Tidy", null, "epoch-1"),
            DeliveryAuthorization.Person("accept-1", "r1", "graph-digest", """{"method":"branch"}""", "[]"));
        Assert.True(started.Started, started.Refused ?? started.Held);

        Assert.Null(InstallRollback.Refused(_home, Release, UpdateRefusal.Running));
        var back = InstallRollback.Refused(_home, "0.0.0", UpdateRefusal.Running);
        Assert.Equal(InstallRollback.Deliveries, back?.Code);
        Assert.Contains("1 delivery is unresolved", back?.Sentence);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }
}
