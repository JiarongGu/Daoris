using System.Diagnostics;
using System.Text;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TERMTRUST1a part (i) (D201's note on part (i)): what a start through the launcher adds to a start made directly, measured on a stand-in
/// harness that exits at once (the system's <c>cmd</c>, never an agent), from the start's call to the harness's exit read. The medians
/// are written to the test's output, and D201's note keeps the measurement; the bound held is a loose one, so a launcher that starts a
/// second runtime or waits on something it should not fails here.
/// </summary>
/// <remarks>Alone (PROCPAR1): its claim is a bound on a real process's wall time, which the load of the classes beside it would move.</remarks>
[Trait(Category.Name, Category.Process)]
[Collection(Alone.Name)]
public sealed class AgentLaunchTimeTests(ITestOutputHelper output) : IDisposable
{
    private const int Runs = 15;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-agent-launch-time-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private ProcessStartInfo Harness()
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            UseShellExecute = false,
            WorkingDirectory = _root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        foreach (var word in new[] { "/d", "/c", "echo ready" }) info.ArgumentList.Add(word);
        return info;
    }

    private async Task<double> MedianAsync(string? home, AgentJobs jobs)
    {
        var times = new List<double>();
        // One first start of each, unmeasured: the system's caches for both programs warmed alike.
        for (var run = 0; run <= Runs; run++)
        {
            var clock = Stopwatch.StartNew();
            Process process;
            using (AgentJobs.Within(jobs))
            {
                process = (await AgentStarts.StartAsync(Harness(), ContainedKind.Agent, StartClass.Background, home, log: null, CancellationToken.None))!;
            }

            using (process)
            {
                Assert.Equal("ready", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)));
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Equal(0, process.ExitCode);
            }

            if (run > 0) times.Add(clock.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        return times[times.Count / 2];
    }

    [Fact]
    public async Task A_start_through_the_launcher_adds_a_bounded_time_to_a_direct_one()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(_root);
        var direct = Path.Combine(_root, "direct");
        Directory.CreateDirectory(direct);
        var root = Path.Combine(_root, "install");
        var home = Path.Combine(root, "data");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        var bin = Path.Combine([root, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        var built = Path.GetDirectoryName(PieceLauncherProcessTests.Launcher())!;
        foreach (var file in Directory.EnumerateFiles(built)) File.Copy(file, Path.Combine(bin, Path.GetFileName(file)));
        using var jobs = new AgentJobs(1_900_000_000 + Random.Shared.Next(100_000_000));

        var uncontained = await MedianAsync(direct, jobs);
        var launched = await MedianAsync(home, jobs);

        output.WriteLine($"median of {Runs}: direct {uncontained:F0} ms, through the launcher {launched:F0} ms, added {launched - uncontained:F0} ms");
        Assert.True(launched - uncontained < 2_000, $"the launcher added {launched - uncontained:F0} ms to a start");
    }
}
