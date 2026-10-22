using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// DONEEND1, through a REAL tick on the protocol door. On the install a driven session backgrounded a long test run, ended its
/// turn holding its quest, and the quest then closed done; the session went on working, holding a slot of the cap, until the
/// 90-minute timeout killed it, and the kill left the test's scratch in its tree. Here the stand-in agent does the same and then
/// idles, never exiting when its stdin closes, its test run still going: the run must end well inside its timeout, completed,
/// and the test run must end with the agent's tree.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class DoneEndTickTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-done-end-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public DoneEndTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "done-end@example.com");
        Git("config", "user.name", "DONEEND1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_run_whose_quest_closes_while_it_waits_on_its_background_work_ends_completed_and_its_work_with_it()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var pidFile = Path.Combine(_home, "work.pid");
        var driver = Driver(service, pidFile);
        var clock = Stopwatch.StartNew();

        // Its timeout is three minutes: the run ends long before it.
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(90));
        clock.Stop();

        var record = service.Session("s1");
        var note = record["note"]!.GetValue<string>();
        Assert.Equal("Done", service.Status("q1"));
        Assert.Equal("completed", record["state"]!.GetValue<string>());
        Assert.StartsWith("the quest reached done.", note, StringComparison.Ordinal);
        Assert.DoesNotContain("timed out", note, StringComparison.Ordinal);
        // Its agent ended as its session did, cleanly: the tree Daoris ended is no exit of the agent's.
        Assert.DoesNotContain("exit", note, StringComparison.Ordinal);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(75), $"the run took {clock.Elapsed}");

        // It waited on its work, then ended once its quest no longer did.
        var transcript = File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.log"));
        Assert.Contains("The session waits for that work", transcript, StringComparison.Ordinal);
        Assert.Contains("its quest no longer waits on its background work", transcript, StringComparison.Ordinal);

        // Its agent never exited by itself, so its tree was ended, and its test run with it.
        Assert.Contains("Daoris ended its process tree", transcript, StringComparison.Ordinal);
        var work = int.Parse(File.ReadAllText(pidFile).Trim(), System.Globalization.CultureInfo.InvariantCulture);
        await Poll.Until(() => !Alive(work), () => $"the test run {work} still runs", TimeSpan.FromSeconds(20));
    }

    private static bool Alive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, string pidFile)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = "acp-stub",
            TestBound = 3,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), pidFile] },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// The protocol door's stand-in for the install's session: it takes its quest, starts its test run beside itself (a child that
    /// never ends, its pid written down), announces it as the adapter does, says it will pick up when it finishes and ends its turn.
    /// Three seconds later its quest closes done. It answers every request after, and never exits on its own, its stdin closed or not.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { spawn } from 'node:child_process';
            import { writeFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const pidFile = process.argv[2];
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const update = (body) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-1', update: body } });
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            // It idles: nothing ends it but its tree.
            setInterval(() => {}, 1000);
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                await respond({ action: 'take' });
                const work = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { stdio: 'ignore' });
                writeFileSync(pidFile, String(work.pid));
                update({ sessionUpdate: 'async_task_spawned', asyncTaskId: 't1', name: 'Run the tests', taskType: 'shell', canStop: true });
                update({ sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: "The tests run in the background; I'll pick up when they finish." } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                setTimeout(() => { respond({ action: 'done', reason: 'landed' }); }, 3000);
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
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
