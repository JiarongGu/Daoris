using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driver hands every session it starts the rules composed for it (PERM1, D72), through a REAL
/// tick: a real process, spawned by the executor, against a stand-in service on a loopback port. What
/// is asserted is what the harness would have been handed — on each door — and that the file goes when
/// the session does. No model anywhere, and no account.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class PermissionSpawnTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-permission-spawn-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public PermissionSpawnTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "perm1@example.com");
        Git("config", "user.name", "PERM1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The machine's rules as a person set them: one for the session's circle, one for its repository.</summary>
    private void Rules()
    {
        var file = PermissionRules.Load(_home);
        file = PermissionRules.Add(file, RuleScope.Workspace, "default", RuleList.Deny, "Bash(rm -rf:*)");
        file = PermissionRules.Add(file, RuleScope.Repository, "engine", RuleList.Allow, "Bash(make:*)");
        file = PermissionRules.Add(file, RuleScope.Repository, "game", RuleList.Allow, "WebFetch");
        PermissionRules.Save(_home, file);
    }

    [Fact]
    public async Task A_pipe_door_session_is_handed_its_composed_rules_and_they_go_when_it_does()
    {
        Rules();
        await using var service = DrivenSessionInputTests.StandInService.Start(_repository);
        var adapter = new RecordingPipeAdapter();
        var driver = Driver(adapter, ["node", Agent("pipe-agent.mjs", "console.log('done'); process.exit(0);")], service);

        await driver.TickAsync();

        Assert.NotNull(adapter.Handed);
        using var handed = JsonDocument.Parse(adapter.HandedText!);
        var permissions = handed.RootElement.GetProperty("permissions");
        var allow = permissions.GetProperty("allow").EnumerateArray().Select(e => e.GetString()).ToList();
        var deny = permissions.GetProperty("deny").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("mcp__daoris-knowledge__quest_respond", allow);
        Assert.Contains("Bash(make:*)", allow);
        Assert.DoesNotContain("WebFetch", allow);
        Assert.Contains("Bash(git push:*)", deny);
        Assert.Contains("Bash(rm -rf:*)", deny);

        Assert.False(File.Exists(adapter.Handed), "the session's settings file outlived it");
    }

    /// <summary>
    /// The tree guard (PERM3) rides the same file, naming the tree THIS session works in — the
    /// executor's own, never re-derived by the hook — and the commit the session may make (PERM4).
    /// </summary>
    [Fact]
    public async Task A_session_is_handed_the_tree_guard_on_its_own_tree_and_may_commit()
    {
        await using var service = DrivenSessionInputTests.StandInService.Start(_repository);
        var adapter = new RecordingPipeAdapter();
        var driver = Driver(adapter, ["node", Agent("pipe-agent.mjs", "console.log('done'); process.exit(0);")], service);

        await driver.TickAsync();

        using var handed = JsonDocument.Parse(adapter.HandedText!);
        var hook = handed.RootElement.GetProperty("hooks").GetProperty("PreToolUse")[0].GetProperty("hooks")[0];
        var args = hook.GetProperty("args").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(Path.GetFullPath(_repository), Path.GetFullPath(args[1]!));
        Assert.True(File.Exists(args[0]), "the guard's script was not where the hook names it");
        Assert.Contains(
            "Bash(git commit:*)",
            handed.RootElement.GetProperty("permissions").GetProperty("allow").EnumerateArray().Select(e => e.GetString()));
    }

    /// <summary>
    /// A session may READ the folder its quest's files are kept in (INT4j), and nothing else under the
    /// home: they live outside its tree, and a read there would otherwise be asked, and refused (D52).
    /// </summary>
    [Fact]
    public async Task A_session_may_read_its_quests_kept_files_and_nothing_else_of_the_home()
    {
        await using var service = DrivenSessionInputTests.StandInService.Start(
            _repository, kept: "C:/somewhere/data/quests/q1/attachments/ab12-before.png");
        var adapter = new RecordingPipeAdapter();
        var driver = Driver(adapter, ["node", Agent("pipe-agent.mjs", "console.log('done'); process.exit(0);")], service);

        await driver.TickAsync();

        using var handed = JsonDocument.Parse(adapter.HandedText!);
        var reads = handed.RootElement.GetProperty("permissions").GetProperty("allow").EnumerateArray()
            .Select(e => e.GetString()!).Where(rule => rule.StartsWith("Read(", StringComparison.Ordinal)).ToList();
        Assert.Equal(["Read(//c/somewhere/data/quests/q1/attachments/**)"], reads);
    }

    /// <summary>A quest with nothing kept on this machine is handed no read of any folder.</summary>
    [Fact]
    public async Task A_session_whose_quest_keeps_nothing_is_handed_no_read()
    {
        await using var service = DrivenSessionInputTests.StandInService.Start(_repository);
        var adapter = new RecordingPipeAdapter();
        var driver = Driver(adapter, ["node", Agent("pipe-agent.mjs", "console.log('done'); process.exit(0);")], service);

        await driver.TickAsync();

        using var handed = JsonDocument.Parse(adapter.HandedText!);
        Assert.DoesNotContain(
            handed.RootElement.GetProperty("permissions").GetProperty("allow").EnumerateArray().Select(e => e.GetString()!),
            rule => rule.StartsWith("Read(", StringComparison.Ordinal));
    }

    /// <summary>
    /// An agent's narrowing (PERM2) is settled at the START of a tick, so the session that tick spawns
    /// is already handed the narrower rules — "applies at the next tick" means this one's spawns too.
    /// </summary>
    [Fact]
    public async Task A_narrowing_proposed_before_a_tick_is_in_the_rules_its_session_is_handed()
    {
        Rules();
        var proposals = Path.Combine(_home, RuleProposals.Folder);
        Directory.CreateDirectory(proposals);
        File.WriteAllText(Path.Combine(proposals, "p1a2b3c4.json"), """
            {
              "id": "p1a2b3c4",
              "proposed": "2026-09-24T10:00:00.0000000+00:00",
              "by": { "session": "s0f1r2s3", "ask": null, "folder": "C:/somewhere/engine" },
              "change": { "action": "remove", "scope": "repository", "name": "engine", "list": null, "rule": "Bash(make:*)", "default": null, "on": null },
              "why": "make rebuilt the whole tree when one target was asked for.",
              "state": "proposed"
            }
            """);
        await using var service = DrivenSessionInputTests.StandInService.Start(_repository);
        var adapter = new RecordingPipeAdapter();
        var driver = Driver(adapter, ["node", Agent("pipe-agent.mjs", "console.log('done'); process.exit(0);")], service);

        var report = await driver.TickAsync();

        using var handed = JsonDocument.Parse(adapter.HandedText!);
        Assert.DoesNotContain(
            "Bash(make:*)",
            handed.RootElement.GetProperty("permissions").GetProperty("allow").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains(report.Events, line => line.Contains("applied #p1a2b3c4") && line.Contains("session s0f1r2s3"));
    }

    /// <summary>
    /// The connector the driver offers names THIS session and the home its rules live in, so a proposal
    /// says who made it and lands beside the rules it would change (PERM2) — an intake's ask rides too.
    /// </summary>
    [Fact]
    public void The_connector_carries_the_session_and_the_rules_home_beside_an_intakes_scope()
    {
        var quest = Daoris.Driver.Driver.ConnectorScope("C:/somewhere/data", "s1a2b3c4", scope: null);
        var intake = Daoris.Driver.Driver.ConnectorScope("C:/somewhere/data", "i9n8t7k6", IntakeRoom.Scope("a1b2c3", "i9n8t7k6"));

        Assert.Equal("C:/somewhere/data", quest[KnowledgeConnector.RulesHomeVariable]);
        Assert.Equal("s1a2b3c4", quest[IntakeRoom.SessionVariable]);
        Assert.False(quest.ContainsKey(IntakeRoom.AskVariable));
        Assert.Equal("a1b2c3", intake[IntakeRoom.AskVariable]);
        Assert.Equal("i9n8t7k6", intake[IntakeRoom.SessionVariable]);
    }

    /// <summary>
    /// 🔴 On the protocol door the rules ride `session/new` — the stand-in agent writes down the `_meta`
    /// it was given, and the file it names is the one composed for this session.
    /// </summary>
    [Fact]
    public async Task A_protocol_door_session_is_handed_them_on_session_new()
    {
        Rules();
        await using var service = DrivenSessionInputTests.StandInService.Start(_repository);
        var heard = Path.Combine(_home, "meta.json");
        var adapter = new RecordingAcpAdapter();
        var driver = Driver(adapter, ["node", ProtocolAgent(), heard], service);

        await driver.TickAsync();

        Assert.True(File.Exists(heard), "the agent was never given a session/new");
        using var meta = JsonDocument.Parse(File.ReadAllText(heard));
        var settings = meta.RootElement.GetProperty("claudeCode").GetProperty("options").GetProperty("settings").GetString();
        Assert.Equal(adapter.Handed, settings);
        using var handed = JsonDocument.Parse(adapter.HandedText!);
        Assert.Contains("Bash(make:*)", handed.RootElement.GetProperty("permissions").GetProperty("allow").EnumerateArray().Select(e => e.GetString()));
    }

    private Daoris.Driver.Driver Driver(
        ISessionAdapter adapter, IReadOnlyList<string> command, DrivenSessionInputTests.StandInService service)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = adapter.Name,
            TimeoutMinutes = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { [adapter.Name] = command },
        };
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter> { [adapter.Name] = adapter });
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>A pipe-door harness that takes a settings file, and remembers what it was handed.</summary>
    private sealed class RecordingPipeAdapter : ISessionAdapter
    {
        public string Name => "recording";
        public bool TakesSettings => true;
        public string? Handed { get; private set; }
        public string? HandedText { get; private set; }

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            new StubAdapter().Prepare(target, command);

        public void HandSettings(ProcessStartInfo info, string settingsFile)
        {
            Handed = settingsFile;
            HandedText = File.ReadAllText(settingsFile);
        }
    }

    /// <summary>A protocol-door harness that takes the settings on `session/new`, as Claude Code's adapter does.</summary>
    private sealed class RecordingAcpAdapter : ISessionAdapter
    {
        public string Name => "recording-acp";
        public SessionWire Wire => SessionWire.Acp;
        public bool TakesSettings => true;
        public string? Handed { get; private set; }
        public string? HandedText { get; private set; }

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
        {
            var info = new StubAdapter().Prepare(target, command);
            info.RedirectStandardInput = true;
            return info;
        }

        public object? AcpSessionMeta(string settingsFile)
        {
            Handed = settingsFile;
            HandedText = File.ReadAllText(settingsFile);
            return new { claudeCode = new { options = new { settings = settingsFile } } };
        }
    }

    private string Agent(string name, string body)
    {
        var script = Path.Combine(_home, name);
        File.WriteAllText(script, body);
        return script;
    }

    /// <summary>A protocol stand-in that writes down the `_meta` its `session/new` carried, then ends its turn.</summary>
    private string ProtocolAgent() => Agent("acp-agent.mjs", """
        import { writeFileSync } from 'node:fs';
        import { createInterface } from 'node:readline';
        const heard = process.argv[2];
        const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
        for await (const line of createInterface({ input: process.stdin })) {
          let frame;
          try { frame = JSON.parse(line); } catch { continue; }
          if (frame.method === 'initialize') {
            send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
          } else if (frame.method === 'session/new') {
            writeFileSync(heard, JSON.stringify(frame.params._meta ?? null));
            send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
          } else if (frame.method === 'session/prompt') {
            send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
          } else if (frame.id !== undefined && frame.method) {
            send({ jsonrpc: '2.0', id: frame.id, result: {} });
          }
        }
        """);

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
