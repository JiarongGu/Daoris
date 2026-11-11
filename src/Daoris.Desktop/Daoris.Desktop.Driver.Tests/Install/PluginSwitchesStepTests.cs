using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>plugins.json</c>'s <c>off</c>, each plugin's interfaces switched off, moves once into <c>switches</c> at the machine, and <c>off</c>
/// is removed: a word the person already wrote at the machine is never overwritten.
/// </summary>
public sealed class PluginSwitchesStepTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plugin-switches-step-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginSwitchesStepTests() => Directory.CreateDirectory(Path.GetDirectoryName(PluginState.PathIn(_home))!);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string File => PluginState.PathIn(_home);

    [Fact]
    public void Each_off_row_is_a_machine_switch_and_off_goes()
    {
        System.IO.File.WriteAllText(File, """
            { "disabled": ["acme-quiet"], "off": { "acme-desk": ["storage", "commands"], "acme-tickets": ["views"] },
              "switches": { "acme-desk": { "commands": { "machine": true } } } }
            """);
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new PluginSwitchesStep().Run(run);
        new PluginSwitchesStep().Run(run);

        var root = JsonNode.Parse(System.IO.File.ReadAllText(File))!.AsObject();
        Assert.False(root.ContainsKey("off"));
        Assert.Equal("acme-quiet", root["disabled"]![0]!.GetValue<string>());
        Assert.False(PluginSwitches.IsOn(_home, "acme-desk", PluginInterfaces.Storage, PluginPlace.Machine));
        Assert.True(PluginSwitches.IsOn(_home, "acme-desk", PluginInterfaces.Commands, PluginPlace.Machine));
        Assert.False(PluginSwitches.IsOn(_home, "acme-tickets", PluginInterfaces.Views, new PluginPlace(Repository: "engine")));
        Assert.Single(run.Changed);
        Assert.True(System.IO.File.Exists(Path.Combine(run.Copies, run.Changed[0])));
    }

    [Fact]
    public void A_home_with_no_off_is_left_as_it_is()
    {
        System.IO.File.WriteAllText(File, """{ "disabled": [] }""");
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new PluginSwitchesStep().Run(run);

        Assert.Empty(run.Changed);
        Assert.Equal("""{ "disabled": [] }""", System.IO.File.ReadAllText(File));
    }

    [Fact]
    public void The_migration_carries_the_step()
    {
        Assert.Contains(HomeMigration.Steps, step => step is PluginSwitchesStep && step.Name == "plugin-switches");
    }
}
