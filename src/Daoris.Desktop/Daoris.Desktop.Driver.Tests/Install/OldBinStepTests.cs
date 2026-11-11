using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The <c>bin</c> entry's old place (<c>data/bin</c>) goes once the install's own <c>app/</c> carries both service hosts, which run before
/// it; with either host missing it stays, the place the host would still be found at, and a second run changes nothing.
/// </summary>
public sealed class OldBinStepTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "old-bin-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "data");

    private string OldBin => DaorisHome.Places(Home, HomeEntry.Bin)[^1];

    private void Install(bool http, bool connector)
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        if (http) Put(Path.Combine(_root, "app", "daoris-knowledge-http", ServiceHostLocator.ExecutableName));
        if (connector) Put(Path.Combine(_root, "app", KnowledgeConnector.Folder, KnowledgeConnector.ExecutableName));
        Put(Path.Combine(OldBin, "daoris-knowledge-http", ServiceHostLocator.ExecutableName));
    }

    private static void Put(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "host");
    }

    [Fact]
    public void The_old_place_goes_once_the_application_carries_both_hosts_and_a_second_run_changes_nothing()
    {
        Install(http: true, connector: true);
        var run = new HomeMigrationRun(Home, HomeMigration.Name);

        new OldBinStep().Run(run);

        Assert.False(Directory.Exists(OldBin));
        Assert.Equal(1, run.Counts["old-bin.removed"]);
        var again = new HomeMigrationRun(Home, HomeMigration.Name);
        new OldBinStep().Run(again);
        Assert.Empty(again.Counts);
    }

    [Fact]
    public void The_old_place_stays_while_the_application_lacks_a_host()
    {
        Install(http: true, connector: false);

        new OldBinStep().Run(new HomeMigrationRun(Home, HomeMigration.Name));

        Assert.True(Directory.Exists(OldBin));
    }
}
