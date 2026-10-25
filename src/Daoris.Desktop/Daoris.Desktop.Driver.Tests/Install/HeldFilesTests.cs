using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// DEPLOYBUSY1: what holds a folder the swap must move. The deployment rehearsal's update rolled back <c>busy</c> after two
/// minutes with nothing saying which file was held or by what; <see cref="HeldFiles"/> names both, where Windows can tell.
/// </summary>
/// <remarks>The holder here is this test's own process, holding a file open: nothing is started.</remarks>
public sealed class HeldFilesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "daoris-held-" + Guid.NewGuid().ToString("N")[..8]);

    public HeldFilesTests()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "inner"));
        File.WriteAllText(Path.Combine(_folder, "inner", "held.dll"), "held");
        File.WriteAllText(Path.Combine(_folder, "free.dll"), "free");
        File.WriteAllText(Path.Combine(_folder, "also.dll"), "also");
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void A_folder_nothing_holds_is_found_held_by_nothing()
    {
        var found = HeldFiles.Find(_folder);
        Assert.True(found.Count == 0, Said(found));
    }

    /// <summary>What was found, for a failure's message.</summary>
    private static string Said(IReadOnlyList<HeldEntry> found) =>
        string.Join("; ", found.Select(entry => $"{entry.Path} by {string.Join(", ", entry.By.Select(by => $"{by.Said} {by.Command}"))}"));

    [Fact]
    public void A_path_that_is_not_there_is_found_held_by_nothing()
    {
        Assert.Empty(HeldFiles.Find(Path.Combine(_folder, "gone")));
    }

    /// <summary>A file open within the folder, even shared for reading, is named, with the process that holds it.</summary>
    [Fact]
    public void A_file_held_open_within_a_folder_is_named_with_the_process_holding_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var held = Path.Combine(_folder, "inner", "held.dll");

        IReadOnlyList<HeldEntry> found;
        using (new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            found = HeldFiles.Find(_folder);
        }

        Assert.True(found.Count == 1, Said(found));
        var entry = found[0];
        Assert.Equal(held, entry.Path, ignoreCase: true);
        var holder = Assert.Single(entry.By);
        Assert.Equal(Environment.ProcessId, holder.Pid);
        using var me = Process.GetCurrentProcess();
        Assert.Equal(me.ProcessName, holder.Name);
        Assert.Equal($"{me.ProcessName} (pid {Environment.ProcessId})", holder.Said);
        Assert.Empty(HeldFiles.Find(_folder));
    }

    /// <summary>The search stops at as many entries as it was asked for: a log line names a few, never a folder's every file.</summary>
    [Fact]
    public void The_search_stops_at_the_most_it_was_asked_for()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var first = new FileStream(Path.Combine(_folder, "free.dll"), FileMode.Open, FileAccess.Read, FileShare.Read);
        using var second = new FileStream(Path.Combine(_folder, "also.dll"), FileMode.Open, FileAccess.Read, FileShare.Read);
        using var third = new FileStream(Path.Combine(_folder, "inner", "held.dll"), FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.Equal(2, HeldFiles.Find(_folder, most: 2).Count);
        Assert.Equal(3, HeldFiles.Find(_folder).Count);
    }
}
