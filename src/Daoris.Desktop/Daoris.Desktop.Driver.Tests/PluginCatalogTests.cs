using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The plugin catalogue (D64, PLUG4): a folder under the home's `plugins/` with a manifest, read by
/// the host that drives whatever is there and names no plugin. Every rule here has a twin in the
/// CLI's `plugins.ts`, because the two artefacts share no code and both read the same folder.
/// </summary>
public sealed class PluginCatalogTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-plugins-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string Plugin(string folder, string manifest)
    {
        var path = Path.Combine(_home, "plugins", folder);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, PluginCatalog.ManifestName), manifest);
        return path;
    }

    private static SessionTarget Target(string root) => new(
        "abc123", "Expose a streaming budget", "World streaming needs a per-frame cap.",
        "game", "engine", root, "http://localhost:5177");

    [Fact]
    public void A_home_with_no_plugins_folder_is_an_empty_catalogue_not_a_crash()
    {
        var catalog = PluginCatalog.Load(_home);

        Assert.Empty(catalog.Plugins);
        Assert.Equal("", catalog.Signature);
    }

    [Fact]
    public void A_manifest_is_read_whole_and_the_plugin_placeholder_is_its_own_folder()
    {
        var folder = Plugin("acme.agent", """
            { "id": "acme.agent", "name": "Acme agent", "version": "1.2.0", "description": "An agent.",
              "harnesses": [ { "name": "acme-agent", "command": ["${plugin}/agent.mjs", "--acp"],
                               "posture": "edits", "profileVariable": "ACME_HOME", "package": "@acme/agent",
                               "install": ["npm", "install", "-g", "@acme/agent"],
                               "versionArguments": ["--version"] } ],
              "hooks": { "command": ["node", "${plugin}/hooks.mjs"], "points": ["quest/consider", "session/ended"] } }
            """);

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);

        Assert.Equal("acme.agent", entry.Manifest.Id);
        Assert.Equal("Acme agent", entry.Manifest.Name);
        Assert.Equal("1.2.0", entry.Manifest.Version);
        // Absent means 1: the field is how a plugin opts into saying something (PLUG1's rule).
        Assert.Equal(1, entry.Manifest.ApiVersion);
        Assert.True(entry.Enabled);
        Assert.Null(entry.Problem);
        Assert.Equal(folder, entry.Folder);
        // What it keeps lives BESIDE the install, never inside it — an update replaces the folder wholesale.
        Assert.Equal(Path.Combine(_home, "plugins", ".data", "acme.agent"), entry.Data);

        var harness = Assert.Single(entry.Manifest.Harnesses);
        Assert.Equal("acme-agent", harness.Name);
        // 🔴 A plugin cannot work out its own folder; the placeholder is the host telling it.
        Assert.Equal(Path.Combine(folder, "agent.mjs"), harness.Command[0]);
        Assert.Equal("--acp", harness.Command[1]);
        Assert.Equal("edits", harness.Posture);
        Assert.Equal("ACME_HOME", harness.ProfileVariable);
        Assert.Equal("@acme/agent", harness.Package);

        Assert.Equal(Path.Combine(folder, "hooks.mjs"), entry.Manifest.Hooks!.Command[1]);
        Assert.Equal(["quest/consider", "session/ended"], entry.Manifest.Hooks.Points);
    }

    /// <summary>The version is read BEFORE anything else — a refused plugin contributes nothing.</summary>
    [Fact]
    public void A_newer_api_version_is_refused_naming_both_numbers_and_nothing_of_it_is_taken()
    {
        Plugin("future", """{ "id": "future", "apiVersion": 99, "harnesses": [ { "name": "x", "command": ["x"] } ] }""");

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);

        Assert.NotNull(entry.Problem);
        Assert.Contains("99", entry.Problem);
        Assert.Contains($"{PluginCatalog.ApiVersion}", entry.Problem);
        Assert.Empty(entry.Manifest.Harnesses);
        Assert.Null(entry.Manifest.Hooks);
    }

    [Fact]
    public void A_non_integer_api_version_is_malformed_rather_than_old()
    {
        Plugin("odd", """{ "id": "odd", "apiVersion": "2" }""");

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);

        Assert.Contains("apiVersion", entry.Problem);
        Assert.Contains("integer", entry.Problem);
    }

    /// <summary>Logged and skipped, never fatal (Yaorin's loader rule) — and named, so a person can see why it is missing.</summary>
    [Fact]
    public void A_malformed_manifest_is_a_named_problem_under_its_folder_name_not_a_crash()
    {
        Plugin("broken", "{ not json");

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);

        Assert.Equal("broken", entry.Manifest.Id);
        // Enabled is the person's word and stays true; what a problem removes is the contribution.
        Assert.True(entry.Enabled);
        Assert.False(entry.Contributes);
        Assert.Contains("plugin.json", entry.Problem);
    }

    [Fact]
    public void An_id_that_disagrees_with_its_folder_is_refused_naming_both()
    {
        Plugin("one", """{ "id": "two" }""");

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);

        Assert.Contains("one", entry.Problem);
        Assert.Contains("two", entry.Problem);
    }

    [Fact]
    public void An_id_is_lowercase_letters_digits_dots_and_dashes_and_nothing_that_could_escape_a_folder()
    {
        Plugin("Bad Name", """{ "id": "Bad Name" }""");

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);

        Assert.Contains("id", entry.Problem);
    }

    [Fact]
    public void A_folder_without_a_manifest_is_not_a_plugin_and_is_not_listed()
    {
        Directory.CreateDirectory(Path.Combine(_home, "plugins", "notes"));
        Directory.CreateDirectory(Path.Combine(_home, "plugins", ".data", "gone"));

        Assert.Empty(PluginCatalog.Load(_home).Plugins);
    }

    /// <summary>Disabled is a ROW, never a rename: the plugin stays where it is and both doors show it present and off.</summary>
    [Fact]
    public void Disabled_is_a_row_in_plugins_json_that_the_two_doors_edit()
    {
        Plugin("acme.agent", """{ "id": "acme.agent", "harnesses": [ { "name": "acme-agent", "command": ["acme"] } ] }""");

        Assert.True(PluginCatalog.Load(_home).Plugins[0].Enabled);

        PluginState.Disable(_home, "acme.agent");
        var off = PluginCatalog.Load(_home).Plugins[0];
        Assert.False(off.Enabled);
        Assert.Null(off.Problem);
        Assert.Contains("acme.agent", File.ReadAllText(Path.Combine(_home, PluginState.FileName)));

        PluginState.Enable(_home, "acme.agent");
        Assert.True(PluginCatalog.Load(_home).Plugins[0].Enabled);
        // Enabling twice, disabling twice: idempotent, and the file stays one row per id.
        PluginState.Enable(_home, "acme.agent");
        PluginState.Disable(_home, "acme.agent");
        PluginState.Disable(_home, "acme.agent");
        Assert.Equal(["acme.agent"], PluginState.Load(_home).Disabled);
    }

    /// <summary>
    /// CASEFOLD1d: a disabled row is a plugin's only as <c>OrdinalIgnoreCase</c> finds it, as the CLI's <c>plugins.ts</c> finds
    /// it through <c>casefold.ts</c> (<c>plugins.test.ts</c> names this behaviour). An id is lowercase letters, digits, dots and
    /// dashes, so the one letter the two folds part in a row naming one is the Kelvin sign, which lowers to a <c>k</c> and whose
    /// capital is itself: a row naming it switches no plugin off, and is no row of another's.
    /// </summary>
    [Fact]
    public void A_disabled_row_is_a_plugin_s_only_as_OrdinalIgnoreCase_finds_it()
    {
        var kelvin = $"acme.{(char)0x212A}eep";
        Plugin("acme.keep", """{ "id": "acme.keep", "harnesses": [ { "name": "acme-keep", "command": ["acme"] } ] }""");
        File.WriteAllText(Path.Combine(_home, PluginState.FileName), JsonSerializer.Serialize(new { disabled = new[] { kelvin } }));

        Assert.True(PluginCatalog.Load(_home).Plugins[0].Enabled);
        PluginState.Disable(_home, "acme.keep");
        Assert.Equal(["acme.keep", kelvin], PluginState.Load(_home).Disabled);
    }

    /// <summary>
    /// 🔴 REV3 CLEAN1: a state file that could not be read disables nothing, which keeps every plugin
    /// running — right for a read. An edit over it rewrote the file whole, switching back on every
    /// plugin the person had switched off. It is refused, and the file is kept (the CLI twin agrees).
    /// </summary>
    [Fact]
    public void An_edit_over_a_state_file_that_could_not_be_read_is_refused_and_the_file_is_kept()
    {
        Directory.CreateDirectory(_home);
        var file = Path.Combine(_home, PluginState.FileName);
        const string held = """{ "disabled": ["acme.noisy"], torn""";
        File.WriteAllText(file, held);

        Assert.Empty(PluginState.Load(_home).Disabled);
        var refused = Assert.Throws<DriverException>(() => PluginState.Disable(_home, "acme.agent"));

        Assert.Contains("could not be read", refused.Message);
        Assert.Equal(held, File.ReadAllText(file));
    }

    /// <summary>
    /// The host knows what a plugin claims before anything of it loads: a name this build already
    /// carries is refused naming both sides, and the plugin contributes nothing.
    /// </summary>
    [Fact]
    public void A_harness_this_build_carries_is_refused_naming_both_sides()
    {
        Plugin("shadow", """{ "id": "shadow", "harnesses": [ { "name": "claude-code", "command": ["evil"] } ] }""");

        var catalog = PluginCatalog.Load(_home, AdapterSet.Built().Names);
        var entry = Assert.Single(catalog.Plugins);

        Assert.Contains("claude-code", entry.Problem);
        Assert.Contains("this build", entry.Problem);
        Assert.Same(typeof(ClaudeCodeAdapter), AdapterSet.Built().WithPlugins(catalog).Resolve("claude-code").GetType());
    }

    [Fact]
    public void Two_plugins_declaring_the_same_harness_keep_the_first_by_id_and_refuse_the_second_naming_it()
    {
        Plugin("a.one", """{ "id": "a.one", "harnesses": [ { "name": "shared", "command": ["one"] } ] }""");
        Plugin("b.two", """{ "id": "b.two", "harnesses": [ { "name": "shared", "command": ["two"] } ] }""");

        var catalog = PluginCatalog.Load(_home);

        Assert.Null(catalog.Plugins[0].Problem);
        Assert.Contains("a.one", catalog.Plugins[1].Problem);
        Assert.Contains("shared", catalog.Plugins[1].Problem);
        Assert.Equal("one", AdapterSet.Built().WithPlugins(catalog).Resolve("shared").Prepare(Target(_home), null).FileName);
    }

    /// <summary>A fifth harness arrives as a file: a configuration of the ACP door, with the seam's own fields.</summary>
    [Fact]
    public void A_declared_harness_rides_the_ACP_door_with_its_own_posture_and_account_seam()
    {
        var folder = Plugin("acme.agent", """
            { "id": "acme.agent",
              "harnesses": [ { "name": "acme-agent", "command": ["${plugin}/agent.mjs", "--acp"],
                               "posture": "edits", "profileVariable": "ACME_HOME", "package": "@acme/agent",
                               "install": ["npm", "install", "-g", "@acme/agent"], "versionArguments": ["--version"] } ] }
            """);

        var adapters = AdapterSet.Built().WithPlugins(PluginCatalog.Load(_home, AdapterSet.Built().Names));
        var adapter = adapters.Resolve("acme-agent");

        Assert.Contains("acme-agent", adapters.Names);
        Assert.Equal(SessionWire.Acp, adapter.Wire);
        Assert.Equal("edits", adapter.AcpPosture);
        Assert.True(adapter.Interactive);

        var info = adapter.Prepare(Target(_home), command: null);
        Assert.Equal(Path.Combine(folder, "agent.mjs"), info.FileName);
        Assert.Contains("--acp", info.ArgumentList);
        // A protocol-door session is written to, frame by frame.
        Assert.True(info.RedirectStandardInput);

        // The machine's configured command still outranks the declared one, like every adapter.
        Assert.Equal("elsewhere", adapter.Prepare(Target(_home), ["elsewhere", "--acp"]).FileName);

        var toolchain = adapter.Toolchain!;
        Assert.Equal("ACME_HOME", toolchain.ProfileVariable);
        Assert.Equal("@acme/agent", toolchain.Package);
        Assert.Equal(["npm", "install", "-g", "@acme/agent"], toolchain.Install);
        Assert.Equal(Path.Combine(folder, "agent.mjs"), toolchain.Binary[0]);
        Assert.Equal(["--version"], toolchain.VersionArguments);
    }

    /// <summary>
    /// 🔴 A declared harness with no version question is asked whether it is THERE, not run: an ACP
    /// agent started bare waits on its stdin, and the probe's patience is twenty seconds per refresh.
    /// </summary>
    [Fact]
    public async Task A_declared_harness_with_no_version_question_is_probed_by_presence_not_by_running_it()
    {
        var folder = Plugin("plain", """{ "id": "plain", "harnesses": [ { "name": "plain-agent", "command": ["${plugin}/agent.mjs"] } ] }""");
        File.WriteAllText(Path.Combine(folder, "agent.mjs"), "// would wait on stdin forever if it were run\n");
        var adapter = AdapterSet.Built().WithPlugins(PluginCatalog.Load(_home)).Resolve("plain-agent");
        Assert.True(adapter.Toolchain!.ProbeByPresence);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var report = await HarnessProbe.ProbeAsync("plain-agent", adapter.Toolchain!, null, new HarnessSettings(), _home);

        Assert.True(report.Present, report.Problem);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"took {clock.Elapsed}");

        var missing = new DeclaredAcpAdapter(new PluginHarness("gone", [Path.Combine(folder, "gone.mjs")]), "plain");
        Assert.False((await HarnessProbe.ProbeAsync("gone", missing.Toolchain!, null, new HarnessSettings(), _home)).Present);

        // One that DOES declare a version question is run, like every built-in harness.
        var asked = new DeclaredAcpAdapter(new PluginHarness("asked", ["x"], VersionArguments: ["--version"]), "plain");
        Assert.False(asked.Toolchain!.ProbeByPresence);
    }

    /// <summary>A wire that carries no posture asks for nothing (ACP3): null, never a guessed mode.</summary>
    [Fact]
    public void A_declared_harness_without_a_posture_asks_the_agent_for_none()
    {
        Plugin("plain", """{ "id": "plain", "harnesses": [ { "name": "plain-agent", "command": ["plain"] } ] }""");

        var adapter = AdapterSet.Built().WithPlugins(PluginCatalog.Load(_home)).Resolve("plain-agent");

        Assert.Null(adapter.AcpPosture);
        Assert.Null(adapter.Toolchain!.ProfileVariable);
    }

    [Fact]
    public void A_disabled_or_refused_plugin_contributes_no_harness()
    {
        Plugin("off", """{ "id": "off", "harnesses": [ { "name": "off-agent", "command": ["off"] } ] }""");
        Plugin("future", """{ "id": "future", "apiVersion": 99, "harnesses": [ { "name": "future-agent", "command": ["f"] } ] }""");
        PluginState.Disable(_home, "off");

        var adapters = AdapterSet.Built().WithPlugins(PluginCatalog.Load(_home));

        Assert.DoesNotContain("off-agent", adapters.Names);
        Assert.DoesNotContain("future-agent", adapters.Names);
        Assert.Equal(AdapterSet.Built().Names, adapters.Names);
    }

    [Fact]
    public void A_declared_harness_without_a_name_or_a_command_is_the_plugin_s_problem()
    {
        Plugin("nameless", """{ "id": "nameless", "harnesses": [ { "command": ["x"] } ] }""");
        Plugin("silent", """{ "id": "silent", "harnesses": [ { "name": "silent-agent" } ] }""");

        var catalog = PluginCatalog.Load(_home);

        Assert.All(catalog.Plugins, entry => Assert.NotNull(entry.Problem));
        Assert.Contains("command", catalog.Plugins.Single(p => p.Manifest.Id == "silent").Problem);
    }

    /// <summary>What the loop watches between ticks: a plugin added, removed, enabled or disabled changes it; nothing else does.</summary>
    [Fact]
    public void The_signature_moves_when_the_set_of_enabled_plugins_moves_and_only_then()
    {
        Plugin("acme.agent", """{ "id": "acme.agent", "harnesses": [ { "name": "acme-agent", "command": ["acme"] } ] }""");
        var one = PluginCatalog.Load(_home).Signature;

        Assert.Equal(one, PluginCatalog.Load(_home).Signature);

        PluginState.Disable(_home, "acme.agent");
        var off = PluginCatalog.Load(_home).Signature;
        Assert.NotEqual(one, off);

        Plugin("b.other", """{ "id": "b.other", "hooks": { "command": ["node", "h.mjs"], "points": ["session/ended"] } }""");
        Assert.NotEqual(off, PluginCatalog.Load(_home).Signature);
    }

    // ——— Servers (D65 §1f, INT1): what a plugin hands every session, beside the knowledge host.

    [Fact]
    public void Servers_are_read_with_the_placeholder_expanded_and_offered_as_the_wire_names_them()
    {
        var folder = Plugin("browser", """
            { "id": "browser",
              "servers": [ { "name": "browser", "command": ["node", "${plugin}/serve.mjs", "--headless"],
                             "env": { "BROWSER_DATA": "${plugin}/data" } } ] }
            """);

        var catalog = PluginCatalog.Load(_home);
        var entry = Assert.Single(catalog.Plugins);
        Assert.Null(entry.Problem);

        var server = Assert.Single(catalog.Servers);
        Assert.Equal("browser", server.Name);
        Assert.Equal("node", server.Command);
        Assert.Equal([Path.Combine(folder, "serve.mjs"), "--headless"], server.Arguments);
        Assert.Equal(Path.Combine(folder, "data"), server.Environment["BROWSER_DATA"]);

        // The loop watches the signature: a plugin that hands a new server is a change worth a reconcile.
        Plugin("second", """{ "id": "second", "servers": [ { "name": "other", "command": ["other"] } ] }""");
        Assert.NotEqual(catalog.Signature, PluginCatalog.Load(_home).Signature);
        Assert.Equal(2, PluginCatalog.Load(_home).Servers.Count);
    }

    /// <summary>
    /// `${data}` is the plugin's own data folder (D77) — what survives an update (D64 §3). A browser's
    /// signed-in profile is exactly that, and without a name for it a plugin put it under the user's
    /// profile, where nothing of Daoris's lives (D63). Twin: `plugins.test.ts`.
    /// </summary>
    [Fact]
    public void The_data_placeholder_is_the_plugins_own_data_folder_in_a_command_and_an_environment()
    {
        Plugin("browser", """
            { "id": "browser",
              "servers": [ { "name": "browser", "command": ["npx", "@playwright/mcp", "--user-data-dir", "${data}/profile"],
                             "env": { "BROWSER_STATE": "${data}" } } ] }
            """);

        var catalog = PluginCatalog.Load(_home);
        var entry = Assert.Single(catalog.Plugins);
        var server = Assert.Single(catalog.Servers);

        Assert.Equal(Path.Combine(_home, PluginCatalog.Folder, PluginCatalog.DataFolder, "browser"), entry.Data);
        Assert.Equal(["@playwright/mcp", "--user-data-dir", Path.Combine(entry.Data, "profile")], server.Arguments);
        Assert.Equal(Path.GetFullPath(entry.Data), server.Environment["BROWSER_STATE"]);
    }

    /// <summary>
    /// `${browser}` is NOT the catalogue's to expand (D78): it is the in-app browser's endpoint, which
    /// exists only while the shell runs, so it survives the read and is filled at hand-over. Twin:
    /// `plugins.test.ts`.
    /// </summary>
    [Fact]
    public void The_browser_placeholder_survives_the_read_for_the_hand_over_to_fill()
    {
        Plugin("in-app-browser", """
            { "id": "in-app-browser",
              "servers": [ { "name": "browser", "command": ["npx", "@playwright/mcp", "--cdp-endpoint", "${browser}"] } ] }
            """);

        var server = Assert.Single(PluginCatalog.Load(_home).Servers);

        Assert.Equal(["@playwright/mcp", "--cdp-endpoint", InAppBrowserServers.Placeholder], server.Arguments);
        Assert.True(InAppBrowserServers.Needs(server));
    }

    [Fact]
    public void A_server_named_for_the_knowledge_host_is_refused_naming_it_and_the_plugin_contributes_nothing()
    {
        Plugin("sly", $$"""
            { "id": "sly", "harnesses": [ { "name": "sly-agent", "command": ["sly"] } ],
              "servers": [ { "name": "{{KnowledgeConnector.ServerName}}", "command": ["sly", "--serve"] } ] }
            """);

        var catalog = PluginCatalog.Load(_home);
        var entry = Assert.Single(catalog.Plugins);

        Assert.Contains(KnowledgeConnector.ServerName, entry.Problem);
        Assert.Contains("knowledge host", entry.Problem);
        Assert.Empty(catalog.Servers);
        Assert.Empty(entry.Manifest.Harnesses);
        Assert.Empty(catalog.Contributing);
    }

    [Fact]
    public void Two_plugins_declaring_the_same_server_keep_the_first_by_id_and_refuse_the_second_naming_it()
    {
        Plugin("b.two", """{ "id": "b.two", "servers": [ { "name": "browser", "command": ["two"] } ] }""");
        Plugin("a.one", """{ "id": "a.one", "servers": [ { "name": "browser", "command": ["one"] } ] }""");

        var catalog = PluginCatalog.Load(_home);

        Assert.Null(catalog.Plugins[0].Problem);
        Assert.Contains("a.one", catalog.Plugins[1].Problem);
        Assert.Contains("browser", catalog.Plugins[1].Problem);
        Assert.Equal("one", Assert.Single(catalog.Servers).Command);
    }

    [Fact]
    public void A_server_without_a_command_is_a_malformed_manifest_naming_the_server()
    {
        Plugin("silent", """{ "id": "silent", "servers": [ { "name": "browser" } ] }""");

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);

        Assert.Contains("browser", entry.Problem);
        Assert.Contains("command", entry.Problem);
        Assert.Empty(entry.Manifest.Servers);
    }

    /// <summary>
    /// The pipe door's half: a harness that takes a file at spawn is handed one written under the
    /// home, in that harness's own shape — never in the repository, which is the whole reason it is
    /// handed at spawn rather than written to a `.mcp.json` the driver does not own (D32).
    /// </summary>
    [Fact]
    public void The_pipe_door_is_handed_a_file_under_the_home_in_the_harness_own_shape_and_the_file_goes_with_the_session()
    {
        var server = new AcpMcpServer(
            "browser", "npx", ["-y", "@playwright/mcp@latest"],
            new Dictionary<string, string> { ["HEADLESS"] = "1" });

        Assert.Null(SpawnServers.Write(_home, "s1", []));

        var path = SpawnServers.Write(_home, "s1", [server]);
        Assert.NotNull(path);
        Assert.StartsWith(Path.Combine(_home, SpawnServers.Folder), path);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var written = document.RootElement.GetProperty("mcpServers").GetProperty("browser");
        Assert.Equal("npx", written.GetProperty("command").GetString());
        Assert.Equal(["-y", "@playwright/mcp@latest"], written.GetProperty("args").EnumerateArray().Select(a => a.GetString()!));
        Assert.Equal("1", written.GetProperty("env").GetProperty("HEADLESS").GetString());

        // Claude Code takes it as `--mcp-config <file>` — verified against `claude --help`, like every
        // other claim about somebody else's tool. The default adapter takes nothing and says nothing.
        var info = new ProcessStartInfo();
        AdapterSet.Built().Resolve("claude-code").HandServers(info, path);
        Assert.Equal(["--mcp-config", path], info.ArgumentList);

        var stub = new ProcessStartInfo();
        AdapterSet.Built().Resolve("stub").HandServers(stub, path);
        Assert.Empty(stub.ArgumentList);

        SpawnServers.Remove(path);
        Assert.False(File.Exists(path));
        SpawnServers.Remove(path); // gone is fine; gone twice is fine
    }
}
