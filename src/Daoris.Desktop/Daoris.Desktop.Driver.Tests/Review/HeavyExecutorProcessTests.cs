using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1d's executor (the pipeline design §4.2), on scratch repositories with stand-in gates: two verifications never overlap
/// and wait in the order sent; a repository whose slot a heavy run holds keeps another's run from nothing; a gate's failure, a runner's
/// answer by file, busy, a changed declaration, the bound, the person's stop and a restart each come to their result in the attempt's
/// folder; the job's first process runs below normal and a <c>clock</c> gate at normal.
/// </summary>
/// <remarks>Real git, node and job processes: the Process half's.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HeavyExecutorProcessTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "heavy-executor", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<HeavyExecutor> _executors = [];
    private string Home => Path.Combine(_folder, "home");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Home);
        // Marks its start and end in one log and starts a detached child beating, the shape of a build's lingering workers; it ends when its
        // go file is there, or exits with the code its tag names after `exit-`.
        await File.WriteAllTextAsync(At("gate.mjs"), """
            import { appendFileSync, existsSync, writeFileSync } from 'node:fs';
            import { spawn } from 'node:child_process';
            import { join } from 'node:path';
            import { getPriority } from 'node:os';
            const [tag, folder, mode] = process.argv.slice(2);
            writeFileSync(join(folder, `priority-${tag}.txt`), String(getPriority()));
            const child = spawn(process.execPath, ['-e', `setInterval(() => require('fs').writeFileSync(${JSON.stringify(join(folder, `beat-${tag}.txt`))}, String(Date.now())), 20); setTimeout(() => process.exit(0), 120000)`], { detached: true, stdio: 'ignore' });
            child.unref();
            writeFileSync(join(folder, `child-${tag}.txt`), String(child.pid));
            appendFileSync(join(folder, 'intervals.log'), `start ${tag} ${Date.now()}\n`);
            if (mode?.startsWith('exit-')) { appendFileSync(join(folder, 'intervals.log'), `end ${tag} ${Date.now()}\n`); process.exit(Number(mode.slice(5))); }
            const go = join(folder, `go-${tag}`);
            const end = setInterval(() => {
              if (mode !== 'hold' && !existsSync(go)) return;
              if (mode === 'hold') return;
              clearInterval(end);
              appendFileSync(join(folder, 'intervals.log'), `end ${tag} ${Date.now()}\n`);
              process.exit(0);
            }, 20);
            setTimeout(() => process.exit(9), 120000);
            """);
        // A runner that answers by file: what it read of its inputs, then the answer its mode names.
        await File.WriteAllTextAsync(At("runner.mjs"), """
            import { writeFileSync, appendFileSync } from 'node:fs';
            import { join } from 'node:path';
            const [mode, folder] = process.argv.slice(2);
            const commit = process.env.DAORIS_VERIFY_COMMIT, result = process.env.DAORIS_VERIFY_RESULT;
            appendFileSync(join(folder, 'runner-calls.log'), `${mode} ${commit ?? '-'} ${result ? 'result' : '-'} ${process.env.DAORIS_HEAVY_RUN ? 'marked' : '-'}\n`);
            if (mode === 'busy') { if (result) writeFileSync(result, JSON.stringify({ schema: 1, commit, busy: true })); process.exit(75); }
            if (mode === 'opaque-fail') process.exit(1);
            if (mode === 'opaque-pass') process.exit(0);
            const gates = mode === 'fail'
              ? [{ name: 'unit', verdict: 'pass', exit: 0 }, { name: 'suite', verdict: 'fail', exit: 4, log: join(folder, 'suite.log') }]
              : [{ name: 'unit', verdict: 'pass', exit: 0, wallSeconds: 1 }, { name: 'suite', verdict: 'flake', exit: 0 }];
            writeFileSync(join(folder, 'suite.log'), 'suite said this\n');
            writeFileSync(result, JSON.stringify({ schema: 1, commit, gates }));
            """);
    }

    public async Task DisposeAsync()
    {
        foreach (var executor in _executors) await executor.DisposeAsync();
        GitSeed.Discard(_folder);
    }

    private string At(string name) => Path.Combine(_folder, name);

    private static string Node(string script, params string[] words) => $"node \"{script}\" {string.Join(' ', words.Select(word => $"\"{word}\""))}";

    /// <summary>A repository with its declaration committed on main: its checkout, its tip and the declaration's blob.</summary>
    private async Task<(string Checkout, string Commit, string Blob)> RepositoryAsync(string name, object declaration)
    {
        var checkout = At(name);
        Directory.CreateDirectory(checkout);
        await GitFixture.GitAsync(checkout, "init", "-q", "-b", "main");
        await GitFixture.GitAsync(checkout, "config", "user.email", "test@example.com");
        await GitFixture.GitAsync(checkout, "config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(checkout, "daoris.gates.json"), JsonSerializer.Serialize(declaration));
        await GitFixture.GitAsync(checkout, "add", "-A");
        await GitFixture.GitAsync(checkout, "commit", "-q", "-m", "gates");
        var commit = (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD")).Trim();
        var blob = (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD:daoris.gates.json")).Trim();
        return (checkout, commit, blob);
    }

    private static object Gates(params (string Name, string Run, bool Heavy, bool Clock)[] gates) => new
    {
        gates = gates.Select(gate => new Dictionary<string, object> { ["name"] = gate.Name, ["run"] = gate.Run, ["heavy"] = gate.Heavy, ["clock"] = gate.Clock }).ToList(),
    };

    private HeavyExecutor Executor(VerifyWorld? world = null)
    {
        var executor = new HeavyExecutor(Home, (world ?? new VerifyWorld()) with { Poll = TimeSpan.FromMilliseconds(50) });
        _executors.Add(executor);
        return executor;
    }

    private static VerifySend Send(string run, string invocation, (string Checkout, string Commit, string Blob) repository, string name = "engine") =>
        new(run, invocation, name, repository.Checkout, repository.Commit, repository.Blob) { Tree = repository.Checkout + "-verify" };

    private async Task<VerifyResult> SettledAsync(string run, string invocation)
    {
        var path = VerifyFiles.ResultOf(Home, run, invocation);
        await Poll.Until(() => File.Exists(path), () => $"no result at {path}");
        return VerifyResult.Read(path) ?? throw new InvalidOperationException($"the result at {path} does not read");
    }

    private IReadOnlyList<(string What, string Tag)> Intervals() =>
        [.. File.ReadAllLines(At("intervals.log")).Select(line => line.Split(' ')).Select(parts => (parts[0], parts[1]))];

    [Fact]
    public async Task Two_verifications_never_overlap_and_wait_in_the_order_they_were_sent()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false)));
        var game = await RepositoryAsync("game", Gates(("unit", Node(At("gate.mjs"), "b", _folder, "exit-0"), true, false)));
        var executor = Executor();

        // The home's verification slot held from outside, so both wait, then run one after another in the order they came.
        using (var held = VerifySlot.TryTake(Home))
        {
            Assert.NotNull(held);
            Assert.True(await executor.SendAsync(Send("v-0000000000000001", "i-a", engine)));
            Assert.True(await executor.SendAsync(Send("v-0000000000000002", "i-b", game, "game")));
            // Each says what it waits for once the executor has looked; read then, so no rewrite is read half-made.
            await Poll.Until(() => VerifySlot.Look(Home).Waiters is { Count: 2 } waiters && waiters.All(waiter => waiter.For is not null));
            Assert.Equal(["i-a", "i-b"], VerifySlot.Look(Home).Waiters.Select(waiter => waiter.Invocation));
            Assert.All(VerifySlot.Look(Home).Waiters, waiter => Assert.Equal("another verification", waiter.For));
        }

        Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync("v-0000000000000001", "i-a")).Verdict);
        Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync("v-0000000000000002", "i-b")).Verdict);
        Assert.Equal([("start", "a"), ("end", "a"), ("start", "b"), ("end", "b")], Intervals());
        Assert.Empty(VerifySlot.Look(Home).Waiters);
    }

    [Fact]
    public async Task A_heavy_run_holding_one_repository_keeps_another_repository_s_run_from_nothing()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false)));
        var game = await RepositoryAsync("game", Gates(("unit", Node(At("gate.mjs"), "b", _folder, "exit-0"), true, false)));
        var executor = Executor();

        var held = HeavySlot.TryTake(Home, HeavySlot.KeyOf(engine.Checkout)!);
        Assert.NotNull(held);
        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine));
        await executor.SendAsync(Send("v-0000000000000002", "i-b", game, "game"));

        // The engine's run waits behind the heavy run holding its slot, and holds no slot while it waits: the game's runs to its verdict.
        Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync("v-0000000000000002", "i-b")).Verdict);
        Assert.False(File.Exists(VerifyFiles.ResultOf(Home, "v-0000000000000001", "i-a")));
        held.Dispose();
        Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync("v-0000000000000001", "i-a")).Verdict);
    }

    [Fact]
    public async Task A_failing_gate_s_verdict_exit_and_log_are_on_the_result_and_the_gates_after_it_not_run()
    {
        var engine = await RepositoryAsync("engine", Gates(
            ("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false),
            ("suite", Node(At("gate.mjs"), "b", _folder, "exit-3"), true, false),
            ("e2e", Node(At("gate.mjs"), "c", _folder, "exit-0"), true, false),
            ("lint", "exit 0", false, false)));
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine));
        var result = await SettledAsync("v-0000000000000001", "i-a");

        Assert.True(result.Verdict == VerifyVerdicts.Failed, result.Why);
        Assert.Equal("suite", result.Failed);
        Assert.Equal(["unit", "suite", "e2e"], result.Gates.Select(gate => gate.Name));
        Assert.Equal(["pass", "fail", "not-run"], result.Gates.Select(gate => gate.Verdict));
        Assert.Equal(3, result.Gates[1].Exit);
        Assert.True(File.Exists(result.Gates[1].Log), "the failing gate's log is kept");
        Assert.Equal(engine.Blob, result.Declaration);
        // The gates ran in Daoris's verification tree at the commit, never the checkout.
        Assert.Equal(engine.Commit, (await GitFixture.GitAsync(engine.Checkout + "-verify", "rev-parse", "HEAD")).Trim());
    }

    [Fact]
    public async Task A_runner_that_answers_by_file_is_read_gate_by_gate_and_handed_its_inputs_alone()
    {
        var engine = await RepositoryAsync("engine", new
        {
            gates = new[] { new { name = "unit", run = "exit 0", heavy = true }, new { name = "suite", run = "exit 0", heavy = true } },
            verification = new { run = Node(At("runner.mjs"), "fail", _folder), result = true },
        });
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine));
        var result = await SettledAsync("v-0000000000000001", "i-a");

        Assert.Equal(VerifyVerdicts.Failed, result.Verdict);
        Assert.Equal("suite", result.Failed);
        Assert.Equal(4, result.Gates.Single(gate => gate.Name == "suite").Exit);
        Assert.Equal($"fail {engine.Commit} result marked", File.ReadAllText(At("runner-calls.log")).Trim());
        Assert.Null(Environment.GetEnvironmentVariable("DAORIS_VERIFY_COMMIT"));
    }

    [Fact]
    public async Task An_opaque_runner_is_one_check()
    {
        var engine = await RepositoryAsync("engine", new
        {
            gates = new[] { new { name = "unit", run = "exit 0", heavy = true } },
            verification = new { run = Node(At("runner.mjs"), "opaque-fail", _folder) },
        });
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine));
        var result = await SettledAsync("v-0000000000000001", "i-a");

        Assert.Equal(VerifyVerdicts.Failed, result.Verdict);
        Assert.Equal(VerifyJudge.RunnerGate, Assert.Single(result.Gates).Name);
        Assert.Equal($"opaque-fail - - marked", File.ReadAllText(At("runner-calls.log")).Trim());
    }

    [Fact]
    public async Task Busy_is_waited_out_with_the_slots_let_go_and_then_unknown()
    {
        var engine = await RepositoryAsync("engine", new
        {
            gates = new[] { new { name = "unit", run = "exit 0", heavy = true } },
            verification = new { run = Node(At("runner.mjs"), "busy", _folder) },
        });
        var clock = new MovedClock();
        var executor = Executor(new VerifyWorld { Clock = clock });

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine));
        for (var offered = 1; offered <= HeavyExecutor.BusyTries; offered++)
        {
            var calls = offered;
            await Poll.Until(() => File.Exists(At("runner-calls.log")) && File.ReadAllLines(At("runner-calls.log")).Length == calls
                                   && VerifySlot.Look(Home).Waiters.SingleOrDefault()?.For == HeavyExecutor.RunnersOwnRun);
            // Between offers, both slots are free: a session's heavy run of the repository may run.
            using (var slot = HeavySlot.TryTake(Home, HeavySlot.KeyOf(engine.Checkout)!)) Assert.NotNull(slot);
            using (var home = VerifySlot.TryTake(Home)) Assert.NotNull(home);
            Assert.False(File.Exists(VerifyFiles.ResultOf(Home, "v-0000000000000001", "i-a")));
            clock.Advance(HeavyExecutor.BusyAgain);
        }

        var result = await SettledAsync("v-0000000000000001", "i-a");
        Assert.Equal(VerifyVerdicts.Unknown, result.Verdict);
        Assert.Contains("the runner's own run held its lock", result.Why);
        Assert.Equal(HeavyExecutor.BusyTries + 1, File.ReadAllLines(At("runner-calls.log")).Length);
    }

    [Fact]
    public async Task A_changed_declaration_is_held_unknown_and_runs_nothing()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false)));
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine) with { Accepted = new string('1', 40) });
        var result = await SettledAsync("v-0000000000000001", "i-a");

        Assert.Equal(VerifyVerdicts.Unknown, result.Verdict);
        Assert.Equal(VerifyResult.Changed, result.Code);
        Assert.False(File.Exists(At("intervals.log")), "nothing ran");
    }

    [Fact]
    public async Task A_run_past_its_bound_is_ended_unknown_its_job_empty_before_either_slot_goes()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "hold"), true, false)));
        var clock = new MovedClock();
        var executor = Executor(new VerifyWorld { Clock = clock });

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine) with { Bound = TimeSpan.FromMinutes(30) });
        await Poll.Until(() => File.Exists(At("beat-a.txt")) && clock.Waiting);
        Assert.Equal("v-0000000000000001", HeavySlot.Holder(Home, HeavySlot.KeyOf(engine.Checkout)!)!.Run);
        var child = int.Parse(File.ReadAllText(At("child-a.txt")));
        clock.Advance(TimeSpan.FromMinutes(30));

        var result = await SettledAsync("v-0000000000000001", "i-a");
        Assert.Equal(VerifyVerdicts.Unknown, result.Verdict);
        Assert.Equal(VerifyResult.Bound, result.Code);
        Assert.Equal("ended", result.Gates.Single().Verdict);
        Assert.False(Alive(child), "what the gate left was ended before the result");
        using (var slot = HeavySlot.TryTake(Home, HeavySlot.KeyOf(engine.Checkout)!)) Assert.NotNull(slot);
    }

    [Fact]
    public async Task The_person_s_stop_ends_the_job()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "hold"), true, false)));
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine));
        await Poll.Until(() => File.Exists(At("beat-a.txt")));
        var child = int.Parse(File.ReadAllText(At("child-a.txt")));
        executor.Stop("i-a");

        var result = await SettledAsync("v-0000000000000001", "i-a");
        Assert.Equal(VerifyResult.Stopped, result.Code);
        Assert.False(Alive(child));
    }

    [Fact]
    public async Task A_restart_ends_the_job_its_name_finds_and_answers_not_started_then_runs_the_same_attempt_again()
    {
        if (!OperatingSystem.IsWindows()) return; // a job by name is Windows'
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false)));

        // What a driver ended mid-run left: a job of the attempt's name, its gate still in it.
        var executor = Executor();
        using (var left = ProcessJob.Create(HeavyExecutor.JobName("i-a"))!)
        using (HeavyStep.Start(left, Node(At("gate.mjs"), "left", _folder, "hold"), _folder, new Dictionary<string, string>()))
        {
            await Poll.Until(() => File.Exists(At("child-left.txt")));
            var child = int.Parse(File.ReadAllText(At("child-left.txt")));
            var answer = await executor.ReconcileAsync("v-0000000000000001", "i-a");

            Assert.Equal(VerifyAnswers.NotStarted, answer.State);
            Assert.False(Alive(child), "the job its name found was ended first");
        }

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine));
        Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync("v-0000000000000001", "i-a")).Verdict);
        Assert.Equal(VerifyAnswers.Result, (await executor.ReconcileAsync("v-0000000000000001", "i-a")).State);
    }

    [Fact]
    public async Task The_job_s_first_process_runs_below_normal_and_a_clock_gate_at_normal()
    {
        if (!OperatingSystem.IsWindows()) return; // the priority class is Windows'
        var engine = await RepositoryAsync("engine", Gates(
            ("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false),
            ("clocked", Node(At("gate.mjs"), "b", _folder, "exit-0"), true, true)));
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine));
        Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync("v-0000000000000001", "i-a")).Verdict);

        // node's os.getPriority(): 10 below normal, 0 normal.
        Assert.Equal("10", File.ReadAllText(At("priority-a.txt")));
        Assert.Equal("0", File.ReadAllText(At("priority-b.txt")));
    }

    private static bool Alive(int pid)
    {
        for (var look = 0; look < 100; look++)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(pid);
                if (process.HasExited) return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            Thread.Sleep(20);
        }

        return true;
    }
}
