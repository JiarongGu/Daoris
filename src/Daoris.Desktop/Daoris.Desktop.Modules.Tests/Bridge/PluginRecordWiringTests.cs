using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The shell's half of the plugin record: the loop keeps one record of its plugins' health, and hands it and
/// the shell's machine log to its hook set and to every landing a route builds, so what a landing or a hand-off does
/// with a plugin is the loop's to know and the log's to keep.
/// </summary>
/// <remarks>
/// Read from the modules' sources, as a reviewer would: the hook set is built only once the loop's host is up, and a
/// landing needs a service, git and a plugin's process, none of which this half of the suite starts.
/// </remarks>
[Trait(Category.Name, Category.Census)]
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
        Assert.Equal(PluginHealthRules.Ready, loop.Health.Of(entry).State);
    }

    [Fact]
    public void The_hook_set_and_every_landing_a_route_builds_are_handed_the_loops_log_and_record()
    {
        var built = SourceFiles.All(SourceFiles.ModulesProject, "*.cs")
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);
                return Regex.Matches(source, @"new (LandingPlugins|HookSet)\(")
                    .Select(call => (File: Path.GetFileName(path), Kind: call.Groups[1].Value, Call: source[call.Index..source.IndexOf(';', call.Index)]));
            })
            .ToList();

        // An entry's or an ask's Accept, and the clean-up's list a terminal asks for, which says a plugin's own lines into
        // its answer. A landing is a run's land, which builds no plugin wire here; Ask Daoris hands no branch on.
        Assert.Equal(2, built.Count(call => call.Kind == "LandingPlugins"));
        Assert.Contains(built, call => call is { Kind: "HookSet", File: "LoopRun.cs" });
        Assert.All(built, call => Assert.True(
            Regex.IsMatch(call.Call, @"\blog:\s") && Regex.IsMatch(call.Call, @"\bhealth:\s"),
            $"{call.File} builds a {call.Kind} without the loop's log and health: {call.Call}"));
    }

    /// <summary>
    /// The plan and an Accept whose run's <c>land</c> Daoris performs are a read and the review's word and a wait, so they
    /// build no plugin wire and call no landing: a run's land is the one way to land. Read from the source, as a reviewer would,
    /// since a landing needs git and a plugin's process.
    /// </summary>
    [Fact]
    public void The_plan_and_an_accept_on_a_run_whose_land_is_performed_build_no_plugin_wire_and_call_no_landing()
    {
        foreach (var file in new[] { "TreesLandDesk.cs", "ChatAccepts.cs", "PerformedWaits.cs", "AcceptWaits.cs" })
        {
            var source = File.ReadAllText(SourceFiles.Module(file));
            Assert.DoesNotContain("LandingPlugins", source);
            Assert.DoesNotContain(".LandAsync(", source);
            Assert.DoesNotContain("TreeLands", source);
        }
    }

    /// <summary>
    /// The loop's conversations write <c>plugin.served</c> to the shell's log for each plugin server they are
    /// handed or withheld, as its driven sessions do through the hook set. A conversation needs a service, git and a
    /// harness, so this half reads the construction; the driver's tests hold what the runner writes.
    /// </summary>
    [Fact]
    public void The_loops_conversations_are_handed_its_log_for_the_servers_they_are_handed()
    {
        var source = File.ReadAllText(SourceFiles.Module("LoopRun.cs"));
        var built = Regex.Matches(source, @"new ChatRunner\(").Select(call => source[call.Index..source.IndexOf(';', call.Index)]).ToList();

        var call = Assert.Single(built);
        Assert.Matches(@"\bplugins:\s*new PluginLog\(log\b", call);
    }
}
