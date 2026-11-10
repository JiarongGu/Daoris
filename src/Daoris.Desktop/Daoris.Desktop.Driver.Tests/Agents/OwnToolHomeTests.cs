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
