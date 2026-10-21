using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Each of the plugins' presses both ways (PUSH1f2f, D194 points 4 and 6, the alone-path design §3): <c>daoris-driver plugin</c> acting
/// alone, through the library's operation, and the same words' press sent to the Plugins place's route (<c>PLUGIN_ADD</c>,
/// <c>PLUGIN_INSTALL</c>, <c>PLUGIN_UPDATE</c>, <c>PLUGIN_ACTION</c>), its answer said by the same words function, over one home from the
/// same files. The files left, the text, the refusal's line and the exit are equal: red where a route and its operation disagree, or
/// where the payload the command writes does not read back through the route as the command meant it.
/// </summary>
public sealed class PluginsBothWaysTests() : DriverModuleBridge("plugins-both-ways")
{
    private string Plugins => DaorisHome.Entry(Home, HomeEntry.Plugins);

    private string State => DaorisHome.Entry(Home, HomeEntry.PluginState);

    /// <summary>Beside the home and never in it: a folder inside the home is no place to add a plugin from.</summary>
    private string Outside => Home + "-from";

    private string Offers => Path.Combine(Outside, "plugin-offers");

    private PluginWorld World(StringWriter output, StringWriter error) => new()
    {
        // No address file is read here (a workspace build), so the command acts alone; the route is asked of the module itself.
        Instance = new InstanceWorld { Home = Home, Environment = _ => null, Out = output, Error = error, Verb = "daoris" },
        WorkingDirectory = Home,
        Offers = Offers,
    };

    private sealed record Said(int Exit, string Out, string Error, string Files);

    private static void Folder(string at, string manifest)
    {
        Directory.CreateDirectory(at);
        File.WriteAllText(Path.Combine(at, PluginCatalog.ManifestName), manifest);
    }

    /// <summary>
    /// The home as each case starts it: <c>acme.gate</c> 1.0.0 installed from a folder that now holds 1.1.0, switched off, what it kept
    /// beside it; a new plugin's folder and one offer, both outside the home.
    /// </summary>
    private void Laid(string? state = null)
    {
        if (Directory.Exists(Outside)) Directory.Delete(Outside, recursive: true);
        if (Directory.Exists(Plugins)) Directory.Delete(Plugins, recursive: true);
        Folder(Path.Combine(Outside, "gate"), """{ "id": "acme.gate", "name": "Acme gate", "version": "1.1.0" }""");
        Folder(Path.Combine(Outside, "new"), """{ "id": "acme.new", "hooks": { "command": ["node", "x.mjs"], "points": ["work/land"] } }""");
        Folder(Path.Combine(Offers, "acme.offered"), """{ "id": "acme.offered", "version": "3.0.0" }""");
        var installed = Path.Combine(Plugins, "acme.gate");
        Folder(installed, """{ "id": "acme.gate", "name": "Acme gate", "version": "1.0.0" }""");
        File.WriteAllText(Path.Combine(installed, PluginSource.FileName),
            $$"""{ "folder": {{System.Text.Json.JsonSerializer.Serialize(Path.Combine(Outside, "gate"))}} }""");
        Directory.CreateDirectory(Path.Combine(Plugins, PluginCatalog.DataFolder, "acme.gate"));
        File.WriteAllText(State, state ?? """{ "disabled": ["acme.gate"] }""");
    }

    /// <summary>Every file under the plugins folder and the state file, a record's whole path spelled the same both ways.</summary>
    private Said Now(StringWriter output, StringWriter error, int exit)
    {
        IEnumerable<string> files = Directory.Exists(Plugins)
            ? Directory.EnumerateFiles(Plugins, "*", SearchOption.AllDirectories)
                .Select(path => $"{Path.GetRelativePath(Plugins, path)}={File.ReadAllText(path)}")
                .Order(StringComparer.Ordinal)
            : [];
        return new(exit, output.ToString(), error.ToString(), string.Join('\n', [.. files, File.Exists(State) ? File.ReadAllText(State) : "(no state)"]));
    }

    private async Task<Said> AloneAsync(string[] words, string? state)
    {
        Laid(state);
        var (output, error) = (new StringWriter(), new StringWriter());
        var exit = await PluginCommand.RunAsync(words, World(output, error), CancellationToken.None);
        return Now(output, error, exit);
    }

