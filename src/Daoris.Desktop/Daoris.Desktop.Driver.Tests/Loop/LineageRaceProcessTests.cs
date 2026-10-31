using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORKVIEW1a rule 5's races through the driver's own run (the design §3.6; rule 5's <i>Process</i> cases): starts of one record,
/// each held by <c>Driver.HoldAsync</c> over a real stand-in agent, interleaved by the test holding the service's doors. The lineage
/// is the run's own brackets and what each agent's hook writes; each step's fold is read against the test's own record of which agents
/// live, kept from their processes, never from the code under test.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class LineageRaceProcessTests : IDisposable
{
    /// <summary>Which start a request is asked for, so the doors hold one start's and answer the other's.</summary>
    private static readonly AsyncLocal<string?> Whose = new();

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-lineage-race-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _root;
    private readonly string _signals;
    private readonly string _script;
    private readonly StandInLedger _ledger = new() { FinishedStays = true };
    private readonly LineageTokens _window = new(Environment.ProcessId, DateTime.UtcNow);
    private readonly Dictionary<string, TaskCompletionSource> _gates = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reached = new(StringComparer.Ordinal);
    private readonly List<Process> _started = [];

    public LineageRaceProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        _signals = Path.Combine(_home, "signals");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_signals);
        _script = Path.Combine(_home, "standin.mjs");
        File.WriteAllText(_script, Script);
        _ledger.Register("engine", _root);
        _ledger.Publish("q1", "engine");
        _ledger.Driven("s1", "q1", _root, state: "starting");
        _ledger.Before = HeldAsync;
    }

    public void Dispose()
    {
        lock (_gates)
        {
            foreach (var gate in _gates.Values) gate.TrySetResult();
        }

        foreach (var process in _started)
        {
            SessionProcesses.EndIfRunning(process);
            process.Dispose();
        }

        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Two starts of one record in this window, the second's advance refused and abandoned while a stray of its agent holds its capture
    /// open: the first's child reads working before, during and after the abandonment, and each child stays in its own generation.
    /// </summary>
    [Fact]
    public async Task Two_starts_raced_under_a_held_capture_each_keep_their_child_and_the_firsts_works_throughout()
    {
        var processes = new SessionProcesses();
        var driver = Driver(processes);
        var first = Run(driver, "a", "x1", _window);
        var a = await AgentAsync("a");
        await Poll.Until(() => State() == "working", Seen, Poll.Patience);
        Holds("the first's child, its record moved", ("x1", a, ChildBindings.Working));

        Hold("b", "move");
        var second = Run(driver, "b", "y1", _window, holds: true);
        var b = await AgentAsync("b", holds: true);
        await Poll.Until(() => Reached("b", "move"), Seen, Poll.Patience);
        Holds("the second's child, its advance not answered", ("x1", a, ChildBindings.Working), ("y1", b, ChildBindings.Working));

        Release("b", "move");
        await Poll.Until(() => Ended(b.Proc), Seen, Poll.Patience);
        await b.Process.WaitForExitAsync().WaitAsync(Poll.Patience);
        await Poll.Until(() => _window.Reading(b.Proc) == TokenReading.Gone, Seen, Poll.Patience);
        Assert.False(second.IsCompleted, "the second's run did not wait on its held capture");
        Holds("the second refused, its capture held", ("x1", a, ChildBindings.Working), ("y1", b, ChildBindings.Lost));
        var lost = Child("y1").LastSeen;

        // A line its stray writes once its process's end is written: placed in its own closed generation, by its token.
        Signal("b", "late");
        await Poll.Until(() => Signalled("b", "lated"), Seen, Poll.Patience);
        Assert.False(second.IsCompleted, "the second's run did not wait on its held capture");
        Holds("a late line of the second's", ("x1", a, ChildBindings.Working), ("y1", b, ChildBindings.Lost));
        Assert.Equal(lost, Child("y1").LastSeen);

        var refused = await Assert.ThrowsAsync<DriverException>(() => second.WaitAsync(Poll.Patience));
        Assert.Contains("cannot move working → working", refused.Message, StringComparison.Ordinal);
        Assert.True(processes.RunsHere("s1"), "the second's end took the first's entry");
        Assert.Equal(TokenReading.Watched, _window.Reading(a.Proc));
        Holds("the second's run let go", ("x1", a, ChildBindings.Working), ("y1", b, ChildBindings.Lost));

        Signal("a", "end");
        Assert.Equal("completed", await first.WaitAsync(Poll.Patience));
        await Poll.Until(() => _window.Reading(a.Proc) == TokenReading.Gone, Seen, Poll.Patience);
        Holds("the first ended clean", ("x1", a, ChildBindings.Ended), ("y1", b, ChildBindings.Lost));
        Assert.False(processes.RunsHere("s1"));
    }

    /// <summary>
    /// The design's Codex ordering (C7-2.1): the second start held at its record read while the first's process exits, its
    /// continuation starts and ends, and the record concludes; then the second's advance refused by the finished record. Its child reads
    /// working at every step before its own exit, none of those moving it, and lost at the end its refusal writes.
    /// </summary>
    [Fact]
    public async Task A_second_start_held_before_its_advance_keeps_its_child_working_through_the_firsts_exit_continuation_and_conclusion()
    {
        var processes = new SessionProcesses();
        var driver = Driver(processes);
        var continued = new TaskCompletionSource<(Process Process, string Proc)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Run(driver, "a", "x1", _window, concluding: async () =>
        {
            // Its continuation: a run of the same record, moved already, under a token of its own.
            var continuation = Run(driver, "c", "z1", _window, movedFirst: true);
            continued.SetResult(await AgentAsync("c"));
            return await continuation;
        });
        var a = await AgentAsync("a");
        await Poll.Until(() => State() == "working", Seen, Poll.Patience);

        Hold("b", "read");
        var second = Run(driver, "b", "y1", _window);
        var b = await AgentAsync("b");
        await Poll.Until(() => Reached("b", "read"), Seen, Poll.Patience);
        Holds("both starts' children", ("x1", a, ChildBindings.Working), ("y1", b, ChildBindings.Working));

        Signal("a", "end");
        var c = await continued.Task.WaitAsync(Poll.Patience);
        await Poll.Until(() => _window.Reading(a.Proc) == TokenReading.Gone, Seen, Poll.Patience);
        Holds(
            "the first ended, its continuation working",
            ("x1", a, ChildBindings.Ended), ("y1", b, ChildBindings.Working), ("z1", c, ChildBindings.Working));

        Signal("c", "end");
        Assert.Equal("completed", await first.WaitAsync(Poll.Patience));
        await Poll.Until(() => _window.Reading(c.Proc) == TokenReading.Gone, Seen, Poll.Patience);
        Assert.Equal("completed", State());
        Assert.True(processes.RunsHere("s1"), "the first's and its continuation's ends took the second's entry, tracked from its spawn");
        Holds(
            "the record concluded",
            ("x1", a, ChildBindings.Ended), ("y1", b, ChildBindings.Working), ("z1", c, ChildBindings.Ended));

        Release("b", "read");
        var refused = await Assert.ThrowsAsync<DriverException>(() => second.WaitAsync(Poll.Patience));
        Assert.Contains("a finished session goes on only with the person's words", refused.Message, StringComparison.Ordinal);
        await b.Process.WaitForExitAsync().WaitAsync(Poll.Patience);
        Assert.True(Ended(b.Proc), "the refused start wrote no end");
        Assert.Equal(TokenReading.Gone, _window.Reading(b.Proc));
        Holds(
            "the second refused by the finished record",
            ("x1", a, ChildBindings.Ended), ("y1", b, ChildBindings.Lost), ("z1", c, ChildBindings.Ended));
    }

    /// <summary>
    /// The design's surviving agent (C8-2.2): a competitor another Daoris process started, held before its advance, its child working;
    /// this window's run starts, works and concludes; then the competitor's issuer exits while its agent runs on, and a call of its
    /// child lands after. That child reads not known at every step, never working and never lost, its call moving its last seen, until
    /// its own end.
    /// </summary>
    [Fact]
    public async Task A_competitor_another_daoris_process_started_reads_not_known_through_its_issuers_exit_its_call_moving_its_last_seen()
    {
        var issuer = Started(new ProcessStartInfo("node", ["-e", "setInterval(() => {}, 1000)"]) { UseShellExecute = false, CreateNoWindow = true });
        var theirs = new LineageTokens(issuer.Id, issuer.StartTime);
        var competing = Driver(new SessionProcesses());
        var ours = Driver(new SessionProcesses());

        Hold("f", "read");
        var competitor = Run(competing, "f", "w1", theirs);
        var f = await AgentAsync("f");
        await Poll.Until(() => Reached("f", "read"), Seen, Poll.Patience);
        Holds("the competitor's child, held before its advance", ("w1", f, ChildBindings.Uncertain));

        var winner = Run(ours, "a", "x1", _window);
        var a = await AgentAsync("a");
        await Poll.Until(() => State() == "working", Seen, Poll.Patience);
        Holds("the winning run's child", ("w1", f, ChildBindings.Uncertain), ("x1", a, ChildBindings.Working));

        Signal("a", "end");
        Assert.Equal("completed", await winner.WaitAsync(Poll.Patience));
        await Poll.Until(() => _window.Reading(a.Proc) == TokenReading.Gone, Seen, Poll.Patience);
        Holds("the record concluded", ("w1", f, ChildBindings.Uncertain), ("x1", a, ChildBindings.Ended));

        SessionProcesses.EndIfRunning(issuer);
        await issuer.WaitForExitAsync().WaitAsync(Poll.Patience);
        Holds("its issuer gone, its agent running on", ("w1", f, ChildBindings.Uncertain), ("x1", a, ChildBindings.Ended));
        var before = Child("w1").LastSeen;

        Signal("f", "call");
        await Poll.Until(() => Signalled("f", "called"), Seen, Poll.Patience);
        Holds("a later call of its child", ("w1", f, ChildBindings.Uncertain), ("x1", a, ChildBindings.Ended));
        Assert.True(Child("w1").LastSeen > before, $"its call moved no last seen: {before} then {Child("w1").LastSeen}");

        Release("f", "read");
        await Assert.ThrowsAsync<DriverException>(() => competitor.WaitAsync(Poll.Patience));
        await f.Process.WaitForExitAsync().WaitAsync(Poll.Patience);
        Assert.True(Ended(f.Proc), "the refused competitor wrote no end");
        Holds("the competitor's own end", ("w1", f, ChildBindings.Lost), ("x1", a, ChildBindings.Ended));
    }

    /// <summary>
    /// One fold of the record's lineage as this window reads it, each child's state held to what is written for it, and to the test's own
    /// record of its agent: while the agent lives its child is never closed, and once it has exited no child of it reads working.
    /// </summary>
    private void Holds(string step, params (string Native, (Process Process, string Proc) Agent, string Expected)[] children)
    {
        var bindings = ChildLineage.Reading(_home, "s1", "driven", _window).Bindings;
        foreach (var (native, agent, expected) in children)
        {
            if (bindings.Children.SingleOrDefault(each => each.Native == native) is not { } child)
            {
                Assert.Fail($"{step}: no child {native}\n{Seen()}");
                return;
            }

            var alive = !agent.Process.HasExited;
            Assert.True(child.State == expected, $"{step}: {native} read {child.State}, not {expected}\n{Seen()}");
            Assert.False(
                alive && child.State is ChildBindings.Lost or ChildBindings.Ended or ChildBindings.AgentEnded,
                $"{step}: {native} read {child.State} while its agent lives\n{Seen()}");
            Assert.False(!alive && child.State == ChildBindings.Working, $"{step}: {native} read working once its agent exited\n{Seen()}");
        }
    }

    private ChildBinding Child(string native) =>
        ChildLineage.Reading(_home, "s1", "driven", _window).Bindings.Children.Single(each => each.Native == native);

    private bool Ended(string proc) =>
        File.ReadAllLines(ChildLineage.PathOf(_home, "s1")).Any(line => line.Contains("\"by\":\"end\"", StringComparison.Ordinal) && line.Contains(proc, StringComparison.Ordinal));

    private string State() => _ledger.Session("s1")["state"]!.GetValue<string>();

    private string Seen()
    {
        var path = ChildLineage.PathOf(_home, "s1");
        var lines = File.Exists(path) ? string.Join("\n", File.ReadAllLines(path)) : "(no lineage)";
        return $"record: {State()}\nlineage:\n{lines}";
    }

    private Daoris.Driver.Driver Driver(SessionProcesses processes)
    {
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with { Drivable = ["engine"], Adapter = "stub", PollSeconds = 1, TestBound = 3 };
        return new Daoris.Driver.Driver(
            _ledger.Client(), config, adapters, _home, processes: processes, output: new SessionOutput(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// One start of the record through the driver's own run, its doors asked as <paramref name="name"/>'s and its tokens issued from
    /// <paramref name="tokens"/>: the window's, or another Daoris process's. It concludes as its agent's exit says, or as
    /// <paramref name="concluding"/> does.
    /// </summary>
    private Task<string> Run(
        Daoris.Driver.Driver driver, string name, string child, LineageTokens tokens, bool holds = false, Func<Task<string>>? concluding = null,
        bool movedFirst = false) =>
        Task.Run(async () =>
        {
            Whose.Value = name;
            using var within = LineageTokens.Within(tokens);
            var adapter = AdapterSet.Built().Resolve("stub");
            var target = new SessionTarget("q1", "The race", "", "game", "engine", _root, StandInLedger.Url) { Home = _home };
            var info = adapter.Prepare(target, ["node", _script, "agent", ChildLineage.PathOf(_home, "s1"), _signals, name, child, holds ? "holds" : "alone"]);
            var moved = movedFirst ? ChildLineage.Opened(_home, "s1") : null;
            return await driver.HoldAsync(
                "build", adapter, info, target, "s1", Path.Combine(_home, $"{name}.log"), _root, harnessNotice: null, rules: (null, null, null),
                handed: null, refusesInput: null,
                conclude: async (exitCode, _, _, _) =>
                {
                    if (concluding is not null) return await concluding();
                    var state = exitCode == 0 ? "completed" : "failed";
                    await _ledger.Client().AdvanceAsync("s1", state);
                    return state;
                },
                ct: CancellationToken.None, movedFirst: movedFirst, moved: moved);
        });

    /// <summary>A start's agent once it has written its child's line: its process, opened by the test, and the token it was handed.</summary>
    private async Task<(Process Process, string Proc)> AgentAsync(string name, bool holds = false)
    {
        var pid = Path.Combine(_signals, $"{name}.pid");
        await Poll.Until(() => File.Exists(pid) && (!holds || File.Exists(Path.Combine(_signals, $"{name}.stray"))), Seen, Poll.Patience);
        var fields = File.ReadAllText(pid).Split(' ');
        var process = Process.GetProcessById(int.Parse(fields[0], System.Globalization.CultureInfo.InvariantCulture));
        lock (_started) _started.Add(process);
        if (holds)
        {
            var stray = Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(_signals, $"{name}.stray")), System.Globalization.CultureInfo.InvariantCulture));
            lock (_started) _started.Add(stray);
        }

        Assert.Matches("^[0-9a-f]{16}$", fields[1]);
        return (process, fields[1]);
    }

    private Process Started(ProcessStartInfo info)
    {
        var process = Process.Start(info)!;
        lock (_started) _started.Add(process);
        return process;
    }

    private void Signal(string name, string what) => File.WriteAllText(Path.Combine(_signals, $"{name}.{what}"), "");

    private bool Signalled(string name, string what) => File.Exists(Path.Combine(_signals, $"{name}.{what}"));

    private void Hold(string start, string door)
    {
        lock (_gates) _gates[$"{start} {door}"] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void Release(string start, string door)
    {
        lock (_gates) _gates[$"{start} {door}"].TrySetResult();
    }

    private bool Reached(string start, string door)
    {
        lock (_gates) return _reached.Contains($"{start} {door}");
    }

    /// <summary>The doors a test holds for one start: its record's read, the run's first ask after its spawn, and its move.</summary>
    private Task HeldAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (Whose.Value is not { } start) return Task.CompletedTask;
        var path = request.RequestUri!.AbsolutePath;
        var door = request.Method == HttpMethod.Get && path == "/api/sessions" ? "read"
            : request.Method == HttpMethod.Post && path == "/api/sessions/s1/state" ? "move"
            : null;
        if (door is null) return Task.CompletedTask;
        var key = $"{start} {door}";
        TaskCompletionSource? gate;
        lock (_gates)
        {
            if (!_gates.TryGetValue(key, out gate) || gate.Task.IsCompleted) return Task.CompletedTask;
            _reached.Add(key);
        }

        return gate.Task.WaitAsync(ct);
    }

    /// <summary>
    /// The stand-in agent: as Claude Code's hook would, it writes its child's start under the token its environment carries, then its
    /// pid and token; a call of its child once told, and its exit once told. Holding, it leaves a stray outside its own tree that keeps
    /// its output open, and writes one more line of its child, under the same token, once told.
    /// </summary>
    private const string Script = """
        import { appendFileSync, existsSync, renameSync, writeFileSync } from 'node:fs';
        import { spawn } from 'node:child_process';
        import { fileURLToPath } from 'node:url';
        import path from 'node:path';

        const [role, lineage, dir, name, child, holds] = process.argv.slice(2);
        const proc = process.env.DAORIS_LINEAGE_PROC ?? '';
        const pause = new Int32Array(new SharedArrayBuffer(4));
        const append = (said) => {
          const text = JSON.stringify({ ...said, session: 'root-1', at: new Date().toISOString(), proc }) + '\n';
          for (let attempt = 0; attempt < 400; attempt++) {
            try { appendFileSync(lineage, text); return; } catch { Atomics.wait(pause, 0, 0, 25); }
          }
        };
        const signalled = (what) => existsSync(path.join(dir, `${name}.${what}`));
        const mark = (what, text) => {
          const file = path.join(dir, `${name}.${what}`);
          writeFileSync(`${file}.part`, text);
          renameSync(`${file}.part`, file);
        };

        if (role === 'stray') {
          // Ready once the parent that started it is gone, so no kill of the agent's tree reaches it.
          const parentGone = () => { try { process.kill(process.ppid, 0); return false; } catch { return true; } };
          const ready = setInterval(() => {
            if (parentGone()) { clearInterval(ready); mark('stray', String(process.pid)); }
          }, 10);
          setInterval(() => {
            if (signalled('late') && !signalled('lated')) {
              append({ by: 'hook', event: 'call', agent: child, toolUse: `late-${name}` });
              mark('lated', '');
            }
          }, 20);
          setTimeout(() => process.exit(0), 90000);
        } else {
          if (holds === 'holds') {
            // Started by a parent that exits at once, so the stray is outside this agent's tree and its kill; detached, so node's own
            // job does not end it with that parent.
            const stray = JSON.stringify([fileURLToPath(import.meta.url), 'stray', lineage, dir, name, child]);
            spawn(process.execPath, ['-e',
              `require('node:child_process').spawn(process.execPath, ${stray}, { stdio: ['ignore', 'inherit', 'inherit'], windowsHide: true, detached: true }).unref()`],
              { stdio: ['ignore', 'inherit', 'inherit'], windowsHide: true });
          }

          append({ by: 'hook', event: 'started', agent: child });
          mark('pid', `${process.pid} ${proc}`);
          const tick = setInterval(() => {
            if (signalled('call') && !signalled('called')) {
              append({ by: 'hook', event: 'call', agent: child, toolUse: `call-${name}` });
              mark('called', '');
            }
            if (signalled('end')) {
              clearInterval(tick);
              process.exit(0);
            }
          }, 20);
        }
        """;
}
