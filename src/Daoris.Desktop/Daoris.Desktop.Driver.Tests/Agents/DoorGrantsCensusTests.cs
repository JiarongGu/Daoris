using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What each agent door hands its agent is declared once, in its provider's <c>grants.json</c>, and the door is held to it: a door with no
/// row fails, a row for a door nobody builds fails, and a door whose spawn arguments, servers file, session wire or hook differ from its
/// row fails naming the place. Each row names the test that reads the tools its agent actually lists on a stand-in model
/// (<see cref="StandInServer.AssertListed"/>, in <c>ClaudeResumedToolsProcessTests</c> and <c>AcpAdapterDoorProcessTests</c>), or says why none is written yet and the open backlog row that writes it, since a protocol
/// handshake can succeed while a server passed only in <c>session/new</c> never reaches the agent.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class DoorGrantsCensusTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "grants-" + Guid.NewGuid().ToString("N")[..8]);

    public DoorGrantsCensusTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The plan a start hands: Daoris's connector, which every door must carry to its agent.</summary>
    internal static ConnectorPlan Plan { get; } = new(
        "grants", [new ServerEntry(KnowledgeConnector.ServerName, "node", ["host.mjs"], new Dictionary<string, string> { ["DAORIS_SESSION"] = "grants" })]);

    [Fact]
    public void Every_provider_declares_a_well_formed_file_of_rows()
    {
        var problems = Providers.Built().SelectMany(provider => DoorGrants.Declared(provider.Id.Value).Problems).ToList();

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void Every_door_has_a_row_and_no_row_names_a_door_nobody_builds()
    {
        var missing = new List<string>();
        var stray = new List<string>();
        foreach (var provider in Providers.Built())
        {
            var rows = DoorGrants.Declared(provider.Id.Value).Rows;
            missing.AddRange(provider.Doors.Where(door => !rows.ContainsKey(door.Id.Value))
                .Select(door => $"{provider.Id.Value}: door `{door.Id.Value}` has no row in {DoorGrants.FileName}"));
            stray.AddRange(rows.Keys.Where(id => provider.Doors.All(door => !string.Equals(door.Id.Value, id, StringComparison.OrdinalIgnoreCase)))
                .Select(id => $"{provider.Id.Value}: `{id}` is a row of {DoorGrants.FileName} and no door of the provider"));
        }

        Assert.True(missing.Count + stray.Count == 0, string.Join('\n', [.. missing, .. stray]));
    }

    [Fact]
    public void Every_door_hands_its_agent_what_its_row_declares()
    {
        var differences = new List<string>();
        var doors = 0;
        foreach (var provider in Providers.Built())
        {
            var rows = DoorGrants.Declared(provider.Id.Value).Rows;
            foreach (var door in provider.Doors.Where(door => rows.ContainsKey(door.Id.Value)))
            {
                doors++;
                var observed = DoorGrants.Observe(provider, door, Plan, _home, _home);
                differences.AddRange(DoorGrants.Differences(DoorGrants.Grant(rows[door.Id.Value]), observed).Select(found => $"{door.Id.Value}: {found}"));
            }
        }

        Assert.True(doors >= 6, $"expected the shipped doors, read {doors}");
        Assert.True(differences.Count == 0, "a door hands its agent what its row does not declare:\n" + string.Join('\n', differences));
    }

    [Fact]
    public void A_planted_door_whose_arguments_drop_the_knowledge_server_fails_the_census()
    {
        var provider = TestDoors.Door("planted-native", spawn: new DropsTheServers());
        var declared = JsonNode.Parse("""
            { "servers": { "route": "config-file", "flag": "--mcp-config", "names": ["daoris-knowledge"] } }
            """)!;

        var observed = DoorGrants.Observe(provider, provider.Doors[0], Plan, _home, _home);
        var differences = DoorGrants.Differences(declared["servers"], observed["servers"], "servers");

        Assert.Contains(differences, found => found.StartsWith("servers.route:", StringComparison.Ordinal));
        Assert.Contains(differences, found => found.StartsWith("servers.names:", StringComparison.Ordinal));
    }

    [Fact]
    public void A_protocol_door_is_read_as_handing_the_servers_on_its_session_wire_and_asking_by_its_requests()
    {
        var provider = TestDoors.Door("planted-protocol", transport: Transport.Protocol, spawn: new PlainSpawn());

        var observed = DoorGrants.Observe(provider, provider.Doors[0], Plan, _home, _home);

        Assert.Equal(DoorGrants.SessionNew, (string?)observed["servers"]!["route"]);
        Assert.Equal([KnowledgeConnector.ServerName], observed["servers"]!["names"]!.AsArray().Select(name => (string?)name).ToArray());
        Assert.Equal(DoorGrants.PermissionRequest, (string?)observed["approval"]);
    }

    [Fact]
    public void The_stand_in_cases_read_the_agent_s_tools_and_fail_on_none_of_the_server_s_among_them_an_empty_list_too()
    {
        StandInServer.AssertListed(["Bash", "mcp__daoris-knowledge__ask"], "daoris-knowledge", "");
        StandInServer.AssertListed(["mcp__daoris_knowledge", "mcp__daoris_knowledge/ask"], "daoris-knowledge", "");

        Assert.ThrowsAny<Exception>(() => StandInServer.AssertListed([], "daoris-knowledge", ""));
        Assert.ThrowsAny<Exception>(() => StandInServer.AssertListed(["Bash", "Read"], "daoris-knowledge", ""));
        Assert.ThrowsAny<Exception>(() => StandInServer.AssertListed(["mcp__daoris_knowledge"], "daoris-knowledge", ""));
    }

    [Fact]
    public void Every_row_names_the_test_that_reads_the_tools_its_agent_lists_or_says_why_none_is_written_yet()
    {
        var problems = new List<string>();
        var open = OpenBacklogRows.Read();
        var tests = typeof(DoorGrantsCensusTests).Assembly;
        foreach (var provider in Providers.Built())
        {
            foreach (var (id, row) in DoorGrants.Declared(provider.Id.Value).Rows)
            {
                var proof = row["proof"] as JsonObject;
                var test = (string?)proof?["test"];
                var none = (string?)proof?["none"];
                var backlog = (string?)proof?["row"];
                if (test is not null)
                {
                    var (type, method) = (test.Split('.')[0], test.Split('.').ElementAtOrDefault(1));
                    var found = tests.GetTypes().FirstOrDefault(each => each.Name == type)?.GetMethod(method ?? "");
                    if (found?.GetCustomAttributes<FactAttribute>().Any() != true)
                    {
                        problems.Add($"{id}: proof test `{test}` is no test of this project (a class, a dot, a method marked as a test)");
                    }
                }
                else if (string.IsNullOrWhiteSpace(none))
                {
                    problems.Add($"{id}: its proof names a `test`, or a `none` saying why there is none");
                }
                else if (!provider.Identity.StandIn && (backlog is null || !open.Contains(backlog)))
                {
                    problems.Add($"{id}: a door that runs a model has no proof but an open backlog row that writes it: `row` is {backlog ?? "missing"}");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    /// <summary>A door whose spawn names no flag of its own: its servers ride its session wire, or none reach it.</summary>
    private sealed class PlainSpawn : ISpawn
    {
        public ProcessRecipe Start(SpawnRequest request) => ProcessRecipe.Of(request.Command ?? ["program"]);

        public ProcessRecipe Chat(SpawnRequest request) => Start(request);
    }

    /// <summary>A door whose spawn hands the servers nowhere: its arguments carry no file, so the agent never learns of the connector.</summary>
    private sealed class DropsTheServers : ISpawn, IServerHanding
    {
        public ProcessRecipe Start(SpawnRequest request) => ProcessRecipe.Of(request.Command ?? ["program"]);

        public ProcessRecipe Chat(SpawnRequest request) => Start(request);

        public string? Hand(ProcessStartInfo info, string home, ConnectorPlan plan) => null;
    }
}
