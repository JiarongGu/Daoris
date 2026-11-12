using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A <c>try</c> or <c>--cycle</c> cancelled while its plugin runs (the window closing) is no failed call, so no step ends the
/// process: the trial ends it itself, before the scratch under it is removed.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class PluginCancelTests : IDisposable
{
    private readonly string _scratch = Path.Combine(TestHomes.Root, "daoris-cancel-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginCancelTests() => Directory.CreateDirectory(_scratch);

    public void Dispose()
    {
        for (var attempt = 0; Directory.Exists(_scratch); attempt++)
        {
            try
            {
                GitSeed.Remove(_scratch);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20) return;
                Thread.Sleep(250);
            }
        }
    }

    [Fact]
    public async Task A_cancelled_trial_ends_the_plugin_s_process()
    {
        var folder = Scaffold("acme-cancelled", HookPoints.State, out var pidFile);
        var script = Path.Combine(folder, "plugin.mjs");
        var text = File.ReadAllText(script);
        const string answer = "    send({ jsonrpc: '2.0', id: frame.id, result: await answers[frame.method.slice(5)](frame.params ?? {}) });";
        Assert.Contains(answer, text);
        File.WriteAllText(script, text.Replace(answer, "    // silence", StringComparison.Ordinal));
        using var window = new CancellationTokenSource();

        var trial = PluginTrials.TryFolderAsync(Path.Combine(_scratch, "home"), folder, new TrialOptions(Patience: TimeSpan.FromSeconds(30)), window.Token);
        var pid = await StandingAsync(pidFile);
        await Task.Delay(1500);
        window.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => trial);
        Assert.True(Left(pid), "the plugin's process was left running");
    }

    [Fact]
    public async Task A_cancelled_cycle_ends_the_plugin_s_process()
    {
        var folder = Scaffold("acme-cycling", HookPoints.Perform, out var pidFile);
        using var window = new CancellationTokenSource();

        var cycle = PluginCycles.TryAsync(
            Path.Combine(_scratch, "home"), folder, CycleFixture.Default with { Deadline = TimeSpan.FromSeconds(60) }, window.Token);
        var pid = await StandingAsync(pidFile);
        await Task.Delay(3000);
        window.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cycle);
        Assert.True(Left(pid), "the plugin's process was left running");
    }

    private string Scaffold(string id, string point, out string pidFile)
    {
        var plan = PluginKit.Plan(id, [point], _scratch);
        PluginKit.Write(plan);
        pidFile = Path.Combine(_scratch, id + ".pid");
        var script = Path.Combine(plan.Folder, "plugin.mjs");
        File.WriteAllText(script,
            $"import {{ writeFileSync }} from 'node:fs';\nwriteFileSync({JsonSerializer.Serialize(pidFile)}, String(process.pid));\n" + File.ReadAllText(script));
        return plan.Folder;
    }

    private static async Task<int> StandingAsync(string pidFile)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile), out var pid)) return pid;
            await Task.Delay(100);
        }

        throw new InvalidOperationException("the plugin never started");
    }

    private static bool Left(int pid)
    {
        try
        {
            using var running = Process.GetProcessById(pid);
            return running.WaitForExit(5000);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
}
