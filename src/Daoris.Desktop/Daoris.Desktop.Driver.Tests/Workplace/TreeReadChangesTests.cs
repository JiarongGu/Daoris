using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class TreeReadChangesTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "tree-read-changes", Guid.NewGuid().ToString("N")[..8]);
    private readonly FakeChangeSource _source = new();
    private readonly TreeReadChanges _changes;
    private string Tree => Path.Combine(_root, "tree");
    private string Git => Path.Combine(_root, "git");

    public TreeReadChangesTests()
    {
        Directory.CreateDirectory(Tree);
        Directory.CreateDirectory(Git);
        _changes = new TreeReadChanges(_source);
    }

    public void Dispose()
    {
        _changes.Dispose();
        GitSeed.Discard(_root);
    }

    [Fact]
    public void An_unchanged_tree_keeps_its_read_until_a_file_moves()
    {
        var first = _changes.Read(Tree, Git);
        Assert.NotNull(first);
        Assert.Equal(first, _changes.Read(Tree, Git));
        foreach (var kind in new[] { ChangeKind.Created, ChangeKind.Changed, ChangeKind.Deleted, ChangeKind.Renamed })
        {
            _source.On(Tree)!.Raise("nested/file", kind);
            var next = _changes.Read(Tree, Git);
            Assert.NotEqual(first, next);
            first = next;
        }
    }

    [Fact]
    public void The_index_is_read_even_before_its_watch_tells_it_moved()
    {
        var first = _changes.Read(Tree, Git);
        File.WriteAllText(Path.Combine(Git, "index"), "staged");
        var second = _changes.Read(Tree, Git);
        Assert.NotEqual(first, second);
        File.Delete(Path.Combine(Git, "index"));
        Assert.NotEqual(second, _changes.Read(Tree, Git));
    }

    [Theory]
    [InlineData("HEAD")]
    [InlineData("index")]
    [InlineData("sharedindex.abc")]
    [InlineData("config.worktree")]
    public void Git_inputs_invalidate_the_read(string name)
    {
        var first = _changes.Read(Tree, Git);
        _source.On(Git)!.Raise(name);
        Assert.NotEqual(first, _changes.Read(Tree, Git));
    }

    [Fact]
    public void Git_lock_and_log_writes_do_not_invalidate_the_read()
    {
        var first = _changes.Read(Tree, Git);
        _source.On(Git)!.Raise("index.lock");
        _source.On(Git)!.Raise("logs/HEAD");
        Assert.Equal(first, _changes.Read(Tree, Git));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_lost_or_replaced_watch_reopens_without_reusing_its_read(bool replaced)
    {
        var first = _changes.Read(Tree, Git);
        var old = _source.On(Tree)!;
        if (replaced) old.Same = false;
        else old.Lose();
        var second = _changes.Read(Tree, Git);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
        Assert.True(old.Disposed);
        old.Raise("late");
        Assert.Equal(second, _changes.Read(Tree, Git));
    }

    [Fact]
    public void An_unwatchable_tree_keeps_nothing_and_a_gone_tree_closes_its_watches()
    {
        _source.Absent(Tree);
        Assert.Null(_changes.Read(Tree, Git));
        _source.Absent(Tree, false);
        Assert.NotNull(_changes.Read(Tree, Git));
        Directory.Delete(Tree);
        _changes.Prune();
        Assert.All(_source.Opened, handle => Assert.True(handle.Disposed));
    }
}
