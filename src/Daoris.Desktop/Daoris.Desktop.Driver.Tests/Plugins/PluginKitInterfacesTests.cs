using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every plugin the kit writes declares the interfaces Daoris reaches it through, and answers them: a sample command run in rounds
/// of its own process, keeping a count in its private storage through the kit's store, and a view stub. The trial reads them as
/// the driver does and says each entry it refuses.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class PluginKitInterfacesTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-kit-interfaces-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { GitSeed.Remove(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A new plugin, written straight into the home's plugins, so the driver finds it installed.</summary>
    private string Installed(string id)
    {
        var plugins = DaorisHome.Entry(_home, HomeEntry.Plugins);
        Directory.CreateDirectory(plugins);
        var plan = PluginKit.Plan(id, [HookPoints.SessionEnded], plugins);
        PluginKit.Write(plan);
        return plan.Folder;
    }

    [Fact]
    public void A_new_plugin_declares_a_sample_command_its_storage_and_a_view_stub_the_catalogue_reads_sound()
    {
        var folder = Installed("acme-hello");

        var (manifest, problem) = PluginCatalog.ReadFolder(folder);
        Assert.Null(problem);
        var interfaces = manifest.Interfaces;
        Assert.Empty(interfaces.Refused);
        Assert.Equal([PluginInterfaces.Commands, PluginInterfaces.Storage, PluginInterfaces.Views], interfaces.Names);
        var command = Assert.Single(interfaces.Commands);
        Assert.Equal((PluginKitInterfaces.Command, "Say hello", false, false), (command.Name, command.Title.AsWritten, command.Interactive, command.Tool));
        var view = Assert.Single(interfaces.Views);
        Assert.Equal((PluginKitInterfaces.View, "Hello", "page"), (view.Id, view.Title.AsWritten, string.Join(",", view.Where)));

        // The stub its view is read at, which answers a tree of the drawn vocabulary.
        var script = File.ReadAllText(Path.Combine(folder, PluginKit.Script));
        Assert.Contains("'view/read': async () => ({", script);
        Assert.Contains("ui: 1,", script);
    }

    [Fact]
    public async Task The_sample_command_runs_in_rounds_of_its_own_and_keeps_its_count_in_its_storage()
    {
        Installed("acme-hello");
        var (plugin, _) = PluginCommandRun.Find(_home, "acme-hello", PluginKitInterfaces.Command);

        var first = await PluginCommandRun.RoundAsync(_home, plugin, PluginKitInterfaces.Command, [], new Dictionary<string, string>(), CommandWire.Terminal, CancellationToken.None);
        var second = await PluginCommandRun.RoundAsync(_home, plugin, PluginKitInterfaces.Command, [], new Dictionary<string, string>(), CommandWire.Terminal, CancellationToken.None);

        Assert.Equal((0, "Hello from Hello: asked 1 time."), (first.Exit, Assert.Single(first.Said)));
        Assert.Equal("Hello from Hello: asked 2 times.", Assert.Single(second.Said));
        Assert.True(File.Exists(Path.Combine(PluginPrivate.Folder(_home, "acme-hello"), "state.json")));
    }

    [Fact]
    public async Task The_trial_says_the_interfaces_and_fails_an_entry_it_refuses()
    {
        var folder = Installed("acme-hello");

        var sound = await PluginTrials.TryFolderAsync(_home, folder);
        var said = Assert.Single(sound.Steps, step => step.Name == PluginTrialInterfaces.Name);
        Assert.True(said.Ok, said.Sentence);
        Assert.Equal($"runs `hello`; is handed its private storage at {PluginPrivate.Variable}; shows `overview` (page), each as the driver reads it.", said.Sentence);

        var manifest = Path.Combine(folder, PluginCatalog.ManifestName);
        File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("\"where\": [\"page\"]", "\"where\": [\"status-bar\"]", StringComparison.Ordinal));
        var refused = await PluginTrials.TryFolderAsync(_home, folder);
        var failed = Assert.Single(refused.Steps, step => step.Name == PluginTrialInterfaces.Name);
        Assert.False(failed.Ok);
        Assert.StartsWith("not reached: view `overview`'s `where` must list where it may stand", failed.Sentence, StringComparison.Ordinal);
        Assert.False(refused.Passed);
    }
}
