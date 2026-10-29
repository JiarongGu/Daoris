using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PLUGINSHUTDOWN1: a plugin alive after its last step always gets its <c>shutdown</c> step, however fast it leaves on being told.
/// The race is held, not timed: the told step is released only after the process has exited, the order the flake raced.
/// </summary>
public sealed class PluginTrialShutdownTests
{
    private static Process Short()
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!, "--version")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        return Process.Start(info)!;
    }

    [Fact]
    public async Task A_plugin_that_left_the_instant_it_was_told_still_has_its_shutdown_step()
    {
        using var process = Short();
        var release = new TaskCompletionSource();
        var steps = new List<TrialStep>();
        using var trial = PluginTrialProcess.Of(process);

        var ending = PluginTrials.EndAsync(alive: true, trial, async () => await release.Task, TimeSpan.FromSeconds(10), steps);
        await process.WaitForExitAsync();
        Assert.True(trial.HasExited);
        release.SetResult();
        await ending;

        var step = Assert.Single(steps);
        Assert.Equal("shutdown", step.Name);
        Assert.True(step.Ok);
        Assert.StartsWith("left when told (exit ", step.Sentence);
    }

    [Fact]
    public async Task A_plugin_that_died_during_a_step_is_not_told_and_has_no_shutdown_step()
    {
        using var process = Short();
        await process.WaitForExitAsync();
        var steps = new List<TrialStep>();
        using var trial = PluginTrialProcess.Of(process);

        await PluginTrials.EndAsync(alive: false, trial, () => Task.CompletedTask, TimeSpan.FromSeconds(10), steps);

        Assert.Empty(steps);
    }
}
