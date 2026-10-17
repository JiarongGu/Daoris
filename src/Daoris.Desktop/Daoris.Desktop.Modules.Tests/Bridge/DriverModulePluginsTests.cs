using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// This machine's plugins over the bridge (`PluginsRoutes.cs`, MOD5): the catalogue and offers,
/// the switch and removal, an update, an install, and the kit's New and Try.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class DriverModulePluginsTests : DriverModuleBridge
{
    /// <summary>
    /// The plugins (D64) as the page reads them: the same catalogue the driver reads each tick, each
    /// with what it declares and speaks on, and the two doors' switch over the same row.
    /// </summary>
    [Fact]
    public async Task The_plugin_catalogue_is_answered_and_the_switch_edits_the_same_row_a_terminal_does()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins,"acme.gate");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), """
            { "id": "acme.gate", "name": "Acme gate", "version": "1.2.0", "description": "Holds quests overnight.",
              "harnesses": [ { "name": "acme-agent", "command": ["${plugin}/agent.mjs"] } ],
              "hooks": { "command": ["node", "${plugin}/hooks.mjs"], "points": ["quest/consider"] } }
            """);
        var broken = DaorisHome.Entry(Home, HomeEntry.Plugins,"future");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "plugin.json"), """{ "id": "future", "apiVersion": 99 }""");
        var module = Module();

        var answered = await AnswerAsync(module, "PLUGINS");

        Assert.Equal(DaorisHome.Entry(Home, HomeEntry.Plugins), answered.GetProperty("folder").GetString());
        var plugins = answered.GetProperty("plugins").EnumerateArray().ToList();
        Assert.Equal(2, plugins.Count);
        var gate = plugins.Single(p => p.GetProperty("id").GetString() == "acme.gate");
        Assert.Equal("Acme gate", gate.GetProperty("name").GetString());
        Assert.Equal("1.2.0", gate.GetProperty("version").GetString());
        Assert.True(gate.GetProperty("enabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, gate.GetProperty("problem").ValueKind);
        Assert.Equal(["acme-agent"], gate.GetProperty("harnesses").EnumerateArray().Select(h => h.GetString()!).ToArray());
        Assert.Equal(["quest/consider"], gate.GetProperty("points").EnumerateArray().Select(p => p.GetString()!).ToArray());
        // No loop is running here, so nothing is up — and the page must be told so rather than guess.
        Assert.False(gate.GetProperty("running").GetBoolean());
        Assert.Equal(folder, gate.GetProperty("folder").GetString());
        // A refused plugin is listed WITH its sentence, and contributes nothing.
        var future = plugins.Single(p => p.GetProperty("id").GetString() == "future");
        Assert.Contains("99", future.GetProperty("problem").GetString());
        Assert.Empty(future.GetProperty("harnesses").EnumerateArray());

        // The switch: a row in the same file `daoris plugin disable` writes, never a rename.
        var off = await AnswerAsync(module, "PLUGIN_ACTION", new { id = "acme.gate", action = "disable" });
        Assert.Equal("disable", off.GetProperty("action").GetString());
        Assert.Contains("acme.gate", File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.PluginState)));
        Assert.True(Directory.Exists(folder));
        var again = await AnswerAsync(module, "PLUGINS");
        Assert.False(again.GetProperty("plugins").EnumerateArray()
            .Single(p => p.GetProperty("id").GetString() == "acme.gate").GetProperty("enabled").GetBoolean());

        await AnswerAsync(module, "PLUGIN_ACTION", new { id = "acme.gate", action = "enable" });
        Assert.DoesNotContain("acme.gate", File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.PluginState)));

        // Remove takes the install folder and NAMES the data folder, which stays.
        var data = DaorisHome.Entry(Home, HomeEntry.Plugins,".data", "acme.gate");
        Directory.CreateDirectory(data);
        var removed = await AnswerAsync(module, "PLUGIN_ACTION", new { id = "acme.gate", action = "remove" });
        Assert.Equal(data, removed.GetProperty("data").GetString());
        Assert.False(Directory.Exists(folder));
        Assert.True(Directory.Exists(data));

        // An id nobody has, and an action this build lacks, are refusals with the page's own codes.
        Assert.Contains("PLUGIN_UNKNOWN", await RefusalAsync(module, "PLUGIN_ACTION", new { id = "nobody", action = "enable" }));
        Assert.Contains("PLUGIN_ACTION_UNKNOWN", await RefusalAsync(module, "PLUGIN_ACTION", new { id = "future", action = "explode" }));
    }

    /// <summary>
    /// A plugin whose folder something on the machine still holds is not half-removed (REV3 modules
    /// F5). On Windows the folder of a running hook process — its working directory — cannot go, and
    /// the recursive delete took every file it could first, leaving a folder with no manifest: a
    /// plugin stranded, neither there nor gone. Removal moves the folder aside whole, or refuses whole.
    /// </summary>
    [Fact]
    public async Task A_plugin_whose_folder_is_held_is_refused_whole_and_left_intact()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins,"acme.held");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), """{ "id": "acme.held", "harnesses": [] }""");
        File.WriteAllText(Path.Combine(folder, "hooks.mjs"), "// a hook the plugin ships");
        var module = Module();

        string refusal;
        using (File.Open(Path.Combine(folder, "hooks.mjs"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            refusal = await RefusalAsync(module, "PLUGIN_ACTION", new { id = "acme.held", action = "remove" });
        }

        Assert.Contains("PLUGIN_BUSY", refusal);
        Assert.True(File.Exists(Path.Combine(folder, "plugin.json")), "the removal took the manifest and stranded the plugin");
        Assert.True(File.Exists(Path.Combine(folder, "hooks.mjs")));
        Assert.Empty(Directory.GetDirectories(DaorisHome.Entry(Home, HomeEntry.Plugins), ".removing-*"));
    }

    /// <summary>
    /// PLUG9 (c): each installed plugin says where it came from — a folder, an offer, or no record, which is
    /// said rather than guessed — and Update is asked first for what it would change, then pressed. The
    /// install folder is swapped whole and what the plugin kept stays.
    /// </summary>
    [Fact]
    public async Task A_plugins_source_is_answered_and_an_update_says_what_changes_before_it_replaces_the_folder()
    {
        var checkout = Path.Combine(Home, "..", "checkout-" + Path.GetFileName(Home), "gate");
        try
        {
            Directory.CreateDirectory(checkout);
            File.WriteAllText(Path.Combine(checkout, "plugin.json"), """
                { "id": "acme.gate", "version": "1.0.0", "hooks": { "command": ["node", "${plugin}/gate.mjs"], "points": ["session/ended"] } }
                """);
            File.WriteAllText(Path.Combine(checkout, "gate.mjs"), "// v1");
            PluginInstall.Add(Home, checkout, AdapterSet.Built().Names);
            var bare = DaorisHome.Entry(Home, HomeEntry.Plugins,"acme.bare");
            Directory.CreateDirectory(bare);
            File.WriteAllText(Path.Combine(bare, "plugin.json"), """{ "id": "acme.bare" }""");
            var kept = DaorisHome.Entry(Home, HomeEntry.Plugins,".data", "acme.gate");
            Directory.CreateDirectory(kept);
            var module = new DriverModule(Bus, Loop()) { Offers = Path.Combine(Home, "no-offers") };

            var plugins = (await AnswerAsync(module, "PLUGINS")).GetProperty("plugins").EnumerateArray().ToList();
            var gate = plugins.Single(p => p.GetProperty("id").GetString() == "acme.gate").GetProperty("source");
            Assert.Equal("folder", gate.GetProperty("kind").GetString());
            Assert.Equal(Path.GetFullPath(checkout), gate.GetProperty("folder").GetString());
            Assert.Equal("none", plugins.Single(p => p.GetProperty("id").GetString() == "acme.bare").GetProperty("source").GetProperty("kind").GetString());

            File.WriteAllText(Path.Combine(checkout, "plugin.json"), File.ReadAllText(Path.Combine(checkout, "plugin.json")).Replace("1.0.0", "1.1.0"));
            File.WriteAllText(Path.Combine(checkout, "gate.mjs"), "// v2");
            var asked = await AnswerAsync(module, "PLUGIN_UPDATE", new { id = "acme.gate" });
            Assert.False(asked.GetProperty("applied").GetBoolean());
            Assert.Equal(JsonValueKind.Null, asked.GetProperty("refusal").ValueKind);
            var change = Assert.Single(asked.GetProperty("changes").EnumerateArray());
            Assert.Equal(("version", "1.0.0", "1.1.0"), (change.GetProperty("what").GetString(), change.GetProperty("was").GetString(), change.GetProperty("now").GetString()));
            Assert.Equal("// v1", File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.Plugins,"acme.gate", "gate.mjs")));

            var done = await AnswerAsync(module, "PLUGIN_UPDATE", new { id = "acme.gate", apply = true });
            Assert.True(done.GetProperty("applied").GetBoolean());
            Assert.Equal("// v2", File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.Plugins,"acme.gate", "gate.mjs")));
            Assert.True(Directory.Exists(kept));

            // No record: asked, the row is told why in the driver's words; pressed, the same refusal.
            var none = await AnswerAsync(module, "PLUGIN_UPDATE", new { id = "acme.bare" });
            Assert.Contains("has no record of where it came from", none.GetProperty("refusal").GetString());
            Assert.Contains("has no record of where it came from", await RefusalAsync(module, "PLUGIN_UPDATE", new { id = "acme.bare", apply = true }));
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(checkout)!, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// PLUG9 (d): the install's own plugins are answered beside the catalogue, each with what it declares and
    /// needs, and Install copies one in with the offer recorded. 🔴 Nothing it declares runs at the press.
    /// </summary>
    [Fact]
    public async Task The_installs_offers_are_answered_and_one_is_installed_by_a_press_running_nothing()
    {
        var offers = Path.Combine(Home, "..", "offers-" + Path.GetFileName(Home));
        try
        {
            var mark = Path.Combine(offers, "ran.txt");
            var offer = Path.Combine(offers, "github-pull-request");
            Directory.CreateDirectory(offer);
            File.WriteAllText(Path.Combine(offer, "plugin.json"), """
                { "id": "github-pull-request", "name": "GitHub pull request", "version": "1.0.0",
                  "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] } }
                """);
            File.WriteAllText(Path.Combine(offer, "land.mjs"), $"require('fs').writeFileSync({JsonSerializer.Serialize(mark)}, 'ran');");
            File.WriteAllText(Path.Combine(offer, "README.md"), "# x\n\n## What it needs\n\n- **gh**, signed in: `gh auth login`.\n");
            var module = new DriverModule(Bus, Loop()) { Offers = offers };

            var answered = await AnswerAsync(module, "PLUGINS");
            Assert.Equal(offers, answered.GetProperty("offersFolder").GetString());
            var listed = Assert.Single(answered.GetProperty("offers").EnumerateArray());
            Assert.Equal(("github-pull-request", "GitHub pull request", "1.0.0", false),
                (listed.GetProperty("id").GetString(), listed.GetProperty("name").GetString(), listed.GetProperty("version").GetString(), listed.GetProperty("installed").GetBoolean()));
            Assert.Equal(["work/land"], listed.GetProperty("points").EnumerateArray().Select(p => p.GetString()!).ToArray());
            Assert.Equal(["gh, signed in: `gh auth login`."], listed.GetProperty("needs").EnumerateArray().Select(p => p.GetString()!).ToArray());
            Assert.Empty(answered.GetProperty("plugins").EnumerateArray());

            var installed = await AnswerAsync(module, "PLUGIN_INSTALL", new { offer = "github-pull-request" });
            Assert.Equal("github-pull-request", installed.GetProperty("id").GetString());
            Assert.True(File.Exists(DaorisHome.Entry(Home, HomeEntry.Plugins,"github-pull-request", "land.mjs")));
            Assert.False(File.Exists(mark));
            var again = await AnswerAsync(module, "PLUGINS");
            Assert.True(Assert.Single(again.GetProperty("offers").EnumerateArray()).GetProperty("installed").GetBoolean());
            Assert.Equal("offer", Assert.Single(again.GetProperty("plugins").EnumerateArray()).GetProperty("source").GetProperty("kind").GetString());

            Assert.Contains("this install offers no plugin `acme.nothing`", await RefusalAsync(module, "PLUGIN_INSTALL", new { offer = "acme.nothing" }));
            Assert.Contains("already installed", await RefusalAsync(module, "PLUGIN_INSTALL", new { offer = "github-pull-request" }));
        }
        finally
        {
            try { Directory.Delete(offers, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// LANDPIN3 (D119): an installed copy of an example older than the one the install ships is answered with both versions and
    /// what the newer one brings, and its update is the person's press, taken from the offer though the copy has no record. A
    /// copy the person changed is said as changed and its press is refused.
    /// </summary>
    [Fact]
    public async Task An_installed_example_older_than_the_shipped_one_is_said_and_its_update_is_a_press_that_keeps_what_it_kept()
    {
        var offers = Path.Combine(Home, "..", "offers-" + Path.GetFileName(Home));
        try
        {
            string Manifest(string version) => "{ \"id\": \"github-pull-request\", \"version\": \"" + version + "\", \"changes\": \"Pushes the commit judged.\","
                + " \"hooks\": { \"command\": [\"node\", \"${plugin}/land.mjs\"], \"points\": [\"work/land\"] } }";
            var offer = Path.Combine(offers, "github-pull-request");
            Directory.CreateDirectory(offer);
            File.WriteAllText(Path.Combine(offer, "plugin.json"), Manifest("1.3.0"));
            File.WriteAllText(Path.Combine(offer, "land.mjs"), "// shipped");
            var copy = DaorisHome.Entry(Home, HomeEntry.Plugins, "github-pull-request");
            Directory.CreateDirectory(copy);
            File.WriteAllText(Path.Combine(copy, "plugin.json"), Manifest("1.2.0"));
            File.WriteAllText(Path.Combine(copy, "land.mjs"), "// old");
            var kept = DaorisHome.Entry(Home, HomeEntry.Plugins, ".data", "github-pull-request");
            Directory.CreateDirectory(kept);
            var module = new DriverModule(Bus, Loop()) { Offers = offers };

            var said = (await AnswerAsync(module, "PLUGINS")).GetProperty("plugins").EnumerateArray().Single().GetProperty("shipped");
            Assert.Equal(("older", "1.2.0", "1.3.0", "Pushes the commit judged."),
                (said.GetProperty("state").GetString(), said.GetProperty("installed").GetString(), said.GetProperty("shipped").GetString(), said.GetProperty("changes").GetString()));

            // Asked first: what it would change, from the install's offer, with no press made.
            var asked = await AnswerAsync(module, "PLUGIN_UPDATE", new { id = "github-pull-request", shipped = true });
            Assert.False(asked.GetProperty("applied").GetBoolean());
            Assert.Equal("1.3.0", Assert.Single(asked.GetProperty("changes").EnumerateArray()).GetProperty("now").GetString());
            Assert.Equal("// old", File.ReadAllText(Path.Combine(copy, "land.mjs")));

            var done = await AnswerAsync(module, "PLUGIN_UPDATE", new { id = "github-pull-request", shipped = true, apply = true });
            Assert.True(done.GetProperty("applied").GetBoolean());
            Assert.Equal("// shipped", File.ReadAllText(Path.Combine(copy, "land.mjs")));
            Assert.True(Directory.Exists(kept));
            var after = (await AnswerAsync(module, "PLUGINS")).GetProperty("plugins").EnumerateArray().Single();
            Assert.Equal(JsonValueKind.Null, after.GetProperty("shipped").ValueKind);
            Assert.Equal("offer", after.GetProperty("source").GetProperty("kind").GetString());

            // The person edits it: said as changed, and the press is refused whole.
            File.WriteAllText(Path.Combine(copy, "land.mjs"), "// mine");
            var changed = (await AnswerAsync(module, "PLUGINS")).GetProperty("plugins").EnumerateArray().Single().GetProperty("shipped");
            Assert.Equal("changed", changed.GetProperty("state").GetString());
            Assert.Contains("changed", await RefusalAsync(module, "PLUGIN_UPDATE", new { id = "github-pull-request", shipped = true, apply = true }));
            Assert.Equal("// mine", File.ReadAllText(Path.Combine(copy, "land.mjs")));
        }
        finally
        {
            try { Directory.Delete(offers, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// PLUG8: the plugin kit's screen half. The catalogue says where a new plugin may speak; New writes a
    /// plugin's folder where the person named and installs nothing; Try starts a folder or an installed
    /// plugin as the driver would. The kit's refusals arrive in its own words.
    /// </summary>
    [Fact]
    public async Task The_kit_makes_a_plugin_where_the_person_names_and_tries_it_or_an_installed_one()
    {
        var module = Module();
        var kit = (await AnswerAsync(module, "PLUGINS")).GetProperty("kit").GetProperty("points").EnumerateArray().ToList();
        Assert.Equal(["quest/consider", "session/ended", "work/land", "work/state", "state/changed"], kit.Select(p => p.GetProperty("name").GetString()!).ToArray());
        Assert.Equal(["decision", "observation", "act", "query", "observation"], kit.Select(p => p.GetProperty("kind").GetString()!).ToArray());

        var repository = Path.Combine(Home, "a-plugins-repository");
        Directory.CreateDirectory(repository);
        var made = await AnswerAsync(module, "PLUGIN_NEW", new { id = "acme.gate", points = new[] { "quest/consider" }, folder = repository });
        var folder = Path.Combine(repository, "acme.gate");
        Assert.Equal(folder, made.GetProperty("folder").GetString());
        Assert.Equal(["plugin.json", "plugin.mjs", "plugin.test.mjs", "README.md"],
            made.GetProperty("files").EnumerateArray().Select(f => f.GetString()!).ToArray());
        // Making one installs nothing.
        Assert.Empty((await AnswerAsync(module, "PLUGINS")).GetProperty("plugins").EnumerateArray());

        var tried = await AnswerAsync(module, "PLUGIN_TRY", new { folder });
        Assert.True(tried.GetProperty("passed").GetBoolean(), tried.GetRawText());
        Assert.Equal("acme.gate", tried.GetProperty("plugin").GetString());
        Assert.Equal(["handshake", "quest/consider", "shutdown", "stdout"],
            tried.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("name").GetString()!).ToArray());
        Assert.Contains("answered as the driver reads it", tried.GetProperty("summary").GetString());

        Assert.Contains("already holds", await RefusalAsync(module, "PLUGIN_NEW", new { id = "acme.gate", points = new[] { "quest/consider" }, folder = repository }));
        Assert.Contains("is not a point", await RefusalAsync(module, "PLUGIN_NEW", new { id = "acme.other", points = new[] { "quest/started" }, folder = repository }));
        Assert.False(Directory.Exists(Path.Combine(repository, "acme.other")));

        // Installed, it is tried by its id, from its own folder under the home.
        var installed = DaorisHome.Entry(Home, HomeEntry.Plugins,"acme.gate");
        Directory.CreateDirectory(installed);
        foreach (var file in Directory.GetFiles(folder)) File.Copy(file, Path.Combine(installed, Path.GetFileName(file)));
        var byId = await AnswerAsync(module, "PLUGIN_TRY", new { id = "acme.gate" });
        Assert.True(byId.GetProperty("passed").GetBoolean(), byId.GetRawText());
        Assert.Equal(installed, byId.GetProperty("folder").GetString());

        Assert.Contains("no plugin `acme.nobody`", await RefusalAsync(module, "PLUGIN_TRY", new { id = "acme.nobody" }));
        Assert.Contains("an installed plugin's id or a folder", await RefusalAsync(module, "PLUGIN_TRY", new { }));
    }
}
