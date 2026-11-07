using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A unit's node record kept while its file is unchanged (<see cref="NodeRecordFiles.Settled"/>): for an old file, never a young one, and never
/// past a write by an edit or by another hand.
/// </summary>
public sealed class NodeRecordKeptReadTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "record-kept", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private static void Age(string home, string unit) =>
        File.SetLastWriteTimeUtc(NodeRecordFiles.PathOf(home, unit), DateTime.UtcNow - NodeRecordFiles.Settled - TimeSpan.FromMinutes(1));

    [Fact]
    public void An_old_record_is_read_once_and_an_edit_of_it_is_read_at_once()
    {
        var files = new NodeRecordFiles(_home, () => true);
        files.Write(new NodeRecord("u") with { Fired = ["a"] });
        Age(_home, "u");
        var first = files.Read("u");

        Assert.Same(first, files.Read("u"));

        files.Edit("u", record => (record with { Fired = ["a", "b"] }, true));
        Assert.Equal(["a", "b"], files.Read("u")!.Fired);
    }

    [Fact]
    public void A_record_another_hand_wrote_is_read_again_by_its_stamp()
    {
        var files = new NodeRecordFiles(_home, () => true);
        files.Write(new NodeRecord("u") with { Fired = ["a"] });
        Age(_home, "u");
        Assert.Equal(["a"], files.Read("u")!.Fired);

        File.WriteAllText(NodeRecordFiles.PathOf(_home, "u"), NodeRecordFiles.ToJson(new NodeRecord("u") with { Fired = ["a", "b", "c"] }));
        Assert.Equal(["a", "b", "c"], files.Read("u")!.Fired);
    }

    [Fact]
    public void A_record_written_a_moment_ago_is_never_kept()
    {
        var files = new NodeRecordFiles(_home, () => true);
        files.Write(new NodeRecord("u") with { Fired = ["a"] });

        Assert.NotSame(files.Read("u"), files.Read("u"));
    }
}
