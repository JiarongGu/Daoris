using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's word on each interface and point of a plugin, per place, in <c>plugins.json</c>'s <c>switches</c>: the nearest place's
/// word wins, everything is on until switched off, storage reads the machine's word alone, and <c>disabled</c> is over all of it.
/// </summary>
public sealed class PluginSwitchesTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plugin-switches-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly PluginPlace Engine = new(Repository: "engine", Workspace: "home");

    private static readonly PluginPlace Home = new(Workspace: "home");

    public PluginSwitchesTests() => Directory.CreateDirectory(Path.GetDirectoryName(PluginState.PathIn(_home))!);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string File => PluginState.PathIn(_home);

    [Fact]
    public void Everything_is_on_until_switched_off()
    {
        Assert.True(PluginSwitches.IsOn(_home, "acme-desk", PluginSwitches.Tools, Engine));
        Assert.True(PluginSwitches.IsOn(_home, "acme-desk", HookPoints.QuestConsider, PluginPlace.Machine));
    }

    [Fact]
    public void The_nearest_place_s_word_wins()
    {
        PluginSwitches.Set(_home, "acme-desk", PluginSwitches.Tools, "machine", on: false);
        PluginSwitches.Set(_home, "acme-desk", PluginSwitches.Tools, "repository:engine", on: true);

        Assert.True(PluginSwitches.IsOn(_home, "acme-desk", PluginSwitches.Tools, Engine));
        Assert.False(PluginSwitches.IsOn(_home, "acme-desk", PluginSwitches.Tools, Home));
        Assert.False(PluginSwitches.IsOn(_home, "acme-desk", PluginSwitches.Tools, PluginPlace.Machine));
        Assert.False(PluginSwitches.IsOn(_home, "acme-desk", PluginSwitches.Tools, new PluginPlace(Repository: "other", Workspace: "home")));

        PluginSwitches.Set(_home, "acme-desk", PluginSwitches.Tools, "workspace:home", on: true);
        Assert.True(PluginSwitches.IsOn(_home, "acme-desk", PluginSwitches.Tools, new PluginPlace(Repository: "other", Workspace: "home")));
        Assert.True(PluginSwitches.IsOn(_home, "acme-desk", PluginSwitches.Commands, PluginPlace.Machine));
    }

    [Fact]
    public void Storage_reads_the_machine_s_word_alone_and_is_switched_nowhere_else()
    {
        PluginSwitches.Set(_home, "acme-desk", PluginInterfaces.Storage, "machine", on: false);

        Assert.False(PluginSwitches.IsOn(_home, "acme-desk", PluginInterfaces.Storage, Engine));
        var refused = Assert.Throws<DriverException>(() => PluginSwitches.Set(_home, "acme-desk", PluginInterfaces.Storage, "repository:engine", on: true));
        Assert.Contains("machine", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Disabled_is_over_every_switch()
    {
        PluginSwitches.Set(_home, "acme-desk", PluginSwitches.Commands, "repository:engine", on: true);
        PluginState.Disable(_home, "acme-desk");

        Assert.False(PluginSwitches.IsOn(_home, "acme-desk", PluginSwitches.Commands, Engine));
    }

    [Fact]
    public void A_switch_written_keeps_disabled_the_other_rows_and_a_key_this_build_has_no_field_for()
    {
        System.IO.File.WriteAllText(File, """{ "disabled": ["acme-quiet"], "later": { "kept": 1 } }""");

        PluginSwitches.Set(_home, "acme-desk", PluginSwitches.Views, "workspace:home", on: false);
        PluginSwitches.Set(_home, "acme-desk", HookPoints.Event, "machine", on: false);

        var root = JsonNode.Parse(System.IO.File.ReadAllText(File))!.AsObject();
        Assert.Equal("acme-quiet", root["disabled"]![0]!.GetValue<string>());
        Assert.Equal(1, root["later"]!["kept"]!.GetValue<int>());
        Assert.False(root["switches"]!["acme-desk"]!["views"]!["workspace:home"]!.GetValue<bool>());
        Assert.False(root["switches"]!["acme-desk"]![HookPoints.Event]!["machine"]!.GetValue<bool>());
        Assert.Equal(
            [new PluginSwitchWord(PluginSwitches.Views, "workspace:home", false), new PluginSwitchWord(HookPoints.Event, "machine", false)],
            PluginSwitches.Read(_home).Words("acme-desk"));
    }

    [Fact]
    public void On_at_the_machine_is_the_default_and_leaves_no_word()
    {
        PluginSwitches.Set(_home, "acme-desk", PluginSwitches.Tools, "machine", on: false);
        PluginSwitches.Set(_home, "acme-desk", PluginSwitches.Tools, "machine", on: true);

        Assert.Empty(PluginSwitches.Read(_home).Words("acme-desk"));
        Assert.Null(JsonNode.Parse(System.IO.File.ReadAllText(File))!["switches"]?["acme-desk"]);
    }

    [Theory]
    [InlineData("chat", "machine")]
    [InlineData("tools", "room:engine")]
    [InlineData("tools", "repository:")]
    public void A_switch_that_names_nothing_a_plugin_has_or_no_place_is_refused(string what, string place)
    {
        Assert.Throws<DriverException>(() => PluginSwitches.Set(_home, "acme-desk", what, place, on: false));
        Assert.False(System.IO.File.Exists(File));
    }

    [Fact]
    public void An_unreadable_file_switches_nothing_off_and_refuses_an_edit()
    {
        System.IO.File.WriteAllText(File, "{ torn");

        Assert.True(PluginSwitches.IsOn(_home, "acme-desk", PluginSwitches.Tools, Engine));
        Assert.Throws<DriverException>(() => PluginSwitches.Set(_home, "acme-desk", PluginSwitches.Tools, "machine", on: false));
        Assert.Equal("{ torn", System.IO.File.ReadAllText(File));
    }

    [Fact]
    public void A_rename_carries_the_rows_and_a_removal_clears_them()
    {
        PluginSwitches.Set(_home, "acme-tickets", PluginSwitches.Tools, "repository:engine", on: false);

        Assert.True(PluginSwitches.Renamed(_home, "acme-tickets", "acme-desk"));
        Assert.False(PluginSwitches.Renamed(_home, "acme-tickets", "acme-desk"));
        Assert.False(PluginSwitches.IsOn(_home, "acme-desk", PluginSwitches.Tools, Engine));
        Assert.Empty(PluginSwitches.Read(_home).Words("acme-tickets"));

        PluginSwitches.Cleared(_home, "acme-desk");
        Assert.Empty(PluginSwitches.Read(_home).Words("acme-desk"));
    }
}
