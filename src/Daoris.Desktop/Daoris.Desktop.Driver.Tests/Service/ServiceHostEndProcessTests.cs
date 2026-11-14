using System.Diagnostics;
using System.Text;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A test run's service host ends with its run: held in a job that ends it when the run's process goes however that happens, and one a
/// dead run left is ended by the next start before it builds. The run killed is a child <c>dotnet test</c> of this very class, started
/// with its marker set, which starts the fixture and waits to be killed; the stand-ins for the stale host and its dead owner are
/// <c>node</c>, never the service.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class ServiceHostEndProcessTests : IDisposable
{
    // A file, not a variable: the run's environment is the test host's, which a variable of this process's does not reach.
    private static readonly string Marker = Path.Combine(TestHomes.Root, "host-end-child.path");

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-service-host-end-" + Guid.NewGuid().ToString("N")[..8]);

    public ServiceHostEndProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static Process Node(string script)
    {
        var info = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var word in new[] { "-e", script }) info.ArgumentList.Add(word);
        return Process.Start(info)!;
    }

    private static bool Running(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task Until(Func<bool> holds, Func<string> what, int seconds = 60)
    {
        var bound = Stopwatch.StartNew();
        while (!holds())
        {
            Assert.True(bound.Elapsed < TimeSpan.FromSeconds(seconds), what());
            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task A_host_a_dead_run_left_is_ended_at_the_next_start_and_one_of_a_live_run_is_not()
    {
        var folder = ServiceHostRecords.FolderOf(Path.Combine(_root, "artifacts"));
        using var gone = Node("0");
        gone.WaitForExit();
        using var alive = Node("setInterval(() => {}, 1000)");
        using var stale = Node("setInterval(() => {}, 1000)");
        using var kept = Node("setInterval(() => {}, 1000)");
        try
        {
            ServiceHostRecords.Record(folder, stale, by: gone);
            ServiceHostRecords.Record(folder, kept, by: alive);

            Assert.Equal(1, ServiceHostRecords.Reap(folder));

            stale.WaitForExit(15_000);
            Assert.True(stale.HasExited, "the host of the dead run was ended");
            Assert.False(kept.HasExited, "the host of a run still going was left");
            Assert.Single(Directory.GetFiles(folder));
        }
        finally
        {
            foreach (var each in new[] { stale, kept, alive })
            {
                if (!each.HasExited) each.Kill(entireProcessTree: true);
            }
        }
    }

    [Fact]
    public async Task A_run_killed_mid_way_leaves_no_host()
    {
        // The child run: start the fixture, say who it and its host are, and wait to be killed.
        if (File.Exists(Marker) && File.ReadAllText(Marker) is { Length: > 0 } report)
        {
            using var started = await RealServiceHost.StartAsync(report + ".scratch");
            File.WriteAllText(report, $"{Environment.ProcessId} {started.Id} {started.Held}");
            await Task.Delay(TimeSpan.FromMinutes(3));
            return;
        }

        // Built here first, so the child's own build of it is the up-to-date check alone.
        await RealServiceHost.HostDllAsync();
        var dll = Path.Combine(AppContext.BaseDirectory, "Daoris.Desktop.Driver.Tests.dll");
        var reported = Path.Combine(_root, "reported.txt");
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var word in new[] { "test", dll, "--filter", "FullyQualifiedName~" + nameof(ServiceHostEndProcessTests) + "." + nameof(A_run_killed_mid_way_leaves_no_host) })
        {
            info.ArgumentList.Add(word);
        }

        File.WriteAllText(Marker, reported);
        using var run = Process.Start(info)!;
        var said = new StringBuilder();
        run.OutputDataReceived += (_, line) => { lock (said) said.AppendLine(line.Data); };
        run.ErrorDataReceived += (_, line) => { lock (said) said.AppendLine(line.Data); };
        run.BeginOutputReadLine();
        run.BeginErrorReadLine();
        int testHost = 0, host = 0;
        try
        {
            await Until(
                () => File.Exists(reported) && File.ReadAllText(reported).Split(' ').Length == 3,
                () => "the child run started the fixture; it said:\n" + said, 100);
            var words = File.ReadAllText(reported).Split(' ');
            testHost = int.Parse(words[0]);
            host = int.Parse(words[1]);
            Assert.Equal("True", words[2]);
            Assert.True(Running(host));

            // The run's own process killed alone, as a gate killed or a hang's timeout does: nothing walks its tree.
            using (var victim = Process.GetProcessById(testHost)) victim.Kill(entireProcessTree: false);

            await Until(() => !Running(host), () => "the host ended with the run that was killed", 30);
        }
        finally
        {
            File.Delete(Marker);
            if (!run.HasExited) run.Kill(entireProcessTree: true);
            if (host != 0 && Running(host)) Process.GetProcessById(host).Kill(entireProcessTree: true);
        }
    }
}
