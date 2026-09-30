using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>plugin</c> proposal (PLUG9): a plugin that has landed, added from its folder, or one installed
/// here switched — and since PLUG9 (c) and (d) one of the install's own by id, or an update from where one came from.
/// </summary>
public sealed class HelpPluginProposalsTests : HelpProposalsFixture
{
    /// <summary>Checkouts beside the home, never inside it: where a repository that holds plugins lands them.</summary>
    private string Checkouts => Beside("-checkouts");

    private string HousePlugins => Path.Combine(Checkouts, "house-plugins");

    private const string QuietHours = """
        { "id": "acme.quiet-hours", "name": "Quiet hours", "version": "1.0.0",
          "hooks": { "command": ["node", "${plugin}/hooks.mjs"], "points": ["quest/consider", "session/ended"] },
          "harnesses": [ { "name": "acme-agent", "command": ["acme-agent", "--acp"] } ],
          "servers": [ { "name": "browser", "command": ["npx", "-y", "@playwright/mcp@latest"] } ] }
        """;

    /// <summary>A folder in the plugins repository's checkout, as a session there would land one.</summary>
    private string Landed(string folder, string? manifest)
    {
        var path = Path.Combine(HousePlugins, folder);
        Directory.CreateDirectory(path);
        if (manifest is not null) System.IO.File.WriteAllText(Path.Combine(path, PluginCatalog.ManifestName), manifest);
        return path;
    }

