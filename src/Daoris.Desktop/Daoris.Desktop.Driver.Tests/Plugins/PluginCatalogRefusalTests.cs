using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What the catalogue refuses or cannot read, and what each refusal leaves standing for the other plugins.</summary>
public sealed class PluginCatalogRefusalTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plugin-refusal-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private void Plugin(string folder, string manifest)
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Plugins, folder);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, PluginCatalog.ManifestName), manifest);
    }

    [Fact]
    public void A_refused_folder_keeps_its_own_name_as_its_id_so_it_never_stands_for_the_plugin_whose_id_it_claims()
    {
        Plugin("one", """{ "id": "two" }""");
        Plugin("two", """{ "id": "two" }""");

        var catalog = PluginCatalog.Load(_home);

        Assert.Equal(["one", "two"], catalog.Plugins.Select(plugin => plugin.Manifest.Id));
        Assert.Null(catalog.Plugins.Single(plugin => plugin.Manifest.Id == "two").Problem);
    }

    [Fact]
    public void A_server_declared_twice_in_one_manifest_refuses_the_plugin()
    {
        Plugin("twice", """{ "id": "twice", "servers": [ { "name": "x", "command": ["a"] }, { "name": "x", "command": ["b"] } ] }""");

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);

        Assert.Equal("server `x` is declared twice.", entry.Problem);
        Assert.Empty(entry.Manifest.Servers);
    }

    [Fact]
    public void A_state_file_that_cannot_be_opened_is_a_problem_and_disables_nothing_never_a_throw()
    {
        Directory.CreateDirectory(_home);
        var file = DaorisHome.Entry(_home, HomeEntry.PluginState);
        File.WriteAllText(file, """{ "disabled": ["acme.noisy"] }""");
        using var held = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);

        var read = PluginState.Load(_home);

        Assert.Empty(read.Disabled);
        Assert.Contains("could not be read", read.Problem);
        Assert.Throws<DriverException>(() => PluginState.Disable(_home, "acme.agent"));
    }
}
