using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SPLASHDATA1: the window's splash lifts once the page's first screen has its data, which the page says over the kit's
/// <c>CLOSE_SPLASH</c> (Shenora 0.21's <c>WindowCommandOptions.CloseSplash</c>), not as the page's browser is made, which
/// lifted it onto an empty screen. A page that never says is lifted at a bound, and the log says which lifted it.
/// </summary>
public sealed class SplashLiftTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-splashdata1-" + Guid.NewGuid().ToString("N")[..8]);

    public SplashLiftTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string[] Lines()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder).SelectMany(path =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }).ToArray();
    }

    /// <summary>The page's word lifts it, whether it came while the window waited or before, and the log says so.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_pages_word_lifts_it_and_the_log_says_the_page_did(bool before)
    {
        using var log = new MachineLog(_home, "desktop");
        var lift = new SplashLift();

        if (before) lift.PageSaid();
        var waiting = lift.WaitAsync(log, TimeSpan.FromMinutes(5));
        if (!before)
        {
            Assert.False(waiting.IsCompleted, "the splash came down before the page said");
            lift.PageSaid();
        }

        Assert.True(await waiting.WaitAsync(TimeSpan.FromSeconds(30)));
        var line = Assert.Single(Lines());
        Assert.Contains("\"level\":\"info\"", line);
        Assert.Contains("\"event\":\"splash.lifted\"", line);
        Assert.Contains("\"by\":\"page\"", line);
        Assert.Contains("\"ms\":", line);
    }

    /// <summary>Said twice (a page that rendered twice), it is one lift and one line.</summary>
    [Fact]
    public async Task A_word_said_twice_is_one_lift()
    {
        using var log = new MachineLog(_home, "desktop");
        var lift = new SplashLift();

        lift.PageSaid();
        lift.PageSaid();

        Assert.True(await lift.WaitAsync(log, TimeSpan.FromMinutes(5)));
        Assert.Single(Lines());
    }

    /// <summary>
    /// A page that never says (a bundle without the call, a screen whose data never comes) is never held forever: the
    /// bound lifts it, and the log says the bound did, as a warning a person can find.
    /// </summary>
    [Fact]
    public async Task A_page_that_never_says_is_lifted_at_the_bound_and_the_log_says_so()
    {
        using var log = new MachineLog(_home, "desktop");
        var lift = new SplashLift();

        Assert.False(await lift.WaitAsync(log, TimeSpan.FromMilliseconds(100)).WaitAsync(TimeSpan.FromSeconds(30)));

        var line = Assert.Single(Lines());
        Assert.Contains("\"level\":\"warn\"", line);
        Assert.Contains("\"event\":\"splash.lifted\"", line);
        Assert.Contains("\"by\":\"bound\"", line);
    }

    /// <summary>The window's bound: seconds a local page's first screen takes, never a wait a person gives up on.</summary>
    [Fact]
    public void The_bound_is_seconds_not_forever()
    {
        Assert.InRange(SplashLift.Bound, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Held on the application's own source, as the colour scheme's route is (THEMESCHEME1), because the window has no
    /// test project: the kit's route is answered by the lift, and the splash comes down after the lift's wait, not as
    /// the page's browser is made.
    /// </summary>
    [Fact]
    public void The_window_answers_the_kits_route_and_lifts_after_the_wait()
    {
        var main = File.ReadAllText(SourceFiles.Find("Daoris.Desktop.App", "MainForm.cs"));

        Assert.Contains("CloseSplash = _lift.PageSaid,", main);
        var bringUp = main[main.IndexOf("private async Task BringUpAsync()", StringComparison.Ordinal)..];
        var created = bringUp.IndexOf("await browser.Created", StringComparison.Ordinal);
        var waited = bringUp.IndexOf("await _lift.WaitAsync(", StringComparison.Ordinal);
        var lifted = bringUp.IndexOf("Controls.Remove(_splash)", StringComparison.Ordinal);
        Assert.True(created >= 0 && waited > created && lifted > waited, "the splash comes down before the page's word or the bound");
    }
}
