using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row p (the agent-provider design §4.4): a start's connectors are handed by the door's own carrier, asked once, never by
/// a caller's test of the door. Claude Code's native door writes the plan's file in its own layout and names it, the same file as before;
/// a door whose servers ride its wire, and one that takes none at its spawn, has no carrier there, and nothing is written for it. A
/// plugin's or a test's pipe adapter, which declares none, keeps the file named by its own door.
/// </summary>
public sealed class ServerHandingTests : IDisposable
{
    private readonly string _home = Path.GetFullPath(Path.Combine("_fixtures", "serverhanding-" + Guid.NewGuid().ToString("N")[..8]));

    public ServerHandingTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static ConnectorPlan Plan(string session) => new(
        session, [new ServerEntry("browser", "npx", ["-y", "@playwright/mcp@latest"], new Dictionary<string, string> { ["HEADLESS"] = "1" })]);

    [Fact]
    public void Claude_codes_native_door_hands_the_plan_as_its_own_file_and_every_other_built_door_has_no_carrier()
    {
        var info = new ProcessStartInfo();
        var file = AdapterSet.Built().Resolve("claude-code").ServerHanding()!.Hand(info, _home, Plan("s1"));
        Assert.Equal(DaorisHome.Entry(_home, HomeEntry.Spawn, "s1.mcp.json"), file);
        Assert.Equal(["--mcp-config", file], info.ArgumentList);
        Assert.Contains("\"HEADLESS\"", File.ReadAllText(file!), StringComparison.Ordinal);
        SpawnFiles.Remove(file);

        foreach (var door in new[] { "claude-code-acp", "codex-acp", "dsh", "stub", "acp-stub" })
        {
            Assert.Null(AdapterSet.Built().Resolve(door).ServerHanding());
        }

        Assert.Null(AdapterSet.Built().Resolve("claude-code").ServerHanding()!.Hand(new ProcessStartInfo(), _home, new ConnectorPlan("s2", [])));
        Assert.False(File.Exists(DaorisHome.Entry(_home, HomeEntry.Spawn, "s2.mcp.json")));
    }

    [Fact]
    public void An_adapter_declaring_no_carrier_keeps_the_file_named_by_its_own_door_on_the_pipe_and_none_on_the_wire()
    {
        var info = new ProcessStartInfo();
        var file = new Adapter(SessionWire.Pipe).ServerHanding()!.Hand(info, _home, Plan("s3"));
        Assert.Equal(["servers:" + file], info.ArgumentList);
        Assert.Null(new Adapter(SessionWire.Acp).ServerHanding());
    }

    private static ServerEntry Server(string name, string command = "npx") => new(name, command, [], new Dictionary<string, string>());

    [Fact]
    public void One_plan_per_workspace_carries_its_workspace_its_permitted_sources_and_the_connector_it_requires()
    {
        var plan = ConnectorPlans.For("s1", "shop", [Server(KnowledgeConnector.ServerName), Server("docs")]);
        Assert.Equal("shop", plan.Workspace);
        Assert.Equal(ConnectorPlans.Sessions, plan.Permitted);
        Assert.Equal([KnowledgeConnector.ServerName], plan.Includes);
        Assert.True(plan.Allows(ConnectorSource.Plugin));
        Assert.False(plan.Allows(ConnectorSource.Own));

        var only = ConnectorPlans.For("s2", null, [Server(KnowledgeConnector.ServerName)], ConnectorPlans.ConnectorOnly);
        Assert.False(only.Allows(ConnectorSource.Plugin));
        Assert.Empty(ConnectorPlans.For("s3", "shop", []).Includes);
    }

