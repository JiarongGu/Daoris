using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris plugin</c>: each change names the route the window's press
/// takes, its payload written from the words, or none for the terminal's replace; a person's press acting alone is refused from a
/// session's terminal with nothing written; and an alone remove of a folder something holds is refused as the route refuses it, nothing
/// taken; and each verb's exit and the files it writes, a row each. Both ways through the route are the modules' <c>PluginsBothWaysTests</c>.
/// </summary>
public sealed class PluginCommandTests : IDisposable
{
    private const string Gate = """{ "id": "acme.gate", "name": "Acme gate", "version": "1.0.0" }""";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "plugin-command", Guid.NewGuid().ToString("N")[..8]);

    private readonly StringWriter _out = new();

    private readonly StringWriter _error = new();

    /// <summary>An install's folder: its <c>data/</c> the home, its <c>app/plugin-offers/</c> the offers, as the text table's.</summary>
    public PluginCommandTests()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(_root, "INSTALLED.md"), "# Daoris — installed desktop\n");
    }

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "data");

    private string Installed(string id) => DaorisHome.Entry(Home, HomeEntry.Plugins, id);

    private string Folder(string at, string manifest)
    {
        var folder = Path.Combine(_root, at);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), manifest);
        return folder;
    }

    private PluginWorld World(string? session = null, bool home = true) => new()
    {
        Instance = new InstanceWorld
        {
            Home = home ? Home : null,
            Environment = name => name == DaorisHome.Variable && home ? Home : name == DriverChoicesCommand.SessionVariable ? session : null,
            BaseDirectory = Path.Combine(_root, "application"),
            Out = _out,
            Error = _error,
            Verb = "daoris",
        },
        WorkingDirectory = _root,
    };

    private const string GateV1 =
        """{"id":"acme.gate","name":"Acme gate","version":"1.0.0","hooks":{"command":["node","${plugin}/gate.mjs"],"points":["quest/consider"]}}""";

    private const string Agent = """{ "id": "acme.agent" }""";

    private static readonly Dictionary<string, string> Installed1 = new() { ["data/plugins/acme.agent/plugin.json"] = Agent };

    private static readonly Dictionary<string, string> Outdated = new()
    {
        ["checkout/gate/gate.mjs"] = "// v2",
        ["checkout/gate/plugin.json"] = """{"id":"acme.gate","name":"Acme gate","version":"1.1.0","hooks":{"command":["node","${plugin}/gate2.mjs"],"points":["quest/consider"]}}""",
        ["data/plugins/.data/acme.gate/kept.json"] = "{}",
        ["data/plugins/acme.gate/.daoris-source.json"] = """{"folder":"<root>/checkout/gate"}""",
        ["data/plugins/acme.gate/gate.mjs"] = "// v1",
        ["data/plugins/acme.gate/plugin.json"] = GateV1,
        ["data/plugins/acme.gate/stale.txt"] = "old",
    };

    /// <summary>
    /// Each verb's exit and what it changes under the install's folder (a file named with <c>-</c> is removed), from the files before it;
    /// a refusal exits 2 on one line with nothing changed. The rows of the retired TypeScript verb's recording that held a behaviour, not
    /// words. A manifest's own refusals are <see cref="PluginCatalogTests"/>', <see cref="PluginInstallTests"/>' and
    /// <see cref="PluginOfferTests"/>'.
    /// </summary>
    private static readonly Dictionary<string, Row> Rows = new Row[]
    {
        new(["add", "<root>/checkout/gate"], 0, ["data/plugins/acme.gate/.daoris-source.json", "data/plugins/acme.gate/gate.mjs", "data/plugins/acme.gate/plugin.json"],
            new() { ["checkout/gate/gate.mjs"] = "// v1", ["checkout/gate/plugin.json"] = GateV1 }),
        new(["add", "--offer", "ACME.OFFERED"], 0, ["data/plugins/acme.offered/.daoris-source.json", "data/plugins/acme.offered/plugin.json"],
            new() { ["app/plugin-offers/acme.offered/plugin.json"] = """{ "id": "acme.offered", "name": "Acme offered", "version": "3.0.0" }""" }),
        new(["disable", "acme.agent"], 0, ["data/plugins.json"], Installed1),
        new(["enable", "acme.agent"], 0, ["data/plugins.json"],
            new() { ["data/plugins/acme.agent/plugin.json"] = Agent, ["data/plugins.json"] = """{ "disabled": ["acme.agent", "other"] }""" }),
        new(["remove", "acme.agent"], 0, ["-data/plugins/acme.agent/plugin.json"], Installed1),
        new(["switch", "acme.agent", "tools", "off", "--place", "repository:engine"], 0, ["data/plugins.json"], Installed1),
        new(["update", "acme.gate", "--yes"], 0,
            ["data/plugins/acme.gate/.daoris-source.json", "data/plugins/acme.gate/gate.mjs", "data/plugins/acme.gate/plugin.json", "-data/plugins/acme.gate/stale.txt"],
            Outdated),
        new(["update", "acme.gate"], 1, [], Outdated),
        new(["add"], 2, []),
        new(["add", "--offer"], 2, []),
        new(["add", "--offer", "--yes"], 2, []),
        new(["add", "--offer", "../up"], 2, []),
        new(["add", "--offer", "acme.nobody"], 2, []),
        new(["disable"], 2, []),
        new(["frobnicate"], 2, []),
        new(["install", "<root>/Acme.Gate.1.0.0.nupkg"], 2, []),
        new(["remove"], 2, []),
        new(["update"], 2, []),
        new(["update", "--yes"], 2, []),
        new(["update", "../acme.gate"], 2, []),
        new(["update", "acme.nobody"], 2, [], new() { ["data/plugins/.keep"] = "" }),
        new(["enable", "nobody"], 2, [], Installed1),
        new(["try-again", "nobody"], 2, [], Installed1),
        new(["remove", ".."], 2, [], Installed1),
        new(["remove", ".data"], 2, [], new() { ["data/plugins/.data/acme.agent/kept.json"] = "{}" }),
        new(["enable", "acme.agent"], 2, [], Home: false),
        new(["list"], 2, [], Home: false),
        new(["new", "acme.gate"], 2, [], Home: false),
        new(["try", "acme.gate"], 2, [], Home: false),
    }.ToDictionary(row => string.Join(' ', row.Argv) + (row.Home ? "" : ", no home"));

    private sealed record Row(string[] Argv, int Exit, string[] Changed, Dictionary<string, string>? Before = null, bool Home = true);

    public static TheoryData<string> Verbs() => [.. Rows.Keys];

    [Fact]
    public void The_rows_hold_every_verb_the_command_answers() =>
        Assert.All(PluginCommand.Verbs, verb => Assert.Contains(Rows.Values, row => row.Argv[0] == verb));

    [Theory]
    [MemberData(nameof(Verbs))]
    public async Task Each_verb_exits_and_changes_the_files_its_row_says(string name)
    {
        var row = Rows[name];
        foreach (var (file, text) in row.Before ?? [])
        {
            var path = Path.Combine([_root, .. file.Split('/')]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text.Replace("<root>", file.EndsWith(".json", StringComparison.Ordinal)
                ? System.Text.Json.JsonEncodedText.Encode(_root).ToString() : _root, StringComparison.Ordinal));
        }

        var before = Files();
        var exit = await PluginCommand.RunAsync(
            [.. row.Argv.Select(each => each.Replace("<root>", _root, StringComparison.Ordinal))], World(home: row.Home), CancellationToken.None);

        var after = Files();
        string[] changed = [.. after.Where(file => !before.TryGetValue(file.Key, out var was) || was != file.Value).Select(file => file.Key)
            .Concat(before.Keys.Where(file => !after.ContainsKey(file)).Select(file => "-" + file)).Order(StringComparer.Ordinal)];
        var said = $"`{name}` exit {exit}, changed {string.Join(", ", changed)}:\n{_out}\n{_error}";
        Assert.True(row.Exit == exit, said);
        Assert.True(row.Changed.Order(StringComparer.Ordinal).SequenceEqual(changed), said);
        if (row.Exit == 2) Assert.True(_out.ToString().Length == 0 && _error.ToString().TrimEnd().Split('\n').Length == 1, said);
    }

    private Dictionary<string, string> Files() =>
        Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(_root, path).Replace('\\', '/'), File.ReadAllText, StringComparer.Ordinal);

    [Fact]
    public void Each_change_names_its_route_and_the_payload_the_window_s_press_sends()
    {
        var source = Folder("checkout/gate", Gate);

        var add = PluginCommand.Plan(["add", source], World())!;
        Assert.Equal(PluginRoutes.Add, add.Route);
        // Named whole, as the route's install from a folder takes it.
        Assert.Equal(Path.GetFullPath(source), add.Payload["folder"]!.GetValue<string>());

        Directory.CreateDirectory(Path.Combine(_root, "app", "plugin-offers"));
        Folder("app/plugin-offers/acme.offered", """{ "id": "acme.offered" }""");
        var offer = PluginCommand.Plan(["add", "--offer", "acme.offered"], World())!;
        Assert.Equal(PluginRoutes.Install, offer.Route);
        Assert.Equal("acme.offered", offer.Payload["offer"]!.GetValue<string>());

        Folder("data/plugins/acme.gate", Gate);
        Assert.Equal(PluginRoutes.Replace, PluginCommand.Plan(["add", source], World())!.Route);
        foreach (var verb in new[] { "remove", "enable", "disable" })
        {
            var press = PluginCommand.Plan([verb, "ACME.GATE"], World())!;
            Assert.Equal(PluginRoutes.Action, press.Route);
            Assert.Equal(verb, press.Payload["action"]!.GetValue<string>());
        }

        Assert.Null(PluginCommand.Plan(["list"], World()));
        Assert.Null(PluginCommand.Plan(["update", "acme.gate"], World()));
    }

    [Fact]
    public void The_update_s_press_is_sent_with_apply_true_and_its_plan_is_read_here()
    {
        var source = Folder("checkout/gate", Gate);
        Folder("data/plugins/acme.gate", Gate);
        File.WriteAllText(Path.Combine(Installed("acme.gate"), PluginSource.FileName), $$"""{ "folder": {{System.Text.Json.JsonSerializer.Serialize(source)}} }""");

        var press = PluginCommand.Plan(["update", "acme.gate", "--yes"], World())!;

        Assert.Equal(PluginRoutes.Update, press.Route);
        Assert.Equal("acme.gate", press.Payload["id"]!.GetValue<string>());
        Assert.True(press.Payload["apply"]!.GetValue<bool>());
        Assert.Contains("What it declares does not change", _out.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Every plugin press is the person's: from a session's terminal, with no Daoris to confirm it, refused alone.</summary>
    [Theory]
    [InlineData("add")]
    [InlineData("replace")]
    [InlineData("remove")]
    [InlineData("disable")]
    public async Task A_person_s_press_alone_is_refused_from_a_session_s_terminal_with_nothing_written(string verb)
    {
        var source = Folder("checkout/gate", Gate);
        if (verb != "add") Folder("data/plugins/acme.gate", Gate);
        var before = Snapshot();
        string[] words = verb is "add" or "replace" ? ["add", source] : [verb, "acme.gate"];

        var exit = await PluginCommand.RunAsync(words, World(session: "s-1"), CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Contains(DriverChoicesCommand.SessionsTerminal, _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, Snapshot());
    }

    /// <summary>
    /// An alone remove stops nothing, since no Daoris runs the plugin's process; a process holding its folder makes the move fail, refused
    /// as the route refuses it (<c>PLUGIN_BUSY</c>), and nothing is taken: the plugin, its manifest and its disabled row stay.
    /// </summary>
    [Fact]
    public async Task An_alone_remove_of_a_held_folder_is_refused_as_busy_and_nothing_is_taken()
    {
        // A file held open holds its folder against a move on Windows alone; elsewhere the move succeeds.
        if (!OperatingSystem.IsWindows()) return;
        var folder = Folder("data/plugins/acme.gate", Gate);
        PluginState.Disable(Home, "acme.gate");
        var before = Snapshot();

        int exit;
        using (File.Open(Path.Combine(folder, PluginCatalog.ManifestName), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            exit = await PluginCommand.RunAsync(["remove", "acme.gate"], World(), CancellationToken.None);
        }

        Assert.Equal(2, exit);
        Assert.Contains("`acme.gate` was not removed", _error.ToString(), StringComparison.Ordinal);
        Assert.EndsWith($"Nothing was taken. ({PluginCodes.Busy})", _error.ToString().TrimEnd(), StringComparison.Ordinal);
        Assert.Equal(before, Snapshot());
        Assert.Equal(["acme.gate"], PluginState.Load(Home).Disabled);
    }

    [Fact]
    public async Task An_enable_of_a_plugin_this_build_would_refuse_says_so_after_the_switch()
    {
        Folder("data/plugins/acme.shadow", """{ "id": "acme.shadow", "harnesses": [ { "name": "claude-code", "command": ["evil"] } ] }""");
        PluginState.Disable(Home, "acme.shadow");

        var exit = await PluginCommand.RunAsync(["enable", "acme.shadow"], World(), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Contains("is on on this machine", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("⚠", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("claude-code", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_remove_whose_switch_file_cannot_be_read_is_refused_before_the_folder_moves()
    {
        var folder = Folder("data/plugins/acme.gate", Gate);
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.PluginState), "{ not json");
        var before = Snapshot();

        Assert.Throws<DriverException>(() => PluginInstall.Remove(Home, "acme.gate"));

        Assert.True(Directory.Exists(folder));
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public void The_operation_s_refusals_are_coded_by_the_library_s_one_table()
    {
        var unknown = Assert.Throws<PluginRefusal>(() => PluginInstall.Remove(Home, "acme.nobody"));
        Assert.Equal(PluginCodes.Unknown, PluginCodes.Of(unknown));
        Assert.Equal(PluginCodes.Refused, PluginCodes.Of(new DriverException("anything else")));
        Assert.All(PluginCodes.Table.Values, code => Assert.StartsWith("PLUGIN_", code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_usage_names_each_verb_and_holds_nothing()
    {
        Assert.Equal(0, await PluginCommand.RunAsync(["--help"], World(), CancellationToken.None));
        Assert.All(PluginCommand.Verbs, verb => Assert.Contains(verb, PluginCommand.Usage, StringComparison.Ordinal));
        Assert.Contains("daoris plugin new|try", PluginCommand.Usage, StringComparison.Ordinal);
        // Every change waits on its Confirm, and the usage says for how long.
        Assert.Contains(InstanceDoor.WaitUsage, PluginCommand.Usage, StringComparison.Ordinal);
        Assert.Contains("plugin list  ·  plugin add <folder>", DriverCommand.Usage, StringComparison.Ordinal);
    }

    /// <summary>Every file under the root and its bytes, so a refusal can be held to have changed nothing.</summary>
    private string Snapshot() => string.Join('\n', Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal)
        .Select(path => $"{Path.GetRelativePath(_root, path)}={Convert.ToBase64String(File.ReadAllBytes(path))}"));
}
