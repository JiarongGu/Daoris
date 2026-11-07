using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The relay through the built host as processes (EVENTHOOK1c; the event hook design §9's 1c proofs): <c>events relay --where</c> at the host's
/// entry in front of a lease another act holds, an unwritable lease and a <c>bin</c> entry at both its places, each said by one fixed sentence
/// and nothing of the lease's <c>what</c>, the home or a path; two <c>--where</c> at once; a copy swapped for a newer build while a relay runs;
/// and a plugin started through a shim, which the holder's agents' job still holds. Every process is started hidden: no window, no real agent.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class RelayHostProcessTests : IDisposable
{
    private const string Run = "5d2e8c1a7b30";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "relay-host", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<Process> _started = [];

    public RelayHostProcessTests() => Directory.CreateDirectory(_root);

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
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await process.WaitForExitAsync(bound.Token);
        static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (process.ExitCode, Lines(await output), Lines(await error));
    }

    private Process Where(string home) => Start("dotnet", [DriverHostTests.HostDll(), "events", "relay", "--where"], new Dictionary<string, string?> { ["DAORIS_HOME"] = home });

    private Process Sleeper() => Start("node", ["-e", "setTimeout(() => {}, 300000)"], new Dictionary<string, string?>());

    private static void Lease(string home, Process holder, string kind, string? what)
    {
        var lease = HomeLease.PathIn(home);
        Directory.CreateDirectory(Path.GetDirectoryName(lease)!);
        File.WriteAllText(lease, JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = kind, ["pid"] = holder.Id, ["started"] = holder.StartTime.ToUniversalTime().Ticks, ["since"] = DateTimeOffset.UtcNow, ["what"] = what,
        }));
    }

    private static string Builds(string home) => Path.Combine(DaorisHome.Entry(home, HomeEntry.Bin), RelayCopy.Folder);

    private static void AssertNames(string[] said, params string[] unsaid)
    {
        foreach (var text in unsaid) Assert.DoesNotContain(said, line => line.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The host's generic lease in front of <c>--where</c>: another act holds the home for the whole wait (its <c>what</c> a name, and a path),
    /// the one held line and exit 1 with nothing of the <c>what</c>, the home or a path on either stream; an act that lets go within the wait
    /// leaves the home to this run, which lays its copy out and lets it go.
    /// </summary>
    [Fact]
    public async Task Where_in_front_of_another_acts_lease_says_the_one_held_line_and_names_nothing_of_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var named = Path.Combine(_root, "named");
        var pathed = Path.Combine(_root, "pathed");
        var letting = Path.Combine(_root, "letting");
        var secret = Path.Combine(_root, "secret-place", "branch");
        var holders = new[] { Sleeper(), Sleeper(), Sleeper() };
        Lease(named, holders[0], "verb", "Merge-every-landed-branch.now");
        Lease(pathed, holders[1], "verb", secret);
        Lease(letting, holders[2], "verb", "Merge-every-landed-branch.now");

        var first = Where(named);
        var second = Where(pathed);
        var third = Where(letting);
        await Task.Delay(2000);
        holders[2].Kill(entireProcessTree: true);
        var results = await Task.WhenAll(EndedAsync(first), EndedAsync(second), EndedAsync(third));

        foreach (var (held, home) in new[] { (results[0], named), (results[1], pathed) })
        {
            Assert.Equal(1, held.Code);
            Assert.Empty(held.Out);
            Assert.Equal([RelaySaid.HeldByAnother], held.Err);
            AssertNames(held.Err, "Merge-every-landed-branch", "secret-place", home, _root);
        }

        Assert.Equal(0, results[2].Code);
        Assert.Empty(results[2].Err);
        Assert.Single(results[2].Out);
        Assert.Single(Directory.GetFileSystemEntries(Builds(letting)));
        Assert.False(File.Exists(HomeLease.PathIn(letting)), "the lease outlived the verb");
    }

    /// <summary>An unwritable lease and a <c>bin</c> entry at both its places are each the relay-stopped line with its code, exit 2, and nothing of an exception's message.</summary>
    [Fact]
    public async Task Where_with_an_unwritable_lease_or_a_bin_entry_at_both_places_says_only_the_stopped_line()
    {
        if (!OperatingSystem.IsWindows()) return;
        var blocked = Path.Combine(_root, "blocked");
        Directory.CreateDirectory(Path.GetDirectoryName(HomeLease.PathIn(blocked))!);
        Directory.CreateDirectory(HomeLease.PathIn(blocked));
        File.WriteAllText(Path.Combine(HomeLease.PathIn(blocked), "held"), "a directory stands where the lease file goes");

        var twice = Path.Combine(_root, "twice");
        Directory.CreateDirectory(twice);
        File.WriteAllText(Path.Combine(twice, DaorisHome.InstallMarker), "");
        foreach (var place in DaorisHome.Places(twice, HomeEntry.Bin))
        {
            Directory.CreateDirectory(place);
            File.WriteAllText(Path.Combine(place, "entry.txt"), "x");
        }

        var results = await Task.WhenAll(EndedAsync(Where(blocked)), EndedAsync(Where(twice)));

        Assert.Equal(2, results[0].Code);
        Assert.Equal([RelaySaid.Stopped("lease")], results[0].Err);
        Assert.Equal(2, results[1].Code);
        Assert.Equal([RelaySaid.Stopped("home")], results[1].Err);
        Assert.All(results, each => Assert.Empty(each.Out));
        foreach (var result in results) AssertNames(result.Err, _root, "Exception", " at ");
    }

    /// <summary>Two <c>--where</c> at once beside an instance (which holds the home, so neither waits): one whole copy and no staging folder or removal name.</summary>
    [Fact]
    public async Task Two_where_at_once_leave_one_whole_copy_and_no_staging_folder()
    {
        if (!OperatingSystem.IsWindows()) return;
        var home = Path.Combine(_root, "home");
        Lease(home, Sleeper(), "headless", null);

        var results = await Task.WhenAll(EndedAsync(Where(home)), EndedAsync(Where(home)));

        Assert.All(results, each => Assert.Equal(0, each.Code));
        Assert.All(results, each => Assert.Empty(each.Err));
        Assert.Equal(results[0].Out, results[1].Out);
        var entries = Directory.GetFileSystemEntries(Builds(home));
        var copy = Assert.Single(entries);
        Assert.Equal(RelayBuild.Of(Path.GetDirectoryName(DriverHostTests.HostDll())!), Path.GetFileName(copy));
        Assert.True(File.Exists(Path.Combine(copy, RelayCopy.InUse)));
    }

    private async Task<(Process Relay, string Copy, string Data, string File, Process Holder, ProcessJob Job)> StartRelayAsync(string home)
    {
        var holder = Sleeper();
        Lease(home, holder, "headless", null);
        var job = ProcessJob.CreateNamed(AgentJobs.NameOf(holder.Id)).Job!;
        var where = await EndedAsync(Where(home));
        Assert.Equal(0, where.Code);
        var start = Regex.Match(Assert.Single(where.Out), "\"(?<copy>[^\"]+)\" events relay --once --home \"(?<home>[^\"]+)\"");
        Assert.True(start.Success, string.Join('\n', where.Out));
        var relay = Start(start.Groups["copy"].Value, ["events", "relay", "--once", "--home", start.Groups["home"].Value, "--since", $"{Run}:4"], new Dictionary<string, string?>());
        var data = Path.Combine(DaorisHome.Entry(home, HomeEntry.Plugins), PluginCatalog.DataFolder, "event-relay");
        var file = Path.Combine(data, "subscribers", $"{relay.Id}.json");
        for (var tries = 0; !File.Exists(file); tries++)
        {
            Assert.True(tries < 1500 && !relay.HasExited, "the relay wrote no subscriber file");
            await Task.Delay(20);
        }

        return (relay, Path.GetDirectoryName(start.Groups["copy"].Value)!, data, file, holder, job);
    }

    /// <summary>Tells the plugin the handshake, waits until its first line has reached the relay, then the two events, and ends it; returns the relay's end.</summary>
    private async Task<(int Code, string[] Out, string[] Err)> TellAsync(Process plugin, Process relay, string copy)
    {
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
        while (answered < 2 && await plugin.StandardOutput.ReadLineAsync() is { } line) answered += line.Contains("\"result\"", StringComparison.Ordinal) ? 1 : 0;
        await Task.Delay(500);
        await Tell("""{"jsonrpc":"2.0","method":"shutdown"}""");
        var ended = await EndedAsync(relay);
        await said.WaitAsync(Poll.Patience);
        return ended;
    }

    private string PluginFile => RelayStandIn.File;

    private string[] Batch =>
    [
        $"Daoris: session 4b62a9fe parked, waiting on the person [{Run} #5]", RelaySaid.Gap(Run, 5), $"Daoris: read up to [{Run} #5]",
    ];

    /// <summary>
    /// A newer build laid out while a relay runs (the staged update that swaps <c>app/</c>): the build the relay holds is kept, the relay serves
    /// the plugin it started for and prints its batch, and the next <c>--where</c> removes the build once the relay has gone.
    /// </summary>
    [Fact]
    public async Task A_newer_build_laid_out_while_a_relay_runs_keeps_its_copy_and_the_relay_serves_on()
    {
        if (!OperatingSystem.IsWindows()) return;
        var home = Path.Combine(_root, "home");
        var (relay, copy, data, file, _, job) = await StartRelayAsync(home);
        using var held = job;

        var newer = Path.Combine(_root, "newer");
        Directory.CreateDirectory(newer);
        File.WriteAllText(Path.Combine(newer, "daoris-driver.dll"), "a newer build");
        var laid = RelayCopy.LayOut(home, newer, _ => { });
        Assert.NotEqual(copy, laid);
        Assert.True(Directory.Exists(copy), "the build a relay holds was removed");

        var plugin = Start("node", [PluginFile], new Dictionary<string, string?> { ["DAORIS_PLUGIN_ID"] = "event-relay", ["DAORIS_PLUGIN_DATA"] = data }, input: true);
        Assert.True(job.Join(plugin.Handle), "the plugin could not join the holder's agents' job");
        var ran = await TellAsync(plugin, relay, copy);

        Assert.Equal(0, ran.Code);
        Assert.Empty(ran.Err);
        Assert.Equal(Batch, ran.Out);
        Assert.False(File.Exists(file));
        // The ended relay's handle on its `in-use` file is let go with the process, a moment after its exit is seen.
        for (var tries = 0; tries < 50 && Directory.Exists(copy); tries++)
        {
            RelayCopy.LayOut(home, newer, _ => { });
            await Task.Delay(100);
        }

        Assert.False(Directory.Exists(copy), "a build none holds was kept: " + string.Join(", ", Directory.GetFileSystemEntries(Builds(home)).Select(Path.GetFileName)));
    }

    /// <summary>
    /// The host's own <c>drive --once</c> starts the <c>event-relay</c> plugin into its job: a relay already listening is found by the plugin, passes
    /// the holder check by that job, and ends with its batch's closing line when the one tick is over and the plugin is stopped.
    /// </summary>
    [Fact]
    public async Task Drive_once_starts_the_plugin_into_its_job_and_the_relay_takes_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var home = Path.Combine(_root, "home");
        var where = await EndedAsync(Where(home));
        Assert.Equal(0, where.Code);
        var start = Regex.Match(Assert.Single(where.Out), "\"(?<copy>[^\"]+)\" events relay --once --home \"(?<home>[^\"]+)\"");
        var plugins = Path.Combine(DaorisHome.Entry(home, HomeEntry.Plugins), "event-relay");
        Directory.CreateDirectory(plugins);
        foreach (var file in new[] { "plugin.json", "plugin.mjs" }) File.Copy(Path.Combine(RelayStandIn.Folder, file),Path.Combine(plugins, file));

        var relay = Start(start.Groups["copy"].Value, ["events", "relay", "--once", "--home", start.Groups["home"].Value], new Dictionary<string, string?>());
        var subscribers = Path.Combine(DaorisHome.Entry(home, HomeEntry.Plugins), PluginCatalog.DataFolder, "event-relay", "subscribers", $"{relay.Id}.json");
        for (var tries = 0; !File.Exists(subscribers); tries++)
        {
            Assert.True(tries < 1500 && !relay.HasExited, "the relay wrote no subscriber file");
            await Task.Delay(20);
        }

        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        var port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        using var listener = new System.Net.HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var serving = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                System.Net.HttpListenerContext context;
                try { context = await listener.GetContextAsync(); }
                catch (Exception error) when (error is System.Net.HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }
                var read = context.Request.HttpMethod == "GET" && context.Request.Url!.AbsolutePath is "/api/quests" or "/api/registry" or "/api/sessions";
                var bytes = Encoding.UTF8.GetBytes(read ? "[]" : """{"error":"the stand-in answers only the tick's reads"}""");
                context.Response.StatusCode = read ? 200 : 404;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        });

        var drive = await EndedAsync(Start("dotnet", [DriverHostTests.HostDll(), "drive", "--once"],
            new Dictionary<string, string?> { ["DAORIS_HOME"] = home, ["DAORIS_SERVICE_URL"] = $"http://127.0.0.1:{port}" }));
        var ran = await EndedAsync(relay);
        listener.Stop();
        await serving;

        Assert.True(drive.Code == 0, string.Join('\n', drive.Out.Concat(drive.Err)));
        Assert.Equal(0, ran.Code);
        Assert.StartsWith("Daoris: read up to [", ran.Out[^1], StringComparison.Ordinal);
    }

    /// <summary>A plugin started through a version manager's shim: the shim is in the holder's agents' job and starts the plugin later, which the job holds too, so it is taken.</summary>
    [Fact]
    public async Task A_plugin_started_through_a_shim_inside_the_job_is_taken()
    {
        if (!OperatingSystem.IsWindows()) return;
        var home = Path.Combine(_root, "home");
        var (relay, copy, data, _, _, job) = await StartRelayAsync(home);
        using var held = job;

        var shim = Start("node",
            ["-e", "setTimeout(() => require('child_process').spawn(process.execPath, [process.argv[1]], { stdio: 'inherit' }).on('exit', c => process.exit(c)), 1500)", PluginFile],
            new Dictionary<string, string?> { ["DAORIS_PLUGIN_ID"] = "event-relay", ["DAORIS_PLUGIN_DATA"] = data }, input: true);
        Assert.True(job.Join(shim.Handle), "the shim could not join the holder's agents' job");
        var ran = await TellAsync(shim, relay, copy);

        Assert.Equal(0, ran.Code);
        Assert.Equal(Batch, ran.Out);
    }
}
