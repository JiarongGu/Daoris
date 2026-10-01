using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>$DAORIS_HOME/tools.json</c> and its resolution (TOOLS2, D121; the tools design §2.1–§2.3, §5): the
/// driver's half of a TWIN with the CLI's <c>tools.ts</c>, whose <c>tools.test.ts</c> holds the same tables,
/// row for row and in the same order. They share no code; a row changed here is changed there, in the same
/// commit.
/// </summary>
/// <remarks>
/// <para>The file's rules (§2.2), each a table below:</para>
/// <list type="number">
/// <item>No file, no entry, or <c>"use": "system"</c> is the system's.</item>
/// <item><c>managed</c> needs an exact version of one to four numbers; <c>file</c> needs a whole path. An
/// entry that names another way's field, or lacks its own, is refused whole, and its tool is never run
/// another way.</item>
/// <item>A tool id this build does not declare is kept as written and never applied.</item>
/// <item><c>git</c> holds only keys on the allow-list: off it, refused on a write; on a read kept, not
/// applied, said.</item>
/// <item><c>locations</c> holds only https://, or http:// to this machine: refused on a write; skipped and
/// said on a read.</item>
/// <item>Setting one way clears the others, and a writer keeps what it has no field for.</item>
/// </list>
/// <para>Then the resolution (§2.3): the way set decides which file starts, and a managed version nobody
/// downloaded, or a named file that is gone, refuses — never a fall back to <c>PATH</c>.</para>
/// <para>🔴 <b>The CLI reads these theories.</b> <c>tools.test.ts</c>'s <i>the driver's tables are these
/// tables</i> parses each <c>[InlineData]</c> row here and holds it to its own table, cell for cell and in
/// order, so a row changed on one side alone fails <c>npm run verify</c>. Keep each row on one line, its
/// cells literals.</para>
/// <para>Every case runs in a temporary directory, and nothing here starts a program: presence is a file on
/// disk.</para>
/// </remarks>
public sealed class ToolsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-tools-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>The placeholder a row writes where a whole path goes; each side spells its own.</summary>
    private const string Whole = "WHOLE";

    private string Home => Path.Combine(_root, "data");

    private string WholeFile => Path.Combine(_root, "bin", OperatingSystem.IsWindows() ? "git.exe" : "git");

    private string ToolsFile => Path.Combine(Home, Tools.FileName);

    public ToolsTests() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Write(string text) => File.WriteAllText(ToolsFile, text);

    private static string Escaped(string path) => JsonEncodedText.Encode(path).ToString();

    /// <summary>A path as Windows compares it: PATH's match is spelled with PATHEXT's extension, on both sides.</summary>
    private static string? Spelled(string? path) => path is not null && OperatingSystem.IsWindows() ? path.ToLowerInvariant() : path;

    private string MakeWholeFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(WholeFile)!);
        File.WriteAllText(WholeFile, "");
        return WholeFile;
    }

    private static string? WayText(ToolWay? way) => way switch
    {
        ToolWay.System => "system",
        ToolWay.Managed => "managed",
        ToolWay.File => "file",
        _ => null,
    };

    // ── The declared tools (§2.1) ──────────────────────────────────────────────────────────────────

    [Fact]
    public void The_tools_are_declared_in_code_in_the_order_a_childs_PATH_takes_them()
    {
        Assert.Equal(
            [
                "git Git [git] [--version] [core.sshCommand]",
                "node Node.js [node npm npx] [--version] []",
                "pwsh PowerShell [pwsh] [--version] []",
                "gh GitHub CLI [gh] [--version] []",
                "az Azure CLI [az] [version] []",
            ],
            Tools.Declared.Select(tool =>
                $"{tool.Id} {tool.Name} [{string.Join(' ', tool.Answers)}] [{string.Join(' ', tool.Version)}] [{string.Join(' ', tool.Settings)}]"));
        Assert.Equal(["core.sshCommand"], Tools.GitSettings);
        Assert.Equal("tools.json", Tools.FileName);
        Assert.Equal(["tools", "tool.json", "package"], new[] { Tools.Folder, Tools.Record, Tools.Package });
    }

    // ── Rules 1 and 2: one tool's entry. The CLI's `an entry reads as the driver reads it`, row for row ──

    [Theory]
    [InlineData("no file", null, "git", "system", null, null, null)]
    [InlineData("an empty object", "{}", "git", "system", null, null, null)]
    [InlineData("no entry", """{"tools":{}}""", "git", "system", null, null, null)]
    [InlineData("the system's, said", """{"tools":{"git":{"use":"system"}}}""", "git", "system", null, null, null)]
    [InlineData("a key it has no field for", """{"tools":{"git":{"use":"system","note":"mine"}}}""", "git", "system", null, null, null)]
    [InlineData("managed at three numbers", """{"tools":{"git":{"use":"managed","version":"2.51.0"}}}""", "git", "managed", "2.51.0", null, null)]
    [InlineData("managed at four numbers", """{"tools":{"git":{"use":"managed","version":"2.51.0.2"}}}""", "git", "managed", "2.51.0.2", null, null)]
    [InlineData("managed at one number", """{"tools":{"git":{"use":"managed","version":"2"}}}""", "git", "managed", "2", null, null)]
    [InlineData("a file", """{"tools":{"git":{"use":"file","file":"WHOLE"}}}""", "git", "file", null, Whole, null)]
    [InlineData("another tool's entry", """{"tools":{"gh":{"use":"managed"}}}""", "git", "system", null, null, null)]
    [InlineData("not an object", """{"tools":{"git":"managed"}}""", "git", null, null, null, "it is not an object")]
    [InlineData("no way", """{"tools":{"git":{"version":"2.51.0"}}}""", "git", null, null, null, "it names no way")]
    [InlineData("a way nobody declared", """{"tools":{"git":{"use":"portable"}}}""", "git", null, null, null, "`use` is `portable`, not system, managed or file")]
    [InlineData("a way in the wrong case", """{"tools":{"git":{"use":"System"}}}""", "git", null, null, null, "`use` is `System`, not system, managed or file")]
    [InlineData("managed with no version", """{"tools":{"git":{"use":"managed"}}}""", "git", null, null, null, "managed needs a `version`")]
    [InlineData("managed at a word", """{"tools":{"git":{"use":"managed","version":"latest"}}}""", "git", null, null, null, "`latest` is not an exact version")]
    [InlineData("managed at five numbers", """{"tools":{"git":{"use":"managed","version":"2.51.0.1.1"}}}""", "git", null, null, null, "`2.51.0.1.1` is not an exact version")]
    [InlineData("managed at a tag", """{"tools":{"git":{"use":"managed","version":"v2.51.0"}}}""", "git", null, null, null, "`v2.51.0` is not an exact version")]
    [InlineData("managed at a number", """{"tools":{"git":{"use":"managed","version":2.51}}}""", "git", null, null, null, "its `version` is not text")]
    [InlineData("managed and a file", """{"tools":{"git":{"use":"managed","version":"2.51.0","file":"WHOLE"}}}""", "git", null, null, null, "it is managed, and names a file too")]
    [InlineData("a file and a version", """{"tools":{"git":{"use":"file","file":"WHOLE","version":"2.51.0"}}}""", "git", null, null, null, "it is a file, and names a version too")]
    [InlineData("the system's and a version", """{"tools":{"git":{"use":"system","version":"2.51.0"}}}""", "git", null, null, null, "it is the system's, and names a version too")]
    [InlineData("the system's and a file", """{"tools":{"git":{"use":"system","file":"WHOLE"}}}""", "git", null, null, null, "it is the system's, and names a file too")]
    [InlineData("a file with no path", """{"tools":{"git":{"use":"file"}}}""", "git", null, null, null, "a file needs its `file`")]
    [InlineData("a file that is not text", """{"tools":{"git":{"use":"file","file":7}}}""", "git", null, null, null, "its `file` is not text")]
    [InlineData("a file that is not whole", """{"tools":{"git":{"use":"file","file":"bin/git"}}}""", "git", null, null, null, "`bin/git` is not a whole path")]
    [InlineData("a file on a drive with no root", """{"tools":{"git":{"use":"file","file":"C:git.exe"}}}""", "git", null, null, null, "`C:git.exe` is not a whole path")]
    [InlineData("a file that is not JSON", "not json", "git", null, null, null, "is not readable JSON")]
    [InlineData("a file that is a list", "[]", "git", null, null, null, "is not a JSON object")]
    [InlineData("tools that are a list", """{"tools":[]}""", "git", null, null, null, "`tools` is not an object")]
    public void An_entry_reads_as_the_cli_reads_it(
        string name, string? text, string tool, string? way, string? version, string? file, string? problem)
    {
        if (text is not null) Write(text.Replace(Whole, Escaped(WholeFile)));

        var entry = Tools.Read(Home).Entries[tool];

        Assert.True(way == WayText(entry.Way), $"{name}: {WayText(entry.Way)}");
        Assert.Equal(version, entry.Version);
        Assert.Equal(file == Whole ? WholeFile : file, entry.File);
        if (problem is null) Assert.True(entry.Problem is null, $"{name}: {entry.Problem}");
        else Assert.True(entry.Problem?.Contains(problem, StringComparison.Ordinal) == true, $"{name}: {entry.Problem}");
    }

    [Fact]
    public void A_refused_entry_names_the_file_and_the_tool_and_says_it_is_never_run_another_way()
    {
        Write("""{"tools":{"git":{"use":"managed"}}}""");

        Assert.Equal(
            $"the entry for `git` in {ToolsFile} does not read (managed needs a `version`, one to four numbers), "
            + "and Git is never run another way: fix the entry, or `daoris tool use git system` writes a new one",
            Tools.Read(Home).Entries["git"].Problem);
    }

    [Fact]
    public void No_file_is_every_tool_the_systems_and_reading_it_creates_nothing()
    {
        var read = Tools.Read(Home);

        Assert.False(read.Exists);
        Assert.Null(read.Problem);
        Assert.Equal(Tools.Declared.Select(tool => tool.Id), read.Entries.Keys);
        Assert.All(read.Entries.Values, entry => Assert.Equal(new ToolEntry(ToolWay.System, null, null, null), entry));
        Assert.Empty(read.Unknown);
        Assert.Empty(read.Git);
        Assert.Empty(read.Locations);
        Assert.Empty(read.Notes);
        Assert.False(File.Exists(ToolsFile));
        Assert.False(Directory.Exists(Path.Combine(Home, Tools.Folder)));
    }

    // ── Rule 2's whole path: .NET's own `Path.IsPathFullyQualified` ────────────────────────────────

    [Theory]
    [InlineData(@"C:\Tools\git.exe", true, false)]
    [InlineData("C:/Tools/git.exe", true, false)]
    [InlineData(@"\\server\share\git.exe", true, false)]
    [InlineData("/usr/bin/git", false, true)]
    [InlineData("C:git.exe", false, false)]
    [InlineData("bin/git", false, false)]
    [InlineData("git", false, false)]
    [InlineData("", false, false)]
    public void A_whole_path_is_one_dotnet_calls_fully_qualified(string path, bool onWindows, bool elsewhere)
    {
        Assert.Equal(OperatingSystem.IsWindows() ? onWindows : elsewhere, Tools.IsWholePath(path));
    }

    // ── Rule 3: a tool this build does not declare ─────────────────────────────────────────────────

    [Fact]
    public void An_undeclared_tool_is_kept_and_never_applied()
    {
        Write("""{"tools":{"bun":{"use":"file","file":"anything"},"git":{"use":"system"}}}""");

        var read = Tools.Read(Home);

        Assert.Equal(["bun"], read.Unknown);
        Assert.False(read.Entries.ContainsKey("bun"));
        Assert.Equal(["`bun` in `tools` is not a tool this build runs: it is kept as written, and never applied"], read.Notes);
        Assert.Contains("PATH", Tools.Resolve(Home, "git", path: "").Problem);
    }

    // ── Rule 4: what Daoris's git carries. The CLI's `git’s settings read as the driver reads them` ──

    public static TheoryData<string, string, Dictionary<string, string>, string[]> GitRows => new()
    {
        { "the one key", """{"core.sshCommand":"C:/Windows/System32/OpenSSH/ssh.exe"}""", new() { ["core.sshCommand"] = "C:/Windows/System32/OpenSSH/ssh.exe" }, [] },
        { "a key off the list", """{"core.sshCommand":"ssh","credential.helper":"manager"}""", new() { ["core.sshCommand"] = "ssh" },
            ["`credential.helper` in `git` is not a setting Daoris's git carries: it is kept, and not applied"] },
        { "a key that is not text", """{"core.sshCommand":42}""", new(), ["`core.sshCommand` in `git` is not text: it is kept, and not applied"] },
        { "not an object", "\"core.sshCommand\"", new(), ["`git` is not an object: it is kept, and nothing in it is applied"] },
    };

    [Theory]
    [MemberData(nameof(GitRows))]
    public void Git_settings_read_as_the_cli_reads_them(string name, string git, Dictionary<string, string> applied, string[] notes)
    {
        Write("{\"git\":" + git + "}");

        var read = Tools.Read(Home);

        Assert.Equal(applied, read.Git);
        Assert.Equal(notes, read.Notes);
        Assert.True(read.Entries["git"].Way == ToolWay.System, $"{name}: a setting is not a way");
    }

    [Theory]
    [InlineData("credential.helper")]
    [InlineData("core.sshcommand")]
    [InlineData("core.autocrlf")]
    [InlineData("")]
    public void A_key_off_the_allow_list_is_refused_on_a_write(string key)
    {
        Assert.Null(Tools.GitKeyProblem("core.sshCommand"));
        Assert.Equal($"`{key}` is not a setting Daoris's git carries — the one it carries is core.sshCommand", Tools.GitKeyProblem(key));
    }

    // ── Rule 5: where versions come from. The CLI's `a location is judged as the driver judges it` ──

    [Theory]
    [InlineData("https://example.org/daoris/resources.json", true)]
    [InlineData("HTTPS://EXAMPLE.ORG/resources.json", true)]
    [InlineData("http://localhost:8080/resources.json", true)]
    [InlineData("http://127.0.0.1/resources.json", true)]
    [InlineData("http://[::1]:5177/resources.json", true)]
    [InlineData("http://example.org/resources.json", false)]
    [InlineData("http://localhost.example.com/resources.json", false)]
    [InlineData("http://127.0.0.2/resources.json", false)]
    [InlineData("ftp://example.org/resources.json", false)]
    [InlineData("file:///C:/resources.json", false)]
    [InlineData("https://", false)]
    [InlineData("not an address", false)]
    [InlineData("", false)]
    public void A_location_is_judged_as_the_cli_judges_it(string address, bool fine)
    {
        Assert.Equal(
            fine ? null
                : $"`{address}` is not a resource location — an address is https://, or http:// to this machine "
                  + "(localhost, 127.0.0.1 or [::1])",
            Tools.LocationProblem(address));
    }

    [Fact]
    public void Locations_read_in_order_and_one_that_is_not_an_address_is_skipped_and_said()
    {
        Write("""{"locations":["https://example.org/r.json","http://example.org/r.json",7,"http://localhost:8080/r.json"]}""");
        var read = Tools.Read(Home);

        Assert.Equal(["https://example.org/r.json", "http://localhost:8080/r.json"], read.Locations);
        Assert.Equal(
            [
                "`http://example.org/r.json` in `locations` is skipped: an address is https://, or http:// to this machine",
                "an entry in `locations` is not text, and is skipped",
            ],
            read.Notes);

        Write("""{"locations":"https://example.org/r.json"}""");
        read = Tools.Read(Home);

        Assert.Empty(read.Locations);
        Assert.Equal(["`locations` is not a list: it is kept, and no location is read"], read.Notes);
    }

    // ── The resolution (§2.3). The CLI's `a tool resolves as the driver resolves it`, row for row ────

    /// <remarks>
    /// Every row is <c>gh</c>. <paramref name="record"/> is the managed folder: null for none, empty for a
    /// folder with no record, else the record's text. <paramref name="atFile"/> is what is at FILE: none,
    /// file or folder. <paramref name="resolves"/> is PATH, MANAGED, FILE or null.
    /// </remarks>
    [Theory]
    [InlineData("no file, on PATH", null, null, false, "none", true, "PATH", false, null)]
    [InlineData("no file, not on PATH", null, null, false, "none", false, null, false, "`gh` is not on this machine's PATH")]
    [InlineData("the system's, on PATH", """{"tools":{"gh":{"use":"system"}}}""", null, false, "none", true, "PATH", false, null)]
    [InlineData("managed, downloaded", """{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""", """{"exe":"gh_2.62.0/bin/gh.exe"}""", true, "none", true, "MANAGED", false, null)]
    [InlineData("managed, not downloaded", """{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""", null, false, "none", true, null, true, "that version is not downloaded")]
    [InlineData("managed, a folder with no record", """{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""", "", false, "none", true, null, true, "that version is not downloaded")]
    [InlineData("managed, a record that is not JSON", """{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""", "not json", false, "none", true, null, true, "is not readable JSON")]
    [InlineData("managed, a record naming no executable", """{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""", "{}", false, "none", true, null, true, "it names no `exe`")]
    [InlineData("managed, a record that climbs out", """{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""", """{"exe":"../gh.exe"}""", false, "none", true, null, true, "its `exe` is not a relative path inside the package")]
    [InlineData("managed, a record with a backslash", """{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""", """{"exe":"bin\\gh.exe"}""", false, "none", true, null, true, "its `exe` is not a relative path inside the package")]
    [InlineData("managed, the executable gone", """{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""", """{"exe":"gh_2.62.0/bin/gh.exe"}""", false, "none", true, null, true, "and there is no file there")]
    [InlineData("a file, there", """{"tools":{"gh":{"use":"file","file":"FILE"}}}""", null, false, "file", true, "FILE", false, null)]
    [InlineData("a file, gone", """{"tools":{"gh":{"use":"file","file":"FILE"}}}""", null, false, "none", true, null, true, "and there is no file there")]
    [InlineData("a file that is a folder", """{"tools":{"gh":{"use":"file","file":"FILE"}}}""", null, false, "folder", true, null, true, "and there is no file there")]
    [InlineData("an entry that does not read", """{"tools":{"gh":{"use":"managed"}}}""", null, false, "none", true, null, true, "does not read")]
    [InlineData("a file that does not read", "not json", null, false, "none", true, null, true, "is not readable JSON")]
    public void A_tool_resolves_as_the_cli_resolves_it(
        string name, string? text, string? record, bool exe, string atFile, bool onPath, string? resolves, bool refused, string? fragment)
    {
        var pathDir = Path.Combine(_root, "path");
        Directory.CreateDirectory(pathDir);
        var onPathFile = Path.Combine(pathDir, OperatingSystem.IsWindows() ? "gh.exe" : "gh");
        if (onPath) File.WriteAllText(onPathFile, "");
        var named = Path.Combine(_root, "named", OperatingSystem.IsWindows() ? "gh.exe" : "gh");
        if (atFile == "file")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(named)!);
            File.WriteAllText(named, "");
        }
        else if (atFile == "folder")
        {
            Directory.CreateDirectory(named);
        }

        var version = Path.Combine(Home, Tools.Folder, "gh", "2.62.0");
        var managed = Path.Combine(version, Tools.Package, "gh_2.62.0", "bin", "gh.exe");
        if (record is not null)
        {
            Directory.CreateDirectory(version);
            if (record != "") File.WriteAllText(Path.Combine(version, Tools.Record), record);
        }

        if (exe)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(managed)!);
            File.WriteAllText(managed, "");
        }

        if (text is not null) Write(text.Replace("FILE", Escaped(named)));

        var resolution = Tools.Resolve(Home, "gh", path: pathDir);
        var expected = resolves switch { "PATH" => onPathFile, "MANAGED" => managed, "FILE" => named, _ => null };

        Assert.True(Spelled(expected) == Spelled(resolution.File), $"{name}: {resolution.File}");
        Assert.True(refused == resolution.Refused, name);
        if (fragment is null) Assert.True(resolution.Problem is null, $"{name}: {resolution.Problem}");
        else Assert.True(resolution.Problem?.Contains(fragment, StringComparison.Ordinal) == true, $"{name}: {resolution.Problem}");
        if (refused) Assert.True(resolution.Problem?.Contains("never", StringComparison.Ordinal) == true, $"{name} says it is never run another way: {resolution.Problem}");
    }

    [Fact]
    public void A_version_nobody_downloaded_refuses_names_the_download_and_the_way_back_to_PATH_and_never_guesses()
    {
        Write("""{"tools":{"git":{"use":"managed","version":"2.51.0"}}}""");

        Assert.Equal(
            new ToolResolution("git", ToolWay.Managed, "2.51.0", null, true,
                $"Git is managed at 2.51.0, and that version is not downloaded ({Path.Combine(Home, Tools.Folder, "git", "2.51.0")}) "
                + "— it never falls back to PATH. `daoris tool use git managed 2.51.0` downloads it, and `daoris tool use git system` "
                + "runs the one on PATH"),
            Tools.Resolve(Home, "git", path: ""));
    }

    [Fact]
    public void A_named_file_that_is_gone_refuses_naming_the_file()
    {
        Write("""{"tools":{"node":{"use":"file","file":"FILE"}}}""".Replace("FILE", Escaped(WholeFile)));

        Assert.Equal(
            $"Node.js runs the file {WholeFile}, and there is no file there — it never falls back to PATH. "
            + "`daoris tool use node file <path>` names another, and `daoris tool use node system` runs the one on PATH",
            Tools.Resolve(Home, "node", path: "").Problem);
    }

    [Fact]
    public void A_tool_PATH_does_not_find_is_said_naming_the_three_ways_and_is_not_a_refusal()
    {
        Assert.Equal(
            new ToolResolution("az", ToolWay.System, null, null, false,
                "`az` is not on this machine's PATH. A tool is run as the system's, managed, or from a file you name: "
                + "`daoris tool use az file <path>` names one"),
            Tools.Resolve(Home, "az", path: ""));
    }

    // ── Rule 6: a write. The CLI's `setting one way clears the others…`, step for step ─────────────

    private JsonNode Parsed() => JsonNode.Parse(File.ReadAllText(ToolsFile))!;

    private static void Same(string expected, JsonNode actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), actual), actual.ToJsonString());

    [Fact]
    public void A_write_sets_one_way_and_keeps_the_rest()
    {
        var whole = MakeWholeFile();

        Tools.UseSystem(Home, "git");
        Same("""{"tools":{"git":{"use":"system"}}}""", Parsed());

        Write("""
            {"note":"mine",
             "tools":{"git":{"use":"managed","version":"2.51.0","keep":"this"},"bun":{"use":"system"},"gh":{"use":"managed"}},
             "git":{"core.sshCommand":"ssh","credential.helper":"manager"},
             "locations":["https://example.org/r.json","ftp://kept.example/r.json"]}
            """);
        Tools.UseFile(Home, "git", whole);
        Same("""
            {"note":"mine",
             "tools":{"git":{"use":"file","keep":"this","file":"FILE"},"bun":{"use":"system"},"gh":{"use":"managed"}},
             "git":{"core.sshCommand":"ssh","credential.helper":"manager"},
             "locations":["https://example.org/r.json","ftp://kept.example/r.json"]}
            """.Replace("FILE", Escaped(whole)), Parsed());
        Assert.EndsWith("}\n", File.ReadAllText(ToolsFile));

        Tools.UseSystem(Home, "git");
        Same("""{"use":"system","keep":"this"}""", Parsed()["tools"]!["git"]!);

        Write("""{"tools":{"git":"managed"}}""");
        Tools.UseSystem(Home, "git");
        Same("""{"tools":{"git":{"use":"system"}}}""", Parsed());
    }

    /// <summary>The CLI's <c>homefiles.test.ts</c> row for <c>tools.json</c>, step for step.</summary>
    [Fact]
    public void A_BOM_reads_and_a_write_over_a_torn_file_is_refused_with_the_file_untouched()
    {
        Write("﻿{ \"tools\": { \"git\": { \"use\": \"managed\", \"version\": \"2.51.0\" } } }");
        Assert.Equal(new ToolEntry(ToolWay.Managed, "2.51.0", null, null), Tools.Read(Home).Entries["git"]);
        Tools.UseSystem(Home, "node");
        Assert.Equal("2.51.0", Tools.Read(Home).Entries["git"].Version);

        const string torn = """{ "tools": { "git": { "use": "managed", "version": "2.51.0" } }, }""";
        Write(torn);
        var error = Assert.Throws<DriverException>(() => Tools.UseSystem(Home, "node"));

        Assert.Contains("nothing was written", error.Message);
        Assert.Equal(torn, File.ReadAllText(ToolsFile));
    }

    [Theory]
    [InlineData("a file that is not JSON", "not json", "system", "git", null, "is not readable JSON")]
    [InlineData("tools that are a list", """{"tools":[]}""", "system", "git", null, "`tools` is not an object")]
    [InlineData("a tool nobody declared", "{}", "system", "bun", null, "`bun` is not a tool this build runs — one of: git, node, pwsh, gh, az")]
    [InlineData("a file that is not whole", "{}", "file", "git", "bin/git", "`bin/git` is not a whole path")]
    [InlineData("a file that is not there", "{}", "file", "git", "GONE", "no file at")]
    public void A_write_that_cannot_be_made_writes_nothing(string name, string before, string way, string tool, string? file, string fragment)
    {
        var whole = MakeWholeFile();
        Write(before);

        var error = Assert.Throws<DriverException>(() =>
        {
            if (way == "system") Tools.UseSystem(Home, tool);
            else Tools.UseFile(Home, tool, file == "GONE" ? whole + ".gone" : file!);
        });

        Assert.True(error.Message.Contains(fragment, StringComparison.Ordinal), $"{name}: {error.Message}");
        Assert.Equal(before, File.ReadAllText(ToolsFile));
    }

    // ── Managed, written (TOOLS4, rule 6): only over a version that is downloaded. The CLI's `managed is written…` ──

    /// <summary>A downloaded version as the resolution finds it: its record, naming an executable that is there.</summary>
    private void Downloaded(string tool, string version)
    {
        var folder = Path.Combine(Home, Tools.Folder, tool, version);
        Directory.CreateDirectory(Path.Combine(folder, Tools.Package, "bin"));
        File.WriteAllText(Path.Combine(folder, Tools.Package, "bin", $"{tool}.exe"), tool);
        File.WriteAllText(Path.Combine(folder, Tools.Record), $$"""{"exe":"bin/{{tool}}.exe"}""");
    }

    private string? Text() => File.Exists(ToolsFile) ? File.ReadAllText(ToolsFile) : null;

    [Theory]
    [InlineData("a version downloaded", null, "2.62.0", "2.62.0", """{"use":"managed","version":"2.62.0"}""", null)]
    [InlineData("a version not downloaded", null, null, "2.62.0", null, "is not downloaded")]
    [InlineData("another version downloaded", null, "2.61.0", "2.62.0", null, "is not downloaded")]
    [InlineData("a version that is not exact", null, null, "latest", null, "`latest` is not an exact version")]
    [InlineData("a version that climbs out", null, null, "../2.62.0", null, "`../2.62.0` is not an exact version")]
    [InlineData("over a file it names", """{"tools":{"gh":{"use":"file","file":"FILE","keep":"this"}}}""", "2.62.0", "2.62.0", """{"use":"managed","keep":"this","version":"2.62.0"}""", null)]
    [InlineData("over a file that does not read", "not json", "2.62.0", "2.62.0", null, "is not readable JSON")]
    public void Managed_is_written_as_the_cli_writes_it(string name, string? before, string? has, string version, string? after, string? refusal)
    {
        if (before is not null) Write(before.Replace("FILE", Escaped(WholeFile)));
        if (has is not null) Downloaded("gh", has);
        var was = Text();

        if (refusal is null)
        {
            Tools.UseManaged(Home, "gh", version);
            Same(after!, Parsed()["tools"]!["gh"]!);
            Assert.Equal(Path.Combine(Home, Tools.Folder, "gh", version, Tools.Package, "bin", "gh.exe"), Tools.Resolve(Home, "gh", path: "").File);
        }
        else
        {
            var error = Assert.Throws<DriverException>(() => Tools.UseManaged(Home, "gh", version));
            Assert.True(error.Message.Contains(refusal, StringComparison.Ordinal), $"{name}: {error.Message}");
            Assert.Equal(was, Text());
        }
    }

    // ── Locations, written (rule 5's write side, TOOLS4). The CLI's `a location is added or removed…` ──────────────

    [Theory]
    [InlineData("add to no file", null, "add", "https://a.example/r.json", """["https://a.example/r.json"]""", true, null)]
    [InlineData("add after another", """{"locations":["https://a.example/r.json"]}""", "add", "http://localhost:8080/r.json", """["https://a.example/r.json","http://localhost:8080/r.json"]""", true, null)]
    [InlineData("add one already listed", """{"locations":["https://a.example/r.json"]}""", "add", "https://a.example/r.json", """["https://a.example/r.json"]""", false, null)]
    [InlineData("add over http to another host", null, "add", "http://a.example/r.json", null, false, "is not a resource location")]
    [InlineData("add over locations that are not a list", """{"locations":"https://a.example/r.json"}""", "add", "https://b.example/r.json", null, false, "`locations` is not a list")]
    [InlineData("add over a file that does not read", "not json", "add", "https://a.example/r.json", null, false, "is not readable JSON")]
    [InlineData("remove one listed", """{"locations":["https://a.example/r.json","https://b.example/r.json"]}""", "remove", "https://a.example/r.json", """["https://b.example/r.json"]""", true, null)]
    [InlineData("remove one not listed", """{"locations":["https://a.example/r.json"]}""", "remove", "https://b.example/r.json", """["https://a.example/r.json"]""", false, null)]
    [InlineData("remove beside one that is not an address", """{"locations":["ftp://kept.example/r.json","https://a.example/r.json"]}""", "remove", "https://a.example/r.json", """["ftp://kept.example/r.json"]""", true, null)]
    [InlineData("remove from no file", null, "remove", "https://a.example/r.json", null, false, null)]
    public void A_location_is_written_as_the_cli_writes_it(
        string name, string? before, string verb, string address, string? after, bool changed, string? refusal)
    {
        if (before is not null) Write(before);
        bool Change() => verb == "add" ? Tools.AddLocation(Home, address) : Tools.RemoveLocation(Home, address);

        if (refusal is null)
        {
            Assert.True(changed == Change(), name);
            var written = File.Exists(ToolsFile) ? Parsed()["locations"] : null;
            if (after is null) Assert.Null(written);
            else Same(after, written!);
        }
        else
        {
            var error = Assert.Throws<DriverException>(() => Change());
            Assert.True(error.Message.Contains(refusal, StringComparison.Ordinal), $"{name}: {error.Message}");
            Assert.Equal(before, Text());
        }
    }

    [Fact]
    public void A_location_written_keeps_every_key_the_writer_has_no_field_for()
    {
        Write("""{"note":"mine","tools":{"git":{"use":"system"}},"git":{"core.sshCommand":"ssh"}}""");
        Tools.AddLocation(Home, "https://a.example/r.json");
        Same("""{"note":"mine","tools":{"git":{"use":"system"}},"git":{"core.sshCommand":"ssh"},"locations":["https://a.example/r.json"]}""", Parsed());
    }
}
