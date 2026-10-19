using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a plugin hands Ask Daoris reaches it only as the person approved it (HELPPLUG1a's tightening, D170's note): the exact
/// tools a server declares, approved on the plugin's page or at its terminal twin, asked again when an update adds or widens
/// one; a write outside Daoris never allowed silently; and no generic execution tool, whatever the manifest says.
/// </summary>
public sealed class HelpGrantsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-grants-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private PluginCatalog Tickets(string allow = """["search", "get_issue"]""", string writes = """["create_issue"]""")
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.tickets");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), $$"""
            { "id": "acme.tickets", "servers": [
                { "name": "tickets", "command": ["tickets"], "to": ["help"], "allow": {{allow}}, "writes": {{writes}} },
                { "name": "docs", "command": ["docs"] } ] }
            """);
        return PluginCatalog.Load(_home);
    }

    [Fact]
    public void A_server_declared_for_ask_daoris_waits_for_the_persons_approval_and_is_handed_nothing_meanwhile()
    {
        var grants = HelpGrants.Read(_home, Tickets());

        var grant = Assert.Single(grants.All);
        Assert.Equal(("acme.tickets", "tickets", HelpGrants.Waits), (grant.Plugin, grant.Server.Name, grant.State));
        Assert.Empty(grants.Servers);
        Assert.Empty(grants.Allow);
        Assert.Empty(grants.Ask);
    }

    [Fact]
    public void Approved_whole_its_reads_and_its_writes_are_allowed_as_the_harness_spells_a_servers_tool()
    {
        var catalog = Tickets();

        HelpGrants.Approve(_home, catalog, "acme.tickets", "tickets", readsOnly: false);
        var grants = HelpGrants.Read(_home, catalog);

        Assert.Equal(HelpGrants.Approved, Assert.Single(grants.All).State);
        Assert.Equal(["tickets"], grants.Servers.Select(server => server.Name));
        Assert.Equal(["mcp__tickets__search", "mcp__tickets__get_issue", "mcp__tickets__create_issue"], grants.Allow);
        Assert.Empty(grants.Ask);
        Assert.True(File.Exists(HelpGrants.PathIn(_home)));
    }

    /// <summary>
    /// D89's "every change is confirmed": a write the person did not accept is asked, which in Ask Daoris's asking mode is
    /// refused; one an update adds after the approval is asked too, never allowed silently.
    /// </summary>
    [Fact]
    public void A_write_the_person_did_not_accept_is_asked_and_one_an_update_adds_is_never_allowed_silently()
    {
        HelpGrants.Approve(_home, Tickets(), "acme.tickets", "tickets", readsOnly: true);

        var readsOnly = HelpGrants.Read(_home, Tickets());
        Assert.Equal(["mcp__tickets__search", "mcp__tickets__get_issue"], readsOnly.Allow);
        Assert.Equal(["mcp__tickets__create_issue"], readsOnly.Ask);

        HelpGrants.Approve(_home, Tickets(), "acme.tickets", "tickets", readsOnly: false);
        var widened = HelpGrants.Read(_home, Tickets(writes: """["create_issue", "close_issue"]"""));
        Assert.Equal(HelpGrants.Approved, Assert.Single(widened.All).State);
        Assert.Equal(["mcp__tickets__close_issue"], widened.Ask);
        Assert.DoesNotContain("mcp__tickets__close_issue", widened.Allow);
    }

    [Fact]
    public void An_update_that_adds_or_widens_a_tool_asks_again_and_hands_nothing_until_approved()
    {
        HelpGrants.Approve(_home, Tickets(), "acme.tickets", "tickets", readsOnly: false);

        // A tool added to what it reads, and one moved from reading to writing: each is a list the person did not approve.
        foreach (var catalog in new[] { Tickets(allow: """["search", "get_issue", "list_all"]"""), Tickets(allow: """["search"]""", writes: """["create_issue", "get_issue"]""") })
        {
            var grants = HelpGrants.Read(_home, catalog);
            Assert.Equal(HelpGrants.Waits, Assert.Single(grants.All).State);
            Assert.Empty(grants.Servers);
            Assert.Empty(grants.Allow);
        }
    }

    /// <summary>No generic execution tool for the helper (HELP4, D170): judged by the tool's name, refused with a reason, never approved.</summary>
    [Theory]
    [InlineData("run_command")]
    [InlineData("shell")]
    [InlineData("execShell")]
    [InlineData("eval")]
    [InlineData("powershell-exec")]
    [InlineData("terminal_input")]
    public void A_generic_execution_tool_is_refused_for_ask_daoris_with_a_reason_and_cannot_be_approved(string tool)
    {
        var catalog = Tickets(allow: $$"""["search", "{{tool}}"]""");

        var grant = Assert.Single(HelpGrants.Read(_home, catalog).All);

        Assert.Equal(HelpGrants.Refused, grant.State);
        Assert.Contains($"`{tool}`", grant.Refusal);
        Assert.Contains("runs commands", grant.Refusal);
        Assert.Throws<DriverException>(() => HelpGrants.Approve(_home, catalog, "acme.tickets", "tickets", readsOnly: false));
        Assert.Empty(HelpGrants.Read(_home, catalog).Servers);
    }

    [Fact]
    public void A_withdrawal_hands_nothing_again_and_an_unreadable_file_approves_nothing()
    {
        var catalog = Tickets();
        HelpGrants.Approve(_home, catalog, "acme.tickets", "tickets", readsOnly: false);

        HelpGrants.Withdraw(_home, "acme.tickets", "tickets");
        Assert.Equal(HelpGrants.Waits, Assert.Single(HelpGrants.Read(_home, catalog).All).State);

        HelpGrants.Approve(_home, catalog, "acme.tickets", "tickets", readsOnly: false);
        File.WriteAllText(HelpGrants.PathIn(_home), "{ torn");
        Assert.Empty(HelpGrants.Read(_home, catalog).Servers);
        Assert.Throws<DriverException>(() => HelpGrants.Approve(_home, catalog, "acme.none", "tickets", readsOnly: false));
    }

    /// <summary>
    /// HELPPLUG1c: a server that waits says whether it was never approved or was approved and an update changed its tools since,
    /// so the page and the room say "changed since: asks again" rather than taking it away in silence.
    /// </summary>
    [Fact]
    public void A_server_whose_tools_changed_since_its_approval_waits_marked_changed_and_one_never_approved_does_not()
    {
        Assert.False(Assert.Single(HelpGrants.Read(_home, Tickets()).All).Changed);

        HelpGrants.Approve(_home, Tickets(), "acme.tickets", "tickets", readsOnly: false);
        Assert.False(Assert.Single(HelpGrants.Read(_home, Tickets()).All).Changed);

        var widened = Assert.Single(HelpGrants.Read(_home, Tickets(allow: """["search", "get_issue", "list_all"]""")).All);
        Assert.Equal(HelpGrants.Waits, widened.State);
        Assert.True(widened.Changed);
    }

    /// <summary>
    /// HELPPLUG1c: the person accepts each write on its own: all the reads, and only the writes they chose, the rest asked, which
    /// Ask Daoris's asking mode refuses (D89). An approval naming a write the server does not declare approves nothing.
    /// </summary>
    [Fact]
    public void Approved_with_the_writes_the_person_chose_allows_those_and_asks_the_others()
    {
        var catalog = Tickets(writes: """["create_issue", "close_issue"]""");

        var grant = HelpGrants.Approve(_home, catalog, "acme.tickets", "tickets", writes: ["close_issue"]);
        var grants = HelpGrants.Read(_home, catalog);

        Assert.Equal(["close_issue"], grant.Accepted);
        Assert.Equal(HelpGrants.Approved, Assert.Single(grants.All).State);
        Assert.Equal(["mcp__tickets__search", "mcp__tickets__get_issue", "mcp__tickets__close_issue"], grants.Allow);
        Assert.Equal(["mcp__tickets__create_issue"], grants.Ask);

        var refused = Assert.Throws<DriverException>(() => HelpGrants.Approve(_home, catalog, "acme.tickets", "tickets", writes: ["delete_all"]));
        Assert.Contains("`delete_all`", refused.Message);
        Assert.Equal(["close_issue"], Assert.Single(HelpGrants.Read(_home, catalog).All).Accepted);
    }
}
