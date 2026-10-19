using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// HELPPLUG1c (D170's note): Ask Daoris proposes the person's approval of what a plugin's server offers it, the plugin kind's
/// <c>approve</c>, and its withdrawal, <c>withdraw</c>. The card names the plugin, the server and each tool, its reads and its
/// writes apart as the approval keeps them; the person's press writes the approval through the page's own code
/// (<see cref="HelpGrants"/>). A server naming a generic execution tool is still refused, whoever proposes it (HELP4).
/// </summary>
public sealed class HelpPluginApprovalProposalsTests : HelpProposalsFixture
{
    private const string Browser = """
        { "id": "in-app-browser", "name": "In-app browser", "version": "1.1.0", "servers": [
            { "name": "browser", "command": ["npx", "@playwright/mcp"], "to": ["sessions", "help"],
              "allow": ["browser_navigate", "browser_snapshot"], "writes": ["browser_click", "browser_type"] } ] }
        """;

    /// <summary>A plugin installed under the test's home with the manifest given, and the facts a plugin proposal is judged by.</summary>
    private HelpMachineFacts Installed(string manifest)
    {
        var id = System.Text.Json.JsonDocument.Parse(manifest).RootElement.GetProperty("id").GetString()!;
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), manifest);
        return Facts with { Plugins = PluginCatalog.Load(_home), Home = _home };
    }

    private static HelpProposal Approve(string? server = "browser", IReadOnlyList<string>? writes = null) =>
        Of("plugin", "approve", "in-app-browser") with { Server = server, Writes = writes };

    [Fact]
    public async Task An_approval_card_names_the_plugin_the_server_and_every_tool_and_its_apply_writes_the_approval()
    {
        var facts = Installed(Browser);
        var proposal = Approve(writes: ["browser_click"]);

        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, facts);

        Assert.Null(plan.Refusal);
        var grant = Assert.IsType<HelpGrantView>(plan.Grant);
        Assert.Equal(("in-app-browser", "In-app browser", "browser"), (grant.Plugin, grant.Name, grant.Server));
        Assert.Equal(["browser_navigate", "browser_snapshot"], grant.Reads);
        Assert.Equal(["browser_click", "browser_type"], grant.Writes);
        Assert.Equal(["browser_click"], grant.Accepted);
        Assert.False(grant.Withdraw);
        foreach (var tool in new[] { "browser_navigate", "browser_snapshot", "browser_click", "browser_type" }) Assert.Contains($"`{tool}`", plan.Describe);
        Assert.Equal("daoris-driver plugins approve in-app-browser browser --writes browser_click", plan.Terminal);

        var doors = new HelpStandInDoors { GrantsHome = _home };
        var (applied, _, _) = await ApplyAsync(proposal, doors, facts);

        Assert.True(applied.Applied, applied.Told);
        Assert.Equal(["HELP_APPROVE in-app-browser browser browser_click"], doors.Calls);
        var granted = HelpGrants.Read(_home, facts.Plugins);
        Assert.Equal(HelpGrants.Approved, Assert.Single(granted.All).State);
        Assert.Equal(["mcp__browser__browser_navigate", "mcp__browser__browser_snapshot", "mcp__browser__browser_click"], granted.Allow);
        Assert.Equal(["mcp__browser__browser_type"], granted.Ask);
    }

    /// <summary>No write named is its reads only, said on the card and spelled at the terminal as `--reads-only`.</summary>
    [Fact]
    public void An_approval_naming_no_write_accepts_its_reads_only()
    {
        var plan = HelpProposals.Plan(Approve(), DriverConfig.Empty, Installed(Browser));

        Assert.Null(plan.Refusal);
        Assert.Empty(plan.Grant!.Accepted);
        Assert.Equal("daoris-driver plugins approve in-app-browser browser --reads-only", plan.Terminal);
        Assert.Contains("refused", plan.Describe);
    }

    /// <summary>A server is named, or the plugin offers Ask Daoris one only and the approval takes it.</summary>
    [Fact]
    public void An_approval_takes_the_plugins_one_server_for_ask_daoris_when_none_is_named()
    {
        var plan = HelpProposals.Plan(Approve(server: null, writes: ["browser_click", "browser_type"]), DriverConfig.Empty, Installed(Browser));

        Assert.Null(plan.Refusal);
        Assert.Equal("browser", plan.Grant!.Server);
        Assert.Equal("daoris-driver plugins approve in-app-browser browser", plan.Terminal);
    }

    [Theory]
    [InlineData("""{ "id": "in-app-browser", "servers": [ { "name": "browser", "command": ["b"], "to": ["help"], "allow": ["search", "run_command"] } ] }""", "browser", null, "`run_command` runs commands")]
    [InlineData("""{ "id": "in-app-browser", "servers": [ { "name": "browser", "command": ["b"] } ] }""", "browser", null, "offers Ask Daoris no server")]
    [InlineData("""{ "id": "other" }""", "browser", null, "no plugin `in-app-browser`")]
    [InlineData(Browser, "pages", null, "server `pages`")]
    [InlineData(Browser, "browser", "delete_all", "`delete_all` is not a write")]
    public void An_approval_the_page_would_refuse_never_reaches_the_person(string manifest, string server, string? write, string said)
    {
        var plan = HelpProposals.Plan(Approve(server, write is null ? null : [write]), DriverConfig.Empty, Installed(manifest));

        Assert.Contains(said, plan.Refusal);
        Assert.Null(plan.Grant);
    }

    [Fact]
    public void An_approval_already_on_file_as_proposed_changes_nothing_and_is_refused()
    {
        var facts = Installed(Browser);
        HelpGrants.Approve(_home, facts.Plugins, "in-app-browser", "browser", writes: ["browser_click"]);

        Assert.Contains("already approved", HelpProposals.Plan(Approve(writes: ["browser_click"]), DriverConfig.Empty, facts).Refusal);
        Assert.Null(HelpProposals.Plan(Approve(writes: ["browser_click", "browser_type"]), DriverConfig.Empty, facts).Refusal);
    }

    /// <summary>The same door takes it back: the card names what Ask Daoris loses, and the press withdraws it.</summary>
    [Fact]
    public async Task A_withdrawal_names_what_ask_daoris_loses_and_its_apply_takes_it_back()
    {
        var facts = Installed(Browser);
        var withdraw = Of("plugin", "withdraw", "in-app-browser") with { Server = "browser" };
        Assert.Contains("is not approved", HelpProposals.Plan(withdraw, DriverConfig.Empty, facts).Refusal);

        HelpGrants.Approve(_home, facts.Plugins, "in-app-browser", "browser", writes: ["browser_click"]);
        var plan = HelpProposals.Plan(withdraw, DriverConfig.Empty, facts);

        Assert.Null(plan.Refusal);
        Assert.True(plan.Grant!.Withdraw);
        Assert.Equal(["browser_click"], plan.Grant.Accepted);
        Assert.Equal("daoris-driver plugins withdraw in-app-browser browser", plan.Terminal);
        Assert.Contains("`browser_navigate`", plan.Describe);

        var doors = new HelpStandInDoors { GrantsHome = _home };
        var (applied, _, _) = await ApplyAsync(withdraw, doors, facts);

        Assert.True(applied.Applied, applied.Told);
        Assert.Equal(["HELP_WITHDRAW in-app-browser browser"], doors.Calls);
        Assert.Equal(HelpGrants.Waits, Assert.Single(HelpGrants.Read(_home, facts.Plugins).All).State);
    }

    /// <summary>The file the service's box writes carries the server and the writes accepted; the judge reads both.</summary>
    [Fact]
    public void The_proposals_file_names_its_server_and_its_writes()
    {
        var node = new JsonObject
        {
            ["id"] = "p9", ["proposed"] = "2026-10-19T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = "plugin", ["door"] = "approve", ["target"] = "in-app-browser", ["workspace"] = null, ["value"] = null,
            ["sentence"] = null, ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
            ["server"] = "browser", ["writes"] = new JsonArray("browser_click"),
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "p9.json"), node.ToJsonString());

        var read = Assert.Single(HelpProposals.Pending(_home, "h1"));

        Assert.Equal("browser", read.Server);
        Assert.Equal(["browser_click"], read.Writes);
    }
}
