using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SWITCHGAPS1a: Daoris's own plugins update with the build that ships them. At a start, an installed copy of an offer that is older than
/// the shipped one is updated as <c>daoris plugin update &lt;id&gt; --yes</c> does, its <c>.data/</c> kept; a plugin the person installed
/// from a folder, a copy whose files they changed and a copy at the shipped version are never touched. Seen failing first.
/// </summary>
public sealed class PluginUpdateOwnTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-plugin-own-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    private string Offers => Path.Combine(_root, "app", "plugin-offers");

    private static IReadOnlyCollection<string> Reserved => ProviderRegistry.Built().Names;

    public PluginUpdateOwnTests() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string Manifest(string version) =>
        "{ \"id\": \"acme.land\", \"name\": \"Acme land\", \"version\": \"" + version + "\","
        + " \"hooks\": { \"command\": [\"node\", \"${plugin}/land.mjs\"], \"points\": [\"work/land\"] } }";

    private static void Folder(string at, string version, string script)
    {
        Directory.CreateDirectory(at);
        File.WriteAllText(Path.Combine(at, PluginCatalog.ManifestName), Manifest(version));
        File.WriteAllText(Path.Combine(at, "land.mjs"), script);
    }

    private void Offer(string version, string script) => Folder(Path.Combine(Offers, "acme.land"), version, script);

    private string Installed => DaorisHome.Entry(Home, HomeEntry.Plugins, "acme.land");

    private string Script => File.ReadAllText(Path.Combine(Installed, "land.mjs"));

    [Fact]
    public void An_own_plugin_older_than_the_shipped_offer_is_updated_and_its_data_kept()
    {
        Offer("1.2.0", "// old");
        PluginInstall.AddOffer(Home, Offers, "acme.land", Reserved);
        var data = Path.Combine(DaorisHome.Entry(Home, HomeEntry.Plugins), PluginCatalog.DataFolder, "acme.land");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "kept.txt"), "mine");
        Offer("1.3.0", "// shipped");

        var taken = PluginOfferUpdates.Take(Home, Offers, Reserved);

        Assert.Equal(["acme.land"], taken);
        Assert.Equal("// shipped", Script);
        Assert.Equal("1.3.0", PluginCatalog.Load(Home).Plugins.Single().Manifest.Version);
        Assert.Equal("mine", File.ReadAllText(Path.Combine(data, "kept.txt")));
        Assert.Empty(PluginOfferUpdates.Take(Home, Offers, Reserved));
    }

    [Fact]
    public void A_plugin_the_person_installed_from_a_folder_is_never_touched_though_an_offer_carries_its_id()
    {
        Folder(Path.Combine(_root, "mine"), "1.2.0", "// from my folder");
        PluginInstall.Add(Home, Path.Combine(_root, "mine"), Reserved);
        Offer("1.3.0", "// shipped");

        Assert.Empty(PluginOfferUpdates.Take(Home, Offers, Reserved));
        Assert.Equal("// from my folder", Script);
    }

    [Fact]
    public void An_own_copy_the_person_edited_and_one_at_the_shipped_version_are_left_as_they_are()
    {
        Offer("1.2.0", "// old");
        PluginInstall.AddOffer(Home, Offers, "acme.land", Reserved);
        File.WriteAllText(Path.Combine(Installed, "land.mjs"), "// edited by hand");
        Offer("1.3.0", "// shipped");

        Assert.Empty(PluginOfferUpdates.Take(Home, Offers, Reserved));
        Assert.Equal("// edited by hand", Script);

        Directory.Delete(Installed, recursive: true);
        Offer("1.3.0", "// shipped");
        PluginInstall.AddOffer(Home, Offers, "acme.land", Reserved);
        Assert.Empty(PluginOfferUpdates.Take(Home, Offers, Reserved));
    }
}
