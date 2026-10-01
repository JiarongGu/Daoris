using System.ComponentModel;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The host's answers to the Plugins view (PLUGUI1e, D119 §4.1): the list's extended <c>PLUGINS</c>, a plugin's page
/// (<c>PLUGIN</c>), its activity (<c>PLUGIN_ACTIVITY</c>), a folder read and installed from (<c>PLUGIN_READ</c>,
/// <c>PLUGIN_ADD</c>), a folder opened (<c>PLUGIN_OPEN_FOLDER</c>), and the screen's trial in the machine log.
/// </summary>
/// <remarks>
/// Beside <see cref="DriverModulePluginsTests"/>, not in it: that class tries plugins in real processes and runs in the
/// suite's Process half (MOD8), and nothing here starts one. The trial below is of a plugin the driver refuses, which a
/// trial answers from its manifest alone.
/// </remarks>
public sealed class DriverModulePluginPageTests : DriverModuleBridge, IDisposable
{
    private const string Secret = "s3cr3t-token-value";

    private string Checkout => Path.Combine(Home, "..", "checkout-" + Path.GetFileName(Home));

    // Re-implemented so the checkout beside the home, which the base knows nothing of, goes with it.
    public new void Dispose()
    {
        try { Directory.Delete(Checkout, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        base.Dispose();
    }

    /// <summary>A plugin's folder in a checkout outside the home, as a plugins repository holds one.</summary>
    private string Folder(string name, string manifest)
    {
        var folder = Path.Combine(Checkout, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), manifest);
        return folder;
    }

    private static string Gate(string version = "1.0.0") => $$"""
        { "id": "acme.gate", "name": "Gate", "version": "{{version}}", "description": "Holds a quest whose title asks it to.",
          "harnesses": [ { "name": "acme-agent", "command": ["node", "${plugin}/agent.mjs"] } ],
          "hooks": { "command": ["node", "${plugin}/hooks.mjs"], "points": ["quest/consider", "session/ended"] },
          "servers": [ { "name": "tickets", "command": ["node", "${plugin}/tickets.mjs"], "env": { "TICKETS_TOKEN": "{{Secret}}" } } ] }
        """;

    private static JsonElement Row(JsonElement answered, string id) =>
        answered.GetProperty("plugins").EnumerateArray().Single(p => p.GetProperty("id").GetString() == id);

    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(e => e.GetString()!)];

