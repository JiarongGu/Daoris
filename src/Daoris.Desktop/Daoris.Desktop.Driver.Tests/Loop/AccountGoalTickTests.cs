using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL6b (D130 §16.3, §7, §11) through a REAL tick: cap K over N stub accounts on the protocol door, the list's
/// <c>use</c> left at its default, the goal. One look starts K sessions in K repositories, and no account runs more than
/// ⌈K ÷ N⌉ of them; each record opens naming the step that chose its account. A weekly limit then refuses the sessions on
/// one account, and cuts off only its own while the others work on; and the weekly reset that limit told puts that account
/// first in its next week's last day.
/// </summary>
/// <remarks>
/// <para>Real node processes in real git checkouts, against the in-process ledger (<see cref="StandInLedger"/>), as
/// <c>SessionsOutliveTheirLookProcessTests</c> runs them. The protocol stub runs as the stub's accounts (TOOL4j): on stub
/// account 1 its turn is refused with observation 4's weekly sentence; on any other it holds its turn until stopped.</para>
/// <para>Written by TOOL6b and run by the parent at the merge (MOD8): it starts K processes at once.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class AccountGoalTickTests : IDisposable
{
    private const int Cap = 4;

    private static readonly string[] Accounts = ["account-1", "account-2", "account-3"];

    private static readonly string[] Repositories = ["engine", "tools", "game", "docs"];

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    /// <summary>Observation 4's reset, read: 3 October, 16:02 in the test's zone.</summary>
    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-goal-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private DateTimeOffset _now = Seen;

    public AccountGoalTickTests()
    {
        Directory.CreateDirectory(_home);
        foreach (var repository in Repositories) _ledger.Register(repository, Repository(repository));
        foreach (var account in Accounts) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        new HarnessSettings().WithRotation("stub", Accounts).Save(Path.Combine(_home, "harnesses.json"));
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    [Fact]
    public async Task One_look_spreads_K_starts_over_N_accounts_and_a_limit_cuts_off_only_its_own()
    {
        var processes = new SessionProcesses();
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")) { Clock = () => _now, Zone = Zone };
        using var service = _ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += line => { lock (lines) lines.Add(line); };
        var config = DriverConfig.Empty with
        {
            Drivable = Repositories,
            Adapter = "acp-stub",
            Cap = Cap,
            TimeoutMinutes = 2,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", AcpAgent(), Log] },
        };
        var driver = new Daoris.Driver.Driver(service, config, adapters, _home, processes: processes, harnesses: roster);
        for (var i = 0; i < Cap; i++) _ledger.Publish($"q{i + 1}", Repositories[i]);

        // One look: K starts, chosen at once, counted as they are chosen.
        await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(90));

        var records = _ledger.Sessions;
        Assert.Equal(Cap, records.Count);
        var share = (Cap + Accounts.Length - 1) / Accounts.Length;
        var byAccount = records.GroupBy(record => record["profile"]!.GetValue<string>()).ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(Accounts.Order(StringComparer.Ordinal), byAccount.Keys.Order(StringComparer.Ordinal));
        Assert.All(byAccount.Values, count => Assert.InRange(count, Cap / Accounts.Length, share));
        Assert.Equal(share, byAccount["account-1"]);

        // Each record opens naming the step that chose its account, and that no account has said what it has left. The look
        // returns once its starts' records are open, and each start keeps its opening note on its own path after that, so the
        // read waits for the note rather than racing it (FLAKE1).
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        foreach (var record in records)
        {
            var id = record["id"]!.GetValue<string>();
            await Poll.Until(
                () => events.After(id, 0).Events.Count > 0,
                () => $"session {id} has kept no event yet",
                TimeSpan.FromSeconds(90));
            var opening = events.After(id, 0).Events[0];
            Assert.Equal(SessionEventKind.Note, opening.Kind);
            Assert.StartsWith($"opened on `{record["profile"]!.GetValue<string>()}`: ", opening.Text);
            Assert.EndsWith(" No account has said what it has left yet.", opening.Text);
        }

        // The weekly limit refuses the sessions on stub account 1, and only those: the others work on.
        string[] Of(string account) =>
            [.. _ledger.Sessions.Where(record => record["profile"]!.GetValue<string>() == account).Select(record => record["id"]!.GetValue<string>())];
        string State(string id) => _ledger.Session(id)["state"]!.GetValue<string>();
        await Poll.Until(
            () => Of("account-1").All(id => State(id) == "failed"),
            () => string.Join(", ", _ledger.Sessions.Select(record => $"{record["id"]}:{record["profile"]}:{record["state"]}")),
            TimeSpan.FromSeconds(90));
        Assert.All(Of("account-1"), id => Assert.True(_ledger.Session(id)["limit"]?.GetValue<bool>(), id));
        var working = Of("account-2").Concat(Of("account-3")).ToList();
        Assert.All(working, id => Assert.Equal("working", State(id)));
        Assert.All(working, id => Assert.Contains(id, processes.Running));
        lock (lines)
        {
            Assert.All(lines.Where(line => line.Event == "account.limited"),
                line => Assert.Equal("account-1", line.Data.Single(field => field.Key == "account").Value));
            Assert.Equal(share, lines.Count(line => line.Event == "account.limited"));
        }

        // Stub account 1 cools until the reset the agent named, and that reset is its weekly one from now on.
        Assert.Equal(Until, roster.CoolingOf("acp-stub", "account-1")!.Until);
        Assert.Null(roster.CoolingOf("acp-stub", "account-2"));
        Assert.Equal(Until, AccountWindows.WeekOf(_home, "stub", "account-1", _now, fixedWeek: true));

        foreach (var id in working) Assert.True(processes.Stop(id));
        await driver.Running.SettledAsync().WaitAsync(TimeSpan.FromSeconds(90));

        // In its next week's last day, with nothing running, the week about to lapse goes first.
        _now = Until.AddDays(6).AddHours(12);
        var snapshot = await service.SnapshotAsync();
        roster.Look(snapshot.Started, roster.Mark());
        var next = await roster.SelectAsync("acp-stub", config, "default", null);
        Assert.Equal("account-1", next.Profile);
        Assert.Equal(
            new AccountChoice(WalkStep.Lapsing, $"its week resets first, at Oct 10, 16:02 ({Zone.Id})"),
            next.Choice);
    }

    /// <summary>
    /// The protocol door's stand-in (TOOL4j): it speaks the wire and nothing else on stdout. On stub account 1 a prompt is
    /// refused with observation 4's weekly sentence, its zone the test's; on any other account the turn is held until the
    /// process is stopped. Each prompt writes down which stub account it ran as.
    /// </summary>
    private string AcpAgent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, $$"""
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            const [log] = process.argv.slice(2);
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
                appendFileSync(log, JSON.stringify({ account, cwd: process.cwd() }) + '\n');
                if (account === 'account-1') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: "Internal error: You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your weekly limit resets Oct 3, 4pm ({{Zone.Id}})" } });
                }
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
        Git(root, "config", "user.email", "tool6b@example.com");
        Git(root, "config", "user.name", "TOOL6b");
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
