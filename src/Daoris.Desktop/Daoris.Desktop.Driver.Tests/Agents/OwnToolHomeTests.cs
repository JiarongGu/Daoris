using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's own tool home reaches a session (D253 point 7, census row 23): what the person's own run of Claude Code loads from its
/// home is laid out for the session as one harness plugin, <c>own</c>, under the session's spawn folder, and handed by the door's
/// session-only way. The person's folder is read, never written; each family is the person's to switch off; nothing is laid out where
/// the session runs in the person's own home, which loads it all by itself.
/// </summary>
public sealed class OwnToolHomeTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-own-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "daoris");
    private string Person => Path.Combine(_root, "person");
    private string Account => Path.Combine(_root, "account");
    private string ToolHome => Path.Combine(Person, ".claude");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Put(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private void PersonsPieces()
    {
        Put(Path.Combine(ToolHome, "skills", "review", "SKILL.md"), "---\nname: review\ndescription: Review the diff.\n---\n");
        Put(Path.Combine(ToolHome, "skills", "review", "checklist.md"), "the checklist");
        Put(Path.Combine(ToolHome, "agents", "scout.md"), "---\nname: scout\ndescription: Scouts.\n---\n");
        Put(Path.Combine(ToolHome, "commands", "ship.md"), "Ship it.");
        Put(Path.Combine(ToolHome, "settings.json"), "{}");
    }

    private OwnTools Tools(OwnFamilies families, string? sessionHome = null, string? ownVariable = null) => new(
        name => name == "CLAUDE_CONFIG_DIR" ? ownVariable : null,
        Person,
        name => name == "CLAUDE_CONFIG_DIR" ? sessionHome ?? Account : null,
        families);

    [Fact]
    public void The_persons_skills_subagents_and_commands_are_copied_into_one_harness_plugin_and_their_folder_is_only_read()
    {
        PersonsPieces();

        var (folder, plugins) = ClaudeSkills.Layout.Own(Home, "s1a2b3c4", Tools(OwnFamilies.Skills));

        Assert.Equal(DaorisHome.Entry(Home, HomeEntry.Spawn, "s1a2b3c4.plugins"), folder);
        var plugin = Assert.Single(plugins);
        using (var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(plugin, ".claude-plugin", "plugin.json"))))
        {
            Assert.Equal("own", manifest.RootElement.GetProperty("name").GetString());
        }

        Assert.Equal("the checklist", File.ReadAllText(Path.Combine(plugin, "skills", "review", "checklist.md")));
        Assert.True(File.Exists(Path.Combine(plugin, "agents", "scout.md")));
        Assert.True(File.Exists(Path.Combine(plugin, "commands", "ship.md")));
        // Its settings are not this family's, and the person's own folder gains nothing.
        Assert.False(File.Exists(Path.Combine(plugin, "settings.json")));
        Assert.False(Directory.Exists(Path.Combine(ToolHome, ".claude-plugin")));
    }

    [Fact]
    public void The_tool_home_is_where_the_persons_own_run_finds_it_its_variable_first()
    {
        var elsewhere = Path.Combine(_root, "elsewhere");
        Put(Path.Combine(elsewhere, "skills", "mine", "SKILL.md"), "---\nname: mine\n---\n");
        PersonsPieces();

        var (_, plugins) = ClaudeSkills.Layout.Own(Home, "s1", Tools(OwnFamilies.Skills, ownVariable: elsewhere));

        Assert.Equal(["mine"], Directory.GetDirectories(Path.Combine(Assert.Single(plugins), "skills")).Select(Path.GetFileName));
    }

    /// <summary>
    /// A conversation begun in the person's own home goes on there (agents rule 17), and that harness loads its own pieces itself; a
    /// family the person switched off is handed nothing; a door whose provider lays out none is handed none.
    /// </summary>
    [Fact]
    public void Nothing_is_laid_out_in_the_persons_own_home_with_the_family_off_or_on_a_door_that_takes_no_plugin()
    {
        PersonsPieces();

        Assert.Empty(ClaudeSkills.Layout.Own(Home, "s1", Tools(OwnFamilies.Skills, sessionHome: ToolHome + Path.DirectorySeparatorChar)).Plugins);
        Assert.Empty(ClaudeSkills.Layout.Own(Home, "s1", Tools(OwnFamilies.None)).Plugins);
        Assert.False(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Spawn, "s1.plugins")));

        var off = PermissionRules.SwitchDefault(PermissionFile.Empty, OwnHomeDefaults.SkillsId, on: false);
        Assert.False(OwnHomeDefaults.Families(off, connectorOnly: false).HasFlag(OwnFamilies.Skills));
        Assert.True(OwnHomeDefaults.Families(PermissionFile.Empty, connectorOnly: false).HasFlag(OwnFamilies.Skills));

        foreach (var door in new[] { "codex-acp", "dsh", "stub" })
        {
            var info = new ProcessStartInfo();
            var (folder, plugins) = SpawnSkills.Hand(
                ProviderRegistry.Built(), ProviderRegistry.Built().Door(door), info, Home, "s2", PluginCatalog.Load(Home), PluginServerTo.Sessions,
                Tools(OwnFamilies.Skills));
            Assert.Null(folder);
            Assert.Empty(plugins);
            Assert.Empty(info.ArgumentList);
        }
    }

    private const string Servers = """
        { "oauthAccount": { "emailAddress": "someone@example.invalid" },
          "mcpServers": {
            "notes": { "type": "stdio", "command": "node", "args": ["notes.js"], "env": { "NOTES_DIR": "n" } },
            "remote": { "type": "http", "url": "https://example.invalid/mcp" },
            "daoris-knowledge": { "type": "stdio", "command": "daoris", "args": ["mcp"] },
            "keyed": { "type": "stdio", "command": "node", "args": ["k.js"], "env": { "DAORIS_SERVICE_KEY": "secret" } } },
          "projects": { "somewhere": { "mcpServers": { "local": { "command": "x" } } } } }
        """;

    /// <summary>
    /// The person's user-scope servers ride the same plugin's <c>.mcp.json</c>, as written. The floor keeps two out: one named as
    /// Daoris's connector, which every session is handed with its own identity, and one whose environment names a Daoris variable, which
    /// would act as the person on Daoris's records. Nothing else of the global file is read: not its sign-in, not a project's servers.
    /// </summary>
    [Fact]
    public void The_persons_servers_ride_the_plugins_mcp_file_but_daoris_named_or_keyed_ones()
    {
        Put(Path.Combine(Person, ".claude.json"), Servers);
        Directory.CreateDirectory(ToolHome);

        var (_, plugins) = ClaudeSkills.Layout.Own(Home, "s5", Tools(OwnFamilies.Servers));

        var plugin = Assert.Single(plugins);
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(plugin, ".mcp.json")));
        var servers = file.RootElement.GetProperty("mcpServers");
        Assert.Equal(["notes", "remote"], servers.EnumerateObject().Select(server => server.Name));
        Assert.Equal("n", servers.GetProperty("notes").GetProperty("env").GetProperty("NOTES_DIR").GetString());
        Assert.DoesNotContain("someone@", File.ReadAllText(Path.Combine(plugin, ".mcp.json")), StringComparison.Ordinal);

        Assert.False(OwnHomeDefaults.Families(PermissionFile.Empty, connectorOnly: true).HasFlag(OwnFamilies.Servers));
        Assert.True(OwnHomeDefaults.Families(PermissionFile.Empty, connectorOnly: false).HasFlag(OwnFamilies.Servers));
        var off = PermissionRules.SwitchDefault(PermissionFile.Empty, OwnHomeDefaults.ServersId, on: false);
        Assert.False(OwnHomeDefaults.Families(off, connectorOnly: false).HasFlag(OwnFamilies.Servers));
    }

    [Fact]
    public void Under_the_persons_variable_the_global_file_is_the_one_inside_it_and_skills_alone_write_no_server_file()
    {
        var elsewhere = Path.Combine(_root, "elsewhere");
        Put(Path.Combine(elsewhere, ".claude.json"), Servers);
        Put(Path.Combine(elsewhere, "skills", "mine", "SKILL.md"), "---\nname: mine\n---\n");

        var (_, both) = ClaudeSkills.Layout.Own(Home, "s6", Tools(OwnFamilies.Skills | OwnFamilies.Servers, ownVariable: elsewhere));
        Assert.True(File.Exists(Path.Combine(Assert.Single(both), ".mcp.json")));

        var (_, skills) = ClaudeSkills.Layout.Own(Home, "s7", Tools(OwnFamilies.Skills, ownVariable: elsewhere));
        Assert.False(File.Exists(Path.Combine(Assert.Single(skills), ".mcp.json")));
    }

    /// <summary>
    /// A plugin the person's user settings enable is handed at its user-scope install folder, read in place as their own run reads it,
    /// and never removed with the session; one switched off, installed for a project alone, or whose folder is gone is not.
    /// </summary>
    [Fact]
    public void The_plugins_the_persons_settings_enable_are_handed_in_place_and_outlive_the_session()
    {
        PersonsPieces();
        var tidy = Path.Combine(ToolHome, "plugins", "cache", "market", "tidy", "1.0.0");
        var off = Path.Combine(ToolHome, "plugins", "cache", "market", "off", "1.0.0");
        var project = Path.Combine(ToolHome, "plugins", "cache", "market", "project", "1.0.0");
        foreach (var each in new[] { tidy, off, project }) Put(Path.Combine(each, ".claude-plugin", "plugin.json"), "{}");
        Put(Path.Combine(ToolHome, "settings.json"), JsonSerializer.Serialize(new
        {
            enabledPlugins = new Dictionary<string, bool> { ["tidy@market"] = true, ["off@market"] = false, ["project@market"] = true, ["gone@market"] = true },
        }));
        Put(Path.Combine(ToolHome, "plugins", "installed_plugins.json"), JsonSerializer.Serialize(new
        {
            version = 2,
            plugins = new Dictionary<string, object[]>
            {
                ["tidy@market"] = [new { scope = "user", installPath = tidy, version = "1.0.0" }],
                ["off@market"] = [new { scope = "user", installPath = off, version = "1.0.0" }],
                ["project@market"] = [new { scope = "project", projectPath = "somewhere", installPath = project, version = "1.0.0" }],
                ["gone@market"] = [new { scope = "user", installPath = Path.Combine(ToolHome, "plugins", "cache", "gone"), version = "1.0.0" }],
            },
        }));

        var (folder, plugins) = ClaudeSkills.Layout.Own(Home, "s8", Tools(OwnFamilies.Plugins));

        Assert.Null(folder);
        Assert.Equal([tidy], plugins);

        var info = new ProcessStartInfo();
        var (handed, all) = SpawnSkills.Hand(
            ProviderRegistry.Built(), ProviderRegistry.Built().Door("claude-code"), info, Home, "s9", PluginCatalog.Load(Home), PluginServerTo.Sessions,
            Tools(OwnFamilies.Plugins | OwnFamilies.Skills));
        Assert.Equal(["--plugin-dir", all[0], "--plugin-dir", tidy], info.ArgumentList);
        SpawnFiles.RemoveFolder(handed);
        Assert.True(File.Exists(Path.Combine(tidy, ".claude-plugin", "plugin.json")));

        var switched = PermissionRules.SwitchDefault(PermissionFile.Empty, OwnHomeDefaults.PluginsId, on: false);
        Assert.False(OwnHomeDefaults.Families(switched, connectorOnly: false).HasFlag(OwnFamilies.Plugins));
    }

    /// <summary>
    /// The person's Chrome switch (<c>/chrome</c>'s enabled by default, kept in their global file) is handed as <c>--chrome</c> on the
    /// native door, as their own run turns Claude in Chrome on; not where they left it off, not where they switched the family off, and
    /// never on a door that does not run <c>claude</c> itself.
    /// </summary>
    [Fact]
    public void The_persons_chrome_switch_is_handed_on_the_native_door()
    {
        Directory.CreateDirectory(ToolHome);
        Put(Path.Combine(Person, ".claude.json"), """{ "claudeInChromeDefaultEnabled": true }""");

        var native = new ProcessStartInfo();
        SpawnSkills.Hand(
            ProviderRegistry.Built(), ProviderRegistry.Built().Door("claude-code"), native, Home, "s10", PluginCatalog.Load(Home), PluginServerTo.Sessions,
            Tools(OwnFamilies.Chrome));
        Assert.Equal(["--chrome"], native.ArgumentList);

        foreach (var (door, families) in new[] { ("claude-code-acp", OwnFamilies.Chrome), ("codex-acp", OwnFamilies.Chrome), ("claude-code", OwnFamilies.Skills) })
        {
            var info = new ProcessStartInfo();
            SpawnSkills.Hand(
                ProviderRegistry.Built(), ProviderRegistry.Built().Door(door), info, Home, "s11", PluginCatalog.Load(Home), PluginServerTo.Sessions,
                Tools(families));
            Assert.Empty(info.ArgumentList);
        }

        Put(Path.Combine(Person, ".claude.json"), """{ "claudeInChromeDefaultEnabled": false }""");
        var left = new ProcessStartInfo();
        SpawnSkills.Hand(
            ProviderRegistry.Built(), ProviderRegistry.Built().Door("claude-code"), left, Home, "s12", PluginCatalog.Load(Home), PluginServerTo.Sessions,
            Tools(OwnFamilies.Chrome));
        Assert.Empty(left.ArgumentList);

        var off = PermissionRules.SwitchDefault(PermissionFile.Empty, OwnHomeDefaults.ChromeId, on: false);
        Assert.False(OwnHomeDefaults.Families(off, connectorOnly: false).HasFlag(OwnFamilies.Chrome));
    }

    [Fact]
    public void Both_claude_doors_are_handed_the_own_plugin_beside_daoris_plugins_and_it_goes_with_the_session()
    {
        PersonsPieces();

        var info = new ProcessStartInfo();
        var (folder, plugins) = SpawnSkills.Hand(
            ProviderRegistry.Built(), ProviderRegistry.Built().Door("claude-code"), info, Home, "s3", PluginCatalog.Load(Home), PluginServerTo.Sessions,
            Tools(OwnFamilies.Skills));
        Assert.Equal(["--plugin-dir", Assert.Single(plugins)], info.ArgumentList);

        var acp = new ProcessStartInfo();
        var (_, acpPlugins) = SpawnSkills.Hand(
            ProviderRegistry.Built(), ProviderRegistry.Built().Door("claude-code-acp"), acp, Home, "s4", PluginCatalog.Load(Home), PluginServerTo.Sessions,
            Tools(OwnFamilies.Skills));
        Assert.Single(acpPlugins);
        Assert.Empty(acp.ArgumentList);

        SpawnFiles.RemoveFolder(folder);
        Assert.False(Directory.Exists(folder));
        Assert.True(File.Exists(Path.Combine(ToolHome, "agents", "scout.md")));
    }
}
