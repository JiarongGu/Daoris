using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The list of every node record keeps an old record's read while its file stands, as a unit's own read does.</summary>
public sealed class NodeRecordKeptAllTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "record-kept-all", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private static void Age(string home, string unit) =>
        File.SetLastWriteTimeUtc(NodeRecordFiles.PathOf(home, unit), DateTime.UtcNow - NodeRecordFiles.Settled - TimeSpan.FromMinutes(1));

    [Fact]
    public void An_old_record_is_listed_as_the_same_read_until_its_file_moves()
    {
        var files = new NodeRecordFiles(_home, () => true);
        files.Write(new NodeRecord("u") with { Fired = ["a"] });
        files.Write(new NodeRecord("v"));
        Age(_home, "u");
        Age(_home, "v");
        var first = files.All().Records;

        Assert.Equal(2, first.Count);
        Assert.All(first, record => Assert.Same(record, files.All().Records.Single(each => each.Unit == record.Unit)));
        Assert.Same(files.Read("u"), first.Single(record => record.Unit == "u"));

        files.Edit("u", record => (record with { Fired = ["a", "b"] }, true));
        Assert.Equal(["a", "b"], files.All().Records.Single(record => record.Unit == "u").Fired);
    }

    [Fact]
    public void A_file_that_does_not_read_is_listed_as_unreadable_each_time()
    {
        var files = new NodeRecordFiles(_home, () => true);
        files.Write(new NodeRecord("u"));
        File.WriteAllText(NodeRecordFiles.PathOf(_home, "u"), "not a record");
        Age(_home, "u");

        Assert.Empty(files.All().Records);
        Assert.Single(files.All().Unreadable);
        Assert.Single(files.All().Unreadable);
    }
}
