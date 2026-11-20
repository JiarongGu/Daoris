using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The verification executor, on scratch repositories with stand-in gates: as many verifications run at once as the machine's check
/// slots admit (memory for two runs two, the third waiting for a slot; memory for one runs one; <c>checks 1</c> pins one), the waiting in
/// the order sent; a repository whose slot a heavy run holds keeps another's run from nothing; a gate's failure, a runner's
/// answer by file, busy, a changed declaration, the bound, the person's stop and a restart each come to their result in the attempt's
/// folder; the job's first process runs below normal and a <c>clock</c> gate at normal.
/// </summary>
/// <remarks>Real git, node and job processes: the Process half's.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
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
    public async Task With_checks_pinned_at_one_two_verifications_never_overlap_and_wait_in_the_order_they_were_sent()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false)));
        var game = await RepositoryAsync("game", Gates(("unit", Node(At("gate.mjs"), "b", _folder, "exit-0"), true, false)));
        var executor = Executor(new VerifyWorld { Checks = () => 1 });

        // The home's one verification slot held from outside, so both wait, then run one after another in the order they came.
        using (var held = VerifySlot.TryTake(Home))
        {
            Assert.NotNull(held);
            Assert.True(await executor.SendAsync(Send("v-0000000000000001", "i-a", engine)));
            Assert.True(await executor.SendAsync(Send("v-0000000000000002", "i-b", game, "game")));
            // Each says what it waits for once the executor has looked; read then, so no rewrite is read half-made.
            await Poll.Until(() => VerifySlot.Look(Home).Waiters is { Count: 2 } waiters && waiters.All(waiter => waiter.For is not null));
            Assert.Equal(["i-a", "i-b"], VerifySlot.Look(Home).Waiters.Select(waiter => waiter.Invocation));
            Assert.All(VerifySlot.Look(Home).Waiters, waiter => Assert.Equal("a slot: 1 of 1 run", waiter.For));
        }

        Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync("v-0000000000000001", "i-a")).Verdict);
        Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync("v-0000000000000002", "i-b")).Verdict);
        Assert.Equal([("start", "a"), ("end", "a"), ("start", "b"), ("end", "b")], Intervals());
        Assert.Empty(VerifySlot.Look(Home).Waiters);
    }

    [Fact]
    public async Task With_memory_for_two_two_repositories_checks_run_at_once_and_a_third_waits_for_a_slot_saying_so()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder), true, false)));
        var game = await RepositoryAsync("game", Gates(("unit", Node(At("gate.mjs"), "b", _folder), true, false)));
        var tool = await RepositoryAsync("tool", Gates(("unit", Node(At("gate.mjs"), "c", _folder), true, false)));
        // Eight processors and two places in the machine's admission: two checks at most, and 20 GB free admits the second.
        var executor = Executor(new VerifyWorld { Processors = 8, Capacity = 2, Memory = () => new MemoryReading(20L << 30, 20L << 30) });

        Assert.True(await executor.SendAsync(Send("v-0000000000000001", "i-a", engine)));
        Assert.True(await executor.SendAsync(Send("v-0000000000000002", "i-b", game, "game")));
        Assert.True(await executor.SendAsync(Send("v-0000000000000003", "i-c", tool, "tool")));

        // a and b both start before either ends; c waits, saying it waits for a slot, until one lets go.
        await Poll.Until(() => File.Exists(At("intervals.log")) && Intervals().Count(each => each.What == "start") == 2, () => "two checks did not start at once");
        await Poll.Until(() => VerifySlot.Look(Home).Waiters is [{ For: not null }]);
        var waiter = Assert.Single(VerifySlot.Look(Home).Waiters);
        Assert.Equal("i-c", waiter.Invocation);
        Assert.Equal("a slot: 2 of 2 run", waiter.For);
        Assert.Equal(2, VerifySlot.Look(Home).Holders.Count);

        await File.WriteAllTextAsync(At("go-a"), "");
        await File.WriteAllTextAsync(At("go-b"), "");
        await File.WriteAllTextAsync(At("go-c"), "");
        foreach (var (run, invocation) in new[] { ("v-0000000000000001", "i-a"), ("v-0000000000000002", "i-b"), ("v-0000000000000003", "i-c") })
        {
            Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync(run, invocation)).Verdict);
        }

        Assert.Equal(["a", "b"], Intervals().Take(2).Select(each => each.Tag).Order());
        Assert.All(Intervals().Take(2), each => Assert.Equal("start", each.What));
        Assert.Empty(VerifySlot.Look(Home).Holders);
    }

    [Fact]
    public async Task With_memory_for_two_two_checks_of_one_repository_run_at_once_each_in_its_own_slot_s_tree()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder), true, false)));
        var executor = Executor(new VerifyWorld { Processors = 8, Capacity = 2, Memory = () => new MemoryReading(20L << 30, 20L << 30) });

        Assert.True(await executor.SendAsync(Send("v-0000000000000001", "i-a", engine) with { Tree = null }));
        Assert.True(await executor.SendAsync(Send("v-0000000000000002", "i-b", engine) with { Tree = null }));

        await Poll.Until(() => File.Exists(At("intervals.log")) && Intervals().Count(each => each.What == "start") == 2, () => "two checks of one repository did not start at once");
        var key = HeavySlot.KeyOf(engine.Checkout)!;
        Assert.Equal(2, HeavySlot.Look(Home, key).Holders.Count);
        Assert.True(Directory.Exists(Path.Combine(VerifyFiles.LinesFolder(Home), "trees", $"{key}-2")));

        await File.WriteAllTextAsync(At("go-a"), "");
        Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync("v-0000000000000001", "i-a")).Verdict);
        Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync("v-0000000000000002", "i-b")).Verdict);
        Assert.Empty(HeavySlot.Look(Home, key).Holders);
    }

    [Fact]
    public async Task With_memory_for_one_a_second_check_of_one_repository_waits_for_memory_saying_so()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder), true, false)));
        var executor = Executor(new VerifyWorld { Processors = 32, Capacity = 4, Memory = () => new MemoryReading(4L << 30, 9L << 30) });

        Assert.True(await executor.SendAsync(Send("v-0000000000000001", "i-a", engine)));
        await Poll.Until(() => File.Exists(At("intervals.log")));
        Assert.True(await executor.SendAsync(Send("v-0000000000000002", "i-b", engine)));

        await Poll.Until(() => VerifySlot.Look(Home).Waiters is [{ For: not null }]);
        var waiter = Assert.Single(VerifySlot.Look(Home).Waiters);
        Assert.Equal(CheckWait.Memory, waiter.Wait!.Code);
        Assert.Equal("4.0", waiter.Wait.Values["free"]);

        await File.WriteAllTextAsync(At("go-a"), "");
        Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync("v-0000000000000002", "i-b")).Verdict);
        Assert.Equal([("start", "a"), ("end", "a"), ("start", "a"), ("end", "a")], Intervals());
    }

    [Fact]
    public async Task A_heavy_run_holding_one_repository_keeps_another_repository_s_run_from_nothing()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false)));
        var game = await RepositoryAsync("game", Gates(("unit", Node(At("gate.mjs"), "b", _folder, "exit-0"), true, false)));
        var executor = Executor(new VerifyWorld { Checks = () => 1 });

        var held = HeavySlot.TryTake(Home, HeavySlot.KeyOf(engine.Checkout)!);
        Assert.NotNull(held);
        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine));
        await executor.SendAsync(Send("v-0000000000000002", "i-b", game, "game"));

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
    public async Task A_gate_that_would_not_start_makes_the_check_unknown_not_a_failure_blaming_it()
    {
        var engine = await RepositoryAsync("engine", new
        {
            gates = new object[]
            {
                new { name = "unit", run = Node(At("gate.mjs"), "a", _folder, "exit-0"), heavy = true },
                new { name = "suite", run = "exit 0", cwd = "nowhere", heavy = true },
                new { name = "e2e", run = "exit 0", heavy = true },
            },
        });
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine));
        var result = await SettledAsync("v-0000000000000001", "i-a");

        Assert.Equal(VerifyVerdicts.Unknown, result.Verdict);
        Assert.Null(result.Failed);
        Assert.Contains("suite", result.Why);
        Assert.Equal(["unit", "suite", "e2e"], result.Gates.Select(gate => gate.Name));
        Assert.Equal(["pass", "ended", "not-run"], result.Gates.Select(gate => gate.Verdict));
        Assert.Contains("nowhere", File.ReadAllText(result.Gates[1].Log!));
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

    private static async Task<(string Commit, string Blob)> CommitOnAsync(string checkout, string branch, string? from, params (string Path, string Text)[] files)
    {
        await GitFixture.GitAsync(checkout, from is null ? ["checkout", "-q", branch] : ["checkout", "-q", "-b", branch, from]);
        foreach (var (path, text) in files) await File.WriteAllTextAsync(Path.Combine(checkout, path), text);
        await GitFixture.GitAsync(checkout, "add", "-A");
        await GitFixture.GitAsync(checkout, "commit", "-q", "-m", $"on {branch}");
        var commit = (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD")).Trim();
        var blob = (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD:daoris.gates.json")).Trim();
        await GitFixture.GitAsync(checkout, "checkout", "-q", "main");
        return (commit, blob);
    }

    [Fact]
    public async Task A_branch_that_predates_or_follows_the_line_s_declaration_change_is_checked_under_the_accepted_one()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false)));
        var work = await CommitOnAsync(engine.Checkout, "work", "main", ("work.txt", "the work"));
        var line = await CommitOnAsync(engine.Checkout, "main", null,
            ("daoris.gates.json", JsonSerializer.Serialize(Gates(("unit", Node(At("gate.mjs"), "b", _folder, "exit-0"), true, false)))));
        Assert.NotEqual(engine.Blob, line.Blob);
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine) with { Commit = work.Commit });
        var before = await SettledAsync("v-0000000000000001", "i-a");

        Assert.True(before.Verdict == VerifyVerdicts.Reached, before.Why);
        Assert.Equal(engine.Blob, before.Declaration);
        Assert.Equal([("start", "a"), ("end", "a")], Intervals());

        await GitFixture.GitAsync(engine.Checkout, "checkout", "-q", "work");
        await GitFixture.GitAsync(engine.Checkout, "merge", "-q", "--no-edit", "main");
        var merged = (await GitFixture.GitAsync(engine.Checkout, "rev-parse", "HEAD")).Trim();
        await GitFixture.GitAsync(engine.Checkout, "checkout", "-q", "main");

        await executor.SendAsync(Send("v-0000000000000002", "i-b", engine) with { Commit = merged });
        var after = await SettledAsync("v-0000000000000002", "i-b");

        Assert.True(after.Verdict == VerifyVerdicts.Reached, after.Why);
        Assert.Equal(engine.Blob, after.Declaration);
        Assert.Equal([("start", "a"), ("end", "a"), ("start", "a"), ("end", "a")], Intervals());
    }

    /// <summary>Where a work left its line is read on the local branch the acceptance look reads, never origin's.</summary>
    [Fact]
    public async Task A_work_branched_after_an_unpushed_declaration_change_on_the_local_line_is_not_held()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false)));
        await GitFixture.GitAsync(engine.Checkout, "update-ref", "refs/remotes/origin/main", engine.Commit);
        await GitFixture.GitAsync(engine.Checkout, "symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/main");
        var line = await CommitOnAsync(engine.Checkout, "main", null,
            ("daoris.gates.json", JsonSerializer.Serialize(Gates(("unit", Node(At("gate.mjs"), "b", _folder, "exit-0"), true, false)))));
        var work = await CommitOnAsync(engine.Checkout, "work", "main", ("work.txt", "the work"));
        Assert.Equal(line.Blob, work.Blob);
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine) with { Commit = work.Commit });
        var result = await SettledAsync("v-0000000000000001", "i-a");

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        Assert.Equal(engine.Blob, result.Declaration);
        Assert.Equal([("start", "a"), ("end", "a")], Intervals());
    }

    [Fact]
    public async Task A_work_that_changes_its_declaration_s_commands_holds_said_and_one_that_changes_a_pattern_runs_under_the_accepted()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false)));
        var commands = await CommitOnAsync(engine.Checkout, "commands", "main",
            ("daoris.gates.json", JsonSerializer.Serialize(Gates(("unit", Node(At("gate.mjs"), "b", _folder, "exit-0"), true, false)))));
        var pattern = await CommitOnAsync(engine.Checkout, "pattern", "main", ("daoris.gates.json", JsonSerializer.Serialize(new
        {
            gates = new object[] { new { name = "unit", run = Node(At("gate.mjs"), "a", _folder, "exit-0"), heavy = true, clock = false, paths = new[] { "src/" } } },
        })));
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine) with { Commit = commands.Commit });
        var held = await SettledAsync("v-0000000000000001", "i-a");

        Assert.Equal((VerifyVerdicts.Unknown, VerifyResult.Changed), (held.Verdict, held.Code));
        Assert.Equal((commands.Blob, engine.Blob), (held.Declaration, held.Accepted));
        Assert.Contains("this work's own change to daoris.gates.json", held.Why);
        Assert.Contains("changed `unit`", held.Why);
        Assert.Contains("(restricted: waits for you)", held.Why);
        Assert.Contains("once the change lands, accept it on its repository's Setup page", held.Why);
        Assert.Contains("It happened", held.Why);
        Assert.False(File.Exists(At("intervals.log")), "nothing ran");

        await executor.SendAsync(Send("v-0000000000000002", "i-b", engine) with { Commit = pattern.Commit });
        var kept = await SettledAsync("v-0000000000000002", "i-b");

        Assert.True(kept.Verdict == VerifyVerdicts.Reached, kept.Why);
        Assert.Equal(engine.Blob, kept.Declaration);
        Assert.Equal([("start", "a"), ("end", "a")], Intervals());
    }

    [Fact]
    public async Task Under_full_access_a_work_whose_own_change_alters_a_command_runs_its_own_declaration_with_no_hold()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false)));
        var commands = await CommitOnAsync(engine.Checkout, "commands", "main",
            ("daoris.gates.json", JsonSerializer.Serialize(Gates(("unit", Node(At("gate.mjs"), "b", _folder, "exit-0"), true, false)))));
        DriverConfig.Empty.WithAccess(new AccessEdit { Repository = "engine", Set = [new(AccessRules.Permissions, AccessRules.Unrestricted)] })
            .Save(DriverConfig.ResolvePath(Home));
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine) with { Commit = commands.Commit });
        var ran = await SettledAsync("v-0000000000000001", "i-a");

        Assert.True(ran.Verdict == VerifyVerdicts.Reached, ran.Why);
        Assert.Null(ran.Code);
        Assert.Equal(commands.Blob, ran.Declaration);
        Assert.Equal([("start", "b"), ("end", "b")], Intervals());
    }

    /// <summary>The send's own workspace decides the mode, so a verification tree outside a registered checkout reads a workspace-level setting as the line does.</summary>
    [Fact]
    public async Task A_workspace_level_unrestricted_runs_the_work_s_own_declaration_through_the_check_path()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false)));
        var commands = await CommitOnAsync(engine.Checkout, "commands", "main",
            ("daoris.gates.json", JsonSerializer.Serialize(Gates(("unit", Node(At("gate.mjs"), "b", _folder, "exit-0"), true, false)))));
        DriverConfig.Empty.WithAccess(new AccessEdit { Workspace = "aurora", Set = [new(AccessRules.Permissions, AccessRules.Unrestricted)] })
            .Save(DriverConfig.ResolvePath(Home));
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine) with { Commit = commands.Commit, Workspace = "aurora" });
        var ran = await SettledAsync("v-0000000000000001", "i-a");

        Assert.True(ran.Verdict == VerifyVerdicts.Reached, ran.Why);
        Assert.Equal([("start", "b"), ("end", "b")], Intervals());
        Assert.Equal(DeclarationMode.Full, DeclarationMode.Of(Home, "engine", "aurora"));
        Assert.Equal(DeclarationMode.Restricted, DeclarationMode.Of(Home, "engine", "borealis"));
    }

    /// <summary>A driver.json that does not read never loosens: the mode is restricted and a changed command is held.</summary>
    [Fact]
    public async Task A_driver_file_that_does_not_read_is_restricted_and_holds_a_changed_command()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false)));
        var commands = await CommitOnAsync(engine.Checkout, "commands", "main",
            ("daoris.gates.json", JsonSerializer.Serialize(Gates(("unit", Node(At("gate.mjs"), "b", _folder, "exit-0"), true, false)))));
        File.WriteAllText(DriverConfig.ResolvePath(Home), "{ this does not read");
        Assert.Equal(DeclarationMode.Restricted, DeclarationMode.Of(Home, "engine", "aurora"));
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine) with { Commit = commands.Commit, Workspace = "aurora" });
        var held = await SettledAsync("v-0000000000000001", "i-a");

        Assert.Equal((VerifyVerdicts.Unknown, VerifyResult.Changed), (held.Verdict, held.Code));
        Assert.False(File.Exists(At("intervals.log")), "nothing ran");
    }

    /// <summary>Nothing accepted yet: restricted access waits for the person, full access runs the work's own declaration, said in a line.</summary>
    [Fact]
    public async Task With_nothing_accepted_restricted_waits_and_full_access_runs_the_work_s_own_declaration()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "exit-0"), true, false)));
        var executor = Executor();

        await executor.SendAsync(Send("v-0000000000000001", "i-a", engine) with { Accepted = null });
        var waits = await SettledAsync("v-0000000000000001", "i-a");
        Assert.Equal(VerifyVerdicts.Unknown, waits.Verdict);
        Assert.Contains("nothing runs until the person accepts", waits.Why);
        Assert.False(File.Exists(At("intervals.log")), "nothing ran");

        DriverConfig.Empty.WithAccess(new AccessEdit { Repository = "engine", Set = [new(AccessRules.Permissions, AccessRules.Unrestricted)] })
            .Save(DriverConfig.ResolvePath(Home));
        await executor.SendAsync(Send("v-0000000000000002", "i-b", engine) with { Accepted = null });
        var ran = await SettledAsync("v-0000000000000002", "i-b");

        Assert.True(ran.Verdict == VerifyVerdicts.Reached, ran.Why);
        Assert.Contains("nothing was accepted yet, so the work's own declaration ran (full access: taken without asking)", ran.Why);
        Assert.Equal(engine.Blob, ran.Declaration);
        Assert.Equal([("start", "a"), ("end", "a")], Intervals());
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
        using (var left = ProcessJob.Create(HeavyExecutor.JobName(Home, "i-a"))!)
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
    public async Task A_check_cut_off_mid_run_holds_nothing_after_a_restart_is_run_again_and_another_starts_beside_it()
    {
        var engine = await RepositoryAsync("engine", Gates(("unit", Node(At("gate.mjs"), "a", _folder, "go"), true, false)));
        var game = await RepositoryAsync("game", Gates(("unit", Node(At("gate.mjs"), "b", _folder, "exit-0"), true, false)));

        var before = Executor();
        await before.SendAsync(Send("v-0000000000000001", "i-a", engine));
        await Poll.Until(() => File.Exists(At("beat-a.txt")) && File.Exists(At("child-a.txt")));
        var child = int.Parse(File.ReadAllText(At("child-a.txt")));
        _executors.Remove(before);
        await before.DisposeAsync();

        Assert.False(File.Exists(VerifyFiles.ResultOf(Home, "v-0000000000000001", "i-a")), "a check cut off writes no result");
        Assert.True(File.Exists(AttemptMarks.Began(Home, "v-0000000000000001", "i-a", 0)), "its send said it began");
        Assert.True(File.Exists(AttemptMarks.Closed(Home, "v-0000000000000001", "i-a", 0)), "the executor's own close said why it ended");
        Assert.False(Alive(child), "what its gate left was ended with it");
        Assert.Empty(VerifySlot.Look(Home).Holders);
        var after = Executor();
        Assert.Equal(VerifyAnswers.NotStarted, (await after.ReconcileAsync("v-0000000000000001", "i-a")).State);

        await after.SendAsync(Send("v-0000000000000002", "i-b", game, "game"));
        Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync("v-0000000000000002", "i-b")).Verdict);
        await File.WriteAllTextAsync(At("go-a"), "");
        await after.SendAsync(Send("v-0000000000000001", "i-a", engine));
        Assert.Equal(VerifyVerdicts.Reached, (await SettledAsync("v-0000000000000001", "i-a")).Verdict);
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
