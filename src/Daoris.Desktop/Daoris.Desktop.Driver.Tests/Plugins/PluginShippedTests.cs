using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An installed copy of one of Daoris's own example plugins that is older than the one this build ships: the
/// catalogue says so with both versions and what the newer one brings, offers the update as the person's press, and says a copy
/// whose files the person changed as changed, which is never overwritten by that press. An older copy is judged by the digest
/// its install from the offer recorded; one from before the digest is said as unjudged.
/// </summary>
public sealed class PluginShippedTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-plugin-shipped-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    private string Offers => Path.Combine(_root, "app", "plugin-offers");

    private string Installed(string id) => DaorisHome.Entry(Home, HomeEntry.Plugins, id);

    private static IReadOnlyCollection<string> Reserved => ProviderRegistry.Built().Names;

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

        // A copy with no record of what its install copied is unjudged: whether it has been edited cannot be told.
        Assert.Equal(new PluginShipped(PluginShipped.Older, "1.2.0", "1.3.0", "Pushes the commit judged.", Unjudged: true), shipped);
    }

    private PluginManifest InstalledManifest() => PluginCatalog.Load(Home).Plugins.Single().Manifest;

    [Fact]
    public void An_older_copy_installed_from_the_offer_and_edited_since_is_said_as_changed_and_never_updated()
    {
        Offer("1.2.0", "// old");
        PluginInstall.AddOffer(Home, Offers, "acme.land", Reserved);
        var script = Path.Combine(Installed("acme.land"), "land.mjs");
        File.WriteAllText(script, "// old, and then edited by hand");
        var offer = Offer();

        var shipped = PluginOffers.ShippedOf(Installed("acme.land"), InstalledManifest(), offer);

        Assert.Equal(new PluginShipped(PluginShipped.Changed, "1.2.0", "1.3.0", "Pushes the commit judged."), shipped);
        // Neither the update its own record names nor the shipped press replaces it.
        var (plan, refusal) = PluginInstall.PlanUpdate(Home, "acme.land", Reserved, Offers);
        Assert.Null(plan);
        Assert.Contains("changed here since it was installed", refusal);
        Assert.Contains("changed here since it was installed", PluginInstall.PlanUpdate(Home, "acme.land", Reserved, Offers, shipped: true).Refusal);
        Assert.Throws<DriverException>(() => PluginInstall.Update(Home, "acme.land", Reserved, Offers));
        Assert.Equal("// old, and then edited by hand", File.ReadAllText(script));
    }

    [Fact]
    public void A_file_added_to_or_taken_from_an_older_copy_is_a_change_too()
    {
        Offer("1.2.0", "// old");
        PluginInstall.AddOffer(Home, Offers, "acme.land", Reserved);
        var offer = Offer();
        File.WriteAllText(Path.Combine(Installed("acme.land"), "mine.mjs"), "// mine");
        Assert.Equal(PluginShipped.Changed, PluginOffers.ShippedOf(Installed("acme.land"), InstalledManifest(), offer)?.State);

        File.Delete(Path.Combine(Installed("acme.land"), "mine.mjs"));
        File.Delete(Path.Combine(Installed("acme.land"), "land.mjs"));
        Assert.Equal(PluginShipped.Changed, PluginOffers.ShippedOf(Installed("acme.land"), InstalledManifest(), offer)?.State);
    }

    [Fact]
    public void An_older_copy_as_its_install_left_it_is_said_older_and_its_update_records_the_files_it_takes()
    {
        Offer("1.2.0", "// old");
        PluginInstall.AddOffer(Home, Offers, "acme.land", Reserved);
        var offer = Offer();

        Assert.Equal(new PluginShipped(PluginShipped.Older, "1.2.0", "1.3.0", "Pushes the commit judged."),
            PluginOffers.ShippedOf(Installed("acme.land"), InstalledManifest(), offer));

        PluginInstall.Update(Home, "acme.land", Reserved, Offers);
        Assert.Equal("// shipped", File.ReadAllText(Path.Combine(Installed("acme.land"), "land.mjs")));
        Assert.Null(PluginOffers.ShippedOf(Installed("acme.land"), InstalledManifest(), offer));

        // The update recorded what it copied: an edit after it, under a newer offer, is said as changed.
        File.WriteAllText(Path.Combine(Installed("acme.land"), "land.mjs"), "// shipped, and then edited by hand");
        var newer = Offer("1.4.0", "// newer");
        Assert.Equal(PluginShipped.Changed, PluginOffers.ShippedOf(Installed("acme.land"), InstalledManifest(), newer)?.State);
    }

    [Fact]
    public void An_older_copy_whose_record_is_from_before_the_digest_is_said_unjudged_and_its_update_stays_the_persons_press()
    {
        var offer = Offer();
        var (folder, manifest) = Copy("1.2.0", "// old");
        File.WriteAllText(Path.Combine(folder, PluginSource.FileName), "{ \"offer\": \"acme.land\" }");

        Assert.Equal(new PluginShipped(PluginShipped.Older, "1.2.0", "1.3.0", "Pushes the commit judged.", Unjudged: true),
            PluginOffers.ShippedOf(folder, manifest, offer));
        var (plan, refusal) = PluginInstall.PlanUpdate(Home, "acme.land", Reserved, Offers);
        Assert.Null(refusal);
        Assert.Equal("1.3.0", plan!.Changes[0].Now);
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
        Assert.Equal(PluginFolders.Digest(Installed("acme.land")), PluginSource.Read(Installed("acme.land")).Source?.Digest);
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