    /// <summary>
    /// P3 (D119 §4.1): the list answers what each plugin hands sessions and speaks on, which points its running process
    /// listens on, its health from the loop's own record, and whether its source declares something new. The list
    /// stays light: names, a command as written, a state, a word.
    /// </summary>
    [Fact]
    public async Task The_list_answers_each_plugins_servers_hook_listening_health_and_whether_an_update_waits()
    {
        var gate = Folder("gate", Gate());
        PluginInstall.Add(Home, gate, AdapterSet.Built().Names);
        var bare = Path.Combine(Home, "plugins", "acme.bare");
        Directory.CreateDirectory(bare);
        File.WriteAllText(Path.Combine(bare, "plugin.json"), """{ "id": "acme.bare" }""");
        var future = Path.Combine(Home, "plugins", "acme.future");
        Directory.CreateDirectory(future);
        File.WriteAllText(Path.Combine(future, "plugin.json"), """{ "id": "acme.future", "apiVersion": 99 }""");
        using var loop = Loop();
        var module = new DriverModule(Bus, loop) { Offers = Path.Combine(Home, "no-offers") };

        var answered = await AnswerAsync(module, "PLUGINS");
        var row = Row(answered, "acme.gate");
        Assert.Equal(["tickets"], Strings(row.GetProperty("servers")));
        // The command as its manifest writes it, the page's way: `${plugin}`, never the install folder.
        Assert.Equal(["node", "${plugin}/hooks.mjs"], Strings(row.GetProperty("hook").GetProperty("command")));
        Assert.Equal(["quest/consider", "session/ended"], Strings(row.GetProperty("hook").GetProperty("points")));
        Assert.Empty(row.GetProperty("listening").EnumerateArray());
        Assert.Equal("ready", row.GetProperty("health").GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("health").GetProperty("failure").ValueKind);
        Assert.Equal("current", row.GetProperty("update").GetString());
        Assert.DoesNotContain(Secret, answered.GetRawText());
        // No record of where it came from: whether an update waits has no answer, and says none.
        Assert.Equal(JsonValueKind.Null, Row(answered, "acme.bare").GetProperty("update").ValueKind);
        Assert.Equal(JsonValueKind.Null, Row(answered, "acme.bare").GetProperty("hook").ValueKind);
        var refused = Row(answered, "acme.future");
        Assert.Equal("refused", refused.GetProperty("health").GetProperty("state").GetString());
        Assert.Empty(refused.GetProperty("servers").EnumerateArray());

        // The loop's record speaks: its process up and listening, then failing at a point, with where, what and when.
        var record = new PluginLog(null, loop.Health);
        record.Started("acme.gate", ["quest/consider"], 40, PluginEvents.ByLoop);
        File.WriteAllText(Path.Combine(gate, "plugin.json"), Gate("1.1.0"));
        var running = Row(await AnswerAsync(module, "PLUGINS"), "acme.gate");
        Assert.Equal("running", running.GetProperty("health").GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.String, running.GetProperty("health").GetProperty("since").ValueKind);
        Assert.Equal(["quest/consider"], Strings(running.GetProperty("listening")));
        Assert.Equal("waits", running.GetProperty("update").GetString());

        record.Failed("acme.gate", "quest/consider", PluginEvents.Late, null, 10_000, PluginEvents.ByLoop);
        var failing = Row(await AnswerAsync(module, "PLUGINS"), "acme.gate").GetProperty("health");
        Assert.Equal("failing", failing.GetProperty("state").GetString());
        Assert.Equal(("quest/consider", "late"),
            (failing.GetProperty("failure").GetProperty("where").GetString(), failing.GetProperty("failure").GetProperty("kind").GetString()));
        Assert.Equal(JsonValueKind.String, failing.GetProperty("failure").GetProperty("at").ValueKind);

        await AnswerAsync(module, "PLUGIN_ACTION", new { id = "acme.gate", action = "disable" });
        Assert.Equal("off", Row(await AnswerAsync(module, "PLUGINS"), "acme.gate").GetProperty("health").GetProperty("state").GetString());
    }

