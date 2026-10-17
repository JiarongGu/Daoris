using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An installed copy of one of Daoris's own example plugins that is older than the one this build ships (LANDPIN3, D119): the
/// catalogue says so with both versions and what the newer one brings, offers the update as the person's press, and says a copy
/// whose files the person changed as changed, which is never overwritten by that press.
/// </summary>
public sealed class PluginShippedTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-plugin-shipped-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    private string Offers => Path.Combine(_root, "app", "plugin-offers");

    private string Installed(string id) => DaorisHome.Entry(Home, HomeEntry.Plugins, id);

    private static IReadOnlyCollection<string> Reserved => AdapterSet.Built().Names;

    public PluginShippedTests() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string Manifest(string version, string? changes = null) =>
        "{ \"id\": \"acme.land\", \"name\": \"Acme land\", \"version\": \"" + version + "\","
        + (changes is null ? "" : $" \"changes\": \"{changes}\",")
        + " \"hooks\": { \"command\": [\"node\", \"${plugin}/land.mjs\"], \"points\": [\"work/land\"] } }";

    private static string Folder(string at, string manifest, string script)
    {
        Directory.CreateDirectory(at);
        File.WriteAllText(Path.Combine(at, PluginCatalog.ManifestName), manifest);
        File.WriteAllText(Path.Combine(at, "land.mjs"), script);
        return at;
    }

    private PluginOffer Offer(string version = "1.3.0", string script = "// shipped", string? changes = "Pushes the commit judged.")
    {
        Folder(Path.Combine(Offers, "acme.land"), Manifest(version, changes), script);
        return PluginOffers.Load(Offers, Home, Reserved).Single();
    }

    private (string Folder, PluginManifest Manifest) Copy(string version, string script)
    {
        // The shipped version's manifest as the offer writes it, so a copy of it is the same bytes.
        var folder = Folder(Installed("acme.land"), Manifest(version, "Pushes the commit judged."), script);
        return (folder, PluginCatalog.Load(Home).Plugins.Single().Manifest);
    }

    [Fact]
    public void An_older_copy_is_said_with_both_versions_and_what_the_shipped_one_brings()
    {
        var offer = Offer();
        var (folder, manifest) = Copy("1.2.0", "// old");

        var shipped = PluginOffers.ShippedOf(folder, manifest, offer);

        Assert.Equal(new PluginShipped(PluginShipped.Older, "1.2.0", "1.3.0", "Pushes the commit judged."), shipped);
    }

    [Fact]
    public void A_current_copy_and_a_newer_one_say_nothing_and_neither_does_a_plugin_with_no_offer()
    {
        var offer = Offer();
        var (folder, manifest) = Copy("1.3.0", "// shipped");
        Assert.Null(PluginOffers.ShippedOf(folder, manifest, offer));

        Folder(folder, Manifest("1.4.0"), "// mine, newer");
        Assert.Null(PluginOffers.ShippedOf(folder, PluginCatalog.Load(Home).Plugins.Single().Manifest, offer));

        Assert.Null(PluginOffers.ShippedOf(folder, manifest, null));
    }

    [Fact]
    public void A_copy_of_the_shipped_version_with_files_the_person_changed_is_said_as_changed()
    {
        var offer = Offer();
        var (folder, manifest) = Copy("1.3.0", "// shipped, and then edited by hand");

        var shipped = PluginOffers.ShippedOf(folder, manifest, offer);

        Assert.Equal(PluginShipped.Changed, shipped?.State);
        Assert.Equal("1.3.0", shipped?.Installed);
        Assert.Equal("1.3.0", shipped?.Shipped);
    }

    [Fact]
    public void The_installs_own_record_of_where_it_came_from_is_not_a_change()
    {
        var offer = Offer();
        var (folder, manifest) = Copy("1.3.0", "// shipped");
        PluginSource.Write(folder, PluginSource.FromOffer("acme.land"));

        Assert.Null(PluginOffers.ShippedOf(folder, manifest, offer));
    }

    [Fact]
    public void An_offer_that_is_not_sound_says_nothing_about_the_copy()
    {
        Folder(Path.Combine(Offers, "acme.land"), "{ not json", "// x");
        var broken = PluginOffers.Load(Offers, Home, Reserved).Single();
        Assert.NotNull(broken.Problem);
        var (folder, manifest) = Copy("1.2.0", "// old");

        Assert.Null(PluginOffers.ShippedOf(folder, manifest, broken));
    }

    [Theory]
    [InlineData("1.2.0", "1.3.0", -1)]
    [InlineData("1.3.0", "1.3.0", 0)]
    [InlineData("1.10.0", "1.9.0", 1)]
    [InlineData("1.3", "1.3.0", 0)]
    [InlineData("", "1.0.0", -1)]
    public void Versions_compare_number_by_number(string left, string right, int expected) =>
        Assert.Equal(expected, PluginShipped.Compare(left, right));

    [Fact]
    public void The_changes_line_is_the_examples_own_trimmed_text()
    {
        Assert.Equal("Pushes the commit judged.", PluginShipped.ChangesIn(Manifest("1.3.0", "  Pushes the commit judged.  ")));
        Assert.Null(PluginShipped.ChangesIn(Manifest("1.3.0")));
        Assert.Null(PluginShipped.ChangesIn("{ not json"));
    }

    [Fact]
    public void A_shipped_update_replaces_an_older_copy_with_no_record_keeping_what_it_kept_and_records_the_offer()
    {
        Offer();
        Copy("1.2.0", "// old");
        var data = Path.Combine(DaorisHome.Entry(Home, HomeEntry.Plugins), PluginCatalog.DataFolder, "acme.land");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "settings.json"), "mine");

        // Without the shipped door the copy has nothing to update from, as before.
        Assert.NotNull(PluginInstall.PlanUpdate(Home, "acme.land", Reserved, Offers).Refusal);

        var (plan, refusal) = PluginInstall.PlanUpdate(Home, "acme.land", Reserved, Offers, shipped: true);
        Assert.Null(refusal);
        Assert.Equal("acme.land", plan!.Source.Offer);
        Assert.Equal(("version", "1.2.0", "1.3.0"), (plan.Changes[0].What, plan.Changes[0].Was, plan.Changes[0].Now));

        PluginInstall.Update(Home, "acme.land", Reserved, Offers, shipped: true);

        Assert.Equal("// shipped", File.ReadAllText(Path.Combine(Installed("acme.land"), "land.mjs")));
        Assert.Equal("acme.land", PluginSource.Read(Installed("acme.land")).Source?.Offer);
        Assert.Equal("mine", File.ReadAllText(Path.Combine(data, "settings.json")));
        Assert.Equal("1.3.0", PluginCatalog.Load(Home).Plugins.Single().Manifest.Version);
    }

    [Fact]
    public void A_shipped_update_never_replaces_a_copy_the_person_changed()
    {
        Offer();
        var (folder, _) = Copy("1.3.0", "// shipped, and then edited by hand");

        var (plan, refusal) = PluginInstall.PlanUpdate(Home, "acme.land", Reserved, Offers, shipped: true);

        Assert.Null(plan);
        Assert.Contains("changed", refusal);
        Assert.Throws<DriverException>(() => PluginInstall.Update(Home, "acme.land", Reserved, Offers, shipped: true));
        Assert.Equal("// shipped, and then edited by hand", File.ReadAllText(Path.Combine(folder, "land.mjs")));
    }

    [Fact]
    public void A_shipped_update_for_a_plugin_this_install_does_not_offer_says_so()
    {
        Copy("1.2.0", "// old");

        var (plan, refusal) = PluginInstall.PlanUpdate(Home, "acme.land", Reserved, Offers, shipped: true);

        Assert.Null(plan);
        Assert.Contains("not offered", refusal);
    }
}