    /// <summary>The same words' press sent to the module's route, its answer said by the command's words, as the pipe's client says it.</summary>
    private async Task<Said> RouteAsync(string[] words, string? state)
    {
        Laid(state);
        var (output, error) = (new StringWriter(), new StringWriter());
        var press = PluginCommand.Plan(words, World(output, error));
        Assert.NotNull(press);
        Assert.NotNull(press.Route);
        var module = new DriverModule(Bus, Loop()) { Offers = Offers };
        var response = await AskAsync(module, press.Route.Route, press.Payload);
        int exit;
        if (response.Success)
        {
            exit = press.Words();
        }
        else
        {
            // As InstanceDoor.PressAsync says a route's refusal: `{verb}: {message} ({code})`, exit 2.
            error.WriteLine($"daoris: {response.Error!.Message} ({response.Error.Code})");
            exit = 2;
        }

        return Now(output, error, exit);
    }

    [Theory]
    [InlineData("add <from>/new", null)]
    [InlineData("add --offer acme.offered", null)]
    [InlineData("update acme.gate --yes", null)]
    [InlineData("remove acme.gate", null)]
    [InlineData("enable acme.gate", null)]
    [InlineData("disable acme.gate", null)]
    [InlineData("enable ACME.GATE", null)]
    [InlineData("disable acme.gate", """{ "disabled": ["other"], "note": "mine" }""")]
    // An edit over a state file that does not read is refused by PluginState both ways, its line the same.
    [InlineData("enable acme.gate", "{ not json")]
    public async Task A_plugin_s_press_says_and_writes_the_same_alone_and_through_its_route(string text, string? state)
    {
        string[] words = [.. text.Split(' ').Select(word => word.StartsWith("<from>/", StringComparison.Ordinal)
            ? Path.Combine(Outside, word["<from>/".Length..])
            : word)];
        try
        {
            var alone = await AloneAsync(words, state);
            var route = await RouteAsync(words, state);

            Assert.NotEqual("", alone.Out + alone.Error);
            Assert.Equal(alone, route);
        }
        finally
        {
            if (Directory.Exists(Outside)) Directory.Delete(Outside, recursive: true);
        }
    }

    /// <summary>
    /// A remove of a folder something holds (the alone-path design §5's proof): alone, no process is stopped and the move fails; through
    /// the route the plugin's process is stopped first, none runs, and the move fails the same. Both refuse as <c>PLUGIN_BUSY</c>, the
    /// same line, and take nothing.
    /// </summary>
    [Fact]
    public async Task A_remove_of_a_held_folder_is_refused_as_busy_both_ways_and_takes_nothing()
    {
        // A file held open holds its folder against a move on Windows alone; elsewhere the move succeeds.
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            // Each run's files are read once the file is let go.
            Laid();
            var (output, error) = (new StringWriter(), new StringWriter());
            int exit;
            using (File.Open(Path.Combine(Plugins, "acme.gate", PluginCatalog.ManifestName), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                exit = await PluginCommand.RunAsync(["remove", "acme.gate"], World(output, error), CancellationToken.None);
            }

            var alone = Now(output, error, exit);

            Laid();
            (output, error) = (new StringWriter(), new StringWriter());
            using (File.Open(Path.Combine(Plugins, "acme.gate", PluginCatalog.ManifestName), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var press = PluginCommand.Plan(["remove", "acme.gate"], World(output, error))!;
                var response = await AskAsync(new DriverModule(Bus, Loop()) { Offers = Offers }, press.Route!.Route, press.Payload);
                Assert.False(response.Success);
                error.WriteLine($"daoris: {response.Error!.Message} ({response.Error.Code})");
            }

            var route = Now(output, error, 2);

            Assert.Equal(alone, route);
            Assert.EndsWith($"Nothing was taken. ({PluginCodes.Busy})", alone.Error.TrimEnd(), StringComparison.Ordinal);
            Assert.Contains("acme.gate", alone.Files, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(Outside)) Directory.Delete(Outside, recursive: true);
        }
    }
}
