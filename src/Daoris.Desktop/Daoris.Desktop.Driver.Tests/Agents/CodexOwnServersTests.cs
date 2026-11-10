using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The servers of the person's own Codex home reach a Codex session through the connector plan its open hands <c>session/new</c>: the adapter
/// replaces the configuration's <c>mcp_servers</c> with the open's, so they ride there. Daoris's connector and a server naming a Daoris
/// variable stay out; none where the servers' default is off, the pass is held to the connector, or the session runs in the person's home.
/// </summary>
public sealed class CodexOwnServersTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-own-codex-servers-" + Guid.NewGuid().ToString("N")[..8]);

    private string Person => Path.Combine(_root, "person");
    private string Account => Path.Combine(_root, "account");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private OwnTools Tools(OwnFamilies families, string? sessionHome = null) => new(
        _ => null, Person, name => name == "CODEX_HOME" ? sessionHome ?? Account : null, families);

    private void Persons()
    {
        Directory.CreateDirectory(Path.Combine(Person, ".codex"));
        File.WriteAllText(Path.Combine(Person, ".codex", "config.toml"), """
            [mcp_servers.docs]
            command = "docs-server"
            args = ["--stdio"]
            env = { TOKEN = "abc" }

            [mcp_servers.daoris-knowledge]
            command = "their-own-copy"

            [mcp_servers.acting]
            command = "acts-as-them"
            env = { DAORIS_DRIVER_KEY = "x" }

            [mcp_servers.off]
            command = "unused"
            enabled = false

            [mcp_servers.remote]
            url = "https://example.invalid/mcp"
            """);
    }

    private static IReadOnlyList<ServerEntry> Served(OwnTools tools) => ProviderRegistry.Built().Door("codex-acp").Spawned().OwnServers(tools);

    [Fact]
    public void Their_servers_come_but_daoriss_connector_one_naming_a_daoris_variable_and_one_off_or_remote()
    {
        Persons();

        var server = Assert.Single(Served(Tools(OwnFamilies.Servers)));

        Assert.Equal("docs", server.Name);
        Assert.Equal("docs-server", server.Command);
        Assert.Equal(["--stdio"], server.Arguments);
        Assert.Equal("abc", server.Environment["TOKEN"]);
    }

    [Fact]
    public void Nothing_where_the_default_is_off_or_the_session_runs_in_their_own_home()
    {
        Persons();

        Assert.Empty(Served(Tools(OwnFamilies.Settings)));
        Assert.Empty(Served(Tools(OwnFamilies.Servers, sessionHome: Path.Combine(Person, ".codex"))));
    }

    [Fact]
    public void The_open_offers_them_after_the_plan_and_the_plans_own_name_wins()
    {
        Persons();
        var plan = ConnectorPlans.For("s1", null, [new ServerEntry("docs", "plans-docs", [], new Dictionary<string, string>())]);
        var own = Served(Tools(OwnFamilies.Servers));

        var offered = JsonSerializer.SerializeToElement(AcpProtocol.Offer(plan, own));

        var one = Assert.Single(offered.EnumerateArray().ToList());
        Assert.Equal("plans-docs", one.GetProperty("command").GetString());

        var added = JsonSerializer.SerializeToElement(AcpProtocol.Offer(ConnectorPlans.For("s1", null, []), own));
        Assert.Equal(["docs"], added.EnumerateArray().Select(entry => entry.GetProperty("name").GetString()).ToArray());
    }
}
