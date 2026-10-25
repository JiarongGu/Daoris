using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// DEPLOYBUSY1: a process whose working folder is the folder the swap must move holds that folder, and Windows refuses its
/// move however few files are open in it. <see cref="HeldFiles"/> names the folder itself and the process, as it names a file.
/// </summary>
/// <remarks>A real process (node, as every gate already needs), started in the folder and killed at the end.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HeldFilesProcessTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "daoris-held-cwd-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly List<Process> _spawned = [];

    public HeldFilesProcessTests()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "app"));
        File.WriteAllText(Path.Combine(_folder, "app", "Daoris.Desktop.App.dll"), "library");
    }

    public void Dispose()
    {
        foreach (var process in _spawned)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
            catch (InvalidOperationException)
            {
            }

            process.Dispose();
        }

        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void A_process_working_in_the_folder_is_named_as_holding_the_folder_itself_and_its_move_is_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var app = Path.Combine(_folder, "app");
        var child = Process.Start(new ProcessStartInfo("node", ["-e", "setTimeout(() => {}, 60000)"])
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = app,
        })!;
        _spawned.Add(child);

        // Running once it answers for its folder: asked until then, a few seconds at most.
        IReadOnlyList<HeldEntry> found = [];
        for (var tries = 0; tries < 50 && found.Count == 0; tries++)
        {
            found = HeldFiles.Find(app);
            if (found.Count == 0) Thread.Sleep(100);
        }

        var entry = Assert.Single(found);
        Assert.Equal(app, entry.Path, ignoreCase: true);
        var holder = Assert.Single(entry.By, by => by.Pid == child.Id);
        Assert.Equal("node", holder.Name);
        Assert.NotNull(holder.Command);
        Assert.Contains("setTimeout", holder.Command);
        var refused = Record.Exception(() => Directory.Move(app, Path.Combine(_folder, "moved")));
        Assert.True(refused is not null && InstallSwap.IsHeld(refused), refused?.ToString() ?? "the move was not refused");
    }
}
