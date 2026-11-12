using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The relay end to end as processes: the built host's <c>events relay --where</c> lays
/// its copy out in a home beside a stand-in instance and prints its command; that copy's <c>--once</c> serves its pipe; and the real
/// stand-in for the <c>event-relay</c> plugin (<see cref="RelayStandIn"/>), started into the stand-in instance's agents' job and spoken to over the hook wire as the driver does, finds
/// the relay by its file, passes step 3 by the job, and tells two events, after which its stop ends the relay with what it gathered.
/// Every process is started hidden: no window, no console, no real agent.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class RelayOnceProcessTests : IDisposable
{
    private const string Run = "6c0f1a9e2b47";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "relay-once", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<Process> _started = [];

    private string Home => Path.Combine(_root, "home");

    public void Dispose()
    {
        foreach (var process in _started)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.Dispose();
        }

        GitSeed.Discard(_root);
    }

    private Process Start(string file, IEnumerable<string> words, IDictionary<string, string?> environment, bool input = false)
    {
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = input,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = _root,
        };
        foreach (var word in words) info.ArgumentList.Add(word);
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList()) info.Environment.Remove(name);
        foreach (var (name, value) in environment) info.Environment[name] = value;
        var process = Process.Start(info)!;
        _started.Add(process);
        return process;
    }

    private static async Task<(int Code, string[] Out, string[] Err)> EndedAsync(Process process)
    {
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await process.WaitForExitAsync(bound.Token);
        static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (process.ExitCode, Lines(await output), Lines(await error));
    }

    [Fact]
    public async Task Where_lays_the_copy_out_and_its_relay_takes_the_plugin_in_the_holders_job_and_prints_its_batch()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(Home);

        // The stand-in instance: a live process the home's lease names, whose agents' job this test makes.
        var holder = Start("node", ["-e", "setTimeout(() => {}, 300000)"], new Dictionary<string, string?>());
        var lease = HomeLease.PathIn(Home);
        Directory.CreateDirectory(Path.GetDirectoryName(lease)!);
        File.WriteAllText(lease, JsonSerializer.Serialize(new
        {
            kind = "headless", pid = holder.Id, started = holder.StartTime.ToUniversalTime().Ticks, since = DateTimeOffset.UtcNow,
        }));
        using var job = ProcessJob.CreateNamed(AgentJobs.NameOf(holder.Id)).Job;
        Assert.NotNull(job);

        var where = await EndedAsync(Start("dotnet", [DriverHostTests.HostDll(), "events", "relay", "--where"], new Dictionary<string, string?> { ["DAORIS_HOME"] = Home }));
        Assert.Equal(0, where.Code);
        Assert.Empty(where.Err);
        var start = Assert.Single(where.Out);
        var command = Regex.Match(start, "\"(?<copy>[^\"]+)\" events relay --once --home \"(?<home>[^\"]+)\"");
        Assert.True(command.Success, start);
        var copy = Path.GetDirectoryName(command.Groups["copy"].Value)!;
        Assert.Equal(RelayBuild.Of(Path.GetDirectoryName(DriverHostTests.HostDll())!), Path.GetFileName(copy));

        var relay = Start(command.Groups["copy"].Value, ["events", "relay", "--once", "--home", command.Groups["home"].Value, "--since", $"{Run}:4"],
            new Dictionary<string, string?> { ["DAORIS_SERVICE_KEY"] = "a-key-the-relay-drops" });
        var data = Path.Combine(DaorisHome.Entry(Home, HomeEntry.Plugins), PluginCatalog.DataFolder, "event-relay");
        var file = Path.Combine(data, "subscribers", $"{relay.Id}.json");
        for (var tries = 0; !File.Exists(file); tries++)
        {
            Assert.True(tries < 1500 && !relay.HasExited, "the relay wrote no subscriber file");
            await Task.Delay(20);
        }

        var plugin = Start("node", [RelayStandIn.File],
            new Dictionary<string, string?> { ["DAORIS_PLUGIN_ID"] = "event-relay", ["DAORIS_PLUGIN_DATA"] = data }, input: true);
        Assert.True(job.Join(plugin.Handle), "the plugin could not join the holder's agents' job");
        // The plugin says each connection on its stderr: its first line has reached the relay once it says this one's.
        var connected = $"connected subscriber {relay.Id}";
        var heard = new TaskCompletionSource();
        var said = Task.Run(async () =>
        {
            while (await plugin.StandardError.ReadLineAsync() is { } line)
            {
                if (line == connected) heard.TrySetResult();
            }
        });
        async Task Tell(string frame) => await plugin.StandardInput.WriteLineAsync(frame);
        await Tell($$$$"""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1,"points":["work/event"],"events":{"run":"{{{{Run}}}}","seq":4,"grammar":2,"build":"{{{{Path.GetFileName(copy)}}}}"}}}""");
        var answered = 0;
        while (answered < 1 && await plugin.StandardOutput.ReadLineAsync() is { } line) answered += line.Contains("\"id\":1", StringComparison.Ordinal) ? 1 : 0;
        await heard.Task.WaitAsync(Poll.Patience);
        await Tell($$$"""{"jsonrpc":"2.0","id":2,"method":"hook/work/event","params":{"run":"{{{Run}}}","seq":5,"missed":0,"kind":"session.parked","at":"2026-11-06T10:00:00Z","session":"4b62a9fe"}}""");
        await Tell($$$"""{"jsonrpc":"2.0","id":3,"method":"hook/work/event","params":{"run":"{{{Run}}}","seq":6,"missed":0,"kind":"work.landed","at":"2026-11-06T10:00:01Z","session":"4b62a9fe","code":"landed"}}""");
        while (answered < 3 && await plugin.StandardOutput.ReadLineAsync() is { } line) answered += line.Contains("\"result\"", StringComparison.Ordinal) ? 1 : 0;
        // The plugin answers as it queues, and its stop closes the connection unflushed: its two local writes are given a moment to land.
        await Task.Delay(500);
        await Tell("""{"jsonrpc":"2.0","method":"shutdown"}""");

        var ran = await EndedAsync(relay);
        Assert.Equal(0, ran.Code);
        Assert.Empty(ran.Err);
        Assert.Equal(
            [
                $"Daoris: session 4b62a9fe parked, waiting on the person [{Run} #5]",
                $"Daoris: session 4b62a9fe's work landed [{Run} #6]",
                $"Daoris: events may have been missed; read the sessions again [{Run} #6]",
                $"Daoris: read up to [{Run} #6]",
            ],
            ran.Out);
        Assert.False(File.Exists(file), "the relay's file outlived it");
        await said.WaitAsync(Poll.Patience);
    }
}
