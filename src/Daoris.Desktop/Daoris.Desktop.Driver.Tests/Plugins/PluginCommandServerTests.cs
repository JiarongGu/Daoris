using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's tool commands as one server, <c>daoris-plugins</c>: a conversation of a repository is handed it where a tool is listed,
/// Ask Daoris only once the person approved its exact tools (reads, and each write on its own), or in full access, and the grant's
/// rules name each tool as a server's are named.
/// </summary>
public sealed class PluginCommandServerTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plugin-cmdserver-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginCommandServerTests()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme-tickets");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), """
            { "id": "acme-tickets", "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/state"] },
              "interfaces": { "commands": [
                { "name": "open", "about": "Opens a ticket.", "tool": true, "writes": true },
                { "name": "status", "about": "Says what is kept.", "tool": true },
                { "name": "login", "about": "Keeps a token.", "interactive": true } ] } }
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static ServerEntry Entry() => new(PluginToolsServer.Name, "daoris-driver", ["plugin", "tools"], new Dictionary<string, string>());

    private Task<(IReadOnlyList<ServerEntry> Handed, Noted? Notice, bool Drives, IReadOnlyList<string> Drivers)> Hand(string to, bool every = false) =>
        ChatServers.HandAsync(_home, null, PluginLog.None, "s1a2b3c4", CancellationToken.None, to, every, () => Entry());

    [Fact]
    public void The_tool_commands_are_one_server_offered_to_ask_daoris_with_its_reads_and_its_writes()
    {
        var server = Assert.Single(PluginCommandServer.OfferedToHelp(PluginCatalog.Load(_home).Plugins.Single().Manifest));

        Assert.Equal("daoris-plugins", server.Name);
        Assert.Equal(["acme-tickets_status"], server.Allow);
        Assert.Equal(["acme-tickets_open"], server.Writes);
        Assert.True(PluginCommandServer.Is(server));
    }

    [Fact]
    public async Task A_repositorys_conversation_is_handed_the_server_and_ask_daoris_only_once_the_person_approved_it()
    {
        var session = await Hand(PluginServerTo.Sessions);
        var waiting = await Hand(PluginServerTo.Help);
        var grants = HelpGrants.Read(_home, PluginCatalog.Load(_home));
        Assert.Equal(HelpGrants.Waits, Assert.Single(grants.All).State);

        HelpGrants.Approve(_home, PluginCatalog.Load(_home), "acme-tickets", "daoris-plugins", readsOnly: true);
        var approved = await Hand(PluginServerTo.Help);
        var full = await Hand(PluginServerTo.Help, every: true);

        Assert.Equal(["daoris-plugins"], session.Handed.Select(server => server.Name));
        Assert.Empty(waiting.Handed);
        Assert.Equal(["daoris-plugins"], approved.Handed.Select(server => server.Name));
        Assert.Equal(["daoris-plugins"], full.Handed.Select(server => server.Name));
        var read = HelpGrants.Read(_home, PluginCatalog.Load(_home));
        Assert.Equal(["mcp__daoris-plugins__acme-tickets_status"], read.Allow);
        Assert.Equal(["mcp__daoris-plugins__acme-tickets_open"], read.Ask);
        Assert.Empty(read.Servers);
    }

    [Fact]
    public void An_accepted_write_is_allowed_and_asked_no_more()
    {
        HelpGrants.Approve(_home, PluginCatalog.Load(_home), "acme-tickets", "daoris-plugins", readsOnly: false);

        var read = HelpGrants.Read(_home, PluginCatalog.Load(_home));
        Assert.Equal(["mcp__daoris-plugins__acme-tickets_status", "mcp__daoris-plugins__acme-tickets_open"], read.Allow);
        Assert.Empty(read.Ask);
    }
}
