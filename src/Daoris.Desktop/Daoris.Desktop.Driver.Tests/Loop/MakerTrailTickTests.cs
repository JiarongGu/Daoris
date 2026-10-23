using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MAKERTRAIL2 through REAL ticks: a session takes the quest, leaves its work uncommitted in its tree and fails; the next tick carries
/// the quest on in that tree. Carried on to another harness, the new maker's brief names the first one's trailer beside its own, its
/// model unreported since that part's door reported none; carried on within the harness, it says nothing more. A real process (node)
/// and the stand-in service on a loopback port, the shapes <see cref="StartInSyncTickTests"/> uses; no model and no account.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class MakerTrailTickTests : IDisposable
{
    private const string Left = "The part of this line before you ran on `stub`";

    private const string Trailer = "Co-Authored-By: stub (maker unknown, model unreported) <its maker's attribution address>";

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "maker-trail", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public MakerTrailTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "trail@example.com");
        Git("config", "user.name", "MAKERTRAIL2");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }

    private string Log => Path.Combine(_home, "agent.log");

    private JsonObject[] Runs() => [.. File.ReadAllLines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    /// <summary>🔴 The incident's shape: one harness ran out with its work uncommitted; the line carried on to another in its tree.</summary>
    [Fact]
    public async Task A_carry_on_to_another_maker_is_told_the_first_makers_trailer_for_the_work_it_left()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.KeepsAdapter = true;

        await Driver(service, "stub").RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal("failed", service.Session("s1")["state"]!.GetValue<string>());
        await Driver(service, "acp-stub").RunOnceAsync().WaitAsync(Poll.Patience);

        var prompt = Runs().Last(line => line["method"]?.GetValue<string>() == "session/prompt")["prompt"]!.GetValue<string>();
        Assert.Contains("You are carrying on quest `#q1`", prompt);
        Assert.Contains("This start runs on `acp-stub`", prompt);
        Assert.Contains(Left, prompt);
        Assert.Contains(Trailer, prompt);
        Assert.Equal("Done", service.Status("q1"));
    }

    /// <summary>Carried on within its harness, the brief is told its own attribution alone.</summary>
    [Fact]
    public async Task A_carry_on_within_a_maker_says_nothing_more()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.KeepsAdapter = true;

        var driver = Driver(service, "stub");
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal("failed", service.Session("s1")["state"]!.GetValue<string>());
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var target = Runs().Last()["target"]!.GetValue<string>();
        Assert.Contains("You are carrying on quest `#q1`", target);
        Assert.Contains("This start runs on `stub`", target);
        Assert.DoesNotContain("The part of this line before you", target);
        Assert.Equal("Done", service.Status("q1"));
    }

    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, string adapter)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = adapter,
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["stub"] = ["node", Agent(), Log],
                ["acp-stub"] = ["node", AcpAgent(), Log],
            },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// The pipe door's stand-in: a first start takes the quest, writes its fix and fails with it uncommitted; a carry-on closes it.
    /// What it was handed goes to the log.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, writeFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [log] = process.argv.slice(2);
            const target = process.env.DAORIS_TARGET ?? '';
            appendFileSync(log, JSON.stringify({ target }) + '\n');
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            if (target.includes('carrying on quest')) {
              await respond({ action: 'done', reason: 'landed' });
              process.exit(0);
            }
            await respond({ action: 'take' });
            writeFileSync('fix.txt', 'the fix\n');
            process.exit(1);
            """);
        return script;
    }

    /// <summary>The protocol door's stand-in: its first prompt goes to the log, and it closes the quest it carries on.</summary>
    private string AcpAgent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            const [log] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            let conversation = null;
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + process.env.DAORIS_SESSION_ID;
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/prompt') {
                const prompt = (frame.params?.prompt ?? []).map((block) => block.text ?? '').join('\n');
                appendFileSync(log, JSON.stringify({ method: 'session/prompt', prompt }) + '\n');
                if (prompt.includes('carrying on quest')) await respond({ action: 'done', reason: 'landed' });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
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
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        _ = stderr.Result;
    }
}
