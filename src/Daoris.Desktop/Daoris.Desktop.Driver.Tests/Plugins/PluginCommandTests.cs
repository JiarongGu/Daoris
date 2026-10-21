using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris-driver plugin</c> (PUSH1f2f, D194, the alone-path design §1.2, §1.3, §2): each change names the route the window's press
/// takes, its payload written from the words, or none for the terminal's replace; a person's press acting alone is refused from a
/// session's terminal with nothing written; and an alone remove of a folder something holds is refused as the route refuses it, nothing
/// taken. Today's words are <see cref="PluginsTextTests"/>'; both ways through the route are the modules' <c>PluginsBothWaysTests</c>.
/// </summary>
public sealed class PluginCommandTests : IDisposable
{
    private const string Gate = """{ "id": "acme.gate", "name": "Acme gate", "version": "1.0.0" }""";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "plugin-command", Guid.NewGuid().ToString("N")[..8]);

    private readonly StringWriter _out = new();

    private readonly StringWriter _error = new();

    /// <summary>An install's folder (D172 point 2): its <c>data/</c> the home, its <c>app/plugin-offers/</c> the offers, as the text table's.</summary>
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

    private PluginWorld World(string? session = null) => new()
    {
        Instance = new InstanceWorld
        {
            Home = Home,
            Environment = name => name == DaorisHome.Variable ? Home : name == DriverChoicesCommand.SessionVariable ? session : null,
            BaseDirectory = Path.Combine(_root, "application"),
            Out = _out,
            Error = _error,
            Verb = "daoris",
        },
        WorkingDirectory = _root,
    };

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
        Assert.Null(PluginCommand.Plan(["add", source], World())!.Route);
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

    /// <summary>Every plugin press is the person's (D194 point 5): from a session's terminal, with no Daoris to confirm it, refused alone.</summary>
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
        Assert.Contains("daoris-driver plugins new|try", PluginCommand.Usage, StringComparison.Ordinal);
        Assert.Contains("plugin [list | add <folder>", DriverCommand.Usage, StringComparison.Ordinal);
    }

    /// <summary>Every file under the root and its bytes, so a refusal can be held to have changed nothing.</summary>
    private string Snapshot() => string.Join('\n', Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal)
        .Select(path => $"{Path.GetRelativePath(_root, path)}={Convert.ToBase64String(File.ReadAllBytes(path))}"));
}
