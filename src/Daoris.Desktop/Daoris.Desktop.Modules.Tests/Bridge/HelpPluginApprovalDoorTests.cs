using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// HELPPLUG1c (D170's note): Ask Daoris's proposal of the person's approval of what a plugin's server offers it, the modules'
/// half. The card carries every tool, reads and writes apart, and the writes it accepts; the person's Apply writes the approval
/// through the page's own code (<see cref="HelpGrants"/>), and the same door withdraws it.
/// </summary>
public sealed class HelpPluginApprovalDoorTests : DriverModuleBridge
{
    private const string Approve =
        """{"id":"p1a2b3c4","kind":"plugin","door":"approve","target":"in-app-browser","workspace":null,"value":null,"sentence":null,"why":"the browser","state":"proposed","by":{"session":"h1"},"server":"browser","writes":["browser_click"]}""";

    private const string Withdraw =
        """{"id":"p1a2b3c4","kind":"plugin","door":"withdraw","target":"in-app-browser","workspace":null,"value":null,"sentence":null,"why":"not now","state":"proposed","by":{"session":"h1"},"server":"browser","writes":null}""";

    [Fact]
    public async Task An_approval_is_drawn_with_every_tool_and_its_apply_writes_the_approval_and_a_withdrawal_takes_it_back()
    {
        var module = await UpAsync();
        Proposal(Approve);

        var card = Assert.Single((await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" })).GetProperty("proposals").EnumerateArray());

        Assert.Equal("plugin", card.GetProperty("kind").GetString());
        Assert.Equal("daoris-driver plugins approve in-app-browser browser --writes browser_click", card.GetProperty("terminal").GetString());
        var grant = card.GetProperty("grant");
        Assert.Equal(("in-app-browser", "browser"), (grant.GetProperty("plugin").GetString(), grant.GetProperty("server").GetString()));
        Assert.Equal(["browser_navigate", "browser_snapshot"], Strings(grant.GetProperty("reads")));
        Assert.Equal(["browser_click", "browser_type"], Strings(grant.GetProperty("writes")));
        Assert.Equal(["browser_click"], Strings(grant.GetProperty("accepted")));
        Assert.Empty(HelpGrants.Read(Home, PluginCatalog.Load(Home)).Servers);

        var applied = await AnswerAsync(module, "HELP_APPLY", new { id = "p1a2b3c4", revision = card.GetProperty("revision").GetString() });

        Assert.True(applied.GetProperty("applied").GetBoolean(), applied.GetProperty("message").GetString());
        var granted = HelpGrants.Read(Home, PluginCatalog.Load(Home));
        Assert.Equal(["browser"], granted.Servers.Select(server => server.Name));
        Assert.Equal(["mcp__browser__browser_type"], granted.Ask);

        Proposal(Withdraw);
        var withdraw = Assert.Single((await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" })).GetProperty("proposals").EnumerateArray());
        Assert.True(withdraw.GetProperty("grant").GetProperty("withdraw").GetBoolean());
        await AnswerAsync(module, "HELP_APPLY", new { id = "p1a2b3c4", revision = withdraw.GetProperty("revision").GetString() });
        Assert.Empty(HelpGrants.Read(Home, PluginCatalog.Load(Home)).Servers);
    }

    /// <summary>A server naming a generic execution tool is never drawn, whoever proposes it (HELP4): settled refused, with why.</summary>
    [Fact]
    public async Task An_approval_of_a_generic_execution_server_is_never_drawn()
    {
        var module = await UpAsync("""["browser_navigate", "run_command"]""");
        Proposal(Approve.Replace("\"writes\":[\"browser_click\"]", "\"writes\":[]", StringComparison.Ordinal));

        Assert.Empty((await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" })).GetProperty("proposals").EnumerateArray());
        var settled = HelpProposals.Find(Home, "p1a2b3c4")!;
        Assert.Equal("refused", settled.State);
    }

    private static string[] Strings(System.Text.Json.JsonElement list) => [.. list.EnumerateArray().Select(each => each.GetString()!)];

    private void Proposal(string json)
    {
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "p1a2b3c4.json"), json);
    }

    private async Task<DriverModule> UpAsync(string allow = """["browser_navigate", "browser_snapshot"]""")
    {
        var plugin = DaorisHome.Entry(Home, HomeEntry.Plugins, "in-app-browser");
        Directory.CreateDirectory(plugin);
        File.WriteAllText(Path.Combine(plugin, PluginCatalog.ManifestName), $$"""
            { "id": "in-app-browser", "name": "In-app browser", "version": "1.1.0", "servers": [
                { "name": "browser", "command": ["npx", "@playwright/mcp"], "to": ["sessions", "help"],
                  "allow": {{allow}}, "writes": ["browser_click", "browser_type"] } ] }
            """);
        var loop = Loop();
        var module = new DriverModule(Bus, loop) { Offers = Path.Combine(Home, "no-offers") };
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SetupStandIn.EmptyHost())));
        return module;
    }
}
