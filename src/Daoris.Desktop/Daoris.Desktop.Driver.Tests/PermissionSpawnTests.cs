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
