using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The shell's half of PLUGUI1d (D119 §2, §4.2): the loop keeps one record of its plugins' health, and hands it and
/// the shell's machine log to its hook set and to every landing a route builds, so what a landing or a hand-off does
/// with a plugin is the loop's to know and the log's to keep.
/// </summary>
/// <remarks>
/// Read from the modules' sources, as a reviewer would: the hook set is built only once the loop's host is up, and a
/// landing needs a service, git and a plugin's process, none of which this half of the suite starts.
/// </remarks>
public sealed class PluginRecordWiringTests : DriverModuleBridge
{
    [Fact]
    public void The_loop_keeps_one_record_of_its_plugins_health()
    {
        using var loop = Loop();

        Assert.Same(loop.Health, loop.Health);
        var entry = new PluginEntry(
            new PluginManifest("acme.gate", 1, "Gate", "1.0.0", "", [], new PluginHooks(["node", "hooks.mjs"], ["quest/consider"]), []),
            "C:/somewhere/data/plugins/acme.gate", "C:/somewhere/data/plugins/.data/acme.gate", Enabled: true, Problem: null);
        Assert.Equal(PluginHealth.Ready, loop.Health.Of(entry).State);
    }

    [Fact]
    public void The_hook_set_and_every_landing_a_route_builds_are_handed_the_loops_log_and_record()
    {
        var modules = Path.Combine(WorkspaceRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Modules");
        var built = Directory.EnumerateFiles(modules, "*.cs")
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);
                return Regex.Matches(source, @"new (LandingPlugins|HookSet)\(")
                    .Select(call => (File: Path.GetFileName(path), Kind: call.Groups[1].Value, Call: source[call.Index..source.IndexOf(';', call.Index)]));
            })
            .ToList();

        Assert.Equal(3, built.Count(call => call.Kind == "LandingPlugins"));
        Assert.Contains(built, call => call is { Kind: "HookSet", File: "DriverLoop.cs" });
        Assert.All(built, call => Assert.True(
            Regex.IsMatch(call.Call, @"\blog:\s") && Regex.IsMatch(call.Call, @"\bhealth:\s"),
            $"{call.File} builds a {call.Kind} without the loop's log and health: {call.Call}"));
    }

    private static string WorkspaceRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }
}
