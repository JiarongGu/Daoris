using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The migration's end of records nothing reads: a tree's files record and install logs whose tree no longer stands, and a run's node
/// record whose binding is gone. What stands, or is not a record of Daoris's, is left, and a second run changes nothing.
/// </summary>
public sealed class LeftRecordsStepsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "left-records-steps", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private string Tree(string name)
    {
        var path = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Trees), "default", "engine", name);
        Directory.CreateDirectory(path);
        return path;
    }

    private string Log(string tree) => DaorisHome.Entry(_home, HomeEntry.TreeFiles, $"{TreeFilesRecord.IdOf(tree)}.0a1b2c3d.log");

    [Fact]
    public void A_tree_files_record_and_its_logs_go_once_no_tree_stands_at_its_path()
    {
        var standing = Tree("s-standing");
        var gone = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Trees), "default", "engine", "s-gone");
        foreach (var tree in new[] { standing, gone })
        {
            TreeFilesRecord.Mark(_home, tree);
            File.WriteAllText(Log(tree), "output\n");
        }

        var shared = DaorisHome.Entry(_home, HomeEntry.TreeFiles, "common.0123456789ab.json");
        File.WriteAllText(shared, "{}");
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new TreeRecordsStep().Run(run);

        Assert.True(File.Exists(TreeFilesRecord.PathOf(_home, standing)));
        Assert.True(File.Exists(Log(standing)));
        Assert.False(File.Exists(TreeFilesRecord.PathOf(_home, gone)));
        Assert.False(File.Exists(Log(gone)));
        Assert.True(File.Exists(shared));
        Assert.Equal(2, run.Counts["tree-records.removed"]);

        var again = new HomeMigrationRun(_home, HomeMigration.Name);
        new TreeRecordsStep().Run(again);
        Assert.Empty(again.Counts);
    }

    [Fact]
    public void A_run_node_record_goes_once_its_binding_is_gone()
    {
        var records = new NodeRecordFiles(_home, () => true);
        Directory.CreateDirectory(WorkplaceRunFiles.RunsFolder(_home));
        File.WriteAllText(WorkplaceRunFiles.PathOf(_home, "bound"), "{}");
        records.Edit("run:bound", current => (current, true));
        records.Edit("run:unbound", current => (current, true));
        records.Edit("verify:other", current => (current, true));
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new UnboundRecordsStep().Run(run);

        Assert.True(File.Exists(NodeRecordFiles.PathOf(_home, "run:bound")));
        Assert.False(File.Exists(NodeRecordFiles.PathOf(_home, "run:unbound")));
        Assert.True(File.Exists(NodeRecordFiles.PathOf(_home, "verify:other")));
        Assert.Equal(1, run.Counts["unbound-records.removed"]);
    }
}