    [Fact]
    public void A_server_from_a_source_the_workspace_does_not_permit_and_a_name_given_twice_are_refused_naming_it()
    {
        var barred = Assert.Throws<DriverException>(() => ConnectorPlans.For("s1", "shop", [Server("docs")], ConnectorPlans.ConnectorOnly));
        Assert.Contains("`docs`", barred.Message, StringComparison.Ordinal);
        var twice = Assert.Throws<DriverException>(() => ConnectorPlans.For("s1", "shop", [Server("docs"), Server("docs")]));
        Assert.Contains("`docs`", twice.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_plan_names_no_exclusion_of_the_agents_own_servers_since_no_start_asks_for_one()
    {
        Assert.DoesNotContain(typeof(ConnectorPlan).GetProperties(), property => property.Name == "ExcludesOwn");
        Assert.DoesNotContain(typeof(DoorAbilities).GetProperties(), property => property.Name == "OwnServersLeftOut");
    }

    [Fact]
    public void The_protocol_doors_open_offers_the_same_servers_the_file_doors_carrier_writes_from_one_plan()
    {
        var plan = ConnectorPlans.For("s9", "shop", [Server("docs"), Server("wiki")]);
        var wire = AcpProtocol.Offer(plan);
        Assert.Equal(2, wire.Length);
        Assert.Empty(AcpProtocol.Offer(null));
        var file = AdapterSet.Built().Resolve("claude-code").ServerHanding()!.Hand(new ProcessStartInfo(), _home, plan);
        var written = File.ReadAllText(file!);
        Assert.Contains("\"docs\"", written, StringComparison.Ordinal);
        Assert.Contains("\"wiki\"", written, StringComparison.Ordinal);
        SpawnFiles.Remove(file);
    }

    [Fact]
    public void A_native_door_drives_an_adopted_repository_only_where_its_servers_inherit_its_environment()
    {
        var built = AdapterSet.Built();
        Assert.True(built.Resolve("claude-code").Abilities().ServersInherit);
        Assert.False(built.Resolve("claude-code-acp").Abilities().ServersInherit);
        Assert.True(Adoption.Drives(built.Resolve("claude-code").Abilities(), adopted: true));
        Assert.False(Adoption.Drives(built.Resolve("claude-code").Abilities(), adopted: false));
        Assert.True(Adoption.Drives(built.Resolve("codex-acp").Abilities(), adopted: false));
        Assert.True(Adoption.Drives(built.Resolve("codex-acp").Abilities(), adopted: true));
        Assert.False(Adoption.Drives(built.Resolve("claude-code").Abilities() with { ServersInherit = false }, adopted: true));
    }

    [Fact]
    public void The_session_homes_servers_file_is_named_by_the_provider_that_writes_it()
    {
        Assert.Equal(".mcp.json", ClaudeServers.FileSuffix);
        Assert.Contains(ClaudeServers.FileSuffix, SessionHomeFiles.SpawnSuffixes);
        Assert.Equal(".settings.json", ClaudeRules.FileSuffix);
        Assert.Contains(ClaudeRules.FileSuffix, SessionHomeFiles.SpawnSuffixes);
    }

    [Fact]
    public void A_server_is_a_ServerEntry_of_Knowledges_type_with_its_own_environment_in_every_door_it_is_handed_to()
    {
        var entry = Server("docs") with { Environment = new Dictionary<string, string> { ["DAORIS_SESSION"] = "s7" } };
        Assert.Equal("Daoris.Driver", typeof(ServerEntry).Namespace);
        Assert.Equal("ServerEntry", typeof(ServerEntry).Name);
        var file = AdapterSet.Built().Resolve("claude-code").ServerHanding()!.Hand(new ProcessStartInfo(), _home, new ConnectorPlan("s7", [entry]));
        Assert.Contains("DAORIS_SESSION", File.ReadAllText(file!), StringComparison.Ordinal);
        SpawnFiles.Remove(file);
    }

    private sealed class Adapter(SessionWire wire) : ISessionAdapter
    {
        public string Name => "stand-agent";
        public SessionWire Wire => wire;
        public ProcessStartInfo Prepare(SpawnRequest request) => new("stand-command");
        public void HandServers(ProcessStartInfo info, string configFile) => info.ArgumentList.Add("servers:" + configFile);
    }
}
