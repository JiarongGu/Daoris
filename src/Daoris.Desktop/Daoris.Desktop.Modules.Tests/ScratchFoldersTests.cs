namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// HOSTCLEAN1: a test's scratch folder is removed once what holds it lets go, within a bound, and a folder still
/// held past the bound is left behind rather than failing the test it served.
/// </summary>
public sealed class ScratchFoldersTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-scratch-folders-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root)) ScratchFolders.Remove(_root);
    }

    private string FolderWithFile(out string file)
    {
        var folder = Path.Combine(_root, "held");
        Directory.CreateDirectory(folder);
        file = Path.Combine(folder, "stand-in.log");
        File.WriteAllText(file, "x");
        return folder;
    }

    [Fact]
    public async Task A_folder_whose_file_is_let_go_while_it_waits_is_removed()
    {
        var folder = FolderWithFile(out var file);
        var held = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
        var letGo = Task.Run(async () =>
        {
            await Task.Delay(300);
            await held.DisposeAsync();
        });

        var removed = ScratchFolders.Remove(folder, attempts: 40, waitMs: 50);
        await letGo;

        Assert.True(removed);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void A_folder_still_held_past_the_bound_is_left_and_said_without_throwing()
    {
        var folder = FolderWithFile(out var file);
        using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.False(ScratchFolders.Remove(folder, attempts: 3, waitMs: 20));
            Assert.True(Directory.Exists(folder));
        }

        Assert.True(ScratchFolders.Remove(folder));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void A_read_only_file_does_not_stop_the_removal()
    {
        var folder = FolderWithFile(out var file);
        File.SetAttributes(file, FileAttributes.ReadOnly);

        Assert.True(ScratchFolders.Remove(folder));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void A_folder_already_gone_is_removed()
    {
        Assert.True(ScratchFolders.Remove(Path.Combine(_root, "never-made")));
    }
}
