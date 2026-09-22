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
}
