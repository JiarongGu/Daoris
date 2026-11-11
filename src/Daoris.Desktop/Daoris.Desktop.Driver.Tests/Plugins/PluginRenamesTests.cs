using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin renamed by its own manifest, which declares its old ids: Daoris moves what the person had under an old id to
/// the new one once (the installed copy, the switches, the data folder, the private storage and the approvals), knowing no plugin.
/// </summary>
public sealed class PluginRenamesTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plugin-renames-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        foreach (var folder in new[] { _home, _home + "-offers" })
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private const string Storage = """, "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/state"] }, "interfaces": { "storage": { "for": "A token." } }""";

    private string Plugin(string id, string declared = "", string? folder = null)
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Plugins, folder ?? id);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, PluginCatalog.ManifestName), $"{{ \"id\": \"{id}\", \"version\": \"1.0.0\"{Storage}{declared} }}");
        return path;
    }

    private string Logged() =>
        Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Logs))
            ? string.Join("\n", Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*", SearchOption.AllDirectories).Select(File.ReadAllText))
            : "";

    /// <summary>[case, what the manifest declares after its id, the refusal's words or null, the former ids read].</summary>
    [Theory]
    [InlineData("none", "", null, "")]
    [InlineData("one", """, "formerly": ["acme-tickets"]""", null, "acme-tickets")]
    [InlineData("two", """, "formerly": ["acme-tickets", "acme.old-desk"]""", null, "acme-tickets, acme.old-desk")]
    [InlineData("not an array", """, "formerly": "acme-tickets" """, "`formerly` must be an array of the plugin ids this plugin was.", "")]
    [InlineData("not an id", """, "formerly": ["Acme Tickets"]""", "`formerly` names `Acme Tickets`, which is not a plugin id.", "")]
    [InlineData("its own id", """, "formerly": ["acme-desk"]""", "`formerly` names the plugin's own id `acme-desk`.", "")]
    [InlineData("twice", """, "formerly": ["acme-tickets", "acme-tickets"]""", "`formerly` names `acme-tickets` twice.", "")]
    public void Formerly_is_read_or_refuses_the_plugin(string @case, string declared, string? problem, string read)
    {
        Plugin("acme-desk", declared);
        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);
        Assert.True(problem == entry.Problem, $"{@case}: {entry.Problem}");
        Assert.Equal(read, string.Join(", ", entry.Manifest.Formerly));
    }

    [Fact]
    public void A_plugin_declaring_formerly_takes_over_the_old_id_s_copy_switches_data_storage_and_approvals_once()
    {
        Plugin("acme-tickets");
        Plugin("acme-desk", """, "formerly": ["acme-tickets"]""");
        var data = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Plugins), PluginCatalog.DataFolder);
        Directory.CreateDirectory(Path.Combine(data, "acme-tickets"));
        File.WriteAllText(Path.Combine(data, "acme-tickets", "kept.txt"), "kept");
        Directory.CreateDirectory(PluginPrivate.Folder(_home, "acme-tickets"));
        File.WriteAllText(Path.Combine(PluginPrivate.Folder(_home, "acme-tickets"), "token.json"), "{}");
        PluginState.Disable(_home, "acme-tickets");
        PluginSwitches.Set(_home, "acme-tickets", PluginInterfaces.Storage, PluginPlace.MachineKey, on: false);
        File.WriteAllText(HelpGrants.PathIn(_home), """{ "help": [{ "plugin": "acme-tickets", "server": "desk", "allow": ["read"], "writes": [] }] }""");

        var moved = PluginRenames.Move(_home);

        var rename = Assert.Single(moved);
        Assert.Equal(("acme-desk", "acme-tickets"), (rename.Plugin, rename.Old));
        Assert.Equal([PluginRenames.Copy, PluginRenames.Switches, PluginRenames.Data, PluginRenames.Storage, PluginRenames.Approvals], rename.Moved);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Plugins, "acme-tickets")));
        Assert.Equal("kept", File.ReadAllText(Path.Combine(data, "acme-desk", "kept.txt")));
        Assert.False(Directory.Exists(Path.Combine(data, "acme-tickets")));
        Assert.True(File.Exists(Path.Combine(PluginPrivate.Folder(_home, "acme-desk"), "token.json")));
        Assert.False(Directory.Exists(PluginPrivate.Folder(_home, "acme-tickets")));
        Assert.Equal(["acme-desk"], PluginState.Load(_home).Disabled);
        Assert.Equal([new PluginSwitchWord(PluginInterfaces.Storage, PluginPlace.MachineKey, false)], PluginSwitches.Read(_home).Words("acme-desk"));
        Assert.Empty(PluginSwitches.Read(_home).Words("acme-tickets"));
        Assert.Contains("\"plugin\": \"acme-desk\"", File.ReadAllText(HelpGrants.PathIn(_home)), StringComparison.Ordinal);
        Assert.DoesNotContain("acme-tickets", File.ReadAllText(HelpGrants.PathIn(_home)), StringComparison.Ordinal);
        Assert.Equal("acme-desk", Assert.Single(PluginCatalog.Load(_home).Plugins).Manifest.Id);

        var logged = Logged();
        Assert.Single(logged.Split('\n'), line => line.Contains("plugin.renamed", StringComparison.Ordinal));
        Assert.Contains("acme-tickets", logged, StringComparison.Ordinal);
    }

    [Fact]
    public void A_second_start_moves_nothing_again()
    {
        Plugin("acme-tickets");
        Plugin("acme-desk", """, "formerly": ["acme-tickets"]""");
        Directory.CreateDirectory(PluginPrivate.Folder(_home, "acme-tickets"));

        Assert.Single(PluginRenames.Move(_home));
        Assert.Empty(PluginRenames.Move(_home));
        Assert.Empty(PluginRenames.Move(_home));
        Assert.Single(Logged().Split('\n'), line => line.Contains("plugin.renamed", StringComparison.Ordinal));
    }

    [Fact]
    public void What_the_new_id_already_keeps_is_never_overwritten_and_the_old_is_left_said()
    {
        Plugin("acme-desk", """, "formerly": ["acme-tickets"]""");
        Directory.CreateDirectory(PluginPrivate.Folder(_home, "acme-tickets"));
        File.WriteAllText(Path.Combine(PluginPrivate.Folder(_home, "acme-tickets"), "token.json"), "old");
        Directory.CreateDirectory(PluginPrivate.Folder(_home, "acme-desk"));
        File.WriteAllText(Path.Combine(PluginPrivate.Folder(_home, "acme-desk"), "token.json"), "new");
        Directory.CreateDirectory(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Plugins), PluginCatalog.DataFolder, "acme-tickets"));

        var rename = Assert.Single(PluginRenames.Move(_home));

        Assert.Equal("new", File.ReadAllText(Path.Combine(PluginPrivate.Folder(_home, "acme-desk"), "token.json")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(PluginPrivate.Folder(_home, "acme-tickets"), "token.json")));
        Assert.Equal([PluginRenames.Data], rename.Moved);
        Assert.Equal([PluginRenames.Storage], rename.Left);
        Assert.Empty(PluginRenames.Move(_home));
    }

    [Fact]
    public void A_copy_installed_under_the_old_id_updates_from_the_offer_that_was_formerly_it()
    {
        var offers = _home + "-offers";
        var offer = Path.Combine(offers, "acme-desk");
        Directory.CreateDirectory(offer);
        File.WriteAllText(Path.Combine(offer, PluginCatalog.ManifestName),
            $"{{ \"id\": \"acme-desk\", \"version\": \"2.0.0\", \"formerly\": [\"acme-tickets\"]{Storage} }}");
        var installed = Plugin("acme-tickets");
        PluginSource.Write(installed, PluginSource.FromOffer("acme-tickets"));
        Directory.CreateDirectory(PluginPrivate.Folder(_home, "acme-tickets"));

        Assert.Equal("acme-desk", PluginOffers.For(PluginOffers.Load(offers, _home, []), "acme-tickets")?.Id);
        Assert.True(Assert.Single(PluginOffers.Load(offers, _home, [])).Installed);
        var (plan, refusal) = PluginInstall.PlanUpdate(_home, "acme-tickets", [], offers);
        Assert.Null(refusal);
        Assert.Equal(("acme-tickets", "acme-desk"), (plan!.Id, plan.Next));
        Assert.Contains(plan.Changes, change => change.What == "id" && change.Was == "acme-tickets" && change.Now == "acme-desk");

        PluginInstall.Update(_home, "acme-tickets", [], offers);

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);
        Assert.Equal(("acme-desk", "2.0.0"), (entry.Manifest.Id, entry.Manifest.Version));
        Assert.Equal("acme-desk", PluginSource.Read(entry.Folder).Source?.Offer);
        Assert.True(Directory.Exists(PluginPrivate.Folder(_home, "acme-desk")));
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Plugins, "acme-tickets")));
        Assert.Empty(PluginRenames.Move(_home));
    }
}