    /// <summary>
    /// A plugin's page is the driver's reader's (<see cref="PluginPage.Read"/>), with its health from the loop's own
    /// record. A server's environment is answered by name, never by value; a plugin no longer here is refused by the
    /// code the page reads as gone (D119 §3.2, D48 §6).
    /// </summary>
    [Fact]
    public async Task A_plugins_page_is_the_drivers_reader_with_the_loops_health_and_one_gone_is_refused_by_its_code()
    {
        PluginInstall.Add(Home, Folder("gate", Gate()), AdapterSet.Built().Names);
        var kept = Path.Combine(Home, "plugins", ".data", "acme.gate");
        Directory.CreateDirectory(kept);
        File.WriteAllText(Path.Combine(kept, "state.json"), "{}");
        using var loop = Loop();
        new PluginLog(null, loop.Health).Started("acme.gate", ["quest/consider"], 40, PluginEvents.ByLoop);
        var module = new DriverModule(Bus, loop) { Offers = Path.Combine(Home, "no-offers") };

        var page = await AnswerAsync(module, "PLUGIN", new { id = "acme.gate" });

        Assert.Equal(("acme.gate", "Gate", "1.0.0"),
            (page.GetProperty("id").GetString(), page.GetProperty("name").GetString(), page.GetProperty("version").GetString()));
        Assert.Equal("loop", page.GetProperty("healthFrom").GetString());
        Assert.Equal("running", page.GetProperty("health").GetProperty("state").GetString());
        var points = page.GetProperty("points").EnumerateArray().ToList();
        Assert.Equal(["quest/consider", "session/ended"], points.Select(p => p.GetProperty("name").GetString()!).ToArray());
        Assert.Equal("decision", points[0].GetProperty("kind").GetString());
        Assert.Equal(10_000, points[0].GetProperty("waitMs").GetInt64());
        Assert.Equal([true, false], points.Select(p => p.GetProperty("listening").GetBoolean()).ToArray());
        var server = Assert.Single(page.GetProperty("servers").EnumerateArray());
        Assert.Equal(["TICKETS_TOKEN"], Strings(server.GetProperty("environment")));
        Assert.Equal("acme-agent", Assert.Single(page.GetProperty("agents").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal("folder", page.GetProperty("source").GetProperty("kind").GetString());
        Assert.Equal("current", page.GetProperty("source").GetProperty("update").GetString());
        Assert.True(page.GetProperty("data").GetProperty("exists").GetBoolean());
        Assert.Equal(1, page.GetProperty("data").GetProperty("files").GetInt32());
        Assert.DoesNotContain(Secret, page.GetRawText());

        Assert.Contains("PLUGIN_UNKNOWN", await RefusalAsync(module, "PLUGIN", new { id = "acme.gone" }));
    }

    /// <summary>
    /// Activity is the driver's reader over the machine log (<see cref="PluginActivity.Read"/>), for the period the page
    /// chose: the last day, 7 days by default, or 30. A period the log cannot read is refused by the log's own code.
    /// </summary>
    [Fact]
    public async Task Activity_answers_what_the_machine_log_holds_for_the_period_asked()
    {
        PluginInstall.Add(Home, Folder("gate", Gate()), AdapterSet.Built().Names);
        void Write(DateTimeOffset at, Action<PluginLog> write)
        {
            using var log = new MachineLog(Home, "desktop", () => at);
            write(new PluginLog(log));
        }

        var now = DateTimeOffset.UtcNow;
        Write(now.AddDays(-3), log => log.Called("acme.gate", "quest/consider", PluginEvents.Hold, 30));
        Write(now.AddMinutes(-5), log =>
        {
            log.Called("acme.gate", "quest/consider", PluginEvents.Allow, 10);
            log.Failed("acme.gate", "quest/consider", PluginEvents.Late, null, 10_000, PluginEvents.ByLoop);
            log.Served("acme.gate", "tickets", "s1a2b3c4", handed: true);
        });
        var module = Module();

        var day = await AnswerAsync(module, "PLUGIN_ACTIVITY", new { id = "acme.gate", since = "1d" });
        Assert.True(day.GetProperty("logged").GetBoolean());
        var point = day.GetProperty("points").EnumerateArray().Single(p => p.GetProperty("point").GetString() == "quest/consider");
        Assert.Equal(1, point.GetProperty("answers").GetProperty("allow").GetInt32());
        Assert.False(point.GetProperty("answers").TryGetProperty("hold", out _));
        Assert.Equal(1, point.GetProperty("failures").GetProperty("late").GetInt32());
        var tickets = Assert.Single(day.GetProperty("servers").EnumerateArray());
        Assert.Equal(1, tickets.GetProperty("handed").GetInt32());
        Assert.Equal(3, day.GetProperty("recent").GetArrayLength());

        // Without a period, the page's opening one: the last 7 days, which reaches the hold.
        var week = await AnswerAsync(module, "PLUGIN_ACTIVITY", new { id = "acme.gate" });
        Assert.Equal(1, week.GetProperty("points").EnumerateArray()
            .Single(p => p.GetProperty("point").GetString() == "quest/consider").GetProperty("answers").GetProperty("hold").GetInt32());

        Assert.Contains("LOG_FILTER_UNKNOWN", await RefusalAsync(module, "PLUGIN_ACTIVITY", new { id = "acme.gate", since = "a week" }));
        Assert.Contains("PLUGIN_UNKNOWN", await RefusalAsync(module, "PLUGIN_ACTIVITY", new { id = "acme.gone" }));
    }

    /// <summary>
    /// P7's host half (D119 §3.4): before the press, what a folder's plugin would run, read as Ask Daoris's judge reads
    /// a folder; or the refusal, as an ANSWER, since a drawer shows it in place. Reading copies nothing.
    /// </summary>
    [Fact]
    public async Task Reading_a_folder_says_what_its_plugin_would_run_or_answers_the_refusal_and_copies_nothing()
    {
        var gate = Folder("gate", Gate());
        var module = Module();

        var read = await AnswerAsync(module, "PLUGIN_READ", new { folder = gate });
        Assert.Equal(JsonValueKind.Null, read.GetProperty("refusal").ValueKind);
        var plugin = read.GetProperty("plugin");
        Assert.Equal(("acme.gate", "Gate", "1.0.0"),
            (plugin.GetProperty("id").GetString(), plugin.GetProperty("name").GetString(), plugin.GetProperty("version").GetString()));
        Assert.Equal(["node", "${plugin}/hooks.mjs"], Strings(plugin.GetProperty("hook").GetProperty("command")));
        Assert.Equal(["quest/consider", "session/ended"], Strings(plugin.GetProperty("hook").GetProperty("points")));
        Assert.Equal("acme-agent", Assert.Single(plugin.GetProperty("agents").EnumerateArray()).GetProperty("name").GetString());
        var server = Assert.Single(plugin.GetProperty("servers").EnumerateArray());
        Assert.Equal(["TICKETS_TOKEN"], Strings(server.GetProperty("environment")));
        Assert.DoesNotContain(Secret, read.GetRawText());
        Assert.False(Directory.Exists(Path.Combine(Home, "plugins", "acme.gate")));

        async Task<string> Refused(string folder)
        {
            var answered = await AnswerAsync(module, "PLUGIN_READ", new { folder });
            Assert.Equal(JsonValueKind.Null, answered.GetProperty("plugin").ValueKind);
            return answered.GetProperty("refusal").GetString()!;
        }

        var empty = Path.Combine(Checkout, "empty");
        Directory.CreateDirectory(empty);
        Assert.Contains("no `plugin.json`", await Refused(empty));
        Assert.Contains("there is no folder", await Refused(Path.Combine(Checkout, "nowhere")));
        Assert.Contains("named whole", await Refused("gate"));
        var inside = Path.Combine(Home, "plugins", ".data");
        Directory.CreateDirectory(inside);
        Assert.Contains("inside Daoris's home", await Refused(inside));
        Assert.Contains("99", await Refused(Folder("future", """{ "id": "acme.future", "apiVersion": 99 }""")));

        // An id already installed is refused before the press, naming the two ways to replace it.
        PluginInstall.Add(Home, gate, AdapterSet.Built().Names);
        var installed = await Refused(gate);
        Assert.Contains("already installed", installed);
        Assert.Contains("Update…", installed);
        Assert.Contains("daoris plugin add", installed);
    }

    /// <summary>
    /// P7 (D119 §3.4): Install from a folder adds and never replaces, records where it came from, and runs nothing at
    /// the press; the loop starts what it runs at its next look.
    /// </summary>
    [Fact]
    public async Task Installing_from_a_folder_adds_and_never_replaces()
    {
        var mark = Path.Combine(Checkout, "ran.txt");
        var gate = Folder("gate", Gate());
        File.WriteAllText(Path.Combine(gate, "hooks.mjs"), $"require('fs').writeFileSync({JsonSerializer.Serialize(mark)}, 'ran');");
        var module = new DriverModule(Bus, Loop()) { Offers = Path.Combine(Home, "no-offers") };

        var added = await AnswerAsync(module, "PLUGIN_ADD", new { folder = gate });

        Assert.Equal(("acme.gate", "Gate", "1.0.0"),
            (added.GetProperty("id").GetString(), added.GetProperty("name").GetString(), added.GetProperty("version").GetString()));
        Assert.True(File.Exists(Path.Combine(Home, "plugins", "acme.gate", "hooks.mjs")));
        Assert.False(File.Exists(mark));
        var row = Row(await AnswerAsync(module, "PLUGINS"), "acme.gate");
        Assert.Equal("folder", row.GetProperty("source").GetProperty("kind").GetString());

        File.WriteAllText(Path.Combine(gate, "plugin.json"), Gate("2.0.0"));
        var again = await RefusalAsync(module, "PLUGIN_ADD", new { folder = gate });
        Assert.Contains("already installed", again);
        Assert.Contains("Update…", again);
        Assert.Contains("1.0.0", File.ReadAllText(Path.Combine(Home, "plugins", "acme.gate", "plugin.json")));

        var empty = Path.Combine(Checkout, "empty");
        Directory.CreateDirectory(empty);
        Assert.Contains("Nothing was copied", await RefusalAsync(module, "PLUGIN_ADD", new { folder = empty }));
        Assert.Single(Directory.GetDirectories(Path.Combine(Home, "plugins")), path => !Path.GetFileName(path).StartsWith('.'));
    }

    /// <summary>
    /// Open folder (D119 §4.1): the module names the folder, never the page, and opens it through the window kit's
    /// launcher, as the log's folder is. A data folder never made is information, and a launcher's failure is said.
    /// </summary>
    [Fact]
    public async Task Opening_a_folder_opens_the_one_the_module_names()
    {
        var installed = Path.Combine(Home, "plugins", "acme.gate");
        Directory.CreateDirectory(installed);
        File.WriteAllText(Path.Combine(installed, "plugin.json"), """{ "id": "acme.gate" }""");
        var opened = new List<string>();
        var module = new DriverModule(Bus, Loop(), opened.Add);

        var plugins = await AnswerAsync(module, "PLUGIN_OPEN_FOLDER", new { which = "plugins" });
        Assert.True(plugins.GetProperty("opened").GetBoolean());
        await AnswerAsync(module, "PLUGIN_OPEN_FOLDER", new { id = "acme.gate", which = "install" });
        Assert.Equal([Path.Combine(Home, "plugins"), installed], opened);

        // A plugin that keeps nothing has no folder to open: the button is absent then, and this answers a race.
        Assert.Contains("PLUGIN_NOTHING_KEPT", await RefusalAsync(module, "PLUGIN_OPEN_FOLDER", new { id = "acme.gate", which = "data" }));
        var data = Path.Combine(Home, "plugins", ".data", "acme.gate");
        Directory.CreateDirectory(data);
        await AnswerAsync(module, "PLUGIN_OPEN_FOLDER", new { id = "acme.gate", which = "data" });
        Assert.Equal(data, opened[^1]);

        Assert.Contains("PLUGIN_UNKNOWN", await RefusalAsync(module, "PLUGIN_OPEN_FOLDER", new { id = "acme.gone", which = "install" }));
        Assert.Contains("DRIVER_REFUSED", await RefusalAsync(module, "PLUGIN_OPEN_FOLDER", new { which = "elsewhere" }));
        Assert.Contains("DRIVER_REFUSED", await RefusalAsync(module, "PLUGIN_OPEN_FOLDER", new { which = "data" }));

        var failing = new DriverModule(Bus, Loop(), _ => throw new Win32Exception("no file manager answers"));
        var refused = await RefusalAsync(failing, "PLUGIN_OPEN_FOLDER", new { which = "plugins" });
        Assert.Contains("PLUGIN_FOLDER_NOT_OPENED", refused);
        Assert.Contains("no file manager answers", refused);

        // A host with no launcher opens nothing, and says so.
        Assert.False((await AnswerAsync(Module(), "PLUGIN_OPEN_FOLDER", new { which = "plugins" })).GetProperty("opened").GetBoolean());
    }

    /// <summary>
    /// The screen's trial of an installed plugin is part of its history (D119 §4.2), written with the screen's door;
    /// a folder's trial is shown and never kept, since its id may name an installed plugin it is not.
    /// </summary>
    [Fact]
    public async Task A_screen_trial_of_an_installed_plugin_is_logged_as_the_screens_and_a_folders_is_not()
    {
        const string refused = """{ "id": "acme.future", "apiVersion": 99, "hooks": { "command": ["node", "x.mjs"], "points": ["quest/consider"] } }""";
        var installed = Path.Combine(Home, "plugins", "acme.future");
        Directory.CreateDirectory(installed);
        File.WriteAllText(Path.Combine(installed, "plugin.json"), refused);
        var folder = Folder("future", refused);
        using (var log = new MachineLog(Home, "desktop"))
        {
            var module = new DriverModule(Bus, new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0", log: log));

            var tried = await AnswerAsync(module, "PLUGIN_TRY", new { id = "acme.future" });
            Assert.False(tried.GetProperty("passed").GetBoolean());
            await AnswerAsync(module, "PLUGIN_TRY", new { folder });
        }

        var lines = MachineLogReader.Read(Path.Combine(Home, MachineLog.Folder), new LogFilter()).Lines
            .Where(line => line.Event == PluginEvents.Tried).ToList();
        var line = Assert.Single(lines);
        Assert.Equal("warn", line.Level);
        Assert.Equal("acme.future", line.Data.GetProperty("plugin").GetString());
        Assert.Equal("screen", line.Data.GetProperty("door").GetString());
        Assert.False(line.Data.GetProperty("passed").GetBoolean());
    }
}
