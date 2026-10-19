using System.ComponentModel;
using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Each of the driver's own starts passes the start gate (PROCSTART2, UIFREEZE1): a session's process, a harness action, a
/// usage read, a plugin's hook process and its trial. Before, each called the runtime's start on its caller's thread, so a
/// route that reached one before its first wait waited on .NET's one process-start lock on the window's thread.
/// </summary>
/// <remarks>
/// The fast half: no process starts. Each start is made, within the test's flow, by a stand-in the gate hands it to
/// (<see cref="ProcessStarts.Within"/>), which refuses it as the runtime refuses a program that is not there: the site
/// answers as it always has for that, and the test reads where the start was made. The program named is nowhere, so a
/// site that bypassed the gate starts nothing either.
/// </remarks>
public sealed class GatedStartsTests : IDisposable
{
    private const string Nowhere = "daoris-procstart2-nowhere";

    // True on the thread making the call, while it makes it: a start seen there was made on a caller's thread.
    [ThreadStatic]
    private static bool t_calling;

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-gated-starts-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly List<(string File, bool OnCaller)> _seen = [];

    public GatedStartsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private IDisposable Refusing() => ProcessStarts.Within(info =>
    {
        lock (_seen) _seen.Add((Path.GetFileNameWithoutExtension(info.FileName), t_calling));
        throw new Win32Exception(2, "The system cannot find the file specified.");
    });

    /// <summary>The call, its synchronous part marked as the caller's.</summary>
    private static Task Calling(Func<Task> call)
    {
        t_calling = true;
        try
        {
            return call();
        }
        finally
        {
            t_calling = false;
        }
    }

    private void SeenOnceOffTheCaller()
    {
        var seen = Assert.Single(_seen);
        Assert.Equal(Nowhere, seen.File);
        Assert.False(seen.OnCaller, "the start was made on the caller's thread");
    }

    [Fact]
    public async Task A_harness_actions_start_passes_the_gate()
    {
        using var _ = Refusing();
        var refused = await Assert.ThrowsAsync<DriverException>(() => Calling(() => HarnessActions.RunAsync(
            [Nowhere], new HarnessToolchain(Binary: [Nowhere], VersionArguments: ["--version"]), null, _ => { }, CancellationToken.None)));

        Assert.Contains("could not be started", refused.Message);
        SeenOnceOffTheCaller();
    }

    [Fact]
    public async Task A_usage_reads_start_passes_the_gate()
    {
        using var _ = Refusing();
        var info = new ProcessStartInfo(Nowhere) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };

        IReadOnlyList<WindowReading>? read = [];
        await Calling(async () => read = await CodexUsage.ReadAsync(info, TimeSpan.FromSeconds(5), CancellationToken.None));

        Assert.Null(read);
        SeenOnceOffTheCaller();
    }

    [Fact]
    public async Task A_plugins_hook_process_passes_the_gate()
    {
        using var _ = Refusing();
        var refused = await Assert.ThrowsAsync<DriverException>(() => Calling(() => HookProcess.StartAsync(
            Plugin(), _home, _ => { }, CancellationToken.None)));

        Assert.Contains("could not start", refused.Message);
        SeenOnceOffTheCaller();
    }

    [Fact]
    public async Task A_plugins_trial_process_passes_the_gate()
    {
        using var _ = Refusing();
        await Assert.ThrowsAsync<Win32Exception>(() => Calling(() => PluginTrialProcess.StartAsync(Plugin(), _home, noRepositoryAt: null, CancellationToken.None)));

        SeenOnceOffTheCaller();
    }

    [Fact]
    public async Task A_sessions_process_passes_the_gate_before_anything_is_held()
    {
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Empty()));
        var driver = new Daoris.Driver.Driver(service, DriverConfig.Empty, AdapterSet.Built(), _home);
        var spawned = false;

        using var _ = Refusing();
        await Assert.ThrowsAsync<Win32Exception>(() => Calling(() => driver.HoldAsync(
            adapter: null!, info: new ProcessStartInfo(Nowhere), target: null!, sessionId: "s1", transcript: Path.Combine(_home, "s1.log"),
            cwd: _home, harnessNotice: null, rules: (null, null, null), handed: null, refusesInput: null,
            conclude: (_, _, _, _) => Task.FromResult(0), ct: CancellationToken.None, spawned: () => spawned = true)));

        Assert.False(spawned);
        SeenOnceOffTheCaller();
    }

    /// <summary>
    /// PARALLEL1f (D181's PARALLEL1de note): a session's start waits for its place on the loop's token, so a driver closing while
    /// it waits ends the wait, starts nothing and tells no spawn, which its caller reads as a run that never began. On no token,
    /// the start was made once a place came, though the loop that wanted it had gone.
    /// </summary>
    [Fact]
    public async Task A_sessions_start_whose_loop_closed_while_it_waited_starts_nothing()
    {
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Empty()));
        var driver = new Daoris.Driver.Driver(service, DriverConfig.Empty, AdapterSet.Built(), _home);
        var spawned = false;
        using var closed = new CancellationTokenSource();
        await closed.CancelAsync();

        using var _ = Refusing();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Calling(() => driver.HoldAsync(
            adapter: null!, info: new ProcessStartInfo(Nowhere), target: null!, sessionId: "s1", transcript: Path.Combine(_home, "s1.log"),
            cwd: _home, harnessNotice: null, rules: (null, null, null), handed: null, refusesInput: null,
            conclude: (_, _, _, _) => Task.FromResult(0), ct: closed.Token, spawned: () => spawned = true)));

        Assert.False(spawned);
        Assert.Empty(_seen);
    }

    private PluginEntry Plugin() =>
        new(new PluginManifest("probe", 1, "probe", "1.0.0", "", [], new PluginHooks([Nowhere, "hooks.mjs"], ["quest/consider"]), []),
            DaorisHome.Entry(_home, HomeEntry.Plugins, "probe"), DaorisHome.Entry(_home, HomeEntry.Plugins, PluginCatalog.DataFolder, "probe"),
            Enabled: true, Problem: null);

    /// <summary>A service that answers every read with nothing: the start is refused before the driver asks it anything.</summary>
    private sealed class Empty : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
