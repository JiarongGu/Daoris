using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// `daoris plugin add`'s copy, the driver's twin (PLUG9): a plugin's folder read by the catalogue's own
/// reader, then copied into the home under its manifest's id. The CLI's `plugins.test.ts` holds the same
/// table (*add copies a folder in under its id*, *add refuses a folder whose manifest is unsound*), and
/// the two share no code. One difference is deliberate and held here: the driver adds, and never
/// replaces an installed plugin — replacing one stays the terminal's.
/// </summary>
/// <remarks>🔴 Nothing a plugin declares runs here: adding copies a folder, and the loop starts it later.</remarks>
public sealed class PluginInstallTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-plugin-add-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "home");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Source(string name, string manifest)
    {
        var folder = Path.Combine(_root, "somewhere", name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), manifest);
        return folder;
    }

    private static IReadOnlyCollection<string> Reserved => AdapterSet.Built().Names;

    [Fact]
    public void A_folder_is_copied_in_under_its_id_and_the_data_folder_is_left_alone()
    {
        var source = Source("my-plugin-src", """{ "id": "acme.agent", "harnesses": [ { "name": "acme-agent", "command": ["${plugin}/a.mjs"] } ] }""");
        File.WriteAllText(Path.Combine(source, "a.mjs"), "// v1");
        Directory.CreateDirectory(Path.Combine(source, "lib"));
        File.WriteAllText(Path.Combine(source, "lib", "b.mjs"), "// b");
        var kept = Path.Combine(Home, PluginCatalog.Folder, PluginCatalog.DataFolder, "acme.agent");
        Directory.CreateDirectory(kept);
        File.WriteAllText(Path.Combine(kept, "kept.json"), "{}");

        var added = PluginInstall.Add(Home, source, Reserved);

        Assert.Equal("acme.agent", added.Id);
        var installed = Path.Combine(Home, PluginCatalog.Folder, "acme.agent");
        Assert.Equal("// v1", File.ReadAllText(Path.Combine(installed, "a.mjs")));
        Assert.Equal("// b", File.ReadAllText(Path.Combine(installed, "lib", "b.mjs")));
        Assert.True(File.Exists(Path.Combine(kept, "kept.json")));
        Assert.True(File.Exists(Path.Combine(source, "a.mjs")));
        // The catalogue reads it with the placeholder expanded to where it LANDED, not where it came from.
        Assert.Equal(Path.Combine(installed, "a.mjs"), Assert.Single(PluginCatalog.Load(Home).Plugins).Manifest.Harnesses[0].Command[0]);
        // Staged beside and renamed in: nothing but the plugin and its data is left under plugins/.
        Assert.Equal([".data", "acme.agent"], Directory.GetDirectories(Path.Combine(Home, PluginCatalog.Folder)).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    /// <summary>What `plugin add` refuses, refused in the same words, with nothing copied.</summary>
    [Theory]
    [InlineData("bad", """{ "id": "bad", "apiVersion": 99 }""", "needs plugin API 99")]
    [InlineData("shadow", """{ "id": "shadow", "harnesses": [ { "name": "dsh", "command": ["x"] } ] }""", "`dsh`, which this build already carries")]
    [InlineData("sly", """{ "id": "sly", "servers": [ { "name": "daoris-knowledge", "command": ["x"] } ] }""", "`daoris-knowledge`, which is Daoris's own knowledge host")]
    [InlineData("shouting", """{ "id": "shouting", "harnesses": [ { "name": "DSH", "command": ["x"] } ] }""", "`DSH`, which this build already carries")]
    [InlineData("malformed", """{ "id": "malformed", "hooks": { "command": ["node"] } }""", "`hooks` needs a `command` and the `points`")]
    [InlineData("unreadable", "{ not json", "could not be read")]
    public void A_folder_whose_manifest_is_unsound_or_shadows_this_build_is_refused(string name, string manifest, string says)
    {
        var refused = Assert.Throws<DriverException>(() => PluginInstall.Add(Home, Source(name, manifest), Reserved));

        Assert.Contains(says, refused.Message);
        Assert.Contains("Nothing was copied", refused.Message);
        Assert.False(Directory.Exists(Path.Combine(Home, PluginCatalog.Folder)));
    }

    [Fact]
    public void A_folder_with_no_manifest_is_refused()
    {
        var empty = Path.Combine(_root, "somewhere", "empty");
        Directory.CreateDirectory(empty);

        var refused = Assert.Throws<DriverException>(() => PluginInstall.Add(Home, empty, Reserved));

        Assert.Contains("no `plugin.json`", refused.Message);
        Assert.Contains("Nothing was copied", refused.Message);
    }

    /// <summary>The one deliberate difference from `plugin add`: an installed plugin is never replaced from here.</summary>
    [Fact]
    public void An_installed_plugin_is_never_replaced_here()
    {
        var source = Source("acme.agent", """{ "id": "acme.agent" }""");
        File.WriteAllText(Path.Combine(source, "a.mjs"), "// v1");
        PluginInstall.Add(Home, source, Reserved);
        File.WriteAllText(Path.Combine(source, "a.mjs"), "// v2");

        var refused = Assert.Throws<DriverException>(() => PluginInstall.Add(Home, source, Reserved));

        Assert.Contains("already installed", refused.Message);
        Assert.Contains("daoris plugin add", refused.Message);
        Assert.Equal("// v1", File.ReadAllText(Path.Combine(Home, PluginCatalog.Folder, "acme.agent", "a.mjs")));
    }

    /// <summary>A folder inside the home is Daoris's own, and one holding the home would copy it into itself.</summary>
    [Fact]
    public void A_folder_inside_the_home_or_holding_it_is_refused()
    {
        var inside = Path.Combine(Home, "elsewhere", "acme.agent");
        Directory.CreateDirectory(inside);
        File.WriteAllText(Path.Combine(inside, PluginCatalog.ManifestName), """{ "id": "acme.agent" }""");
        File.WriteAllText(Path.Combine(_root, PluginCatalog.ManifestName), """{ "id": "acme.all" }""");

        Assert.Contains("inside Daoris's home", Assert.Throws<DriverException>(() => PluginInstall.Add(Home, inside, Reserved)).Message);
        Assert.Contains("holds Daoris's home", Assert.Throws<DriverException>(() => PluginInstall.Add(Home, _root, Reserved)).Message);
        Assert.False(Directory.Exists(Path.Combine(Home, PluginCatalog.Folder)));
    }

    /// <summary>🔴 The copy starts nothing the plugin declares: its hook writes a mark when it runs, and there is none.</summary>
    [Fact]
    public void Nothing_a_plugin_declares_runs_when_it_is_added()
    {
        var mark = Path.Combine(_root, "ran.txt");
        var source = Source("marks", """{ "id": "acme.marks", "hooks": { "command": ["node", "${plugin}/hook.mjs"], "points": ["session/ended"] } }""");
        File.WriteAllText(Path.Combine(source, "hook.mjs"), $"require('fs').writeFileSync({System.Text.Json.JsonSerializer.Serialize(mark)}, 'ran');");

        PluginInstall.Add(Home, source, Reserved);

        Assert.False(File.Exists(mark));
    }

    /// <summary>What the card reads: the manifest as written, `${plugin}` where it says so and no machine path.</summary>
    [Fact]
    public void A_folder_is_read_as_its_manifest_writes_it()
    {
        var source = Source("quiet", """
            { "id": "acme.quiet", "hooks": { "command": ["node", "${plugin}/hooks.mjs"], "points": ["quest/consider"] },
              "servers": [ { "name": "browser", "command": ["npx", "-y", "@playwright/mcp@latest", "--user-data-dir", "${data}"] } ] }
            """);

        var (manifest, refusal) = PluginInstall.Read(source, Reserved);

        Assert.Null(refusal);
        Assert.Equal(["node", "${plugin}/hooks.mjs"], manifest!.Hooks!.Command);
        Assert.Equal(["npx", "-y", "@playwright/mcp@latest", "--user-data-dir", "${data}"], manifest.Servers[0].Command);
    }
}
