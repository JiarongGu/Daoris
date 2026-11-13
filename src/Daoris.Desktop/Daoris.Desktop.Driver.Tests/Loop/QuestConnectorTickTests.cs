using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Needs, Needed.Network)]
[Trait(Category.Needs, Needed.Harness)]
public sealed class QuestConnectorTickTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "quest-connectors-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _repository;

    public QuestConnectorTickTests()
    {
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        GitSeed.StartingPoint(_repository, "connectors@example.com", "connector test");
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.AgentHomes, "claude-code", "work"));
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Harnesses), """{"defaults":{"claude-code":"work"}}""");
        var plugin = DaorisHome.Entry(_home, HomeEntry.Plugins, "reference");
        Directory.CreateDirectory(plugin);
        File.WriteAllText(Path.Combine(plugin, PluginCatalog.ManifestName), """
            {"id":"reference","servers":[{"name":"reference","command":["reference-server"]}]}
            """);
    }

    public void Dispose() => GitSeed.Discard(_home);

    [Theory]
    [InlineData("claude-code", false)]
    [InlineData("claude-code", true)]
    [InlineData("claude-code-acp", false)]
    [InlineData("claude-code-acp", true)]
    public async Task A_quest_start_and_a_failed_sessions_carry_on_receive_the_connector(string door, bool carryOn)
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var host = Path.Combine(_home, "connector-host");
        File.WriteAllText(host, "the connector executable is located, never started by this harness");
        var log = Path.Combine(_home, "servers.json");
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Actors = StaffedRoles.On(door),
            TestBound = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { [door] = ["node", Harness(), log, carryOn ? "fail-first" : "finish"] },
        };
        var adapters = ProviderRegistry.Built();
        using var client = new ServiceClient(service.Url, null);
        var driver = new Daoris.Driver.Driver(
            client, config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Variable = name => name == KnowledgeConnector.PathVariable ? host : name == DaorisHome.Variable ? _home : null,
        };

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        AssertHanded(log, host, "s1");
        var first = service.Session("s1");
        if (carryOn)
        {
            Assert.Equal("failed", first["state"]!.GetValue<string>());
            Assert.Equal("Taken", service.Status("q1"));
            await driver.RunOnceAsync().WaitAsync(Poll.Patience);
            AssertHanded(log, host, "s2");
            Assert.Equal(first["tree"]!.GetValue<string>(), service.Session("s2")["tree"]!.GetValue<string>());
        }

        Assert.Equal("completed", service.Session(carryOn ? "s2" : "s1")["state"]!.GetValue<string>());
        Assert.Equal("Done", service.Status("q1"));
        Assert.False(File.Exists(Path.Combine(_repository, ".mcp.json")));
        Assert.False(File.Exists(DaorisHome.Entry(_home, HomeEntry.Spawn, $"{(carryOn ? "s2" : "s1")}{ClaudeServers.FileSuffix}")));
    }

    private void AssertHanded(string log, string host, string session)
    {
        using var file = JsonDocument.Parse(File.ReadAllText(log));
        var servers = file.RootElement.EnumerateArray().ToList();
        var connector = Assert.Single(servers, server => server.GetProperty("name").GetString() == KnowledgeConnector.ServerName);
        Assert.Equal(host, connector.GetProperty("command").GetString());
        var env = connector.GetProperty("env");
        Assert.Equal(_home, env.GetProperty(DaorisHome.Variable).GetString());
        Assert.Equal(_home, env.GetProperty(KnowledgeConnector.RulesHomeVariable).GetString());
        Assert.Equal(session, env.GetProperty(ChildMarks.SessionVariable).GetString());
        Assert.Single(servers, server => server.GetProperty("name").GetString() == "reference");
    }

    private string Harness()
    {
        var path = Path.Combine(_home, "claude.mjs");
        File.WriteAllText(path, """
            import { readFileSync, writeFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            const [log, mode, ...argv] = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.0 (Claude Code)'); process.exit(0); }
            if (argv[0] === 'auth' && argv[1] === 'status') { console.log('{"loggedIn":true}'); process.exit(0); }
            const send = (frame) => console.log(JSON.stringify(frame));
            const capture = (servers) => writeFileSync(log, JSON.stringify(servers));
            const respond = (action) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ action, reason: 'connector handed' }) });
            const work = async () => {
              await respond('take');
              if (mode === 'fail-first' && process.env.DAORIS_SESSION_ID === 's1') process.exit(1);
              await respond('done');
            };
            if (argv.includes('-p')) {
              const at = argv.indexOf('--mcp-config');
              const servers = at < 0 ? {} : JSON.parse(readFileSync(argv[at + 1], 'utf8')).mcpServers;
              capture(Object.entries(servers).map(([name, server]) => ({ name, ...server })));
              for await (const chunk of process.stdin) {}
              send({type:'system', subtype:'init', session_id:'native-quest'});
              await work();
              send({type:'result', subtype:'success', is_error:false, result:'finished'});
            } else {
              for await (const line of createInterface({input:process.stdin})) {
                const frame = JSON.parse(line);
                if (frame.method === 'initialize') {
                  send({jsonrpc:'2.0', id:frame.id, result:{protocolVersion:1, agentCapabilities:{}}});
                } else if (frame.method === 'session/new') {
                  capture(frame.params.mcpServers.map(server => ({...server, env:Object.fromEntries(server.env.map(pair => [pair.name, pair.value]))})));
                  send({jsonrpc:'2.0', id:frame.id, result:{sessionId:'protocol-quest', modes:{currentModeId:'auto', availableModes:[{id:'auto', name:'Auto'}]}}});
                } else if (frame.method === 'session/prompt') {
                  await work();
                  send({jsonrpc:'2.0', id:frame.id, result:{stopReason:'end_turn'}});
                } else if (frame.id !== undefined && frame.method) {
                  send({jsonrpc:'2.0', id:frame.id, result:{}});
                }
              }
            }
            """);
        return path;
    }
}