    /// <summary>
    /// The machine a plugin proposal is judged against: the home, each repository's checkout, the harness
    /// names this build carries, and the catalogue — `example.lands` on and `example.off` off.
    /// </summary>
    private HelpMachineFacts PluginMachine()
    {
        WithPlugin("example.lands");
        var facts = WithPlugin("example.off", enabled: false);
        Landed("quiet-hours", QuietHours);
        Landed("no-manifest", null);
        Landed("bad-manifest", """{ "id": "acme.bad", "apiVersion": "one" }""");
        Landed("newer", """{ "id": "acme.newer", "apiVersion": 2 }""");
        Landed("shadow", """{ "id": "acme.shadow", "harnesses": [ { "name": "claude-code", "command": ["x"] } ] }""");
        Landed("lands", """{ "id": "example.lands", "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] } }""");
        Directory.CreateDirectory(Path.Combine(Checkouts, "outside"));
        return facts with
        {
            Repositories = [.. Facts.Repositories, "house-plugins"],
            Home = _home,
            Checkouts = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["house-plugins"] = HousePlugins, ["game"] = Path.Combine(Checkouts, "game"), ["engine"] = null,
            },
            Reserved = AdapterSet.Built().Names,
        };
    }

    private static HelpProposal PluginAdd(string? repository, string? folder) =>
        Of("plugin", "add") with { Repository = repository, Folder = folder };

    private static string Spelled(string folder) => folder.Contains(' ') ? $"\"{folder}\"" : folder;

    [Fact]
    public void A_landed_plugin_is_added_from_its_folder_and_the_plan_says_what_it_runs()
    {
        var facts = PluginMachine();
        var folder = Path.Combine(HousePlugins, "quiet-hours");

        var plan = HelpProposals.Plan(PluginAdd("house-plugins", "quiet-hours"), DriverConfig.Empty, facts);

        Assert.Null(plan.Refusal);
        Assert.Equal($"daoris plugin add {Spelled(folder)}", plan.Terminal);
        Assert.Equal(folder, plan.Source);
        Assert.Null(plan.Apply);
        Assert.Contains("Add plugin `acme.quiet-hours` (Quiet hours 1.0.0) from `quiet-hours` in `house-plugins`", plan.Describe);
        Assert.Contains("copied into Daoris's home under its id", plan.Describe);
        // What it runs, as its manifest writes it: `${plugin}` stays, and no machine path is in the sentence.
        Assert.Contains("It runs `node ${plugin}/hooks.mjs`, speaking on `quest/consider`, `session/ended`.", plan.Describe);
        Assert.Contains("It declares agent `acme-agent` (`acme-agent --acp`).", plan.Describe);
        Assert.Contains("It hands every session server `browser` (`npx -y @playwright/mcp@latest`).", plan.Describe);
        Assert.DoesNotContain(Checkouts, plan.Describe);
        var shown = plan.Plugin!;
        Assert.Equal(("acme.quiet-hours", "Quiet hours", "1.0.0", true), (shown.Id, shown.Name, shown.Version, shown.Copied));
        Assert.Equal(["node", "${plugin}/hooks.mjs"], shown.Command);
        Assert.Equal(["quest/consider", "session/ended"], shown.Points);
        var harness = Assert.Single(shown.Harnesses);
        Assert.Equal("acme-agent", harness.Name);
        Assert.Equal(["acme-agent", "--acp"], harness.Command);
        var server = Assert.Single(shown.Servers);
        Assert.Equal("browser", server.Name);
        Assert.Equal(["npx", "-y", "@playwright/mcp@latest"], server.Command);
    }

    /// <summary>A whole path is taken where the person gave one and named no repository.</summary>
    [Fact]
    public void A_plugin_is_added_from_a_whole_path_the_person_gave()
    {
        var facts = PluginMachine();
        var folder = Path.Combine(HousePlugins, "quiet-hours");

        var plan = HelpProposals.Plan(PluginAdd(null, folder), DriverConfig.Empty, facts);

        Assert.Null(plan.Refusal);
        Assert.Equal(folder, plan.Source);
        Assert.Contains($"from `{folder}`", plan.Describe);
    }

    /// <summary>
    /// What `daoris plugin add` would refuse, the catalogue's own reader first, and what Ask Daoris never
    /// proposes: an installed plugin replaced, a folder outside the checkout it names, one it cannot see.
    /// </summary>
    [Theory]
    [InlineData("house-plugins", "no-manifest", "no `plugin.json`")]
    [InlineData("house-plugins", "bad-manifest", "`apiVersion` must be an integer")]
    [InlineData("house-plugins", "newer", "needs plugin API 2")]
    [InlineData("house-plugins", "shadow", "which this build already carries")]
    [InlineData("house-plugins", "lands", "`example.lands` is already installed on this machine")]
    [InlineData("house-plugins", "nowhere", "there is no folder `nowhere` in `house-plugins`")]
    [InlineData("house-plugins", "../outside", "leaves `house-plugins`'s checkout")]
    [InlineData("house-plugins", "", "names the plugin's folder")]
    [InlineData("nobody", "quiet-hours", "`nobody` is not registered on this machine")]
    [InlineData("engine", "quiet-hours", "`engine` has no checkout on this machine")]
    [InlineData(null, "quiet-hours", "the repository whose checkout holds it")]
    public void A_plugin_the_catalogue_would_refuse_is_never_proposed(string? repository, string folder, string says)
    {
        var plan = HelpProposals.Plan(PluginAdd(repository, folder), DriverConfig.Empty, PluginMachine());

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Source);
        Assert.Null(plan.Plugin);
    }

    [Fact]
    public void A_folder_inside_Daoris_s_home_or_holding_it_is_refused()
    {
        var facts = PluginMachine();

        Assert.Contains("inside Daoris's home",
            HelpProposals.Plan(PluginAdd(null, Path.Combine(_home, PluginCatalog.Folder, "example.lands")), DriverConfig.Empty, facts).Refusal);
        Assert.Contains("holds Daoris's home",
            HelpProposals.Plan(PluginAdd(null, Path.GetDirectoryName(_home)), DriverConfig.Empty, facts).Refusal);
    }

    [Fact]
    public void An_installed_plugin_is_switched_on_or_off_and_the_plan_says_what_it_runs()
    {
        var facts = PluginMachine();

        var on = HelpProposals.Plan(Of("plugin", "enable", "example.off"), DriverConfig.Empty, facts);
        var off = HelpProposals.Plan(Of("plugin", "disable", "example.lands"), DriverConfig.Empty, facts);

        Assert.Null(on.Refusal);
        Assert.Equal("daoris plugin enable example.off", on.Terminal);
        Assert.Contains("Switch plugin `example.off` on", on.Describe);
        Assert.Contains("It runs `node ${plugin}/land.mjs`, speaking on `work/land`.", on.Describe);
        Assert.Equal(["node", "${plugin}/land.mjs"], on.Plugin!.Command);
        Assert.False(on.Plugin.Copied);
        Assert.Null(off.Refusal);
        Assert.Equal("daoris plugin disable example.lands", off.Terminal);
        Assert.Contains("Switch plugin `example.lands` off", off.Describe);
        Assert.Contains("stays installed", off.Describe);
    }

    [Theory]
    [InlineData("enable", "nowhere.lands", "no plugin `nowhere.lands` on this machine")]
    [InlineData("enable", "example.lands", "`example.lands` is already on")]
    [InlineData("disable", "example.off", "`example.off` is already off")]
    [InlineData("remove", "example.lands", "`add`, `enable`, `disable` or `update`")]
    public void A_switch_the_catalogue_would_refuse_is_never_proposed(string door, string id, string says)
    {
        var plan = HelpProposals.Plan(Of("plugin", door, id), DriverConfig.Empty, PluginMachine());

        Assert.Contains(says, plan.Refusal);
    }

    [Fact]
    public async Task A_plugin_is_added_or_switched_through_the_screens_own_doors()
    {
        var facts = PluginMachine();

        var (added, doors, _) = await ApplyAsync(PluginAdd("house-plugins", "quiet-hours"), facts: facts);
        var (switched, switching, _) = await ApplyAsync(Of("plugin", "disable", "example.lands") with { Id = "p7" }, facts: facts);

        Assert.True(added.Applied);
        Assert.Equal([$"PLUGIN_ADD {Path.Combine(HousePlugins, "quiet-hours")}"], doors.Calls);
        Assert.StartsWith("Applied: `#p6` — Add plugin `acme.quiet-hours`", added.Told);
        Assert.True(switched.Applied);
        Assert.Equal(["PLUGIN_ACTION disable example.lands"], switching.Calls);
        Assert.Equal("applied", HelpProposals.Find(_home, "p7")!.State);
    }

    [Fact]
    public async Task A_plugin_doors_refusal_settles_it_refused_in_its_words()
    {
        var doors = new HelpStandInDoors { PluginRefusal = "plugin `acme.quiet-hours` is already installed on this machine." };

        var (applied, _, _) = await ApplyAsync(PluginAdd("house-plugins", "quiet-hours"), doors, PluginMachine());

        Assert.False(applied.Applied);
        Assert.Contains("is already installed", applied.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p6")!.State);
    }

    // ── PLUG9 (c) and (d): one of the install's own plugins, by id; an update from where one came from ──

    /// <summary>The install's offers, beside the test's home as an install lays them out.</summary>
    private string OffersFolder => Beside("-app-plugin-offers");

    private HelpMachineFacts OfferMachine()
    {
        var facts = PluginMachine();
        void Offer(string id, string manifest, string? readme = null)
        {
            var folder = Path.Combine(OffersFolder, id);
            Directory.CreateDirectory(folder);
            System.IO.File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), manifest);
            if (readme is not null) System.IO.File.WriteAllText(Path.Combine(folder, "README.md"), readme);
        }

        Offer("github-pull-request",
            """{ "id": "github-pull-request", "name": "GitHub pull request", "version": "1.0.0", "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] } }""",
            "# x\n\n## What it needs\n\n- **gh**, signed in: `gh auth login`.\n");
        Offer("future", """{ "id": "future", "apiVersion": 99 }""");
        Offer("example.lands", """{ "id": "example.lands" }""");
        return facts with { OffersFolder = OffersFolder, Offers = PluginOffers.Load(OffersFolder, _home, AdapterSet.Built().Names) };
    }

    [Fact]
    public void One_of_the_installs_own_plugins_is_added_by_its_id_and_the_card_says_what_it_needs()
    {
        var plan = HelpProposals.Plan(Of("plugin", "add") with { Offer = "github-pull-request" }, DriverConfig.Empty, OfferMachine());

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris plugin add --offer github-pull-request", plan.Terminal);
        Assert.Contains("Install Daoris's own plugin `github-pull-request` (GitHub pull request 1.0.0), which this install offers", plan.Describe);
        Assert.Contains("It runs `node ${plugin}/land.mjs`, speaking on `work/land`.", plan.Describe);
        Assert.Contains("It needs: gh, signed in: `gh auth login`.", plan.Describe);
        // An offer is named by its id: the sentence the conversation is told names no path on this machine.
        Assert.DoesNotContain(OffersFolder, plan.Describe);
        Assert.Equal(["gh, signed in: `gh auth login`."], plan.Plugin!.Needs);
        Assert.True(plan.Plugin.Copied);
    }

    [Theory]
    [InlineData("acme.nothing", "this install offers no plugin `acme.nothing` — it offers `example.lands`, `future`, `github-pull-request`.")]
    [InlineData("future", "Daoris's own `future` cannot be installed as it stands: needs plugin API 99")]
    [InlineData("example.lands", "plugin `example.lands` is already installed on this machine")]
    public void An_offer_the_install_would_refuse_is_never_proposed(string offer, string says)
    {
        var plan = HelpProposals.Plan(Of("plugin", "add") with { Offer = offer }, DriverConfig.Empty, OfferMachine());

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Plugin);
    }

    [Fact]
    public void An_update_is_planned_from_where_the_plugin_came_from_saying_what_changes_and_no_path()
    {
        var facts = OfferMachine();
        PluginInstall.Add(_home, Path.Combine(HousePlugins, "quiet-hours"), AdapterSet.Built().Names);
        System.IO.File.WriteAllText(Path.Combine(HousePlugins, "quiet-hours", PluginCatalog.ManifestName), QuietHours.Replace("1.0.0", "1.1.0"));

        var plan = HelpProposals.Plan(Of("plugin", "update", "acme.quiet-hours"), DriverConfig.Empty, facts);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris plugin update acme.quiet-hours --yes", plan.Terminal);
        Assert.Contains("Update plugin `acme.quiet-hours` from the folder it was added from", plan.Describe);
        Assert.Contains("It changes its version from `1.0.0` to `1.1.0`.", plan.Describe);
        Assert.DoesNotContain(Checkouts, plan.Describe);
        Assert.Equal([new PluginChange("version", "1.0.0", "1.1.0")], plan.Plugin!.Changes);
        Assert.True(plan.Plugin.Replaced);
    }

    [Theory]
    [InlineData("example.lands", "`example.lands` has no record of where it came from")]
    [InlineData("nowhere.lands", "no plugin `nowhere.lands` on this machine")]
    public void An_update_the_driver_would_refuse_is_never_proposed(string id, string says)
    {
        var plan = HelpProposals.Plan(Of("plugin", "update", id), DriverConfig.Empty, OfferMachine());

        Assert.Contains(says, plan.Refusal);
        Assert.Equal($"daoris plugin update {id} --yes", plan.Terminal);
    }

    [Fact]
    public async Task An_offer_and_an_update_are_applied_through_the_screens_own_doors()
    {
        var facts = OfferMachine();
        PluginInstall.Add(_home, Path.Combine(HousePlugins, "quiet-hours"), AdapterSet.Built().Names);

        var (installed, installing, _) = await ApplyAsync(Of("plugin", "add") with { Id = "p9", Offer = "github-pull-request" }, facts: facts);
        var (updated, updating, _) = await ApplyAsync(Of("plugin", "update", "acme.quiet-hours") with { Id = "p10" }, facts: facts);

        Assert.True(installed.Applied);
        Assert.Equal(["PLUGIN_INSTALL github-pull-request"], installing.Calls);
        Assert.True(updated.Applied);
        Assert.Equal(["PLUGIN_UPDATE acme.quiet-hours"], updating.Calls);
        Assert.StartsWith("Applied: `#p10` — Update plugin `acme.quiet-hours`", updated.Told);
    }

    /// <summary>A plugin's fields, read from the file the service's box writes — the twin's shape.</summary>
    [Fact]
    public void A_plugins_offer_is_read_from_the_file()
    {
        var node = new JsonObject
        {
            ["id"] = "k4", ["proposed"] = "2026-09-30T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = "plugin", ["door"] = "add", ["target"] = null, ["workspace"] = null, ["value"] = null,
            ["sentence"] = null, ["repository"] = null, ["folder"] = null, ["offer"] = "github-pull-request",
            ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "k4.json"), node.ToJsonString());

        Assert.Equal("github-pull-request", HelpProposals.Find(_home, "k4")!.Offer);
    }

    /// <summary>A plugin's fields, read from the file the service's box writes — the twin's shape.</summary>
    [Fact]
    public void A_plugins_repository_and_folder_are_read_from_the_file()
    {
        var node = new JsonObject
        {
            ["id"] = "k3", ["proposed"] = "2026-09-30T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = "plugin", ["door"] = "add", ["target"] = null, ["workspace"] = null, ["value"] = null,
            ["sentence"] = null, ["repository"] = "house-plugins", ["folder"] = "plugins/quiet-hours",
            ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "k3.json"), node.ToJsonString());

        var plugin = HelpProposals.Find(_home, "k3")!;

        Assert.Equal(("plugin", "add", "house-plugins", "plugins/quiet-hours"), (plugin.Kind, plugin.Door, plugin.Repository, plugin.Folder));
        Assert.Null(plugin.Target);
    }
}

public sealed partial class HelpStandInDoors
{
    /// <summary>What the plugin door refuses in its own words, for a plugin that moved since the card was drawn.</summary>
    public string? PluginRefusal { get; init; }

    public void AddPlugin(string folder)
    {
        Calls.Add($"PLUGIN_ADD {folder}");
        if (PluginRefusal is { } refused) throw new DriverException(refused);
    }

    public void SwitchPlugin(string id, bool on) => Calls.Add($"PLUGIN_ACTION {(on ? "enable" : "disable")} {id}");

    public void AddOffer(string id)
    {
        Calls.Add($"PLUGIN_INSTALL {id}");
        if (PluginRefusal is { } refused) throw new DriverException(refused);
    }

    public Task UpdatePluginAsync(string id, CancellationToken ct)
    {
        Calls.Add($"PLUGIN_UPDATE {id}");
        return PluginRefusal is { } refused ? Task.FromException(new DriverException(refused)) : Task.CompletedTask;
    }
}
