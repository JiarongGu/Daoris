using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A Daoris plugin package and its reader, offline (PLUGDIST1a, D120; the distribution design §5.1 and §5.7): a
/// <c>.nupkg</c> is a zip whose <c>.nuspec</c> says it is of the type <c>DaorisPlugin</c>, at a type version whose
/// major number is the plugin API, with no dependencies, and whose plugin folder is under <c>plugin/</c>. Read
/// before anything is extracted, then <c>plugin/**</c> alone extracted into a stage under the home, read there by
/// the catalogue's own reader, and added through <see cref="PluginInstall"/> with where it came from recorded.
/// </summary>
/// <remarks>
/// <para>Every package here is built in the test with <c>System.IO.Compression</c>: nothing reaches a network,
/// and no NuGet client is involved, on either side.</para>
///
/// <para>🔴 Nothing a plugin declares runs here: installing extracts and copies, and the loop starts it later.</para>
/// </remarks>
public sealed class PluginPackageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-plugin-package-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "home");

    private string Feed => Path.Combine(_root, "feed");

    private string Plugins => Path.Combine(Home, PluginCatalog.Folder);

    private static IReadOnlyCollection<string> Reserved => AdapterSet.Built().Names;

    public PluginPackageTests() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A nuspec as <c>dotnet pack</c> writes one for a plugin (§5.2): the type, and no dependencies.</summary>
    private const string Nuspec = """
        <?xml version="1.0" encoding="utf-8"?>
        <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
          <metadata>
            <id>Acme.Gate</id>
            <version>1.0.0</version>
            <authors>Acme</authors>
            <description>A gate on quests.</description>
            <packageTypes>
              <packageType name="DaorisPlugin" version="1.0" />
            </packageTypes>
          </metadata>
        </package>
        """;

    private const string Manifest = """
        { "id": "acme.gate", "name": "Acme gate", "version": "1.0.0",
          "hooks": { "command": ["node", "${plugin}/gate.mjs"], "points": ["quest/consider"] } }
        """;

    /// <summary>The plugin folder a package carries, as entries under <c>plugin/</c>.</summary>
    private static readonly (string Name, string Text)[] Plugin =
    [
        ("plugin/plugin.json", Manifest),
        ("plugin/gate.mjs", "// v1"),
        ("plugin/lib/util.mjs", "// util"),
    ];

    /// <summary>
    /// A package file in the feed folder, written as NuGet lays one out: the nuspec and NuGet's own parts at the
    /// root, and whatever entries a row names, each exactly as named. A null nuspec writes none.
    /// </summary>
    private string Package(string? nuspec = Nuspec, IEnumerable<(string Name, string Text)>? entries = null, string name = "Acme.Gate.1.0.0.nupkg")
    {
        Directory.CreateDirectory(Feed);
        var path = Path.Combine(Feed, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string entry, string text)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entry).Open(), new UTF8Encoding(false));
            writer.Write(text);
        }

        if (nuspec is not null) Add("Acme.Gate.nuspec", nuspec);
        Add("[Content_Types].xml", """<?xml version="1.0" encoding="utf-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types" />""");
        Add("_rels/.rels", """<?xml version="1.0" encoding="utf-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships" />""");
        foreach (var (entry, text) in entries ?? Plugin) Add(entry, text);
        return path;
    }

    /// <summary>The nuspec with its package types replaced by what a row names.</summary>
    private static string Type(string types) =>
        Nuspec.Replace("""<packageType name="DaorisPlugin" version="1.0" />""", types, StringComparison.Ordinal);

    private static string Hash(string file) => Convert.ToBase64String(SHA512.HashData(File.ReadAllBytes(file)));

    private string Installed(string id) => Path.Combine(Plugins, id);

    // ── Read before extract (§5.7 step 1) ────────────────────────────────────────────────────────────

    [Fact]
    public void A_package_is_read_before_anything_is_extracted()
    {
        var file = Package();

        var (read, refusal) = PluginPackage.Read(file);

        Assert.Null(refusal);
        Assert.Equal(new PluginPackageRead("Acme.Gate", "1.0.0", 1, Hash(file)), read);
        Assert.False(Directory.Exists(Plugins));
    }

    // ── Install (§5.7 steps 2–5) ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_package_installs_its_plugin_folder_under_its_id_and_records_where_it_came_from()
    {
        var file = Package();

        var installed = PluginPackage.Install(Home, file, Reserved);

        Assert.Equal("acme.gate", installed.Manifest.Id);
        Assert.Equal(new PluginPackageOrigin("Acme.Gate", "1.0.0", Hash(file), Feed), installed.Origin);
        Assert.Equal("// v1", File.ReadAllText(Path.Combine(Installed("acme.gate"), "gate.mjs")));
        Assert.Equal("// util", File.ReadAllText(Path.Combine(Installed("acme.gate"), "lib", "util.mjs")));
        // NuGet's own parts at the package root are never extracted (§5.1).
        Assert.Equal([PluginSource.FileName, "gate.mjs", PluginCatalog.ManifestName],
            Directory.GetFiles(Installed("acme.gate")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal((PluginSource.FromPackage(installed.Origin), (string?)null), PluginSource.Read(Installed("acme.gate")));
        Assert.Null(Assert.Single(PluginCatalog.Load(Home, Reserved).Plugins).Problem);
        // Staged under the home and gone: nothing but the plugin is under plugins/.
        Assert.Equal(["acme.gate"], Directory.GetDirectories(Plugins).Select(Path.GetFileName));
    }

    /// <summary>NuGet escapes a part's name in the zip and unescapes it on reading; so does this reader.</summary>
    [Fact]
    public void An_escaped_name_is_extracted_as_NuGet_reads_it()
    {
        var file = Package(entries: [.. Plugin, ("plugin/read%20me.txt", "hello")]);

        PluginPackage.Install(Home, file, Reserved);

        Assert.Equal("hello", File.ReadAllText(Path.Combine(Installed("acme.gate"), "read me.txt")));
    }

    /// <summary>Only <c>plugin/**</c> is extracted: an entry outside it is never written anywhere, whatever it names.</summary>
    [Fact]
    public void An_entry_outside_the_plugin_folder_is_never_extracted()
    {
        var file = Package(entries: [.. Plugin, ("../outside.txt", "no"), ("tools/install.ps1", "no"), ("plugin.json", "{}")]);

        PluginPackage.Install(Home, file, Reserved);

        Assert.False(File.Exists(Path.Combine(_root, "outside.txt")));
        Assert.False(File.Exists(Path.Combine(Feed, "..", "outside.txt")));
        Assert.False(Directory.Exists(Path.Combine(Installed("acme.gate"), "tools")));
    }

    /// <summary>
    /// What a package is refused for, each before anything is installed (§5.1, §5.7). A row names the package it
    /// builds; every refusal ends saying nothing was installed, and leaves no plugin and no stage under the home.
    /// </summary>
    [Theory]
    [InlineData("not a zip", "does not open as a zip")]
    [InlineData("no nuspec", "holds no `.nuspec` at its root")]
    [InlineData("two nuspecs", "holds 2 `.nuspec` files at its root")]
    [InlineData("a nuspec that does not read", "does not read")]
    [InlineData("a nuspec with a DTD", "does not read")]
    [InlineData("no metadata", "has no `metadata`")]
    [InlineData("an id that is not a package id", "`Acme Gate` is not a package id")]
    [InlineData("a version that is not a package version", "`latest` is not a package version")]
    [InlineData("no package type", "is not a Daoris plugin: it declares no package type")]
    [InlineData("another package type", "is not a Daoris plugin: it is of the type `Dependency`")]
    [InlineData("the type and another", "is of the type `DaorisPlugin` and `McpServer` — a Daoris plugin package is of that type alone")]
    [InlineData("a type with no version", "names no version for its type `DaorisPlugin`")]
    [InlineData("a type version that is not one", "its type's version `one` is not a version like `1.0`")]
    [InlineData("a newer plugin API", "needs plugin API 2, and this build speaks 1")]
    [InlineData("no plugin API", "its type's version `0.0` names no plugin API")]
    [InlineData("a dependency", "declares dependencies (`Newtonsoft.Json`)")]
    [InlineData("a dependency in a group", "declares dependencies (`Acme.Core`)")]
    [InlineData("no plugin folder", "holds no `plugin/plugin.json`")]
    [InlineData("a manifest at the root only", "holds no `plugin/plugin.json`")]
    [InlineData("an entry that climbs out", "holds `plugin/../evil.txt`, which would land outside its plugin's folder")]
    [InlineData("an escaped entry that climbs out", "holds `plugin/..%2F..%2Fevil.txt`, which would land outside its plugin's folder")]
    [InlineData("a backslashed entry that climbs out", "which would land outside its plugin's folder")]
    [InlineData("an entry that climbs out of a subfolder", "holds `plugin/lib/../../evil.txt`, which would land outside its plugin's folder")]
    [InlineData("an entry on a drive", "holds `plugin/C:/evil.txt`, which would land outside its plugin's folder")]
    [InlineData("a rooted entry", "holds `plugin//evil.txt`, which would land outside its plugin's folder")]
    [InlineData("an entry with a control character", "holds `plugin/a%00b.txt`, a name with a control character")]
    [InlineData("an entry twice", "holds `plugin/Gate.mjs` twice")]
    [InlineData("a manifest the catalogue refuses", "`id` must be lowercase letters, digits, dots and dashes")]
    [InlineData("a plugin this build refuses", "`dsh`, which this build already carries")]
    [InlineData("a plugin at another version", "holds plugin `acme.gate` at version `0.9.0` — a package's version is its plugin's")]
    [InlineData("a plugin at another API", "its type says plugin API 1, and its `plugin.json` says 0")]
    public void A_package_is_refused_whole_and_nothing_is_installed(string name, string says)
    {
        var file = name switch
        {
            "not a zip" => Text("Acme.Gate.1.0.0.nupkg", "not a zip"),
            "no nuspec" => Package(nuspec: null),
            "two nuspecs" => Package(entries: [.. Plugin, ("Other.nuspec", Nuspec)]),
            "a nuspec that does not read" => Package("<package><metadata>"),
            "a nuspec with a DTD" => Package("""<?xml version="1.0"?><!DOCTYPE package [ <!ENTITY e SYSTEM "file:///nowhere"> ]><package><metadata><id>&e;</id></metadata></package>"""),
            "no metadata" => Package("<package />"),
            "an id that is not a package id" => Package(Nuspec.Replace("<id>Acme.Gate</id>", "<id>Acme Gate</id>")),
            "a version that is not a package version" => Package(Nuspec.Replace("<version>1.0.0</version>", "<version>latest</version>")),
            "no package type" => Package(Type("")),
            "another package type" => Package(Type("""<packageType name="Dependency" />""")),
            "the type and another" => Package(Type("""<packageType name="DaorisPlugin" version="1.0" /><packageType name="McpServer" />""")),
            "a type with no version" => Package(Type("""<packageType name="DaorisPlugin" />""")),
            "a type version that is not one" => Package(Type("""<packageType name="DaorisPlugin" version="one" />""")),
            "a newer plugin API" => Package(Type("""<packageType name="DaorisPlugin" version="2.0" />""")),
            "no plugin API" => Package(Type("""<packageType name="DaorisPlugin" version="0.0" />""")),
            "a dependency" => Package(Nuspec.Replace("</packageTypes>",
                """</packageTypes><dependencies><dependency id="Newtonsoft.Json" version="13.0.3" /></dependencies>""")),
            "a dependency in a group" => Package(Nuspec.Replace("</packageTypes>",
                """</packageTypes><dependencies><group targetFramework="net10.0" /><group><dependency id="Acme.Core" version="1.0.0" /></group></dependencies>""")),
            "no plugin folder" => Package(entries: [("lib/net10.0/Acme.Gate.dll", "")]),
            "a manifest at the root only" => Package(entries: [("plugin.json", Manifest), ("gate.mjs", "// v1")]),
            "an entry that climbs out" => Package(entries: [.. Plugin, ("plugin/../evil.txt", "evil")]),
            "an escaped entry that climbs out" => Package(entries: [.. Plugin, ("plugin/..%2F..%2Fevil.txt", "evil")]),
            "a backslashed entry that climbs out" => Package(entries: [.. Plugin, ("plugin\\..\\evil.txt", "evil")]),
            "an entry that climbs out of a subfolder" => Package(entries: [.. Plugin, ("plugin/lib/../../evil.txt", "evil")]),
            "an entry on a drive" => Package(entries: [.. Plugin, ("plugin/C:/evil.txt", "evil")]),
            "a rooted entry" => Package(entries: [.. Plugin, ("plugin//evil.txt", "evil")]),
            "an entry with a control character" => Package(entries: [.. Plugin, ("plugin/a%00b.txt", "evil")]),
            "an entry twice" => Package(entries: [.. Plugin, ("plugin/Gate.mjs", "// another")]),
            "a manifest the catalogue refuses" => Package(entries: [("plugin/plugin.json", """{ "id": "Acme Gate", "version": "1.0.0" }""")]),
            "a plugin this build refuses" => Package(entries: [("plugin/plugin.json",
                """{ "id": "acme.gate", "version": "1.0.0", "harnesses": [ { "name": "dsh", "command": ["x"] } ] }""")]),
            "a plugin at another version" => Package(entries: [("plugin/plugin.json", Manifest.Replace("1.0.0", "0.9.0"))]),
            "a plugin at another API" => Package(entries: [("plugin/plugin.json", """{ "id": "acme.gate", "version": "1.0.0", "apiVersion": 0 }""")]),
            _ => throw new ArgumentException(name),
        };

        var (read, refusal) = PluginPackage.Read(file);
        var thrown = Assert.Throws<DriverException>(() => PluginPackage.Install(Home, file, Reserved));

        Assert.True(thrown.Message.Contains(says, StringComparison.Ordinal), $"{name}: {thrown.Message}");
        Assert.EndsWith("Nothing was installed.", thrown.Message);
        // What a read alone can see, it refuses too: the stage's own checks come after extracting.
        if (refusal is not null) Assert.True(refusal.Contains(says, StringComparison.Ordinal), $"{name}: {refusal}");
        else Assert.NotNull(read);
        Assert.Empty(Directory.Exists(Plugins) ? Directory.GetFileSystemEntries(Plugins) : []);
        Assert.False(File.Exists(Path.Combine(_root, "evil.txt")));
        Assert.False(File.Exists(Path.Combine(Home, "evil.txt")));
    }

    /// <summary>The checks a read alone makes are the ones before anything is extracted (§5.7 step 1).</summary>
    [Theory]
    [InlineData("no plugin folder")]
    [InlineData("an entry that climbs out")]
    [InlineData("a dependency")]
    public void A_read_refuses_what_it_can_see_before_extracting(string name)
    {
        var file = name switch
        {
            "no plugin folder" => Package(entries: [("lib/net10.0/Acme.Gate.dll", "")]),
            "an entry that climbs out" => Package(entries: [.. Plugin, ("plugin/../evil.txt", "evil")]),
            _ => Package(Nuspec.Replace("</packageTypes>", """</packageTypes><dependencies><dependency id="Newtonsoft.Json" /></dependencies>""")),
        };

        var (read, refusal) = PluginPackage.Read(file);

        Assert.Null(read);
        Assert.NotNull(refusal);
        Assert.False(Directory.Exists(Plugins));
    }

    [Fact]
    public void A_file_that_is_not_there_is_refused()
    {
        var missing = Path.Combine(Feed, "Acme.Gate.9.9.9.nupkg");

        Assert.Contains("no file", PluginPackage.Read(missing).Refusal);
        Assert.Contains("no file", Assert.Throws<DriverException>(() => PluginPackage.Install(Home, missing, Reserved)).Message);
    }

    /// <summary>§5.7 step 4: an add never replaces, and the refusal names where the installed one came from.</summary>
    [Fact]
    public void A_plugin_installed_already_is_refused_naming_where_it_came_from()
    {
        var file = Package();
        PluginPackage.Install(Home, file, Reserved);
        File.WriteAllText(Path.Combine(Installed("acme.gate"), "gate.mjs"), "// as installed");

        var thrown = Assert.Throws<DriverException>(() => PluginPackage.Install(Home, file, Reserved));

        Assert.Contains("plugin `acme.gate` is already installed on this machine, from " + $"{Feed}, package `Acme.Gate` 1.0.0", thrown.Message);
        Assert.Contains("`daoris plugin remove acme.gate`", thrown.Message);
        Assert.Equal("// as installed", File.ReadAllText(Path.Combine(Installed("acme.gate"), "gate.mjs")));
        Assert.Equal(["acme.gate"], Directory.GetDirectories(Plugins).Select(Path.GetFileName));
    }

    /// <summary>🔴 Installing a package starts nothing it declares: its hook would leave a mark, and there is none.</summary>
    [Fact]
    public void Installing_a_package_starts_nothing_it_declares()
    {
        var mark = Path.Combine(_root, "ran.txt");
        var file = Package(entries:
        [
            ("plugin/plugin.json", """{ "id": "acme.marks", "version": "1.0.0", "hooks": { "command": ["node", "${plugin}/hook.mjs"], "points": ["session/ended"] } }"""),
            ("plugin/hook.mjs", $"require('fs').writeFileSync({JsonSerializer.Serialize(mark)}, 'ran');"),
        ]);

        PluginPackage.Install(Home, file, Reserved);

        Assert.False(File.Exists(mark));
    }

    // ── The terminal's door: `daoris-driver plugins install <file.nupkg>` (§6.4) ──────────────────────

    [Fact]
    public void The_terminal_installs_a_package_file_and_says_where_it_came_from()
    {
        var file = Package();
        var output = new StringWriter();

        var code = PluginPackageCommand.Install([file], output, Home, Reserved);

        var said = output.ToString().ReplaceLineEndings("\n");
        Assert.True(code == 0, said);
        Assert.Contains($"plugins: installed `acme.gate` 1.0.0 at {Installed("acme.gate")}", said);
        Assert.Contains($"From {Feed}, package `Acme.Gate` 1.0.0, sha512 {Hash(file)}.", said);
        Assert.Contains("It takes effect at the driver's next look", said);
        Assert.True(File.Exists(Path.Combine(Installed("acme.gate"), PluginCatalog.ManifestName)));
    }

    [Theory]
    [InlineData("no file named", "`plugins install` needs a package file")]
    [InlineData("an id, not a file", "`acme.gate` is not a package file")]
    [InlineData("a refused package", "holds no `plugin/plugin.json`")]
    [InlineData("no home", "no Daoris home")]
    [InlineData("something it does not take", "`--yes` is not something `plugins install` takes")]
    public void The_terminal_refuses_with_exit_2_and_installs_nothing(string name, string says)
    {
        var refused = Package(entries: [("lib/net10.0/Acme.Gate.dll", "")], name: "Acme.Refused.1.0.0.nupkg");
        var good = Package();
        var output = new StringWriter();

        var code = name switch
        {
            "no file named" => PluginPackageCommand.Install([], output, Home, Reserved),
            "an id, not a file" => PluginPackageCommand.Install(["acme.gate"], output, Home, Reserved),
            "a refused package" => PluginPackageCommand.Install([refused], output, Home, Reserved),
            "no home" => PluginPackageCommand.Install([good], output, null, Reserved),
            _ => PluginPackageCommand.Install([good, "--yes"], output, Home, Reserved),
        };

        Assert.Equal(2, code);
        Assert.True(output.ToString().Contains(says, StringComparison.Ordinal), $"{name}: {output}");
        Assert.StartsWith("plugins: ", output.ToString());
        Assert.False(Directory.Exists(Installed("acme.gate")));
    }

    private string Text(string name, string text)
    {
        Directory.CreateDirectory(Feed);
        var path = Path.Combine(Feed, name);
        File.WriteAllText(path, text);
        return path;
    }
}
