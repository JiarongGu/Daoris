using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDBACK1 part 3 (a), through REAL ticks on the native door. Its harness ends the session's own work with its turn, so a native session
/// that closes its quest done and leaves its fix uncommitted is carried on in Daoris's own words, as its conversation resumed, and reads
/// completed once the tree is clean, or when the bound (<see cref="TurnCarry.Bound"/>) is met. The words are the run's own prompt: no card,
/// no say, no move record, and Daoris commits nothing (any commit in the tree is the stand-in's).
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class NativeCarryTickTests : IDisposable
{
    private const string Words = "Your quest is done, but your tree has 1 uncommitted path(s).";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-native-carry-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public NativeCarryTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git(_repository, "init", "-q", "-b", "main");
        Git(_repository, "config", "user.email", "native-carry@example.com");
        Git(_repository, "config", "user.name", "LANDBACK1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git(_repository, "add", "-A");
        Git(_repository, "commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    private JsonObject[] Heard() => File.Exists(Log) ? [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())] : [];

    private string Transcript => File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.log"));

    /// <summary>How many times the record says the bound was met (the note is kept in the record's events, never the transcript).</summary>
    private int BoundNotes() => new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After("s1", 0).Events
        .Count(e => e.Parts?.Any(part => part.Code == NoteCodes.BackgroundCarryBound.Code) == true);

    /// <summary>A carry-on that commits: the session reads completed with a clean tree after one resumed run, the commit its own.</summary>
    [Fact]
    public async Task A_native_session_that_ended_done_with_its_fix_uncommitted_is_carried_on_and_commits()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;

        await Tick(service, "commits").WaitAsync(Poll.Patience);

        var record = service.Session("s1");
        var seen = $"record: {record}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        Assert.True(record["state"]!.GetValue<string>() == "completed" && service.Status("q1") == "Done", seen);
        var heard = Heard();
        Assert.True(heard.Length == 2, seen);
        Assert.Null(heard[0]["resume"]);
        Assert.Equal("native-1", heard[1]["resume"]!.GetValue<string>());
        Assert.StartsWith(Words, heard[1]["prompt"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("Daoris never runs `git add` or `git commit` for you", heard[1]["prompt"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("the session was carried on to commit it by its repository's conventions", Transcript, StringComparison.Ordinal);
        Assert.DoesNotContain("Confirm", Transcript, StringComparison.Ordinal);
        Assert.Null(record["awaiting"]);

        var tree = record["tree"]!.GetValue<string>();
        Assert.Equal("", Git(tree, "status", "--porcelain").Trim());
        Assert.Equal(["stand-in agent", "LANDBACK1"], Git(tree, "log", "--format=%an").Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>A session that never commits is carried on twice, the bound is said once, and the record reads completed with its tree dirty.</summary>
    [Fact]
    public async Task A_native_session_that_never_commits_is_carried_on_twice_then_ends_completed_with_its_tree_uncommitted()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;

        await Tick(service, "never").WaitAsync(Poll.Patience);

        var record = service.Session("s1");
        var seen = $"record: {record}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        Assert.True(record["state"]!.GetValue<string>() == "completed" && service.Status("q1") == "Done", seen);
        var heard = Heard();
        Assert.True(heard.Length == 3, seen);
        Assert.All(heard.Skip(1), run => Assert.StartsWith(Words, run["prompt"]!.GetValue<string>(), StringComparison.Ordinal));
        Assert.Equal(2, Occurrences(Transcript, "the session was carried on to commit it"));
        Assert.Equal(1, BoundNotes());
        Assert.Equal("?? fix.txt", Git(record["tree"]!.GetValue<string>(), "status", "--porcelain").Trim());
    }

    /// <summary>A session that committed before its run ended owes nothing: one run, no prompt of Daoris's.</summary>
    [Fact]
    public async Task A_native_session_that_committed_before_its_turn_ended_is_not_carried_on()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;

        await Tick(service, "commits-first").WaitAsync(Poll.Patience);

        var record = service.Session("s1");
        Assert.Single(Heard());
        Assert.Equal("completed", record["state"]!.GetValue<string>());
        Assert.DoesNotContain("carried on", Transcript, StringComparison.Ordinal);
        Assert.Equal("", Git(record["tree"]!.GetValue<string>(), "status", "--porcelain").Trim());
    }

    /// <summary>A carry-on run that exits non-zero is gone from, never carried on again: the session ends as that exit reads.</summary>
    [Fact]
    public async Task A_carry_on_that_exits_non_zero_is_not_carried_on_again()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;

        await Tick(service, "exits").WaitAsync(Poll.Patience);

        var seen = $"record: {service.Session("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        Assert.True(Heard().Length == 2, seen);
        Assert.Equal(1, Occurrences(Transcript, "the session was carried on to commit it"));
        Assert.Equal(0, BoundNotes());
    }

    /// <summary>A carry-on whose turn the harness said failed is not carried on again either (TURNFAIL2's reading, NativeWords.Left).</summary>
    [Fact]
    public async Task A_carry_on_whose_turn_failed_is_not_carried_on_again()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;

        await Tick(service, "fails").WaitAsync(Poll.Patience);

        var seen = $"record: {service.Session("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        Assert.True(Heard().Length == 2, seen);
        Assert.Equal(1, Occurrences(Transcript, "the session was carried on to commit it"));
    }

    /// <summary>The pipe stub does not resume, so it is never carried on: a stub leaving a note uncommitted ends as it always did.</summary>
    [Fact]
    public async Task A_stub_that_cannot_resume_is_not_carried_on()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = "stub",
            TestBound = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Stub(), Log] },
        };

        await Tick(service, config).WaitAsync(Poll.Patience);

        var record = service.Session("s1");
        Assert.Single(Heard());
        Assert.Equal("completed", record["state"]!.GetValue<string>());
        Assert.DoesNotContain("carried on", Transcript, StringComparison.Ordinal);
        Assert.Equal("?? fix.txt", Git(record["tree"]!.GetValue<string>(), "status", "--porcelain").Trim());
    }

    private Task<TickReport> Tick(AskAndWaitTickTests.StandIn service, string mode)
    {
        // The account the native door runs under, as AccessSwapTickTests names it.
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.AgentHomes, "claude-code", "work"));
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Harnesses), """{"defaults":{"claude-code":"work"}}""");
        return Tick(service, NativeConfig(mode));
    }

    private DriverConfig NativeConfig(string mode) => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Adapter = "claude-code",
        TestBound = 1,
        Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = ["node", NativeHarness(), Log, mode] },
    };

    private Task<TickReport> Tick(AskAndWaitTickTests.StandIn service, DriverConfig config)
    {
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses))).RunOnceAsync();
    }

    private static int Occurrences(string text, string needle)
    {
        var count = 0;
        for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    /// <summary>
    /// A stand-in for Claude Code's native door. A first run takes the quest, leaves <c>fix.txt</c> in its tree, closes the quest done
    /// and ends (mode <c>commits-first</c> commits before it ends). A resumed run is a carry-on: it commits, except in mode <c>never</c>;
    /// mode <c>exits</c> exits 1 and mode <c>fails</c> ends its turn on an error result.
    /// </summary>
    private string NativeHarness()
    {
        var script = Path.Combine(_home, "claude.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, writeFileSync } from 'node:fs';
            import { execFileSync } from 'node:child_process';
            const [log, mode, ...argv] = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.0 (Claude Code)'); process.exit(0); }
            if (argv[0] === 'auth' && argv[1] === 'status') { console.log('{"loggedIn": true}'); process.exit(0); }
            if (!argv.includes('-p')) process.exit(0);
            const after = (flag) => { const at = argv.indexOf(flag); return at < 0 ? null : argv[at + 1]; };
            const chunks = [];
            for await (const chunk of process.stdin) chunks.push(chunk);
            const prompt = Buffer.concat(chunks).toString('utf8');
            const resume = after('--resume');
            const url = process.env.DAORIS_SERVICE_URL;
            const line = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const commit = () => {
              const env = { ...process.env, GIT_AUTHOR_NAME: 'stand-in agent', GIT_AUTHOR_EMAIL: 'agent@example.com' };
              execFileSync('git', ['add', '-A'], { cwd: process.cwd(), env });
              execFileSync('git', ['commit', '-qm', 'the fix'], { cwd: process.cwd(), env });
            };
            appendFileSync(log, JSON.stringify({ resume, prompt: resume ? prompt : null }) + '\n');
            line({ type: 'system', subtype: 'init', session_id: resume ?? 'native-1', cwd: process.cwd(), tools: [], model: 'stub' });
            if (!resume) {
              await respond({ action: 'take' });
              writeFileSync('fix.txt', 'the fix\n');
              if (mode === 'commits-first') commit();
              await respond({ action: 'done', reason: 'fixed' });
              line({ type: 'assistant', message: { role: 'assistant', content: [{ type: 'text', text: 'The fix is in.' }] } });
              line({ type: 'result', subtype: 'success', is_error: false, result: 'ok' });
            } else if (mode === 'exits') {
              process.exit(1);
            } else if (mode === 'fails') {
              line({ type: 'result', subtype: 'error_during_execution', is_error: true, result: 'the turn broke' });
            } else {
              if (mode !== 'never') commit();
              line({ type: 'assistant', message: { role: 'assistant', content: [{ type: 'text', text: 'Committed.' }] } });
              line({ type: 'result', subtype: 'success', is_error: false, result: 'ok' });
            }
            """);
        return script;
    }

    /// <summary>The pipe stub's stand-in: takes the quest, leaves <c>fix.txt</c>, closes the quest done and exits.</summary>
    private string Stub()
    {
        var script = Path.Combine(_home, "stub.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, writeFileSync } from 'node:fs';
            const [log, ...argv] = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            appendFileSync(log, JSON.stringify({ resume: null }) + '\n');
            await respond({ action: 'take' });
            writeFileSync('fix.txt', 'the fix\n');
            await respond({ action: 'done', reason: 'fixed' });
            console.log('done');
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
