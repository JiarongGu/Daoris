using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// FABRIC-END6c (finding 5): the main window's focus heard before the loop's watch exists is kept, not dropped, so a start whose window
/// takes the focus first still asks about each open pull request once the moments exist. One pending flag, not a queue.
/// </summary>
public sealed class DriverLoopFocusTests : Bridge
{
    [Fact]
    public void A_focus_heard_before_the_watch_exists_is_kept_for_it()
    {
        var loop = new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0");
        Assert.False(loop.FocusPending);

        loop.Focused();
        loop.Focused();

        Assert.True(loop.FocusPending);
    }

    /// <summary>
    /// FABRIC-END6a: a later main window's focus is a moment too (§A4.3), as the first one's is. Held on the application's own source, as
    /// <see cref="TaskbarIdentityTests"/> holds every window's identity, since no window opens without a desktop session: the first main
    /// window hands its <c>Activated</c> to the loop's <c>Focused</c>, and the host hands each other main window's, once each; a utility
    /// window (the monitor, a detached session) hands nothing, and the form itself never asks the loop. The container makes the host, so
    /// the loop it is handed is the one the first window tells.
    /// </summary>
    [Fact]
    public void Every_main_window_s_focus_reaches_the_loop_once()
    {
        var app = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.App");
        var first = File.ReadAllText(Path.Combine(app, "MainForm.cs"));
        var host = File.ReadAllText(Path.Combine(app, "SecondaryWindowHost.cs"));
        var form = File.ReadAllText(Path.Combine(app, "SecondaryForm.cs"));
        var program = File.ReadAllText(Path.Combine(app, "Program.cs"));

        Assert.Equal(1, Count(first, "Activated += (_, _) => _driver.Focused();"));
        Assert.Equal(1, Count(host, ".Focused()"));
        Assert.Contains("if (focused is not null && SecondaryWindow.IsMain(name)) form.Activated += (_, _) => focused.Focused();", host);
        Assert.Contains("DriverLoop? focused = null", host);
        Assert.DoesNotContain(".Focused()", form);
        Assert.Contains("builder.Services.AddSingleton<SecondaryWindowHost>();", program);
    }

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    /// <summary>Walk up to the workspace root — the tests run from `bin/Debug/net10.0`.</summary>
    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }
}
