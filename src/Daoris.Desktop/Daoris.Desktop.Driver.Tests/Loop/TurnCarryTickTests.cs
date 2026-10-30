using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDBACK1 part 2 (TURNWAIT1), through a REAL tick on the protocol door. On the install a driven session left its fix uncommitted, closed
/// its quest done and ended its turn to wait on its own background check, and was concluded with the check killed. Here the stand-in agent
/// does the same: the run waits for its check, then carries the session on in Daoris's own words, and reads done only once the tree is
/// clean, or the bound (<see cref="LandCarry.Bound"/>) is met. Daoris commits nothing: any commit in the tree is the stand-in's.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class TurnCarryTickTests : IDisposable
{
    private const string Words = "Your quest is done, but your tree has 1 uncommitted path(s).";

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-turn-carry-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public TurnCarryTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git(_repository, "init", "-q", "-b", "main");
        Git(_repository, "config", "user.email", "turn-carry@example.com");
        Git(_repository, "config", "user.name", "LANDBACK1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git(_repository, "add", "-A");
        Git(_repository, "commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>TURNWAIT1's case, whole: the check runs on, the quest is done, the fix is uncommitted; the commit comes on the carry-on.</summary>
    [Fact]
    public async Task A_done_quest_with_its_check_running_and_its_fix_uncommitted_waits_for_the_check_then_ends_only_after_the_commit()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var pidFile = Path.Combine(_home, "check.pid");
        var driver = Driver(service, pidFile, "check");

        var run = driver.RunOnceAsync();
        await Poll.Until(() => File.Exists(pidFile), () => "the check never started", Poll.Patience);
        var check = int.Parse(File.ReadAllText(pidFile).Trim(), System.Globalization.CultureInfo.InvariantCulture);

        // The quest closes done while the check runs; until the check ends, the record is working, never concluded.
        await Poll.Until(() => service.Status("q1") == "Done", () => "the quest never closed", Poll.Patience);
        while (Alive(check))
        {
            Assert.Equal("working", service.Session("s1")["state"]!.GetValue<string>());
            await Task.Delay(100);
        }

        await run.WaitAsync(Poll.Patience);

        var record = service.Session("s1");
        var transcript = File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.log"));
        Assert.Equal("completed", record["state"]!.GetValue<string>());
        Assert.StartsWith("the quest reached done.", record["note"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("The session waits for that work", transcript, StringComparison.Ordinal);
        Assert.Contains("the session was carried on to commit it by its repository's conventions", transcript, StringComparison.Ordinal);
        Assert.True(
            transcript.IndexOf("The session waits for that work", StringComparison.Ordinal)
            < transcript.IndexOf("the session was carried on", StringComparison.Ordinal),
            "the carry-on went before the check ended");
        // The one carry-on prompt the agent was given, after its own first, was Daoris's words and nothing of a card's.
        var told = Prompted(pidFile);
        Assert.Equal(2, told.Count);
        Assert.StartsWith(Words, told[1], StringComparison.Ordinal);
        Assert.Contains("Daoris never runs `git add` or `git commit` for you", told[1], StringComparison.Ordinal);

        // The tree is clean, and the commit is the stand-in's: Daoris committed nothing.
        var tree = record["tree"]!.GetValue<string>();
        Assert.Equal("", Git(tree, "status", "--porcelain").Trim());
        Assert.Equal(["stand-in agent", "LANDBACK1"], Git(tree, "log", "--format=%an").Split('\n', StringSplitOptions.RemoveEmptyEntries));

        // No question of the person's and no card: the carry-on is the run's own prompt.
        Assert.DoesNotContain("Confirm", transcript, StringComparison.Ordinal);
        Assert.Null(record["awaiting"]);
    }

    /// <summary>A session that commits before its turn ends has nothing to be carried on for: the run ends at once, with no prompt.</summary>
    [Fact]
    public async Task A_session_that_committed_before_its_turn_ended_is_not_carried_on()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var pidFile = Path.Combine(_home, "none.pid");
        var driver = Driver(service, pidFile, "commits-first");

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var record = service.Session("s1");
        var transcript = File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.log"));
        Assert.Equal("completed", record["state"]!.GetValue<string>());
        Assert.Single(Prompted(pidFile));
        Assert.DoesNotContain("carried on", transcript, StringComparison.Ordinal);
        Assert.Equal("", Git(record["tree"]!.GetValue<string>(), "status", "--porcelain").Trim());
    }

    /// <summary>A session that never commits is carried on twice, the bound is said, and the record reads done with its tree dirty.</summary>
    [Fact]
    public async Task A_session_that_never_commits_is_carried_on_twice_then_ends_done_with_its_tree_uncommitted()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var pidFile = Path.Combine(_home, "none.pid");
        var driver = Driver(service, pidFile, "never");

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var record = service.Session("s1");
        var transcript = File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.log"));
        Assert.Equal("completed", record["state"]!.GetValue<string>());
        Assert.StartsWith("the quest reached done.", record["note"]!.GetValue<string>(), StringComparison.Ordinal);
        var told = Prompted(pidFile);
        Assert.Equal(3, told.Count);
        Assert.All(told.Skip(1), prompt => Assert.StartsWith(Words, prompt, StringComparison.Ordinal));
        Assert.Equal(2, Occurrences(transcript, "the session was carried on to commit it"));
        Assert.Contains("its session was carried on for the uncommitted work in its tree", transcript, StringComparison.Ordinal);
        Assert.Equal("?? fix.txt", Git(record["tree"]!.GetValue<string>(), "status", "--porcelain").Trim());
    }

    /// <summary>Each prompt the stand-in agent was given, in the order it was given them.</summary>
    private static IReadOnlyList<string> Prompted(string pidFile) =>
        File.ReadAllText($"{pidFile}.prompts").Split("\n---\n", StringSplitOptions.RemoveEmptyEntries);

    private static int Occurrences(string text, string needle)
    {
        var count = 0;
        for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal)) count++;
        return count;
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

    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, string pidFile, string mode)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = "acp-stub",
            TestBound = 3,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), pidFile, mode] },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// The protocol door's stand-in for the install's session. Its first prompt takes the quest, leaves <c>fix.txt</c> in its tree, closes the
    /// quest done and ends its turn: mode <c>check</c> starts a check beside itself first (a child that ends by itself four seconds on,
    /// announced as the adapter does, and its harness wakes the agent with that ending); <c>commits-first</c> commits before it ends its turn.
    /// Every later prompt is a carry-on: it commits (mode <c>never</c> does not), as the person's session would by the repository's conventions.
    /// It never exits on its own, its stdin closed or not.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { spawn, execFileSync } from 'node:child_process';
            import { appendFileSync, writeFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [pidFile, mode] = process.argv.slice(2);
            let cwd = process.cwd();
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const update = (body) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-1', update: body } });
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const commit = () => {
              const env = { ...process.env, GIT_AUTHOR_NAME: 'stand-in agent', GIT_AUTHOR_EMAIL: 'agent@example.com' };
              execFileSync('git', ['add', '-A'], { cwd, env });
              execFileSync('git', ['commit', '-qm', 'the fix'], { cwd, env });
            };
            const woken = (text) => {
              update({ sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } });
              const end = { sessionUpdate: 'usage_update', used: 1000, size: 200000, _meta: { '_claude/origin': { kind: 'task-notification' } } };
              update(end);
              update(end);
            };
            let prompts = 0;
            // It idles until its stdin closes, as an agent does at its session's end.
            setInterval(() => {}, 1000);
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                cwd = frame.params.cwd;
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                prompts += 1;
                appendFileSync(`${pidFile}.prompts`, frame.params.prompt.map((block) => block.text).join('\n') + '\n---\n');
                if (prompts === 1) {
                  await respond({ action: 'take' });
                  writeFileSync(`${cwd}/fix.txt`, 'the fix\n');
                  if (mode === 'commits-first') commit();
                  if (mode === 'check') {
                    const work = spawn(process.execPath, ['-e', 'setTimeout(() => process.exit(0), 4000)'], { stdio: 'ignore' });
                    writeFileSync(pidFile, String(work.pid));
                    update({ sessionUpdate: 'async_task_spawned', asyncTaskId: 't1', name: 'Run the tests', taskType: 'shell', canStop: true });
                    work.on('exit', () => {
                      update({ sessionUpdate: 'async_task_state_update', asyncTaskId: 't1', state: 'stopped' });
                      update({ sessionUpdate: 'async_task_state_update', asyncTaskId: 't1', state: 'completed' });
                      woken('The tests passed; the fix is still uncommitted.');
                    });
                  }
                  update({ sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'The fix is in; closing the quest.' } });
                  await respond({ action: 'done', reason: 'fixed' });
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                } else {
                  if (mode !== 'never') commit();
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                }
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            process.exit(0);
            """);
        return script;
    }

    private static string Git(string folder, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = folder, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
