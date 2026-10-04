using System.Diagnostics;
using System.Net.Http;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ROSTER1b through REAL ticks, the install's case of 2026-10-04: no account read yet, the list
/// <c>[account-1, account-2]</c>, and the protocol door's agent refusing every start on account-1 with
/// <i>Authentication required</i>. The first start runs on account-1 and is refused; its record fails saying why;
/// <c>reads.json</c> holds account-1 signed out with its time; the quest holds no strike, though one would park it; and the
/// next start walks past account-1 to account-2, which closes the quest.
/// </summary>
/// <remarks>
/// The protocol stub runs as the stub's accounts (TOOL4j) and reads the stub's words as its owner's (AGT7). Its agent
/// speaks the wire and nothing else on stdout, refuses <c>session/new</c> on account-1 as Claude Code's adapter refused on the
/// install, and on any other account takes the quest and closes it, writing down where it ran.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class SignInRefusalTickTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 4, 14, 0, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-signin-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public SignInRefusalTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "signin@example.com");
        Git("config", "user.name", "ROSTER1b");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");

        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        }

        new HarnessSettings().WithDefault("stub", "account-1").WithRotation("stub", ["account-1", "account-2"])
            .Save(Path.Combine(_home, "harnesses.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    [Fact]
    public async Task A_start_refused_for_its_sign_in_marks_its_account_and_the_next_start_takes_another()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")) { Clock = () => Seen, Zone = Zone };
        using var client = new ServiceClient(service.Url, null);
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TimeoutMinutes = 1,
            PollSeconds = 1,
            // One failure would park it: a refused sign-in must hold none.
            Strikes = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", AcpAgent(), Log] },
        };
        var driver = new Daoris.Driver.Driver(client, config, adapters, _home, processes: new SessionProcesses(), harnesses: roster);

        // Nothing read yet, as on the install after its update: the first start runs on account-1, the list's first.
        Assert.Empty(AccountReads.Of(_home, "stub").Accounts);
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var refused = service.Session("s1");
        Assert.Equal(("failed", "account-1"), (refused["state"]!.GetValue<string>(), refused["profile"]!.GetValue<string>()));
        var note = refused["note"]!.GetValue<string>();
        Assert.Contains("the ACP agent refused the call: Authentication required", note);
        Assert.Contains("The agent refused the `stub` account `account-1` for its sign-in", note);
        Assert.Contains(refused["noteParts"]!.AsArray(), part => part?["code"]?.GetValue<string>() == "account.signed-out");
        Assert.Equal("Open", service.Status("q1"));

        // reads.json holds account-1 signed out, with its time, as a reading would.
        Assert.Equal(new AccountRead(LoginState.Out, Seen), AccountReads.Of(_home, "stub").Accounts["account-1"]);
        Assert.False(AccountReads.Of(_home, "stub").Accounts.ContainsKey("account-2"));

        // The quest's strikes stay where they were: none, though one would have parked it.
        using (var http = new HttpClient())
        {
            var records = await http.GetStringAsync($"{service.Url}/api/sessions?includeClosed=true");
            Assert.False(ServiceClient.ReadStrikes(records).ContainsKey("q1"));
        }

        // The next start walks past account-1 to account-2, and closes the quest.
        var next = await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        Assert.DoesNotContain(next.Considerations, c => c.Verdict == StartVerdict.Exhausted);
        var carried = service.Session("s2");
        Assert.Equal(("completed", "account-2"), (carried["state"]!.GetValue<string>(), carried["profile"]!.GetValue<string>()));
        Assert.Equal("Done", service.Status("q1"));
        var runs = File.ReadAllLines(Log).Select(line => JsonNode.Parse(line)!).ToList();
        Assert.Equal(
            [("account-1", "session/new"), ("account-2", "session/new"), ("account-2", "session/prompt")],
            runs.Select(run => (run["account"]!.GetValue<string>(), run["method"]!.GetValue<string>())));

        // Its conversation record opens saying why it ran on account-2: the quest was never taken, so this is a start, not a
        // carry-on, and the step that passed account-1 is its sign-in.
        var opening = new SessionEvents(Path.Combine(_home, "sessions")).After("s2", 0).Events[0];
        Assert.StartsWith("opened on `account-2`: the `stub` account `account-1` is not signed in", opening.Text);
    }

    /// <summary>
    /// The protocol door's stand-in: on account-1, <c>session/new</c> is refused as Claude Code's adapter refused on the
    /// install, <c>-32000 Authentication required</c>; on any other account it opens, and a prompt takes the quest and closes it.
    /// </summary>
    private string AcpAgent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            const [log] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const url = process.env.DAORIS_SERVICE_URL;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'session/new' || frame.method === 'session/prompt') {
                appendFileSync(log, JSON.stringify({ account, method: frame.method }) + '\n');
              }
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new' && account === 'account-1') {
                send({ jsonrpc: '2.0', id: frame.id, error: { code: -32000, message: 'Authentication required' } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                await respond({ action: 'take' });
                await respond({ action: 'done', reason: 'finished on the next account of the list' });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            process.exit(0);
            """);
        return script;
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
