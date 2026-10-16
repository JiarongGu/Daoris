using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's skills handed at spawn (HELPPLUG1b, D170 point 4): each contributing plugin's skills for one destination copied,
/// per session, into a harness plugin of its own under the home's <c>spawn/</c>, and the harness pointed at it by its own
/// session-only door (<c>--plugin-dir</c>, measured on Claude Code 2.1.299 in an untrusted folder; D170's note). Never into a
/// repository, and never a skill that would allow itself tools.
/// </summary>
public sealed class SpawnSkillsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-skills-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Plugin(string id, string manifest, params (string Skill, string Text)[] skills)
    {
        var folder = Path.Combine(_home, PluginCatalog.Folder, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), manifest);
        foreach (var (skill, text) in skills)
        {
            Directory.CreateDirectory(Path.Combine(folder, "skills", skill));
            File.WriteAllText(Path.Combine(folder, "skills", skill, SpawnSkills.SkillFile), text);
        }

        return folder;
    }

    private const string Triage = "---\nname: triage\ndescription: Sort the issues by what they block.\n---\n\nRead the board first.\n";

    [Fact]
    public void Each_plugins_skills_for_a_destination_are_copied_into_a_harness_plugin_of_its_own_under_the_home()
    {
        var folder = Plugin("acme.tickets", """
            { "id": "acme.tickets", "version": "1.2.0", "skills": [
                { "name": "triage", "to": ["help"] }, { "name": "write-up", "to": ["sessions", "help"] }, { "name": "sessions-only" } ] }
            """, ("triage", Triage), ("write-up", "---\nname: write-up\ndescription: Write it up.\n---\n"), ("sessions-only", "---\nname: x\n---\n"));
        File.WriteAllText(Path.Combine(folder, "skills", "triage", "board.md"), "the board's columns");
        Plugin("acme.none", """{ "id": "acme.none" }""");

        var (spawn, plugins) = SpawnSkills.Write(_home, "s1a2b3c4", PluginCatalog.Load(_home), PluginServerTo.Help);

        Assert.Equal(Path.Combine(_home, SpawnServers.Folder, "s1a2b3c4.plugins"), spawn);
        var handed = Assert.Single(plugins);
        Assert.Equal(Path.Combine(spawn!, "acme.tickets"), handed);
        using (var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(handed, ".claude-plugin", "plugin.json"))))
        {
            Assert.Equal("acme.tickets", manifest.RootElement.GetProperty("name").GetString());
            Assert.Equal("1.2.0", manifest.RootElement.GetProperty("version").GetString());
        }

        Assert.Equal(Triage, File.ReadAllText(Path.Combine(handed, "skills", "triage", SpawnSkills.SkillFile)));
        Assert.Equal("the board's columns", File.ReadAllText(Path.Combine(handed, "skills", "triage", "board.md")));
        Assert.True(File.Exists(Path.Combine(handed, "skills", "write-up", SpawnSkills.SkillFile)));
        Assert.False(Directory.Exists(Path.Combine(handed, "skills", "sessions-only")));
        // The plugin's own folder is read, never written.
        Assert.False(Directory.Exists(Path.Combine(folder, ".claude-plugin")));

        var (_, forSessions) = SpawnSkills.Write(_home, "s5e6f7a8", PluginCatalog.Load(_home), PluginServerTo.Sessions);
        Assert.Equal(["sessions-only", "write-up"],
            Directory.GetDirectories(Path.Combine(Assert.Single(forSessions), "skills")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// D170 point 3 and the development documents' finding (§2 point 3): a skill's <c>allowed-tools</c> pre-approves its tools
    /// for the turn that invokes it, untrusted too, so a plugin's skill declaring it is never handed, to either destination;
    /// nor one whose folder holds no <c>SKILL.md</c>. Each says why.
    /// </summary>
    [Fact]
    public void A_skill_that_would_allow_itself_tools_or_holds_no_skill_file_is_not_handed_and_says_why()
    {
        Plugin("acme.wide", """
            { "id": "acme.wide", "skills": [ { "name": "shell", "to": ["help"] }, { "name": "missing", "to": ["help"] } ] }
            """, ("shell", "---\nname: shell\nallowed-tools: Bash(*)\n---\nRun anything.\n"));

        var catalog = PluginCatalog.Load(_home);
        var (spawn, plugins) = SpawnSkills.Write(_home, "s1a2b3c4", catalog, PluginServerTo.Help);

        Assert.Null(spawn);
        Assert.Empty(plugins);
        var entry = Assert.Single(catalog.Contributing);
        Assert.Contains("`allowed-tools`", SpawnSkills.Judge(entry.Folder, "shell"));
        Assert.Contains("`skills/missing/SKILL.md`", SpawnSkills.Judge(entry.Folder, "missing"));
        Assert.Null(SpawnSkills.Judge(Plugin("acme.fine", """{ "id": "acme.fine" }""", ("triage", Triage)), "triage"));
    }

    /// <summary>
    /// The harness's own door: on the pipe, one <c>--plugin-dir</c> per plugin, measured on Claude Code 2.1.299; on the protocol
    /// door, the Agent SDK's <c>plugins</c> option, which the adapter spreads from <c>_meta.claudeCode.options</c> and the SDK
    /// passes as <c>--plugin-dir</c> (read from claude-agent-acp 0.91.0 and the SDK 0.3.300). A harness with no such door takes
    /// nothing, and an adapter handed none keeps its meta as before.
    /// </summary>
    [Fact]
    public void The_pipe_takes_a_plugin_dir_each_and_the_protocol_door_the_sdks_plugins_option()
    {
        // Paths in pieces under the test's own home, never a literal machine path (the merge's universal gate).
        var tickets = Path.Combine(_home, SpawnServers.Folder, "s1.plugins", "acme.tickets");
        var docs = Path.Combine(_home, SpawnServers.Folder, "s1.plugins", "acme.docs");
        var settings = Path.Combine(_home, SpawnServers.Folder, "s1.settings.json");
        var info = new ProcessStartInfo();
        AdapterSet.Built().Resolve("claude-code").HandPlugins(info, [tickets, docs]);
        Assert.Equal(["--plugin-dir", tickets, "--plugin-dir", docs], info.ArgumentList);

        var stub = new ProcessStartInfo();
        AdapterSet.Built().Resolve("stub").HandPlugins(stub, [tickets]);
        Assert.Empty(stub.ArgumentList);

        var acp = AdapterSet.Built().Resolve("claude-code-acp");
        using var meta = JsonDocument.Parse(JsonSerializer.Serialize(acp.AcpSessionMeta(settings, [tickets])));
        var options = meta.RootElement.GetProperty("claudeCode").GetProperty("options");
        Assert.Equal(settings, options.GetProperty("settings").GetString());
        var plugin = Assert.Single(options.GetProperty("plugins").EnumerateArray());
        Assert.Equal(("local", tickets), (plugin.GetProperty("type").GetString(), plugin.GetProperty("path").GetString()));
        Assert.Equal(JsonSerializer.Serialize(acp.AcpSessionMeta(settings)), JsonSerializer.Serialize(acp.AcpSessionMeta(settings, [])));
    }

    [Fact]
    public void Hand_points_a_pipe_harness_at_what_it_wrote_and_the_folder_goes_when_the_session_does()
    {
        Plugin("acme.tickets", """{ "id": "acme.tickets", "skills": [ { "name": "triage" } ] }""", ("triage", Triage));
        var info = new ProcessStartInfo();

        var (folder, plugins) = SpawnSkills.Hand(
            AdapterSet.Built().Resolve("claude-code"), info, _home, "s1a2b3c4", PluginCatalog.Load(_home), PluginServerTo.Sessions);

        Assert.Equal(["--plugin-dir", Assert.Single(plugins)], info.ArgumentList);
        SpawnSkills.Remove(folder);
        Assert.False(Directory.Exists(folder));
        SpawnSkills.Remove(folder);
        SpawnSkills.Remove(null);
    }
}
