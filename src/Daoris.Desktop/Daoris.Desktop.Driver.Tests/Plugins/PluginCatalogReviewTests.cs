using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class PluginCatalogReviewTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plugins-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void A_placeholder_inside_a_longer_word_is_replaced_in_place_and_the_word_is_not_made_a_path()
    {
        var folder = Path.Combine(_home, "plugins", "acme.agent");
        var data = Path.Combine(_home, "plugins", ".data", "acme.agent");

        Assert.Equal($"--user-data-dir={data}/profile", PluginFolders.Expand("--user-data-dir=${data}/profile", folder, null));
        Assert.Equal(Path.GetFullPath(Path.Combine(folder, "plugin.mjs")), PluginFolders.Expand("${plugin}/plugin.mjs", folder, null));
    }

    [Theory]
    [InlineData("""{ "id": "bar", "apiVersion": 99 }""")]
    [InlineData("""{ "id": "bar", "apiVersion": "2" }""")]
    public void A_refused_version_is_a_row_under_its_folder_name_whatever_id_the_manifest_claims(string manifest)
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Plugins, "foo");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, PluginCatalog.ManifestName), manifest);

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);

        Assert.Equal("foo", entry.Manifest.Id);
        Assert.Equal("foo", Path.GetFileName(entry.Data));
    }
}
