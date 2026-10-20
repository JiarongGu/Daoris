using System.Diagnostics;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// APPCWD1 against the machine (D139 §5): a child that names no working folder stands in its parent's, and on Windows a process
/// standing in <c>app/</c> keeps it from being renamed while it lives, though it runs from somewhere else and the launcher does not
/// wait for it. So these start a real child where the application stands and try the swap's move of <c>app/</c>.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class AppWorkingFolderProcessTests : IDisposable
{
    private readonly string _install = Path.Combine(Path.GetTempPath(), "daoris-app-cwd-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly List<Process> _spawned = [];

    public AppWorkingFolderProcessTests()
    {
        Directory.CreateDirectory(App);
        File.WriteAllText(Path.Combine(_install, InstallHome.Marker), "# Daoris — installed desktop\n");
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

        try { Directory.Delete(_install, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string App => Path.Combine(_install, InstallHome.AppFolder);

    private string Moved => Path.Combine(_install, "previous-app");

    /// <summary>
    /// The system's command shell, waiting a minute, standing in <paramref name="folder"/>: running from outside the install. Returned
    /// once its wait has said its first words, so the move below never races its start.
    /// </summary>
    private Process StandIn(string folder)
    {
        var child = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), ["/c", "ping", "-n", "60", "127.0.0.1"])
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            WorkingDirectory = folder,
        })!;
        _spawned.Add(child);
        while (child.StandardOutput.ReadLine() is { } line && !line.Contains("127.0.0.1", StringComparison.Ordinal))
        {
        }

        return child;
    }

    /// <summary>
    /// 🔴 The application's working folder, as it leaves <c>app/</c>, is the install's root: a child standing there, as one that
    /// names no working folder does, lets the swap move <c>app/</c> while it lives.
    /// </summary>
    [Fact]
    public void A_child_standing_where_the_application_stands_does_not_hold_app()
    {
        if (!OperatingSystem.IsWindows()) return;
        var stand = InstallHome.WorkingFolder(App, App);
        Assert.Equal(_install, stand);
        var child = StandIn(stand!);

        Directory.Move(App, Moved);

        Assert.False(child.HasExited);
        Assert.True(Directory.Exists(Moved));
    }

    /// <summary>🔴 And the hold it answers: a child standing in <c>app/</c> keeps the swap from moving it.</summary>
    [Fact]
    public void A_child_standing_in_app_holds_its_move()
    {
        if (!OperatingSystem.IsWindows()) return;
        var child = StandIn(App);

        Assert.ThrowsAny<IOException>(() => Directory.Move(App, Moved));
        Assert.False(child.HasExited);

        child.Kill(entireProcessTree: true);
        child.WaitForExit(10_000);
        Assert.True(SpinWait.SpinUntil(() =>
        {
            try { Directory.Move(App, Moved); return true; } catch (IOException) { return false; }
        }, TimeSpan.FromSeconds(10)));
    }
}
