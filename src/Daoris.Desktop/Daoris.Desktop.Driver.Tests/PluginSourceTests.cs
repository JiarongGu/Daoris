using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Where an installed plugin came from, and its update (PLUG9 c, D103): the driver's half of a twin with
/// the CLI's <c>plugins.ts</c>, whose <c>plugin-sources.test.ts</c> holds the same table — the same record,
/// the same refusals in the same order and words, the same changes. They share no code.
/// </summary>
/// <remarks>
/// <para>The record is <c>.daoris-source.json</c> in the install folder, written into the staged copy
/// before the swap, so it is replaced with the install, removed with it, and absent from a folder copied
/// in by hand — which is said, never guessed.</para>
///
/// <para>🔴 Nothing a plugin declares runs here: an add copies, an update swaps, the loop starts it later.</para>
/// </remarks>
public sealed class PluginSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-plugin-source-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>The install's `data/`: its sibling `app/plugin-offers/` is where the install's offers are.</summary>
    private string Home => Path.Combine(_root, "data");

    private string Offers => Path.Combine(_root, "app", "plugin-offers");

    private string Installed(string id) => Path.Combine(Home, PluginCatalog.Folder, id);

    private static IReadOnlyCollection<string> Reserved => AdapterSet.Built().Names;

    public PluginSourceTests() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private const string GateV1 = """
        { "id": "acme.gate", "name": "Acme gate", "version": "1.0.0",
          "hooks": { "command": ["node", "${plugin}/gate.mjs"], "points": ["quest/consider"] } }
        """;

    private static string Folder(string at, string manifest, params (string Name, string Text)[] files)
    {
        Directory.CreateDirectory(at);
        File.WriteAllText(Path.Combine(at, PluginCatalog.ManifestName), manifest);
        foreach (var (name, text) in files) File.WriteAllText(Path.Combine(at, name), text);
        return at;
    }

    private string Checkout() => Folder(Path.Combine(_root, "checkout", "gate"), GateV1, ("gate.mjs", "// v1"));

    // ── The record, on every add door ──────────────────────────────────────────────────────────────

    [Fact]
    public void An_add_records_the_folder_it_came_from_in_the_install_and_the_catalogue_reads_past_it()
    {
        var source = Checkout();

        PluginInstall.Add(Home, source, Reserved);

        Assert.Equal((PluginSource.FromFolder(source), (string?)null), PluginSource.Read(Installed("acme.gate")));
        Assert.True(File.Exists(Path.Combine(Installed("acme.gate"), PluginSource.FileName)));
        Assert.Null(Assert.Single(PluginCatalog.Load(Home, Reserved).Plugins).Problem);
        Assert.Equal(["acme.gate"], Directory.GetDirectories(Path.Combine(Home, PluginCatalog.Folder)).Select(Path.GetFileName));
    }

    [Fact]
    public void A_plugin_copied_in_by_hand_has_no_record_and_none_is_guessed()
    {
        Folder(Installed("acme.gate"), GateV1);

        Assert.Equal(((PluginSource?)null, (string?)null), PluginSource.Read(Installed("acme.gate")));
    }

    /// <summary>The record's shape: the CLI's <c>the record reads a folder or an offer…</c>, row for row.</summary>
    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData("""{ "offer": "github-pull-request" }""", null, "github-pull-request", null)]
    [InlineData("{ not json", null, null, "does not read")]
    [InlineData("[]", null, null, "it is not a JSON object")]
    [InlineData("{}", null, null, "it names neither a folder nor an offer")]
    [InlineData("""{ "folder": "relative/path" }""", null, null, "its folder is not a whole path")]
    [InlineData("""{ "offer": "Not An Id" }""", null, null, "its offer is not a plugin id")]
    [InlineData("""{ "folder": "WHOLE", "offer": "x" }""", null, null, "it names both a folder and an offer")]
    public void The_record_reads_as_the_cli_reads_it(string? text, string? folder, string? offer, string? problem)
    {
        var install = Path.Combine(_root, "install");
        Directory.CreateDirectory(install);
        var whole = Path.Combine(_root, "somewhere");
        if (text is not null) File.WriteAllText(Path.Combine(install, PluginSource.FileName), text.Replace("WHOLE", JsonEncodedText.Encode(whole).ToString()));

        var (source, said) = PluginSource.Read(install);

        Assert.Equal(folder, source?.Folder);
        Assert.Equal(offer, source?.Offer);
        if (problem is null) Assert.Null(said);
        else
        {
            Assert.Null(source);
            Assert.Contains("does not read", said);
            Assert.Contains(problem, said);
        }
    }

    [Fact]
    public void A_whole_folder_reads_back_as_written()
    {
        var install = Path.Combine(_root, "install");
        Directory.CreateDirectory(install);
        var whole = Path.Combine(_root, "somewhere");
        File.WriteAllText(Path.Combine(install, PluginSource.FileName), JsonSerializer.Serialize(new { folder = whole }));

        Assert.Equal(PluginSource.FromFolder(whole), PluginSource.Read(install).Source);
    }

    // ── Update: what changes, then the swap ───────────────────────────────────────────────────────

    [Fact]
    public void An_update_is_planned_as_what_changes_and_replaces_nothing()
    {
        var source = Checkout();
        PluginInstall.Add(Home, source, Reserved);
        Folder(source, """
            { "id": "acme.gate", "name": "Acme gate", "version": "1.1.0",
              "hooks": { "command": ["node", "${plugin}/gate2.mjs"], "points": ["quest/consider", "session/ended"] },
              "harnesses": [ { "name": "acme-agent", "command": ["acme-agent", "--acp"] } ],
              "servers": [ { "name": "browser", "command": ["npx", "-y", "@playwright/mcp@0.0.82"] } ] }
            """, ("gate.mjs", "// v2"));

        var (plan, refusal) = PluginInstall.PlanUpdate(Home, "acme.gate", Reserved, Offers);

        Assert.Null(refusal);
        Assert.Equal(
            [
                new PluginChange("version", "1.0.0", "1.1.0"),
                new PluginChange("command", "node ${plugin}/gate.mjs", "node ${plugin}/gate2.mjs"),
                new PluginChange("points", "quest/consider", "quest/consider, session/ended"),
                new PluginChange("harnesses", "", "acme-agent (acme-agent --acp)"),
                new PluginChange("servers", "", "browser (npx -y @playwright/mcp@0.0.82)"),
            ],
            plan!.Changes);
        Assert.Equal(source, plan.From);
        Assert.Equal("// v1", File.ReadAllText(Path.Combine(Installed("acme.gate"), "gate.mjs")));
    }

    [Fact]
    public void An_update_swaps_the_install_folder_whole_keeping_data_and_the_record()
    {
        var source = Checkout();
        PluginInstall.Add(Home, source, Reserved);
        File.WriteAllText(Path.Combine(Installed("acme.gate"), "stale.txt"), "from the install before");
        var kept = Path.Combine(Home, PluginCatalog.Folder, PluginCatalog.DataFolder, "acme.gate");
        Directory.CreateDirectory(kept);
        File.WriteAllText(Path.Combine(kept, "kept.json"), "{}");
        File.WriteAllText(Path.Combine(source, "gate.mjs"), "// v2");

        var plan = PluginInstall.Update(Home, "acme.gate", Reserved, Offers);

        Assert.Empty(plan.Changes);
        Assert.Equal("// v2", File.ReadAllText(Path.Combine(Installed("acme.gate"), "gate.mjs")));
        Assert.False(File.Exists(Path.Combine(Installed("acme.gate"), "stale.txt")));
        Assert.True(File.Exists(Path.Combine(kept, "kept.json")));
        Assert.Equal(PluginSource.FromFolder(source), PluginSource.Read(Installed("acme.gate")).Source);
        Assert.Equal([".data", "acme.gate"], Directory.GetDirectories(Path.Combine(Home, PluginCatalog.Folder)).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Update's refusals, the CLI's <c>update refuses a source that is gone…</c> row for row, in the same
    /// words; each leaves the installed version exactly as it was.
    /// </summary>
    [Theory]
    [InlineData("not installed", "acme.nobody", "no plugin `acme.nobody` on this machine")]
    [InlineData("no source recorded", "acme.bare", "`acme.bare` has no record of where it came from")]
    [InlineData("record unreadable", "acme.gate", "record of where it came from does not read")]
    [InlineData("folder gone", "acme.gate", "is not there any more")]
    [InlineData("no manifest", "acme.gate", "no `plugin.json` in")]
    [InlineData("unsound manifest", "acme.gate", "needs plugin API 99")]
    [InlineData("another plugin", "acme.gate", "now holds plugin `acme.other`, not `acme.gate`")]
    [InlineData("refused by this build", "acme.gate", "`dsh`, which this build already carries")]
    [InlineData("offer no longer offered", "acme.gate", "`acme.gate` is not offered by this install any more")]
    [InlineData("source inside the home", "acme.gate", "inside Daoris's home")]
    [InlineData("not an id", "../acme.gate", "is not a plugin id")]
    public void An_update_refuses_what_the_cli_refuses(string name, string id, string says)
    {
        var source = Checkout();
        PluginInstall.Add(Home, source, Reserved);
        var record = Path.Combine(Installed("acme.gate"), PluginSource.FileName);
        switch (name)
        {
            case "no source recorded": Folder(Installed("acme.bare"), GateV1.Replace("acme.gate", "acme.bare")); break;
            case "record unreadable": File.WriteAllText(record, "{ not json"); break;
            case "folder gone": Directory.Move(source, source + "-moved"); break;
            case "no manifest": File.Delete(Path.Combine(source, PluginCatalog.ManifestName)); break;
            case "unsound manifest": File.WriteAllText(Path.Combine(source, PluginCatalog.ManifestName), """{ "id": "acme.gate", "apiVersion": 99 }"""); break;
            case "another plugin": File.WriteAllText(Path.Combine(source, PluginCatalog.ManifestName), """{ "id": "acme.other" }"""); break;
            case "refused by this build": File.WriteAllText(Path.Combine(source, PluginCatalog.ManifestName), """{ "id": "acme.gate", "harnesses": [ { "name": "dsh", "command": ["x"] } ] }"""); break;
            case "offer no longer offered": File.WriteAllText(record, """{ "offer": "acme.gate" }"""); break;
            case "source inside the home":
                var inside = Folder(Path.Combine(Home, "elsewhere", "gate"), GateV1);
                File.WriteAllText(record, JsonSerializer.Serialize(new { folder = inside }));
                break;
        }

        var (plan, refusal) = PluginInstall.PlanUpdate(Home, id, Reserved, Offers);
        var thrown = Assert.Throws<DriverException>(() => PluginInstall.Update(Home, id, Reserved, Offers));

        Assert.Null(plan);
        Assert.Contains(says, refusal);
        Assert.Contains(says, thrown.Message);
        Assert.Contains("Nothing was replaced", thrown.Message);
        Assert.Equal("// v1", File.ReadAllText(Path.Combine(Installed("acme.gate"), "gate.mjs")));
    }

    /// <summary>On Windows a running hook process holds its folder: the update refuses whole, and the install stands.</summary>
    [Fact]
    public void A_plugin_whose_folder_is_held_is_not_updated_and_stays_whole()
    {
        if (!OperatingSystem.IsWindows()) return;
        var source = Checkout();
        PluginInstall.Add(Home, source, Reserved);
        File.WriteAllText(Path.Combine(source, "gate.mjs"), "// v2");

        string message;
        using (File.Open(Path.Combine(Installed("acme.gate"), "gate.mjs"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            message = Assert.Throws<DriverException>(() => PluginInstall.Update(Home, "acme.gate", Reserved, Offers)).Message;
        }

        Assert.Contains("was not updated", message);
        Assert.Equal("// v1", File.ReadAllText(Path.Combine(Installed("acme.gate"), "gate.mjs")));
        Assert.Equal(["acme.gate"], Directory.GetDirectories(Path.Combine(Home, PluginCatalog.Folder)).Select(Path.GetFileName));
    }

    // ── An offer, installed and then updated ───────────────────────────────────────────────────────

    private const string Offered = """
        { "id": "github-pull-request", "name": "GitHub pull request", "version": "1.0.0",
          "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] } }
        """;

    [Fact]
    public void An_offer_is_installed_with_the_offer_recorded_and_updated_from_the_offer_as_the_install_has_it_now()
    {
        Folder(Path.Combine(Offers, "github-pull-request"), Offered, ("land.mjs", "// v1"));

        var added = PluginInstall.AddOffer(Home, Offers, "github-pull-request", Reserved);

        Assert.Equal("github-pull-request", added.Id);
        Assert.Equal("// v1", File.ReadAllText(Path.Combine(Installed("github-pull-request"), "land.mjs")));
        Assert.Equal(PluginSource.FromOffer("github-pull-request"), PluginSource.Read(Installed("github-pull-request")).Source);
        Assert.True(Assert.Single(PluginOffers.Load(Offers, Home, Reserved)).Installed);

        // A republish brings a newer offer.
        Folder(Path.Combine(Offers, "github-pull-request"), Offered.Replace("1.0.0", "1.1.0"), ("land.mjs", "// v2"));
        var (plan, _) = PluginInstall.PlanUpdate(Home, "github-pull-request", Reserved, Offers);
        Assert.Equal([new PluginChange("version", "1.0.0", "1.1.0")], plan!.Changes);
        PluginInstall.Update(Home, "github-pull-request", Reserved, Offers);
        Assert.Equal("// v2", File.ReadAllText(Path.Combine(Installed("github-pull-request"), "land.mjs")));
        Assert.Equal(PluginSource.FromOffer("github-pull-request"), PluginSource.Read(Installed("github-pull-request")).Source);
    }

    [Fact]
    public void An_offer_the_install_does_not_carry_is_refused_naming_what_it_offers()
    {
        Folder(Path.Combine(Offers, "github-pull-request"), Offered);

        var refused = Assert.Throws<DriverException>(() => PluginInstall.AddOffer(Home, Offers, "acme.nothing", Reserved));

        Assert.Contains("this install offers no plugin `acme.nothing`", refused.Message);
        Assert.Contains("github-pull-request", refused.Message);
        Assert.Contains("is not a plugin id", Assert.Throws<DriverException>(() => PluginInstall.AddOffer(Home, Offers, "../x", Reserved)).Message);
        Assert.False(Directory.Exists(Path.Combine(Home, PluginCatalog.Folder)));
    }

    /// <summary>The driver adds and never replaces (PLUG9's one difference): an installed offer is refused here.</summary>
    [Fact]
    public void An_installed_offer_is_never_replaced_here()
    {
        Folder(Path.Combine(Offers, "github-pull-request"), Offered);
        PluginInstall.AddOffer(Home, Offers, "github-pull-request", Reserved);

        Assert.Contains("already installed", Assert.Throws<DriverException>(() => PluginInstall.AddOffer(Home, Offers, "github-pull-request", Reserved)).Message);
    }

    /// <summary>🔴 Installing an offer starts nothing it declares: its hook would leave a mark, and there is none.</summary>
    [Fact]
    public void Installing_an_offer_starts_nothing_it_declares()
    {
        var mark = Path.Combine(_root, "ran.txt");
        Folder(Path.Combine(Offers, "acme.marks"),
            """{ "id": "acme.marks", "hooks": { "command": ["node", "${plugin}/hook.mjs"], "points": ["session/ended"] } }""",
            ("hook.mjs", $"require('fs').writeFileSync({JsonSerializer.Serialize(mark)}, 'ran');"));

        PluginInstall.AddOffer(Home, Offers, "acme.marks", Reserved);

        Assert.False(File.Exists(mark));
    }
}
