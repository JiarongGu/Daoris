using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A placement from the dependency store links each file to the entry's own, so a tree's dependencies cost directories, not bytes.
/// Two names of one file record share the read-only mark, which is how these cases tell a link from a copy on any platform.
/// </summary>
public sealed class DependencyLinkedPlacementTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "deps-linked", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string First => Path.Combine(_scratch, "first");

    public void Dispose() => GitSeed.Discard(_scratch);

    private (string Key, DependencyManifest Manifest) Sealed()
    {
        Directory.CreateDirectory(Path.Combine(First, "deps", "pkg"));
        File.WriteAllText(Path.Combine(First, "deps", "pkg", "index.js"), "module.exports = 1;\n");
        File.WriteAllText(Path.Combine(First, "deps", ".state"), "last");
        var manifest = DependencyManifest.Read(First, "deps", [], out _, hashed: true);
        Assert.True(DependencyStore.Seal(Home, "key", First, "deps", manifest, CancellationToken.None));
        return ("key", manifest);
    }

    private string StoreFile(string relative) => Path.Combine(DependencyStore.EntryOf(Home, "key"), "files", relative);

    [Fact]
    public void A_placed_file_is_the_entry_s_own_record_and_a_file_at_the_folder_s_top_is_a_copy()
    {
        Sealed();
        var tree = Path.Combine(_scratch, "second");
        Directory.CreateDirectory(tree);

        var placed = DependencyStore.Place(Home, "key", tree, "deps", CancellationToken.None);

        Assert.NotNull(placed);
        var linked = Path.Combine(tree, "deps", "pkg", "index.js");
        Assert.Equal("module.exports = 1;\n", File.ReadAllText(linked));
        // One file record under both names: the mark set on the entry's is read on the tree's.
        Assert.True(File.GetAttributes(linked).HasFlag(FileAttributes.ReadOnly), "a linked file keeps the entry's read-only mark");
        File.SetAttributes(linked, File.GetAttributes(linked) & ~FileAttributes.ReadOnly);
        Assert.False(File.GetAttributes(StoreFile("pkg/index.js")).HasFlag(FileAttributes.ReadOnly), "the mark is the one record's");
        File.SetAttributes(linked, File.GetAttributes(linked) | FileAttributes.ReadOnly);

        // The manager's own record at the top is written last and stamped: its own copy, writable.
        var top = Path.Combine(tree, "deps", ".state");
        Assert.False(File.GetAttributes(top).HasFlag(FileAttributes.ReadOnly));
        File.SetAttributes(StoreFile(".state"), File.GetAttributes(StoreFile(".state")) & ~FileAttributes.ReadOnly);
        File.WriteAllText(top, "changed");
        Assert.Equal("last", File.ReadAllText(StoreFile(".state")));
    }

    [Fact]
    public void A_removal_of_one_tree_and_an_eviction_leave_every_other_name_read_only_and_count_only_the_last_name_freed()
    {
        Sealed();
        var one = Path.Combine(_scratch, "one");
        var two = Path.Combine(_scratch, "two");
        Directory.CreateDirectory(one);
        Directory.CreateDirectory(two);
        Assert.NotNull(DependencyStore.Place(Home, "key", one, "deps", CancellationToken.None));
        Assert.NotNull(DependencyStore.Place(Home, "key", two, "deps", CancellationToken.None));
        var twos = Path.Combine(two, "deps", "pkg", "index.js");

        var freed = TreeSetAside.DeleteNeverFollowing(Path.Combine(one, "deps"));

        Assert.False(Directory.Exists(Path.Combine(one, "deps")));
        Assert.Equal("module.exports = 1;\n", File.ReadAllText(StoreFile("pkg/index.js")));
        Assert.Equal("module.exports = 1;\n", File.ReadAllText(twos));
        Assert.True(File.GetAttributes(twos).HasFlag(FileAttributes.ReadOnly), "a removal unlinks one name and never clears the record's mark");
        Assert.True(File.GetAttributes(StoreFile("pkg/index.js")).HasFlag(FileAttributes.ReadOnly));
        Assert.Equal("last".Length, freed);

        Assert.True(DependencyStore.Evict(Home, "key"));
        Assert.True(File.GetAttributes(twos).HasFlag(FileAttributes.ReadOnly), "an eviction leaves the trees' names marked");
        Assert.Equal("module.exports = 1;\n", File.ReadAllText(twos));

        Assert.Equal("module.exports = 1;\n".Length + "last".Length, TreeSetAside.DeleteNeverFollowing(Path.Combine(two, "deps")));
    }

    [Fact]
    public void Where_a_link_cannot_be_made_the_file_is_copied_writable()
    {
        Sealed();
        var tree = Path.Combine(_scratch, "second");
        Directory.CreateDirectory(tree);

        FileLinks.Declined.Value = true;
        try
        {
            Assert.NotNull(DependencyStore.Place(Home, "key", tree, "deps", CancellationToken.None));
        }
        finally
        {
            FileLinks.Declined.Value = false;
        }

        var copied = Path.Combine(tree, "deps", "pkg", "index.js");
        Assert.False(File.GetAttributes(copied).HasFlag(FileAttributes.ReadOnly));
        File.WriteAllText(copied, "changed");
        Assert.Equal("module.exports = 1;\n", File.ReadAllText(StoreFile("pkg/index.js")));
    }
}
