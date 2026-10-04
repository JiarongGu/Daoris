using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's tools (PLUGTOOL1a, D150 point 7, the UX6 design §7.2): the manifest's <c>tools</c>, the programs its process
/// runs, read by the catalogue by one table. The driver's half of a twin with the CLI's <c>plugins.ts</c>:
/// <c>plugin-tools.test.ts</c> parses <see cref="The_tools_read_as_the_cli_reads_them"/> and holds its own table to it,
/// cell for cell and in order. They share no code.
/// </summary>
/// <remarks>
/// 🔴 <b>A problem in <c>tools</c> never refuses the plugin</b>: it is that tool's sentence, and the plugin still
/// contributes. Reading a manifest starts nothing: a tool is found, asked its version and checked only at a trial
/// (<see cref="PluginToolCheckTests"/>).
/// </remarks>
public sealed class PluginToolsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-plugin-tools-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Plugin(string id, string? tools, string extra = "")
    {
        var folder = Path.Combine(_home, PluginCatalog.Folder, id);
        Directory.CreateDirectory(folder);
        var field = tools is null ? "" : $", \"tools\": {tools}";
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
            $"{{ \"id\": \"{id}\", \"name\": \"Acme lands\", \"hooks\": {{ \"command\": [\"node\", \"lands.mjs\"], \"points\": [\"work/land\"] }}{field}{extra} }}");
        return folder;
    }

    private static string? Word(PluginToolKind? kind) => kind?.ToString().ToLowerInvariant();

    /// <summary>
    /// The tools' table (§7.2): [case, the manifest's <c>tools</c> as JSON or null for none, how many tools are read, then
    /// the last one read: its id, its kind (<c>own</c>, <c>known</c>, <c>other</c>), its name, the program found for it, its
    /// range as written, how many checks it carries, and a fragment of its problem, which with no tool read is the field's].
    /// The rules are checked in this order and each tool's first problem is said. The twin is <c>plugin-tools.test.ts</c>'s
    /// <c>TOOL_ROWS</c>.
    /// </summary>
    [Theory]
    [InlineData("no tools", null, 0, null, null, null, null, null, 0, null)]
    [InlineData("null is none", "null", 0, null, null, null, null, null, 0, null)]
    [InlineData("none declared", "[]", 0, null, null, null, null, null, 0, null)]
    [InlineData("not an array", """{ "id": "az" }""", 0, null, null, null, null, null, 0, "`tools` must be an array of the tools the plugin runs")]
    [InlineData("Daoris's own", """[{ "id": "git" }]""", 1, "git", "own", "Git", null, null, 0, null)]
    [InlineData("one Daoris knows and does not run", """[{ "id": "az", "versions": ">=2.60", "for": "Pushes the branch and opens its pull request.", "ready": [{ "run": ["az", "account", "show", "--output", "none"], "says": "Signed in", "fix": "az login" }] }]""", 1, "az", "known", "Azure CLI", null, ">=2.60", 1, null)]
    [InlineData("one Daoris does not know", """[{ "id": "terraform" }]""", 1, "terraform", "other", "terraform", "terraform", null, 0, null)]
    [InlineData("one Daoris does not know, named", """[{ "id": "tf", "name": "Terraform", "command": "terraform", "versionArguments": ["version"] }]""", 1, "tf", "other", "Terraform", "terraform", null, 0, null)]
    [InlineData("a range with its top", """[{ "id": "az", "versions": ">=2.60 <3" }]""", 1, "az", "known", "Azure CLI", null, ">=2.60 <3", 0, null)]
    [InlineData("one exact version", """[{ "id": "node", "versions": "22.11.0" }]""", 1, "node", "own", "Node.js", null, "22.11.0", 0, null)]
    [InlineData("spaces between and around", """[{ "id": "az", "versions": " >=2.60  <3 " }]""", 1, "az", "known", "Azure CLI", null, " >=2.60  <3 ", 0, null)]
    [InlineData("four checks", """[{ "id": "gh", "ready": [{ "run": ["gh"], "says": "a" }, { "run": ["gh"], "says": "b" }, { "run": ["gh"], "says": "c" }, { "run": ["gh"], "says": "d" }] }]""", 1, "gh", "known", "GitHub CLI", null, null, 4, null)]
    [InlineData("an empty argument in a check", """[{ "id": "gh", "ready": [{ "run": ["gh", "auth", "status", "--hostname", ""], "says": "Signed in" }] }]""", 1, "gh", "known", "GitHub CLI", null, null, 1, null)]
    [InlineData("null fields are none", """[{ "id": "az", "name": null, "command": null, "versionArguments": null, "versions": null, "for": null, "ready": null }]""", 1, "az", "known", "Azure CLI", null, null, 0, null)]
    [InlineData("a field it does not know is passed over", """[{ "id": "az", "url": "https://example.test/az.zip", "sha256": "00" }]""", 1, "az", "known", "Azure CLI", null, null, 0, null)]
    [InlineData("a broken tool leaves the next one read", """[{ "id": 7 }, { "id": "git" }]""", 2, "git", "own", "Git", null, null, 0, null)]
    [InlineData("not an object", """["az"]""", 1, null, null, null, null, null, 0, "tool 1 in `tools` is not an object with an `id`")]
    [InlineData("no id", """[{ "versions": ">=1" }]""", 1, null, null, null, null, null, 0, "tool 1 in `tools` needs an `id`: a tool's name in lowercase, like `az`")]
    [InlineData("an id that is not text", """[{ "id": 7 }]""", 1, null, null, null, null, null, 0, "tool 1 in `tools` needs an `id`")]
    [InlineData("a blank id", """[{ "id": " " }]""", 1, null, null, null, null, null, 0, "tool 1 in `tools` needs an `id`")]
    [InlineData("an id in capitals", """[{ "id": "Az" }]""", 1, null, null, null, null, null, 0, "tool 1 in `tools` has the `id` `Az`, which is not one: lowercase letters, digits, dots and dashes")]
    [InlineData("the second of two", """[{ "id": "git" }, { "id": "a z" }]""", 2, null, null, null, null, null, 0, "tool 2 in `tools` has the `id` `a z`")]
    [InlineData("an id twice", """[{ "id": "az" }, { "id": "az", "versions": ">=2" }]""", 2, "az", null, null, null, null, 0, "tool `az` is declared twice in `tools`; the first is read")]
    [InlineData("a known tool's command", """[{ "id": "gh", "command": "gh2" }]""", 1, "gh", null, null, null, null, 0, "tool `gh` is GitHub CLI, which Daoris knows: its name, its file and how its version is asked are Daoris's, so `command` is not a plugin's to declare")]
    [InlineData("a known tool's name", """[{ "id": "git", "name": "Git" }]""", 1, "git", null, null, null, null, 0, "so `name` is not a plugin's to declare")]
    [InlineData("a known tool's version question", """[{ "id": "node", "versionArguments": ["-v"] }]""", 1, "node", null, null, null, null, 0, "so `versionArguments` is not a plugin's to declare")]
    [InlineData("a name that is not text", """[{ "id": "tf", "name": 5 }]""", 1, "tf", null, null, null, null, 0, "tool `tf`'s `name` must be text: what a person calls it")]
    [InlineData("a blank name", """[{ "id": "tf", "name": "" }]""", 1, "tf", null, null, null, null, 0, "tool `tf`'s `name` must be text")]
    [InlineData("a command with a folder", """[{ "id": "tf", "command": "bin/terraform" }]""", 1, "tf", null, null, null, null, 0, "tool `tf`'s `command` must be the name of a program found on the PATH, with no folder in it")]
    [InlineData("a command that is a whole path", """[{ "id": "tf", "command": "/usr/bin/terraform" }]""", 1, "tf", null, null, null, null, 0, "tool `tf`'s `command` must be the name of a program")]
    [InlineData("a command on a drive", """[{ "id": "tf", "command": "C:terraform" }]""", 1, "tf", null, null, null, null, 0, "tool `tf`'s `command` must be the name of a program")]
    [InlineData("a blank command", """[{ "id": "tf", "command": " " }]""", 1, "tf", null, null, null, null, 0, "tool `tf`'s `command` must be the name of a program")]
    [InlineData("a version question that is not a list", """[{ "id": "tf", "versionArguments": "version" }]""", 1, "tf", null, null, null, null, 0, "tool `tf`'s `versionArguments` must be an array of text: what prints its version")]
    [InlineData("a version question holding a number", """[{ "id": "tf", "versionArguments": ["version", 2] }]""", 1, "tf", null, null, null, null, 0, "tool `tf`'s `versionArguments` must be an array of text")]
    [InlineData("not a range", """[{ "id": "az", "versions": ">2.60" }]""", 1, "az", null, null, null, null, 0, "tool `az`'s `versions` must be a range: `>=2.60`, `>=2.60 <3`, or one exact version")]
    [InlineData("a word", """[{ "id": "az", "versions": "latest" }]""", 1, "az", null, null, null, null, 0, "tool `az`'s `versions` must be a range")]
    [InlineData("a v before it", """[{ "id": "az", "versions": "v2.60" }]""", 1, "az", null, null, null, null, 0, "tool `az`'s `versions` must be a range")]
    [InlineData("five numbers", """[{ "id": "az", "versions": "1.2.3.4.5" }]""", 1, "az", null, null, null, null, 0, "tool `az`'s `versions` must be a range")]
    [InlineData("its top first", """[{ "id": "az", "versions": "<3 >=2.60" }]""", 1, "az", null, null, null, null, 0, "tool `az`'s `versions` must be a range")]
    [InlineData("a top alone", """[{ "id": "az", "versions": "<3" }]""", 1, "az", null, null, null, null, 0, "tool `az`'s `versions` must be a range")]
    [InlineData("blank", """[{ "id": "az", "versions": "  " }]""", 1, "az", null, null, null, null, 0, "tool `az`'s `versions` must be a range")]
    [InlineData("versions that are not text", """[{ "id": "az", "versions": 2.6 }]""", 1, "az", null, null, null, null, 0, "tool `az`'s `versions` must be a range")]
    [InlineData("a range that holds nothing", """[{ "id": "az", "versions": ">=3 <2" }]""", 1, "az", null, null, null, null, 0, "tool `az`'s `versions` `>=3 <2` holds no version: `<2` is not above `>=3`")]
    [InlineData("a top no higher than its floor", """[{ "id": "az", "versions": ">=2.60 <2.60.0" }]""", 1, "az", null, null, null, null, 0, "holds no version: `<2.60.0` is not above `>=2.60`")]
    [InlineData("a blank for", """[{ "id": "az", "for": " " }]""", 1, "az", null, null, null, null, 0, "tool `az`'s `for` must be one sentence: why the plugin runs it")]
    [InlineData("for that is not text", """[{ "id": "az", "for": ["pushes"] }]""", 1, "az", null, null, null, null, 0, "tool `az`'s `for` must be one sentence")]
    [InlineData("ready that is not a list", """[{ "id": "az", "ready": { "run": ["az"], "says": "a" } }]""", 1, "az", null, null, null, null, 0, "tool `az`'s `ready` must be an array of checks")]
    [InlineData("five checks", """[{ "id": "gh", "ready": [{ "run": ["gh"], "says": "a" }, { "run": ["gh"], "says": "b" }, { "run": ["gh"], "says": "c" }, { "run": ["gh"], "says": "d" }, { "run": ["gh"], "says": "e" }] }]""", 1, "gh", null, null, null, null, 0, "tool `gh` has 5 checks in `ready`, and a tool has at most four")]
    [InlineData("a check that is not an object", """[{ "id": "az", "ready": ["az login"] }]""", 1, "az", null, null, null, null, 0, "tool `az`'s check 1 needs a `run`: the command whose exit 0 means ready, as an array")]
    [InlineData("a check with no run", """[{ "id": "az", "ready": [{ "says": "Signed in" }] }]""", 1, "az", null, null, null, null, 0, "tool `az`'s check 1 needs a `run`")]
    [InlineData("a run that is empty", """[{ "id": "az", "ready": [{ "run": [], "says": "Signed in" }] }]""", 1, "az", null, null, null, null, 0, "tool `az`'s check 1 needs a `run`")]
    [InlineData("a run that is a line", """[{ "id": "az", "ready": [{ "run": "az account show", "says": "Signed in" }] }]""", 1, "az", null, null, null, null, 0, "tool `az`'s check 1 needs a `run`")]
    [InlineData("a run whose first word is blank", """[{ "id": "az", "ready": [{ "run": [" ", "account"], "says": "Signed in" }] }]""", 1, "az", null, null, null, null, 0, "tool `az`'s check 1 needs a `run`")]
    [InlineData("a run holding a number", """[{ "id": "az", "ready": [{ "run": ["az", 7], "says": "Signed in" }] }]""", 1, "az", null, null, null, null, 0, "tool `az`'s check 1 needs a `run`")]
    [InlineData("a check with no says", """[{ "id": "az", "ready": [{ "run": ["az", "account", "show"] }] }]""", 1, "az", null, null, null, null, 0, "tool `az`'s check 1 needs `says`: what it means when it passes")]
    [InlineData("a fix that is not text", """[{ "id": "az", "ready": [{ "run": ["az", "account", "show"], "says": "Signed in", "fix": ["az", "login"] }] }]""", 1, "az", null, null, null, null, 0, "tool `az`'s check 1 has a `fix` that is not text: the command a person runs when it does not pass")]
    [InlineData("the second check", """[{ "id": "az", "ready": [{ "run": ["az", "version"], "says": "Runs" }, { "run": ["az", "account", "show"], "says": " " }] }]""", 1, "az", null, null, null, null, 0, "tool `az`'s check 2 needs `says`")]
    public void The_tools_read_as_the_cli_reads_them(
        string name, string? tools, int count, string? id, string? kind, string? toolName, string? command, string? versions, int checks,
        string? problem)
    {
        Plugin("acme.lands", tools);

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);
        var read = entry.Manifest.Tools;

        // 🔴 Never the plugin's problem: it is sound whatever its tools say.
        Assert.True(entry.Problem is null, $"{name}: {entry.Problem}");
        Assert.True(entry.Contributes, name);
        Assert.True(count == read.Count, $"{name}: {read.Count} tools read");

        var last = read.Count > 0 ? read[^1] : null;
        Assert.True(id == last?.Id, $"{name}: id {last?.Id}");
        Assert.True(kind == Word(last?.Kind), $"{name}: kind {Word(last?.Kind)}");
        Assert.True(toolName == last?.Name, $"{name}: name {last?.Name}");
        Assert.True(command == last?.Command, $"{name}: command {last?.Command}");
        Assert.True(versions == last?.Versions, $"{name}: versions {last?.Versions}");
        Assert.True(checks == (last?.Ready.Count ?? 0), $"{name}: {last?.Ready.Count} checks");

        var said = last is null ? entry.Manifest.ToolsProblem : last.Problem;
        if (problem is null) Assert.True(said is null, $"{name}: {said}");
        else Assert.True(said?.Contains(problem, StringComparison.Ordinal) == true, $"{name}: {said}");
        if (last is not null) Assert.Null(entry.Manifest.ToolsProblem);
    }

    /// <summary>The design's own example (§7.2), read whole: every field a person is shown, as written.</summary>
    [Fact]
    public void The_designs_example_is_read_whole()
    {
        Plugin("acme.lands", """
            [
              { "id": "node", "versions": ">=22" },
              { "id": "git", "versions": ">=2.29" },
              {
                "id": "az",
                "versions": ">=2.60",
                "for": "Pushes the branch and opens its pull request.",
                "ready": [
                  { "run": ["az", "extension", "show", "--name", "azure-devops", "--output", "none"],
                    "says": "Its devops extension is added", "fix": "az extension add --name azure-devops" },
                  { "run": ["az", "account", "show", "--output", "none"], "says": "Signed in" }
                ]
              },
              { "id": "tf", "name": "Terraform", "command": "terraform", "versionArguments": ["version", "-json"] }
            ]
            """);

        var tools = Assert.Single(PluginCatalog.Load(_home).Plugins).Manifest.Tools;

        Assert.Equal(["node", "git", "az", "tf"], tools.Select(tool => tool.Id));
        Assert.Equal([PluginToolKind.Own, PluginToolKind.Own, PluginToolKind.Known, PluginToolKind.Other], tools.Select(tool => tool.Kind));
        Assert.All(tools, tool => Assert.Null(tool.Problem));

        var az = tools[2];
        Assert.Equal("Azure CLI", az.Name);
        Assert.Null(az.Command);
        Assert.Null(az.VersionArguments);
        Assert.Equal("Pushes the branch and opens its pull request.", az.For);
        Assert.Equal(["az", "extension", "show", "--name", "azure-devops", "--output", "none"], az.Ready[0].Run);
        Assert.Equal("Its devops extension is added", az.Ready[0].Says);
        Assert.Equal("az extension add --name azure-devops", az.Ready[0].Fix);
        Assert.Null(az.Ready[1].Fix);

        var tf = tools[3];
        Assert.Equal("Terraform", tf.Name);
        Assert.Equal("terraform", tf.Command);
        Assert.Equal(["version", "-json"], tf.VersionArguments);
        Assert.Null(tf.Versions);
        Assert.Empty(tf.Ready);
    }

    /// <summary>A refused plugin takes nothing it declares, its tools among them; read as written, they are there to show.</summary>
    [Fact]
    public void A_refused_plugin_takes_no_tools_and_its_manifest_as_written_keeps_them()
    {
        var folder = Path.Combine(_home, PluginCatalog.Folder, "acme.later");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), """
            { "id": "acme.later", "tools": [{ "id": "gh" }], "harnesses": [ { "name": "claude-code", "command": ["claude"] } ] }
            """);

        var refused = Assert.Single(PluginCatalog.Load(_home, AdapterSet.Built().Names).Plugins);
        var (written, problem) = PluginCatalog.ReadAsWritten("acme.later", Path.Combine(folder, PluginCatalog.ManifestName));

        Assert.NotNull(refused.Problem);
        Assert.Empty(refused.Manifest.Tools);
        Assert.Null(problem);
        Assert.Equal("gh", Assert.Single(written.Tools).Id);
    }

    /// <summary>Daoris's own tools are the three it runs itself (D150 point 7); the other two it knows, a plugin declares.</summary>
    [Fact]
    public void Daoris_runs_three_of_the_tools_it_knows()
    {
        Assert.Equal(["git", "node", "pwsh"], PluginTools.DaorisOwn);
        Assert.All(PluginTools.DaorisOwn, id => Assert.NotNull(Tools.Find(id)));
        Assert.Equal(["gh", "az"], Tools.Declared.Select(tool => tool.Id).Except(PluginTools.DaorisOwn));
        Assert.Empty(PluginManifest.Empty("acme.none").Tools);
    }

    /// <summary>
    /// A range (§7.2), as a trial reads it against the version a tool says: a floor, a floor and a top, or one exact
    /// version, compared number by number with a missing number as 0. [range, version, whether it holds, what it needs].
    /// </summary>
    [Theory]
    [InlineData(">=2.60", "2.66.0", true, "2.60 or newer")]
    [InlineData(">=2.60", "2.60", true, "2.60 or newer")]
    [InlineData(">=2.60", "2.60.0", true, "2.60 or newer")]
    [InlineData(">=2.60", "2.55.1", false, "2.60 or newer")]
    [InlineData(">=2.60", "2.9", false, "2.60 or newer")]
    [InlineData(">=2.9", "2.60", true, "2.9 or newer")]
    [InlineData(">=2.60 <3", "2.99.9", true, "2.60 or newer, below 3")]
    [InlineData(">=2.60 <3", "3.0.0", false, "2.60 or newer, below 3")]
    [InlineData(" >=2.60  <3 ", "2.70", true, "2.60 or newer, below 3")]
    [InlineData("22.11.0", "22.11.0", true, "exactly 22.11.0")]
    [InlineData("22.11", "22.11.0", true, "exactly 22.11")]
    [InlineData("22.11.0", "22.11.1", false, "exactly 22.11.0")]
    [InlineData(">=1", "0010.2", true, "1 or newer")]
    public void A_range_holds_a_version_number_by_number(string range, string version, bool holds, string needs)
    {
        var parsed = VersionRange.Parse(range);

        Assert.NotNull(parsed);
        Assert.Equal(holds, parsed.Holds(version));
        Assert.Equal(needs, parsed.Needs);
    }
}
