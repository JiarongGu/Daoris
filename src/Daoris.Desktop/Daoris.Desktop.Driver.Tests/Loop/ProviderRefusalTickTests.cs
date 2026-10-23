using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// APIREFUSAL1, through a REAL tick on the protocol door. Seen on the install (2026-10-23): a driven session took its quest, and
/// its model's provider refused a call. The adapter said the refusal as the agent's message (an <c>API Error: …</c> with its
/// request id) and answered the prompt with <c>stopReason: refusal</c>, then exited 0. Its quest was still taken, so the record
/// parked on the person, quoting the error as a question nobody could answer.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ProviderRefusalTickTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-api-refusal-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public ProviderRefusalTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "refusal@example.com");
        Git("config", "user.name", "APIREFUSAL1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The words the install's adapter said before its <c>refusal</c>, cut to their facts.</summary>
    private const string Refusal =
        "API Error: the model's safeguards flagged this message. Details: `[reasoning_extraction]`\n\nRequest ID: req_011StandIn";

    /// <summary>
    /// 🔴 The install's case: the turn its provider refused is a failed run, by its own code, keeping the provider's words and
    /// the request id; never a park, so never in <i>What needs you</i>.
    /// </summary>
    [Fact]
    public async Task A_turn_its_provider_refused_is_a_failed_run_by_its_code_and_never_a_park()
    {
        await using var service = AnswerContinuesTickTests.ParkStandIn.Start(_repository);

        await Driver(service, "refusal").RunOnceAsync().WaitAsync(Poll.Patience);

        var record = service.Session("s1");
        Assert.Equal("Taken", service.Status("q1"));
        Assert.Equal("failed", record["state"]!.GetValue<string>());
        var parts = record["noteParts"]!.AsArray();
        Assert.Equal("ended.provider-refused", parts[0]!["code"]!.GetValue<string>());
        Assert.DoesNotContain(parts, part => part!["code"]?.GetValue<string>() is "ended.parked-asked" or "ended.parked-transcript");
        var note = record["note"]!.GetValue<string>();
        Assert.Contains("req_011StandIn", note);
        Assert.Contains("[reasoning_extraction]", note);
    }

    /// <summary>A turn that ends on a real question, on the same door, still parks on the person.</summary>
    [Fact]
    public async Task A_turn_that_ends_on_a_real_question_still_parks()
    {
        await using var service = AnswerContinuesTickTests.ParkStandIn.Start(_repository);

        await Driver(service, "question").RunOnceAsync().WaitAsync(Poll.Patience);

        var record = service.Session("s1");
        Assert.Equal("awaiting-person", record["state"]!.GetValue<string>());
        Assert.Equal("ended.parked-asked", record["noteParts"]!.AsArray()[0]!["code"]!.GetValue<string>());
        Assert.Contains("Which port should it listen on?", record["note"]!.GetValue<string>());
    }

    private Daoris.Driver.Driver Driver(AnswerContinuesTickTests.ParkStandIn service, string mode)
    {
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), mode] },
        };
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// The protocol door's stand-in: it takes its quest, then says the provider's refusal and ends its turn <c>refusal</c>, as the
    /// install's adapter did; or asks a question and ends it <c>end_turn</c>. It exits 0 once its stdin closes.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, $$"""
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const mode = process.argv[2];
            const refusal = {{System.Text.Json.JsonSerializer.Serialize(Refusal)}};
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-1',
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                await respond({ action: 'take' });
                if (mode === 'refusal') {
                  say(refusal);
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'refusal' } });
                } else {
                  say('Which port should it listen on?');
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                }
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            // stdin closed: the ending. Never process.exit (STUB1): the process ends when the loop drains.
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
