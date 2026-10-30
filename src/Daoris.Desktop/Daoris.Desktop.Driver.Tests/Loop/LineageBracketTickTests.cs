using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORKVIEW1a rule 4, through a REAL tick on the protocol door: a driven session's first process is bracketed, a <c>start</c>
/// naming its token and this Daoris process before its spawn and an <c>end</c> naming the same token once its exit was read and
/// its capture drained; its environment and every line written for it carry that token, and the token leaves once its exit is seen.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class LineageBracketTickTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-lineage-bracket-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _repository;

    public LineageBracketTickTests()
    {
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "lineage@example.com");
        Git("config", "user.name", "COWORKVIEW1a");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Git(params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    [Fact]
    public async Task A_driven_first_process_is_bracketed_by_its_own_token_and_every_line_of_it_names_that_token()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var told = Path.Combine(_home, "proc.txt");
        var tokens = new LineageTokens(Environment.ProcessId, DateTime.UtcNow);
        using var within = LineageTokens.Within(tokens);

        await Driver(service, told).RunOnceAsync().WaitAsync(Poll.Patience);

        var lines = File.ReadAllLines(ChildLineage.PathOf(_home, "s1")).Select(line => JsonDocument.Parse(line).RootElement).ToList();
        var proc = File.ReadAllText(told).Trim();
        Assert.Matches("^[0-9a-f]{16}$", proc);
        Assert.Equal(
            [("start", (string?)null), ("acp", "started"), ("acp", "ended"), ("end", null)],
            lines.Select(line => (line.GetProperty("by").GetString(), line.TryGetProperty("event", out var said) ? said.GetString() : null)));
        Assert.All(lines, line => Assert.Equal(proc, line.GetProperty("proc").GetString()));
        Assert.True(tokens.Issued(lines[0].GetProperty("issuer").GetString()));
        Assert.True(lines[^1].GetProperty("clean").GetBoolean());
        await Poll.Until(() => tokens.Reading(proc) == TokenReading.Gone, () => $"the token {proc} is still held", TimeSpan.FromSeconds(20));
    }

    /// <summary>
    /// A driven run whose loop closes while it waits on its process: the wait's kill reads the exit, so the process's <c>end</c> is
    /// written unclean as the throw leaves the run, and its token leaves, the agent's last word never said.
    /// </summary>
    [Fact]
    public async Task A_driven_run_whose_loop_closes_while_it_waits_writes_its_end_unclean()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var told = Path.Combine(_home, "proc.txt");
        var tokens = new LineageTokens(Environment.ProcessId, DateTime.UtcNow);
        using var within = LineageTokens.Within(tokens);
        using var closing = new CancellationTokenSource();

        var tick = Driver(service, told, hangs: true).RunOnceAsync(closing.Token);
        await Poll.Until(() => File.Exists(told) && File.ReadAllText(told).Trim().Length > 0, () => "the agent never started", Poll.Patience);
        await closing.CancelAsync();
        try { await tick.WaitAsync(Poll.Patience); } catch (OperationCanceledException) { }

        var lines = File.ReadAllLines(ChildLineage.PathOf(_home, "s1")).Select(line => JsonDocument.Parse(line).RootElement).ToList();
        var proc = File.ReadAllText(told).Trim();
        Assert.Equal("start", lines[0].GetProperty("by").GetString());
        var end = Assert.Single(lines, line => line.GetProperty("by").GetString() == "end");
        Assert.Equal(proc, end.GetProperty("proc").GetString());
        Assert.False(end.GetProperty("clean").GetBoolean());
        Assert.Equal(TokenReading.Gone, tokens.Reading(proc));
    }

    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, string told, bool hangs = false)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = "acp-stub",
            TestBound = 3,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), told, hangs ? "hangs" : "ends"] },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(), output: new SessionOutput(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// The protocol door's stand-in: it writes down the token its environment carries, takes its quest, starts one subagent and
    /// says it completed, closes its quest done and ends its turn, then exits once its input closes.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { writeFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            writeFileSync(process.argv[2], process.env.DAORIS_LINEAGE_PROC ?? '');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const update = (body) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-1', update: body } });
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                await respond({ action: 'take' });
                if (process.argv[3] === 'hangs') continue;
                update({ sessionUpdate: 'subagent_spawned', subagentSessionId: 'a1', name: 'Reader', task: 'Read the README.' });
                update({ sessionUpdate: 'subagent_state_update', subagentSessionId: 'a1', state: 'completed' });
                await respond({ action: 'done', reason: 'landed' });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            """);
        return script;
    }
}
