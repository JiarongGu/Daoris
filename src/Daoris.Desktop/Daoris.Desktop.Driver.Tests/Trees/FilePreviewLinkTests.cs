using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The preview's link refusal over a REAL link (PREVIEW1, D111): the fast tests hold the walk through the
/// reader's seam, and these hold that the seam as shipped sees what the file system made.
/// </summary>
/// <remarks>
/// A process class (MOD8): Windows makes a link without the symlink privilege only as a junction, and only
/// `mklink /J` makes one, so these start `cmd`. Elsewhere a symbolic link needs no process.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class FilePreviewLinkTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-preview-links-" + Guid.NewGuid().ToString("N")[..8]);

    private string Tree => Path.Combine(_home, "work", "engine");

    private string Outside => Path.Combine(_home, "work", "other");

    public FilePreviewLinkTests()
    {
        Directory.CreateDirectory(Path.Combine(Tree, "src"));
        Directory.CreateDirectory(Outside);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_path_through_a_real_link_out_of_the_tree_is_refused()
    {
        File.WriteAllText(Path.Combine(Outside, "secret.txt"), "not yours");
        var link = Path.Combine(Tree, "escape");
        Link(link, Outside);
        Assert.True(File.Exists(Path.Combine(link, "secret.txt")), "the link was not made, so this proves nothing");

        var read = await FilePreview.ReadAsync(Tree, "escape/secret.txt");

        Assert.Equal(FilePreviewRefusal.LinkLeaves, read.Refusal);
        Assert.Null(read.File);
    }

    [Fact]
    public async Task A_path_through_a_real_link_that_stays_inside_is_read()
    {
        File.WriteAllText(Path.Combine(Tree, "src", "chunk.ts"), "inside\n");
        var link = Path.Combine(Tree, "alias");
        Link(link, Path.Combine(Tree, "src"));
        Assert.True(File.Exists(Path.Combine(link, "chunk.ts")), "the link was not made, so this proves nothing");

        var read = await FilePreview.ReadAsync(Tree, "alias/chunk.ts");

        Assert.Equal(FilePreviewRefusal.None, read.Refusal);
        Assert.Equal("inside\n", read.File!.Text);
    }

    /// <summary>A folder link, the kind Windows makes without a privilege.</summary>
    private static void Link(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        var info = new ProcessStartInfo("cmd") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
