using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The copy a home proof is made on (<see cref="HomeCopy"/>) is made from a home in use: a file a running check holds is an empty
/// stand-in, a read-only or hard-linked file is copied as its content with no mark, what lies deeper than the copy's depth in the home (a
/// check's work tree) is left out, and a copy is removed whatever marks it holds.
/// </summary>
public sealed class HomeCopyTests : IDisposable
{
    private readonly string _install = Path.Combine(TestHomes.Root, "daoris-copy-" + Guid.NewGuid().ToString("N")[..8]);

    public HomeCopyTests()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(_install, "INSTALLED.md"), "# Daoris — installed desktop\n");
    }

    private string Home => Path.Combine(_install, "data");

    private string Into => Path.Combine(_install, "update", "probe", "c1");

    public void Dispose()
    {
        if (!Directory.Exists(_install)) return;
        foreach (var file in new DirectoryInfo(_install).EnumerateFiles("*", SearchOption.AllDirectories)) file.IsReadOnly = false;
        Directory.Delete(_install, recursive: true);
    }

    private string Put(string relative, string text = "")
    {
        var path = Path.Combine(_install, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void A_file_a_running_check_holds_is_an_empty_stand_in_and_the_rest_is_copied()
    {
        var seat = Put("data/checks/broker.lock", "seat");
        Put("data/workflows/w.json", "{}");

        string copy;
        using (new FileStream(seat, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            copy = HomeCopy.Make(Home, Into);
        }

        Assert.Equal(0, new FileInfo(Path.Combine(copy, "checks", "broker.lock")).Length);
        Assert.Equal("{}", File.ReadAllText(Path.Combine(copy, "workflows", "w.json")));
    }

    [Fact]
    public void A_read_only_hard_link_is_copied_as_its_content_with_no_mark_a_file_over_the_bound_as_an_empty_one_and_the_copy_is_removed()
    {
        var store = Put("work/deps/k1/node_modules/pkg/index.js", "module.exports = 1;");
        File.SetAttributes(store, FileAttributes.ReadOnly);
        var linked = Path.Combine(Home, "kept", "pkg", "index.js");
        Directory.CreateDirectory(Path.GetDirectoryName(linked)!);
        Assert.True(FileLinks.Make(linked, store), "the volume links");
        using (var large = File.Create(Path.Combine(Home, "knowledge.db"))) large.SetLength(HomeCopy.MaxFile + 1);

        var copy = HomeCopy.Make(Home, Into);

        var copied = new FileInfo(Path.Combine(copy, "kept", "pkg", "index.js"));
        Assert.Equal("module.exports = 1;", File.ReadAllText(copied.FullName));
        Assert.False(copied.IsReadOnly);
        Assert.Equal(0, new FileInfo(Path.Combine(copy, "knowledge.db")).Length);
        HomeCopy.Remove(Into);
        Assert.False(Directory.Exists(Into));
        Assert.True(File.Exists(store) && File.Exists(linked), "the source is read, never written");
    }

    [Fact]
    public void What_lies_deeper_than_the_depth_in_the_home_a_check_s_work_tree_is_left_out_its_folders_kept_empty()
    {
        var slot = "data/workflows/lines/trees/slot-1";
        Put(slot + "/README.md", "a tree's own file");
        Put(slot + "/src/a.cs", "class A {}");
        Put(slot + "/node_modules/p/lib/x.js", "x");
        Put("data/sessions/4f0c2a91.events.jsonl", "{}\n");

        var copy = HomeCopy.Make(Home, Into);

        var tree = Path.Combine(copy, "workflows", "lines", "trees", "slot-1");
        Assert.Equal("a tree's own file", File.ReadAllText(Path.Combine(tree, "README.md")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(tree, "src")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(tree, "node_modules")));
        Assert.Equal("{}\n", File.ReadAllText(Path.Combine(copy, "sessions", "4f0c2a91.events.jsonl")));
    }

    [Fact]
    public void A_copy_left_with_a_read_only_mark_is_removed()
    {
        var made = Path.Combine(_install, "update", "probe", "left");
        var marked = Put("update/probe/left/data/x.js", "x");
        File.SetAttributes(marked, FileAttributes.ReadOnly);

        HomeCopy.Remove(made);

        Assert.False(Directory.Exists(made));
    }
}
