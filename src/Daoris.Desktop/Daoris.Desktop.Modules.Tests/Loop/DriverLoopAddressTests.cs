using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// FINDDAORIS1 (D171 point 1): the desktop leaves the address of the service it holds or adopted in its home while it runs,
/// naming its own process, and takes it away at its clean exit, so a terminal's <c>daoris-driver</c> finds the Daoris that runs.
/// </summary>
public sealed class DriverLoopAddressTests : Bridge
{
    private DriverLoop Loop() => new(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:5181/");

    [Fact]
    public void The_desktop_leaves_its_services_address_in_its_home_and_takes_it_away_as_it_stops()
    {
        var loop = Loop();
        Assert.Null(RunningAddress.Read(Home));

        loop.Announce();

        var named = RunningAddress.Read(Home);
        Assert.Equal("http://localhost:5181", named!.Url);
        Assert.Equal(Environment.ProcessId, named.Pid);
        Assert.True(RunningService.Lives(named.Pid, named.Started));

        loop.Stop();
        Assert.False(File.Exists(RunningAddress.PathIn(Home)));
    }

    /// <summary>Said once: a second word while it runs keeps the one address, and the stop still takes it away.</summary>
    [Fact]
    public void Announcing_twice_keeps_one_address_and_the_stop_takes_it()
    {
        var loop = Loop();
        loop.Announce();
        loop.Announce();

        Assert.NotNull(RunningAddress.Read(Home));
        loop.Dispose();
        Assert.False(File.Exists(RunningAddress.PathIn(Home)));
    }

    /// <summary>
    /// Held on the loop's own source, because nothing short of a running service holds it: the address is left once the
    /// supervisor says the service answers, held or adopted, and never on the path where it did not.
    /// </summary>
    [Fact]
    public void The_loop_leaves_the_address_once_the_service_answers()
    {
        var source = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Modules", "Loop", "DriverLoop.cs"));
        var run = source[source.IndexOf("private async Task RunAsync()", StringComparison.Ordinal)..];

        var ensured = run.IndexOf("supervisor.EnsureAsync(ct)", StringComparison.Ordinal);
        var down = run.IndexOf("if (!up)", StringComparison.Ordinal);
        var announced = run.IndexOf("Announce();", StringComparison.Ordinal);
        Assert.True(ensured > 0 && down > ensured && announced > down, $"ensured at {ensured}, down at {down}, announced at {announced}");
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Daoris.Desktop.Modules")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("the desktop's sources are not above the tests");
    }
}
