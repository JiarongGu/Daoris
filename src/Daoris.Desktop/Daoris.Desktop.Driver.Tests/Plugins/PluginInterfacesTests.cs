using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The interfaces a plugin declares it implements, the only ways Daoris reaches it besides its points: its commands, run in its own
/// process, its private storage, a folder handed to that process alone, and its views. The rules' one table: a problem refuses its
/// entry alone and never the plugin, an interface this build does not know is named and skipped, and what names a refused entry is
/// disabled with the reason.
/// </summary>
public sealed class PluginInterfacesTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plugin-interfaces-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private const string Hooks = """, "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/state"] }""";

    private PluginEntry Plugin(string declared)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.tickets");
        Directory.CreateDirectory(folder);
        var hooks = declared.Contains("\"hooks\"", StringComparison.Ordinal) || declared.Contains("\"nohooks\"", StringComparison.Ordinal) ? "" : Hooks;
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), $"{{ \"id\": \"acme.tickets\"{hooks}{declared} }}");
        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);
        Assert.True(entry.Problem is null, entry.Problem);
        return entry;
    }

    private static string Read(PluginInterfacesDeclared interfaces) => string.Join("; ", (IEnumerable<string>)
    [
        .. interfaces.Commands.Select(command => $"{command.Name}|{command.Interactive}|{command.Tool}"),
        .. interfaces.Storage is null ? [] : new[] { "storage" },
        .. interfaces.Views.Select(view => $"view {view.Id}"),
    ]);

    private static string Refused(PluginInterfacesDeclared interfaces) =>
        string.Join(" / ", interfaces.Refused.Select(refusal => $"{refusal.Interface}{(refusal.Entry is { } entry ? $" {entry}" : "")}: {refusal.Problem}"));

    /// <summary>[case, what the manifest declares after its id, what is read (each command as <c>name|interactive|tool</c>, <c>storage</c>, each view), what is refused].</summary>
    [Theory]
    [InlineData("none", "", "", "")]
    [InlineData("a command, the least", """, "interfaces": { "commands": [{ "name": "status", "about": "Says what is kept." }] } """, "status|False|False", "")]
    [InlineData("commands, storage and a view", """, "interfaces": { "commands": [{ "name": "login", "about": "Keeps a token.", "interactive": true }, { "name": "status", "about": "Says what is kept.", "tool": true }], "storage": { "for": "The token login keeps." }, "views": [{ "id": "overview", "title": "Overview" }] } """, "login|True|False; status|False|True; storage; view overview", "")]
    [InlineData("not an object", """, "interfaces": ["commands"] """, "", "interfaces: `interfaces` must be an object naming the interfaces the plugin implements: `commands`, `storage`, `views`.")]
    [InlineData("one this build does not know, skipped", """, "interfaces": { "pull-request": {}, "commands": [{ "name": "status", "about": "x" }] } """, "status|False|False", "")]
    [InlineData("commands not an array", """, "interfaces": { "commands": { "name": "login" }, "storage": { "for": "x" } } """, "storage", "commands: `interfaces.commands` must be an array of the commands the plugin runs.")]
    [InlineData("no name", """, "interfaces": { "commands": [{ "about": "x" }, { "name": "status", "about": "y" }] } """, "status|False|False", "commands: a declared command needs a `name` — lowercase letters, digits and dashes, at most 40.")]
    [InlineData("twice, the first kept", """, "interfaces": { "commands": [{ "name": "login", "about": "x" }, { "name": "login", "about": "y" }] } """, "login|False|False", "commands login: command `login` is declared twice.")]
    [InlineData("no about", """, "interfaces": { "commands": [{ "name": "login" }] } """, "", "commands login: command `login` needs an `about`: what it does, in a sentence.")]
    [InlineData("interactive not a flag", """, "interfaces": { "commands": [{ "name": "login", "about": "x", "interactive": "yes" }] } """, "", "commands login: command `login`'s `interactive` must be true or false: whether it asks the person.")]
    [InlineData("an agent's tool that asks the person", """, "interfaces": { "commands": [{ "name": "login", "about": "x", "interactive": true, "tool": true }] } """, "", "commands login: command `login` asks the person, so it is never an agent's tool.")]
    [InlineData("an adoption", """, "interfaces": { "commands": [{ "name": "login", "about": "x", "interactive": true, "adopts": { "hosts": ["tickets.example"], "answer": "token" } }] } """, "login|True|False", "")]
    [InlineData("an adoption with no answer", """, "interfaces": { "commands": [{ "name": "login", "about": "x", "adopts": { "hosts": ["tickets.example"] } }] } """, "", "commands login: command `login`'s `adopts` must name its `hosts` and the `answer` the token is sent as.")]
    [InlineData("a title not words", """, "interfaces": { "commands": [{ "name": "login", "about": "x", "title": 3 }] } """, "", "commands login: command `login`'s `title` must be words on one line of at most 80: a string, or an object of language tags to text.")]
    [InlineData("writes not a flag", """, "interfaces": { "commands": [{ "name": "start", "about": "x", "writes": "yes" }] } """, "", "commands start: command `start`'s `writes` must be true or false: whether it changes something outside the plugin.")]
    [InlineData("a confirm not words", """, "interfaces": { "commands": [{ "name": "start", "about": "x", "writes": true, "confirm": "" }] } """, "", "commands start: command `start`'s `confirm` must be words on one line of at most 200: the question the window asks once before it runs.")]
    [InlineData("on a record Daoris has not", """, "interfaces": { "commands": [{ "name": "start", "about": "x", "on": ["ticket"] }] } """, "", "commands start: command `start`'s `on` must list the records whose menus offer it, each once: `session`, `quest`, `run`, `repository`, `workspace`.")]
    [InlineData("an argument of no type", """, "interfaces": { "commands": [{ "name": "start", "about": "x", "args": [{ "name": "stage" }] }] } """, "", "commands start: command `start`'s argument `stage` needs a `type`: `text`, `number`, `boolean` or `choice`.")]
    [InlineData("an argument twice", """, "interfaces": { "commands": [{ "name": "start", "about": "x", "args": [{ "name": "stage", "type": "text" }, { "name": "stage", "type": "number" }] }] } """, "", "commands start: command `start` declares the argument `stage` twice.")]
    [InlineData("a choice with no choices", """, "interfaces": { "commands": [{ "name": "start", "about": "x", "args": [{ "name": "stage", "type": "choice" }] }] } """, "", "commands start: command `start`'s argument `stage` is a `choice`, so it needs `choices`: a list of at least one value, each once.")]
    [InlineData("choices on a text", """, "interfaces": { "commands": [{ "name": "start", "about": "x", "args": [{ "name": "stage", "type": "text", "choices": ["a"] }] }] } """, "", "commands start: command `start`'s argument `stage` has `choices`, which only a `choice` takes.")]
    [InlineData("storage with no for", """, "interfaces": { "storage": {} } """, "", "storage: `interfaces.storage` needs a `for`: what the plugin keeps there.")]
    [InlineData("a view with no title", """, "interfaces": { "views": [{ "id": "overview" }] } """, "", "views overview: view `overview` needs a `title`: words on one line of at most 80.")]
    [InlineData("a view twice", """, "interfaces": { "views": [{ "id": "overview", "title": "A" }, { "id": "overview", "title": "B" }] } """, "view overview", "views overview: view `overview` is declared twice.")]
    [InlineData("a view standing nowhere Daoris has", """, "interfaces": { "views": [{ "id": "overview", "title": "A", "where": ["status-bar"] }] } """, "", "views overview: view `overview`'s `where` must list where it may stand, at least one, each once: `side`, `page`.")]
    [InlineData("a view about a place Daoris has not", """, "interfaces": { "views": [{ "id": "overview", "title": "A", "places": ["team"] }] } """, "", "views overview: view `overview`'s `places` must list the kinds of place it is about, at least one, each once: `machine`, `workspace`, `repository`.")]
    [InlineData("a view told what Daoris does not tell", """, "interfaces": { "views": [{ "id": "overview", "title": "A", "context": ["diff"] }] } """, "", "views overview: view `overview`'s `context` must list what it is told of the window, each once: `session`, `quest`.")]
    [InlineData("a view drawing its own body", """, "interfaces": { "views": [{ "id": "overview", "title": "A", "body": "frame" }] } """, "", "views overview: view `overview` draws its own body (`frame`), which this build does not draw: its `body` is `drawn`.")]
    [InlineData("a view's icon outside its folder", """, "interfaces": { "views": [{ "id": "overview", "title": "A", "icon": "../icon.svg" }] } """, "", "views overview: view `overview`'s `icon` must be a path inside the plugin's folder.")]
    [InlineData("no process to reach", """, "nohooks": true, "interfaces": { "storage": { "for": "x" } } """, "", "storage: interface `storage` is reached through the plugin's process, so the manifest must declare `hooks`.")]
    public void A_manifest_s_interfaces_are_read_each_problem_refusing_its_entry_alone(string @case, string declared, string read, string refused)
    {
        var interfaces = Plugin(declared).Manifest.Interfaces;
        Assert.True(read == Read(interfaces), $"{@case}: {Read(interfaces)}");
        Assert.True(refused == Refused(interfaces), $"{@case}: {Refused(interfaces)}");
    }

    [Fact]
    public void An_interface_this_build_does_not_know_is_named_once_and_skipped()
    {
        var interfaces = Plugin(""", "interfaces": { "pull-request": {}, "settings": [], "pull-request": 1 } """).Manifest.Interfaces;

        Assert.Equal(["pull-request", "settings"], interfaces.Unknown);
        Assert.Empty(interfaces.Refused);
    }

    [Fact]
    public void A_command_reads_its_title_typed_arguments_records_writes_and_confirm()
    {
        var command = Assert.Single(Plugin("""
            , "interfaces": { "commands": [{ "name": "start-stage", "title": { "en": "Start a release stage", "zh": "启动发布阶段" },
              "about": { "en": "Starts a stage.", "zh": "启动阶段。" },
              "args": [{ "name": "stage", "type": "text", "title": { "en": "Stage", "zh": "阶段" }, "required": true },
                       { "name": "wait", "type": "boolean" }, { "name": "speed", "type": "choice", "choices": ["slow", "fast"] }],
              "on": ["session", "run"], "writes": true, "confirm": { "en": "Start this stage now?", "zh": "现在启动此阶段？" }, "tool": true }] }
            """).Manifest.Interfaces.Commands);

        Assert.Equal(("启动发布阶段", "Starts a stage."), (command.Title.In("zh"), command.About.In("en")));
        Assert.Equal(["stage text required 阶段", "wait boolean wait", "speed choice slow,fast speed"],
            command.Args.Select(arg => $"{arg.Name} {arg.Type}{(arg.Choices.Count > 0 ? " " + string.Join(",", arg.Choices) : "")}{(arg.Required ? " required" : "")} {arg.Title.In("zh")}"));
        Assert.Equal(["session", "run"], command.On);
        Assert.True(command.Writes);
        Assert.Equal("Start this stage now?", command.Confirm!.In("en"));

        // Unwritten, the title is the command's name and nothing is asked before it runs.
        var plain = Assert.Single(Plugin(""", "interfaces": { "commands": [{ "name": "status", "about": "x" }] } """).Manifest.Interfaces.Commands);
        Assert.Equal(("status", false, null, 0), (plain.Title.AsWritten, plain.Writes, plain.Confirm, plain.Args.Count));
    }

    [Fact]
    public void A_view_reads_where_it_stands_the_places_it_is_about_and_what_it_is_told()
    {
        var views = Plugin("""
            , "interfaces": { "views": [{ "id": "pull-requests", "title": { "en": "Pull requests", "zh": "拉取请求" }, "icon": "icons/pr.svg",
              "where": ["side", "page"], "places": ["repository", "workspace"], "context": ["session"], "body": "drawn" },
              { "id": "overview", "title": "Overview" }] }
            """).Manifest.Interfaces.Views;

        Assert.Equal(("拉取请求", "icons/pr.svg"), (views[0].Title.In("zh"), views[0].Icon));
        Assert.Equal("side page | repository workspace | session", Said(views[0]));
        // Unwritten, a view stands on its plugin's page, is about the machine, and is told nothing of the window.
        Assert.Equal("page | machine | ", Said(views[1]));

        static string Said(PluginViewDeclared view) => $"{string.Join(" ", view.Where)} | {string.Join(" ", view.Places)} | {string.Join(" ", view.Context)}";
    }

    /// <summary>[case, the commands declared, storage's forget, the forget kept or null, why it is disabled].</summary>
    [Theory]
    [InlineData("a command that runs alone", """{ "name": "logout", "about": "x" }""", "logout", "logout", null)]
    [InlineData("none named", """{ "name": "logout", "about": "x" }""", null, null, null)]
    [InlineData("a command not declared", """{ "name": "logout", "about": "x" }""", "sign-out", "sign-out", "`forget` names `sign-out`, which is not a command the plugin declares.")]
    [InlineData("a refused command", """{ "name": "logout" }""", "logout", "logout", "`forget` names `logout`, which is refused: command `logout` needs an `about`: what it does, in a sentence.")]
    [InlineData("a command that asks the person", """{ "name": "logout", "about": "x", "interactive": true }""", "logout", "logout", "`forget` names `logout`, which asks the person, and a removal has no one to answer it.")]
    public void Storage_s_forget_names_a_command_and_one_it_cannot_run_is_disabled_with_the_reason(
        string @case, string commands, string? forget, string? kept, string? disabled)
    {
        var named = forget is null ? "" : $", \"forget\": \"{forget}\"";
        var storage = Plugin($$""", "interfaces": { "commands": [{{commands}}], "storage": { "for": "x"{{named}} } } """).Manifest.Interfaces.Storage!;

        Assert.True((kept, disabled) == (storage.Forget, storage.ForgetDisabled), $"{@case}: {storage.Forget} / {storage.ForgetDisabled}");
    }

    [Fact]
    public void What_is_not_reached_is_said_entry_by_entry()
    {
        var interfaces = Plugin("""
            , "interfaces": { "commands": [{ "name": "login" }], "storage": { "for": "x", "forget": "login" }, "settings": {} }
            """).Manifest.Interfaces;

        Assert.Equal(
        [
            "not reached: command `login` needs an `about`: what it does, in a sentence.",
            "interface `settings` is not one this build knows, so it is skipped.",
            "storage's forget is not run: `forget` names `login`, which is refused: command `login` needs an `about`: what it does, in a sentence.",
        ], PluginInterfaceLines.Of(interfaces));
    }

    [Theory]
    [InlineData("daoris-azure-devops")]
    [InlineData("daoris-github")]
    public void Each_pull_request_example_signs_in_through_its_own_commands_and_storage(string example)
    {
        var (manifest, problem) = PluginCatalog.ReadFolder(Path.Combine(WorkspaceRoot.Folder, "examples", "plugins", example));
        Assert.Null(problem);
        Assert.Equal(["login", "logout", "status"], manifest.Interfaces.Commands.Select(command => command.Name));
        Assert.True(manifest.Interfaces.Commands[0].Interactive);
        Assert.NotNull(manifest.Interfaces.Storage);
        Assert.Empty(manifest.Interfaces.Refused);
    }
}
