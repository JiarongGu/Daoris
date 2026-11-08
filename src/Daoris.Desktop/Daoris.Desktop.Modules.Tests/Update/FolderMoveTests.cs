using Daoris.Desktop.Driver.Tests;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A folder moved by copying (the cross-volume case, forced here) lands whole or not at all: a failed file leaves the source
/// whole and the target as it was, so the move is tried again.
/// </summary>
public sealed class FolderMoveTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-folder-move-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Source()
    {
        var source = Path.Combine(_root, "source");
        Directory.CreateDirectory(Path.Combine(source, "inner"));
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");
        File.WriteAllText(Path.Combine(source, "inner", "b.txt"), "b");
        return source;
    }

    [Fact]
    public void A_copied_folder_lands_whole_and_the_source_goes()
    {
        var source = Source();
        var target = Path.Combine(_root, "target");

        FolderMove.Move(source, target, copying: true);

        Assert.Equal("a", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.Equal("b", File.ReadAllText(Path.Combine(target, "inner", "b.txt")));
        Assert.False(Directory.Exists(source));
    }

    [Fact]
    public void A_file_that_fails_to_copy_leaves_no_target_and_the_source_whole()
    {
        var source = Source();
        var target = Path.Combine(_root, "target");
        var copied = 0;

        Assert.Throws<IOException>(() => FolderMove.Move(source, target, copying: true, copyFile: (from, to) =>
        {
            if (copied++ == 1) throw new IOException("held");
            File.Copy(from, to, overwrite: false);
        }));

        Assert.False(Directory.Exists(target));
        Assert.True(File.Exists(Path.Combine(source, "a.txt")));
        Assert.True(File.Exists(Path.Combine(source, "inner", "b.txt")));
    }

    [Fact]
    public void A_failed_copy_into_a_folder_that_was_there_removes_only_what_it_made()
    {
        var source = Source();
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "kept.txt"), "kept");
        var copied = 0;

        Assert.Throws<IOException>(() => FolderMove.Move(source, target, copying: true, copyFile: (from, to) =>
        {
            if (copied++ == 1) throw new IOException("held");
            File.Copy(from, to, overwrite: false);
        }));

        Assert.Equal(["kept.txt"], Directory.EnumerateFileSystemEntries(target).Select(Path.GetFileName));
    }
}
