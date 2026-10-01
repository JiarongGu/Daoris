using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One answer for Daoris and every child (TOOLS5, D121; the tools design §2.4, §2.6, §2.7): the driver's half of
/// a TWIN with the CLI's <c>tools.ts</c>, whose <c>tools-children.test.ts</c> holds the same tables, row for row and
/// in the same order, and parses these theories to hold them to its own.
/// </summary>
/// <remarks>
/// <para><b>The environment</b>: a child's <c>PATH</c> is each tool that is managed or a named file, in the declared
/// order, its folders first, then the <c>PATH</c> it inherited; with every tool the system's it is the inherited one
/// exactly. <b>A command's first word</b>: a name a tool answers for is that tool's, by its way, and never falls
/// back to <c>PATH</c>; any other name is the caller's own resolver's.</para>
/// <para>Every case runs in a temporary directory, and nothing here starts a program: the files are empty, and
/// presence is a file on disk. The real start is <c>ToolsChildProcessTests</c>, in the Process half.</para>
/// <para>🔴 <b>Keep each row on one line, its cells literals</b>: the CLI's test reads them.</para>
/// </remarks>
public sealed class ToolsChildrenTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-tools-children-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    private string ToolsFile => Path.Combine(Home, Tools.FileName);

    private static readonly bool Windows = OperatingSystem.IsWindows();

    /// <summary>
    /// What the fixture lays out under the home, the same on both sides: each tool downloaded at one version, its
    /// record naming its executable and, for some, the folders it puts on a child's PATH.
    /// </summary>
    private static readonly (string Tool, string Version, string Exe, string? Paths)[] Laid =
    [
        ("git", "2.51.0", "cmd/git.exe", """["cmd","mingw64/bin"]"""),
        ("node", "22.20.0", "node-v22.20.0-win-x64/node.exe", null),
        ("pwsh", "7.5.3", "pwsh.exe", """["."]"""),
        ("gh", "2.62.0", "bin/gh.exe", null),
        ("az", "2.77.0", "bin/az.cmd", """["bin"]"""),
    ];

    public ToolsChildrenTests()
    {
        Directory.CreateDirectory(Home);
        foreach (var (tool, version, exe, paths) in Laid)
        {
            var folder = Path.Combine(Home, Tools.Folder, tool, version);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, Tools.Record), paths is null ? $$"""{"exe":"{{exe}}"}""" : $$"""{"exe":"{{exe}}","paths":{{paths}}}""");
            Touch(Path.Combine([folder, Tools.Package, .. exe.Split('/')]));
        }

        // npm and npx beside the managed node, as its archive carries them; and beside the named node.
        var managedNode = Path.Combine(Home, Tools.Folder, "node", "22.20.0", Tools.Package, "node-v22.20.0-win-x64");
        Touch(Path.Combine(managedNode, Program("npm")));
        Touch(Path.Combine(managedNode, Program("npx")));
        foreach (var tool in Tools.Declared) Touch(Named(tool.Id));
        Touch(Path.Combine(_root, "named", "node", Program("npm")));
        Touch(Path.Combine(_root, "named", "node", Program("npx")));
        Touch(Path.Combine(_root, "named", "lonely", Program("node")));

        // The inherited PATH: node and npm in the first folder, nothing in the second.
        Touch(Path.Combine(_root, "path", Program("node")));
        Touch(Path.Combine(_root, "path", Program("npm")));
        Directory.CreateDirectory(Path.Combine(_root, "more"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static void Touch(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "");
    }

    /// <summary>A program's file name on this platform: what PATHEXT finds on Windows, the bare name elsewhere.</summary>
    private static string Program(string name) => !Windows ? name : name is "npm" or "npx" ? name + ".cmd" : name + ".exe";

    /// <summary>A file the person names for a tool: <c>&lt;root&gt;/named/&lt;tool&gt;/&lt;tool&gt;</c>.</summary>
    private string Named(string tool) => Path.Combine(_root, "named", tool, Program(tool));

    private string Inherited(string? cell) => cell switch
    {
        "two" => Path.Combine(_root, "path") + Path.PathSeparator + Path.Combine(_root, "more"),
        "empty" => "",
        _ => null!,
    };

    /// <summary>The file's text, with <c>@tool</c> read as the named file for that tool (and <c>@gone</c> as one that is not there).</summary>
    private void Write(string? json)
    {
        if (json is null) return;
        var text = json;
        string[] names = ["gone", "lonely", .. Tools.Declared.Select(tool => tool.Id)];
        foreach (var name in names)
        {
            var file = name switch
            {
                "gone" => Path.Combine(_root, "named", "gone", Program("gh")),
                "lonely" => Path.Combine(_root, "named", "lonely", Program("node")),
                _ => Named(name),
            };
            text = text.Replace($"\"@{name}\"", JsonSerializer.Serialize(file));
        }

        File.WriteAllText(ToolsFile, text);
    }

    /// <summary>A folder token: <c>managed:&lt;tool&gt;/&lt;path&gt;</c>, <c>file:&lt;tool&gt;</c>, or <c>inherited</c>.</summary>
    private string Folder(string token, string? inherited)
    {
        if (token == "inherited") return inherited!;
        if (token.StartsWith("file:", StringComparison.Ordinal)) return Path.GetDirectoryName(Named(token[5..]))!;
        var (tool, path) = (token[8..token.IndexOf('/')], token[(token.IndexOf('/') + 1)..]);
        var version = Laid.Single(each => each.Tool == tool).Version;
        var package = Path.Combine(Home, Tools.Folder, tool, version, Tools.Package);
        return path == "." ? package : Path.Combine([package, .. path.Split('/')]);
    }

    private static string? Spelled(string? path) => path is not null && Windows ? path.ToLowerInvariant() : path;

    // ── The environment (§2.4) ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("no file", null, "two", "unchanged")]
    [InlineData("every tool the system's", """{"tools":{"git":{"use":"system"},"node":{"use":"system"},"pwsh":{"use":"system"},"gh":{"use":"system"},"az":{"use":"system"}}}""", "two", "unchanged")]
    [InlineData("git managed: the record's folders, in its order", """{"tools":{"git":{"use":"managed","version":"2.51.0"}}}""", "two", "managed:git/cmd managed:git/mingw64/bin inherited")]
    [InlineData("node managed: the folder its executable is in, when the record names none", """{"tools":{"node":{"use":"managed","version":"22.20.0"}}}""", "two", "managed:node/node-v22.20.0-win-x64 inherited")]
    [InlineData("pwsh managed: a dot is the package itself", """{"tools":{"pwsh":{"use":"managed","version":"7.5.3"}}}""", "two", "managed:pwsh/. inherited")]
    [InlineData("gh a named file: its folder", """{"tools":{"gh":{"use":"file","file":"@gh"}}}""", "two", "file:gh inherited")]
    [InlineData("every way at once: the declared order, whatever the file's", """{"tools":{"az":{"use":"managed","version":"2.77.0"},"gh":{"use":"file","file":"@gh"},"pwsh":{"use":"system"},"node":{"use":"file","file":"@node"},"git":{"use":"managed","version":"2.51.0"}}}""", "two", "managed:git/cmd managed:git/mingw64/bin file:node file:gh managed:az/bin inherited")]
    [InlineData("a version nobody downloaded puts nothing first", """{"tools":{"git":{"use":"managed","version":"9.9.9"}}}""", "two", "unchanged")]
    [InlineData("a named file that is gone puts nothing first", """{"tools":{"gh":{"use":"file","file":"@gone"}}}""", "two", "unchanged")]
    [InlineData("an entry that does not read puts nothing first", """{"tools":{"git":{"use":"managed"}}}""", "two", "unchanged")]
    [InlineData("a file that does not read puts nothing first", "not json", "two", "unchanged")]
    [InlineData("an empty inherited PATH adds no empty folder", """{"tools":{"git":{"use":"managed","version":"2.51.0"}}}""", "empty", "managed:git/cmd managed:git/mingw64/bin")]
    [InlineData("no inherited PATH", """{"tools":{"git":{"use":"managed","version":"2.51.0"}}}""", null, "managed:git/cmd managed:git/mingw64/bin")]
    public void A_childs_PATH_is_the_tools_folders_then_what_it_inherited(string name, string? json, string? inherited, string expected)
    {
        Write(json);
        var from = Inherited(inherited);

        var path = Tools.ChildPath(Tools.Read(Home), Home, from);

        var wanted = expected == "unchanged"
            ? null
            : string.Join(Path.PathSeparator, expected.Split(' ').Select(token => Folder(token, from)));
        Assert.True(Spelled(wanted) == Spelled(path), $"{name}: {path}");
        var variables = Tools.ChildEnvironment(Tools.Read(Home), Home, from);
        if (wanted is null) Assert.Empty(variables);
        else Assert.Equal([Tools.PathVariable], variables.Keys);
    }

    [Fact]
    public void With_every_tool_the_systems_a_start_is_handed_the_environment_it_inherited_byte_for_byte()
    {
        var info = new ProcessStartInfo("x");
        var before = info.Environment.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        Tools.Hand(info, Home);
        Tools.Hand(info, home: null);

        Assert.Equal(before, info.Environment.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    [Fact]
    public void A_start_is_handed_the_tools_PATH_over_the_one_it_holds_and_nothing_else_changes()
    {
        Write("""{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""");
        var info = new ProcessStartInfo("x");
        info.Environment[Tools.PathVariable] = Inherited("two");
        var before = info.Environment.Where(pair => !string.Equals(pair.Key, Tools.PathVariable, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        Tools.Hand(info, Home);

        Assert.Equal(Folder("managed:gh/bin", null) + Path.PathSeparator + Inherited("two"), info.Environment[Tools.PathVariable]);
        Assert.Equal(before, info.Environment.Where(pair => !string.Equals(pair.Key, Tools.PathVariable, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    // ── A command's first word (§2.4, §2.7) ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("a name no tool answers for is the caller's", null, "python", "not a tool")]
    [InlineData("node, the system's: the one PATH finds", null, "node", "path")]
    [InlineData("npm, the system's: the one PATH finds", null, "npm", "path")]
    [InlineData("npx, the system's, not on PATH: said, not refused", null, "npx", "none")]
    [InlineData("node managed: its record's executable", """{"tools":{"node":{"use":"managed","version":"22.20.0"}}}""", "node", "managed")]
    [InlineData("npm, node managed: the npm beside it", """{"tools":{"node":{"use":"managed","version":"22.20.0"}}}""", "npm", "managed")]
    [InlineData("npx, node managed: the npx beside it", """{"tools":{"node":{"use":"managed","version":"22.20.0"}}}""", "npx", "managed")]
    [InlineData("npm, node a named file: the npm beside the file", """{"tools":{"node":{"use":"file","file":"@node"}}}""", "npm", "file")]
    [InlineData("npm, node a named file with none beside it: refused, never PATH's", """{"tools":{"node":{"use":"file","file":"@lonely"}}}""", "npm", "refused")]
    [InlineData("npm, node managed at a version nobody downloaded: refused", """{"tools":{"node":{"use":"managed","version":"9.9.9"}}}""", "npm", "refused")]
    [InlineData("git managed: its record's executable", """{"tools":{"git":{"use":"managed","version":"2.51.0"}}}""", "git", "managed")]
    [InlineData("az managed: a batch file is its executable", """{"tools":{"az":{"use":"managed","version":"2.77.0"}}}""", "az", "managed")]
    public void A_commands_first_word_is_the_tool_that_answers_for_it(string name, string? json, string word, string expected)
    {
        Write(json);

        var resolution = Tools.ResolveCommand(Tools.Read(Home), Home, word, Inherited("two"));

        if (expected == "not a tool")
        {
            Assert.Null(resolution);
            return;
        }

        Assert.NotNull(resolution);
        var answers = Tools.Declared.Single(tool => tool.Answers.Contains(word));
        var wanted = expected switch
        {
            "path" => Path.Combine(_root, "path", Program(word)),
            "managed" when word == answers.Answers[0] => Folder(
                $"managed:{answers.Id}/{Laid.Single(each => each.Tool == answers.Id).Exe}", null),
            "managed" => Path.Combine(Folder("managed:node/node-v22.20.0-win-x64", null), Program(word)),
            "file" => Path.Combine(_root, "named", "node", Program(word)),
            _ => null,
        };
        Assert.True(Spelled(wanted) == Spelled(resolution!.File), $"{name}: {resolution.File} ({resolution.Problem})");
        Assert.Equal(expected == "refused", resolution.Refused);
        if (expected == "none") Assert.Contains("is not on this machine's PATH", resolution.Problem);
        if (expected == "refused") Assert.Contains("never falls back to PATH", resolution.Problem);
        if (expected is not ("none" or "refused")) Assert.Null(resolution.Problem);
    }

    [Fact]
    public void A_name_beside_a_named_file_that_is_not_there_says_so_naming_the_tool_and_the_way_back()
    {
        Write("""{"tools":{"node":{"use":"file","file":"@lonely"}}}""");

        var resolution = Tools.ResolveCommand(Tools.Read(Home), Home, "npm", Inherited("two"))!;

        Assert.Contains("`npm`", resolution.Problem);
        Assert.Contains("Node.js", resolution.Problem);
        Assert.Contains("daoris tool use node system", resolution.Problem);
    }

    // ── Daoris's own git (§2.4) ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Daoris_own_git_is_the_file_tools_resolves_and_its_start_carries_the_tools_PATH()
    {
        Write("""{"tools":{"git":{"use":"managed","version":"2.51.0"}}}""");

        var (info, refusal) = WorkingTree.GitStart(_root, ["status"], Home);

        Assert.Null(refusal);
        Assert.Equal(Folder("managed:git/cmd/git.exe", null), info!.FileName);
        Assert.Equal(["-c", "core.longpaths=true", "status"], info.ArgumentList);
        Assert.StartsWith(Folder("managed:git/cmd", null) + Path.PathSeparator, info.Environment[Tools.PathVariable]);
    }

    [Fact]
    public void Daoris_own_git_that_cannot_run_is_refused_and_never_started_from_PATH()
    {
        Write("""{"tools":{"git":{"use":"managed","version":"9.9.9"}}}""");

        var (info, refusal) = WorkingTree.GitStart(_root, ["status"], Home);

        Assert.Null(info);
        Assert.Contains("never falls back to PATH", refusal);
    }

    [Fact]
    public void With_no_home_Daoris_own_git_is_the_one_PATH_finds_as_before()
    {
        var (info, refusal) = WorkingTree.GitStart(_root, ["status"], home: null);

        Assert.Null(refusal);
        Assert.Equal(CommandPresence.Resolve("git", startable: true) ?? "git", info!.FileName);
    }

    // ── A hook's first word (§2.4) ──────────────────────────────────────────────────────────────────

    private PluginEntry Plugin(params string[] command) =>
        new(new PluginManifest("probe", 1, "probe", "1.0.0", "", [], new PluginHooks(command, ["quest/consider"]), []),
            Path.Combine(Home, "plugins", "probe"), Path.Combine(Home, "plugins", ".data", "probe"), Enabled: true, Problem: null);

    [Fact]
    public void A_hooks_first_word_a_tool_answers_for_is_that_tools_file()
    {
        Write("""{"tools":{"node":{"use":"file","file":"@node"}}}""");

        var info = HookProcess.StartInfo(Plugin("node", "hooks.mjs"), Home);

        Assert.Equal(Named("node"), info.FileName);
        Assert.Equal(["hooks.mjs"], info.ArgumentList);
        Assert.StartsWith(Path.GetDirectoryName(Named("node"))! + Path.PathSeparator, info.Environment[Tools.PathVariable]);
    }

    [Fact]
    public void A_hooks_first_word_whose_tool_cannot_run_is_refused_naming_the_plugin_and_the_tool()
    {
        Write("""{"tools":{"node":{"use":"managed","version":"9.9.9"}}}""");

        var refused = Assert.Throws<DriverException>(() => HookProcess.StartInfo(Plugin("node", "hooks.mjs"), Home));

        Assert.Contains("`probe`", refused.Message);
        Assert.Contains("Node.js", refused.Message);
        Assert.Contains("never falls back to PATH", refused.Message);
    }

    [Fact]
    public void A_hooks_first_word_no_tool_answers_for_is_found_on_the_childs_PATH_as_before()
    {
        var info = HookProcess.StartInfo(Plugin("python", "hooks.py"), Home);

        // No such program here: the bare name stays, and the start says so in its own words, as before.
        Assert.Equal(CommandPresence.Resolve("python", startable: true) ?? "python", info.FileName);
    }

    // ── npm in a pin, and agent install on the system's (§2.7) ─────────────────────────────────────

    [Fact]
    public void A_pins_npm_is_the_one_tools_resolves()
    {
        Write("""{"tools":{"node":{"use":"managed","version":"22.20.0"}}}""");

        var command = HarnessActions.ThroughTools(Home, ["npm", "install", "--prefix", "x", "p@1.0.0"]);

        Assert.Equal(Spelled(Path.Combine(Folder("managed:node/node-v22.20.0-win-x64", null), Program("npm"))), Spelled(command[0]));
        Assert.Equal(["install", "--prefix", "x", "p@1.0.0"], command.Skip(1));
    }

    [Fact]
    public void A_pins_npm_whose_tool_cannot_run_is_refused_before_anything_starts()
    {
        Write("""{"tools":{"node":{"use":"managed","version":"9.9.9"}}}""");

        var refused = Assert.Throws<DriverException>(() => HarnessActions.ThroughTools(Home, ["npm", "view", "p", "version"]));

        Assert.Contains("never falls back to PATH", refused.Message);
    }

    [Fact]
    public void A_stand_in_named_by_its_whole_path_is_run_as_named()
    {
        var standIn = Path.Combine(_root, "stand-in.mjs");

        Assert.Equal([standIn, "view"], HarnessActions.ThroughTools(Home, [standIn, "view"]));
    }

    [Fact]
    public void Agent_install_keeps_the_systems_npm_and_refuses_naming_agent_pin_where_there_is_none()
    {
        Write("""{"tools":{"node":{"use":"managed","version":"22.20.0"}}}""");

        var found = HarnessActions.OnTheSystem(["npm", "install", "-g", "p"], Inherited("two"));
        Assert.Equal(Spelled(Path.Combine(_root, "path", Program("npm"))), Spelled(found[0]));

        var refused = Assert.Throws<DriverException>(() => HarnessActions.OnTheSystem(["npx", "p"], Inherited("two")));
        Assert.Contains("agent pin", refused.Message);
        Assert.Equal(["claude", "install"], HarnessActions.OnTheSystem(["claude", "install"], Inherited("two")));
    }

    // ── The tree guard's node (§2.4) ────────────────────────────────────────────────────────────────

    [Fact]
    public void The_tree_guard_is_written_with_the_node_tools_resolves()
    {
        Write("""{"tools":{"node":{"use":"file","file":"@node"}}}""");

        var guard = TreeGuard.For(Home, _root);
        var path = SpawnSettings.Write(Home, "s1", RuleLists.Empty, [], guard);

        using var document = JsonDocument.Parse(File.ReadAllText(path!));
        var hook = document.RootElement.GetProperty("hooks").GetProperty("PreToolUse")[0].GetProperty("hooks")[0];
        Assert.Equal(Named("node"), hook.GetProperty("command").GetString());
        Assert.Equal(Named("node"), guard.Node);
    }

    [Fact]
    public void A_tree_guard_whose_node_cannot_run_refuses_the_start_rather_than_trusting_PATH()
    {
        Write("""{"tools":{"node":{"use":"managed","version":"9.9.9"}}}""");

        var refused = Assert.Throws<DriverException>(() => TreeGuard.For(Home, _root));

        Assert.Contains("tree guard", refused.Message);
        Assert.Contains("never falls back to PATH", refused.Message);
    }

    // ── The terminal's shells (§2.6) ────────────────────────────────────────────────────────────────

    [Fact]
    public void A_managed_pwsh_is_the_terminals_and_a_minimal_git_falls_back_to_the_systems_bash()
    {
        if (!Windows) return;
        Write("""{"tools":{"pwsh":{"use":"managed","version":"7.5.3"},"git":{"use":"managed","version":"2.51.0"}}}""");
        var system = Path.Combine(_root, "system-git");
        Touch(Path.Combine(system, "cmd", "git.exe"));
        Touch(Path.Combine(system, "bin", "bash.exe"));

        var shells = TerminalShells.For(Tools.Read(Home), Home, Path.Combine(system, "cmd"));

        Assert.Equal(Folder("managed:pwsh/pwsh.exe", null), shells.Single(shell => shell.Id == TerminalShells.Pwsh).Path);
        Assert.Equal(Path.Combine(system, "bin", "bash.exe"), shells.Single(shell => shell.Id == TerminalShells.GitBash).Path);
    }

    [Fact]
    public void Git_bash_is_the_one_beside_the_git_tools_resolves_when_it_has_one()
    {
        if (!Windows) return;
        var named = Path.Combine(_root, "portable-git");
        Touch(Path.Combine(named, "cmd", "git.exe"));
        Touch(Path.Combine(named, "bin", "bash.exe"));
        var system = Path.Combine(_root, "system-git");
        Touch(Path.Combine(system, "cmd", "git.exe"));
        Touch(Path.Combine(system, "bin", "bash.exe"));
        File.WriteAllText(ToolsFile, """{"tools":{"git":{"use":"file","file":""" + JsonSerializer.Serialize(Path.Combine(named, "cmd", "git.exe")) + "}}}");

        var shells = TerminalShells.For(Tools.Read(Home), Home, Path.Combine(system, "cmd"));

        Assert.Equal(Path.Combine(named, "bin", "bash.exe"), shells.Single(shell => shell.Id == TerminalShells.GitBash).Path);
    }

    [Fact]
    public void A_pwsh_whose_way_cannot_run_is_not_offered_and_never_found_on_PATH_instead()
    {
        if (!Windows) return;
        Write("""{"tools":{"pwsh":{"use":"managed","version":"9.9.9"}}}""");
        var system = Path.Combine(_root, "system");
        Touch(Path.Combine(system, "pwsh.exe"));

        var shells = TerminalShells.For(Tools.Read(Home), Home, system);

        Assert.DoesNotContain(shells, shell => shell.Id == TerminalShells.Pwsh);
    }

    [Fact]
    public void A_terminals_environment_block_carries_the_tools_PATH_over_this_processs()
    {
        Write("""{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""");

        var block = PseudoConsole.EnvironmentBlock(new Dictionary<string, string> { [DaorisHome.Variable] = Home });

        var path = block.Split('\0').Single(line => line.StartsWith(Tools.PathVariable + "=", StringComparison.OrdinalIgnoreCase));
        var inherited = Environment.GetEnvironmentVariable(Tools.PathVariable);
        Assert.Equal(Folder("managed:gh/bin", null) + (string.IsNullOrEmpty(inherited) ? "" : Path.PathSeparator + inherited), path[5..]);
        Assert.Contains($"{DaorisHome.Variable}={Home}", block.Split('\0'));
    }

    [Fact]
    public void With_no_file_the_terminal_offers_what_PATH_has_as_before()
    {
        if (!Windows) return;
        var system = Path.Combine(_root, "system");
        Touch(Path.Combine(system, "pwsh.exe"));
        Touch(Path.Combine(system, "cmd.exe"));

        Assert.Equal(
            TerminalShells.Available(system).Select(shell => (shell.Id, shell.Path)),
            TerminalShells.For(Tools.Read(Home), Home, system).Select(shell => (shell.Id, shell.Path)));
    }
}
