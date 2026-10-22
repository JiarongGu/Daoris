using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSWATCH1a's watcher over REAL runs on the pipe door (D200, the session-watch design §7, cases 1 and 4), told what to do through the
/// watch's stand-in (<see cref="WatchStandIn"/>). The watcher's clock is moved, never waited out: a second of the run is five minutes
/// of its clock. A stand-in that streams on stdout, or on stderr alone, is left alone across three hours of that clock; a silent
/// one is marked quiet under the same clock; and a silent one whose child computes is busy, never marked, and brought to the person as
/// <i>busy, nothing heard</i>.
/// </summary>
/// <remarks>
/// Alone (PROCPAR1): its claim is a bound of a few seconds on a real process, a line heard within a second or two of the last on a clock
/// that counts each second as five minutes, and a computation's CPU over a one-second window, which the load of classes beside it could
/// break.
/// </remarks>
[Collection(Alone.Name)]
[Trait(Category.Name, Category.Process)]
public sealed class SessionWatchProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "session-watch-process", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public SessionWatchProcessTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "watch@example.com");
        Git("config", "user.name", "SESSWATCH1a");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A second of the run is five minutes of the watcher's clock: three hours pass in thirty-six seconds.</summary>
    private static QuietClock Fast()
    {
        var started = Stopwatch.GetTimestamp();
        return new QuietClock(() => TimeSpan.FromTicks(Stopwatch.GetElapsedTime(started).Ticks * 300), stopsInSleep: true);
    }

    /// <summary>Case 1: streaming, on stdout or on stderr alone, past any old bound is left alone: no mark, no note, no end.</summary>
    [Theory]
    [InlineData("stream")]
    [InlineData("stderr")]
    public async Task A_run_that_streams_is_left_alone_across_three_hours_of_its_clock(string cue)
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var driver = Driver(service, cue, "300", "120");
        driver.Watch.Clock = Fast();
        driver.Watch.EndAfter = TimeSpan.FromHours(1);

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(90));

        Assert.Equal("completed", service.Session("s1")["state"]!.GetValue<string>());
        Assert.DoesNotContain("\"watch.quiet\"", Events(), StringComparison.Ordinal);
    }

    /// <summary>The control under the same clock: a stand-in that says one line and nothing more is marked, then ended by --quiet-end.</summary>
    [Fact]
    public async Task A_run_that_goes_silent_is_marked_under_the_same_clock()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var driver = Driver(service, "silent");
        driver.Watch.Clock = Fast();
        driver.Watch.EndAfter = TimeSpan.FromHours(1);

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(90));

        Assert.Contains("\"watch.quiet\"", Events(), StringComparison.Ordinal);
        Assert.Equal("failed", service.Session("s1")["state"]!.GetValue<string>());
        Assert.Contains("nothing came from its agent", service.Session("s1")["note"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Case 4: a busy foreground call, nothing said while a child computes: busy by its job's CPU over the window, never marked, and at
    /// three times its bound the <i>busy, nothing heard</i> row; done, it ends as it finished.
    /// </summary>
    [Fact]
    public async Task A_run_whose_child_computes_silently_is_busy_never_marked_and_brought_as_busy()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var driver = Driver(service, "compute", "20000");
        driver.Watch.Quiet = TimeSpan.FromSeconds(3);
        driver.Watch.Window = TimeSpan.FromSeconds(1);
        var busyRow = false;
        using var watching = new CancellationTokenSource();
        var looking = Task.Run(async () =>
        {
            while (!watching.IsCancellationRequested)
            {
                busyRow |= driver.Watch.RowOf("s1") is { Busy: true };
                await Task.Delay(200);
            }
        });

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(90));
        await watching.CancelAsync();
        await looking;

        Assert.Equal("completed", service.Session("s1")["state"]!.GetValue<string>());
        Assert.True(busyRow, "the busy, nothing heard row never came");
        Assert.DoesNotContain("\"watch.quiet\"", Events(), StringComparison.Ordinal);
    }

    private string Events()
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.events.jsonl");
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, params string[] cue)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = "stub",
            TestBound = 3,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", WatchStandIn.Write(_home), .. cue] },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
