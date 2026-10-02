using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL6c (D130 §5.2, §6, §16.3 step 2, §16.4) through REAL ticks: the protocol stub, running as stub account 1, replays the
/// frame the limit-signals evidence recorded (§1.1: 88% of the session window, 14% of the week) as Claude Code's adapter
/// forwards it, a <c>usage_update</c> whose <c>_meta["_claude/rateLimit"]</c> is the frame (§3). The driver keeps it in
/// <c>windows.json</c> for stub account 1, by the stub's table, which mirrors Claude Code's. With the scope's <i>near</i> at
/// 85, the next start passes stub account 1 for stub account 2, under <c>order</c> and then under the goal, and each record
/// opens saying what each account said, and the log says <c>account.rotated</c> with <c>why: near</c>.
/// </summary>
/// <remarks>
/// <para>Real node processes in a real git checkout, against the in-process ledger (<see cref="StandInLedger"/>), as
/// <c>AccountGoalTickTests</c> runs them. Each session ends its turn at once, so each look settles before the next.</para>
/// <para>Written by TOOL6c and run by the parent at the merge (MOD8).</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class AccountReadingTickTests : IDisposable
{
    private static readonly string[] Accounts = ["account-1", "account-2"];

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-reading-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    public AccountReadingTickTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Repository("engine"));
        foreach (var account in Accounts) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        Wire("order");
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => Path.Combine(_home, "harnesses.json");

    private void Wire(string use) =>
        new HarnessSettings().WithRotation("stub", Accounts).WithUse("stub", new UseChange(Use: use, Near: 85)).Save(Settings);

    private const string Said =
        "What each account said: `account-1` just now, 88% of its session limit and 14% of its weekly limit used, near at 85%; "
        + "`account-2` nothing yet.";

    private const string Passed = "`account-1` has used 88% of its session limit, at or over the 85% that counts as near";

    [Fact]
    public async Task A_frame_replayed_on_the_protocol_door_is_kept_and_the_next_start_passes_the_account_it_says_is_near()
    {
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, Settings) { Clock = () => Now, Zone = Zone };
        using var service = _ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += line => { lock (lines) lines.Add(line); };
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = "acp-stub",
            Cap = 1,
            TimeoutMinutes = 2,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", AcpAgent(), Frame()] },
        };
        var driver = new Daoris.Driver.Driver(service, config, adapters, _home, processes: new SessionProcesses(), harnesses: roster);
        var events = new SessionEvents(Path.Combine(_home, "sessions"));

        // The first start runs where the list begins, as nothing has been said; its agent replays the recorded frame.
        _ledger.Publish("q1", "engine");
        await Look(driver);
        var first = Assert.Single(_ledger.Sessions);
        Assert.Equal("account-1", first["profile"]!.GetValue<string>());

        // Kept for stub account 1, by its owner's table, as of when it was said, on the session that said it.
        var kept = AccountWindows.SaidOf(_home, "stub", "account-1", Now)!;
        var id = first["id"]!.GetValue<string>();
        Assert.Equal(new WindowSaid("session", 0.88, kept.Of("session")!.Reset, "clear", false, Now, id), kept.Of("session"));
        Assert.Equal(new WindowSaid("weekly", 0.14, kept.Of("weekly")!.Reset, null, false, Now, id), kept.Of("weekly"));
        Assert.Null(AccountWindows.SaidOf(_home, "stub", "account-2", Now));

        // Under `order`, the next start passes stub account 1, near at 85%, and its record says what each account said.
        _ledger.Move("q1", "Done");
        _ledger.Publish("q2", "engine");
        await Look(driver);
        var second = _ledger.SessionsFor("q2").Single();
        Assert.Equal("account-2", second["profile"]!.GetValue<string>());
        Assert.Equal($"opened on `account-2`: {Passed}. {Said}", Opening(events, second));

        // Under the goal, stub account 1 is still near and goes last, though Daoris started on it least recently.
        Wire("goal");
        _ledger.Move("q2", "Done");
        _ledger.Publish("q3", "engine");
        await Look(driver);
        var third = _ledger.SessionsFor("q3").Single();
        Assert.Equal("account-2", third["profile"]!.GetValue<string>());
        Assert.Equal($"opened on `account-2`: {Passed}. {Said}", Opening(events, third));
        Assert.DoesNotContain(RotationWords.Unsaid, Opening(events, third));

        lock (lines)
        {
            var rotated = lines.Where(line => line.Event == "account.rotated").ToList();
            Assert.Equal(2, rotated.Count);
            Assert.All(rotated, line => Assert.Equal(
                new object?[] { "acp-stub", "account-1", "account-2", null, "near", null, true, "near", null },
                line.Data.Skip(1).Select(field => field.Value)));
        }
    }

    private static async Task Look(Daoris.Driver.Driver driver)
    {
        await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(90));
        await driver.Running.SettledAsync().WaitAsync(TimeSpan.FromSeconds(90));
    }

    private static string? Opening(SessionEvents events, System.Text.Json.Nodes.JsonObject record) =>
        events.After(record["id"]!.GetValue<string>(), 0).Events.FirstOrDefault(e => e.Kind == SessionEventKind.Note)?.Text;

    /// <summary>The evidence's recorded frame (§1.1), its moments placed against this test's clock, in a file the stub reads.</summary>
    private string Frame()
    {
        var path = Path.Combine(_home, "frame.json");
        File.WriteAllText(path, AccountReadingsTests.Recorded(Now));
        return path;
    }

    /// <summary>
    /// The protocol door's stand-in (TOOL4j): it speaks the wire and nothing else on stdout. On stub account 1 a prompt is
    /// answered after one <c>usage_update</c> carrying the recorded frame as Claude Code's adapter forwards it; on any other
    /// account it is answered at once, saying nothing about the account's windows.
    /// </summary>
    private string AcpAgent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { readFileSync } from 'node:fs';
            import { basename } from 'node:path';
            const [framePath] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                if (account === 'account-1') {
                  const rateLimit = JSON.parse(readFileSync(framePath, 'utf8'));
                  send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-1',
                    update: { sessionUpdate: 'usage_update', used: 1200, size: 200000,
                      _meta: { '_claude/rateLimit': rateLimit, '_claude/model': 'm' } } } });
                }
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            process.exit(0);
            """);
        return script;
    }

    private string Repository(string name)
    {
        var root = Path.Combine(_home, name);
        Directory.CreateDirectory(root);
        Git(root, "init", "-q", "-b", "main");
        Git(root, "config", "user.email", "tool6c@example.com");
        Git(root, "config", "user.name", "TOOL6c");
        File.WriteAllText(Path.Combine(root, "README.md"), $"# {name}\n");
        Git(root, "add", "-A");
        Git(root, "commit", "-qm", "the starting point");
        return root;
    }

    private static void Git(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
