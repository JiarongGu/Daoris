using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A reached run whose tests timed out on a loaded machine (a stand-in load: the free commit reads short) runs its red
/// classes again alone at a quiet slot before its result counts, by the command its declaration names. Passed alone it is reached and says
/// load; a real assertion failure, a timeout with the machine quiet, a failure that stays red alone and a declaration that names no rerun
/// are the work's; no quiet slot, a rerun that timed out under load again or ran no class is unknown with the load code, asked again by the
/// check and never a round.
/// </summary>
/// <remarks>Real git, node and job processes: the Process half's.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class HeavyExecutorLoadProcessTests : IAsyncLifetime
{
    private const long Roomy = 16L << 30;
    private const long Tight = 2L << 30;

    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "heavy-load", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<HeavyExecutor> _executors = [];
    private string Home => Path.Combine(_folder, "home");
    private string Checkout => At("engine");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Home);
        // A stand-in reached runner: notes its arguments; a run naming classes (`--classes`) is the rerun and answers as its mode says.
        await File.WriteAllTextAsync(At("reached.mjs"), """
            import { appendFileSync } from 'node:fs';
            import { join } from 'node:path';
            const [mode, folder, base, ...rest] = process.argv.slice(2);
            appendFileSync(join(folder, 'reached-calls.log'), `${mode} ${base} ${rest.join(' ')}\n`);
            const line = (outcome) => console.log(`reached-classes: ${JSON.stringify({ flaked: [], mainRed: [], red: [], ...outcome })}`);
            const again = rest[0] === '--classes';
            const timeout = () => console.log('FAIL X.Y > it waits\nError: Test timed out in 10000ms.');
            if (!again && ['timeout', 'stays', 'twice', 'empty'].includes(mode)) { timeout(); line({ ran: 2, red: ['dotnet|X.Y'] }); process.exit(1); }
            if (mode === 'timeout' && again) { line({ ran: 1 }); process.exit(0); }
            if (mode === 'stays' && again) { console.log('FAIL X.Y > it waits\nAssertionError: expected 1 to be 2'); line({ ran: 1, red: ['dotnet|X.Y'] }); process.exit(1); }
            if (mode === 'twice' && again) { timeout(); line({ ran: 1, red: ['dotnet|X.Y'] }); process.exit(1); }
            if (mode === 'empty' && again) { line({ ran: 0 }); process.exit(0); }
            if (mode === 'assert') { console.log('FAIL X.Y > it adds\nAssertionError: expected 1 to be 2'); line({ ran: 2, red: ['dotnet|X.Y'] }); process.exit(1); }
            if (mode === 'other') { console.log('FAIL Z.W > it waits\nError: Test timed out in 10000ms.\n\nFAIL X.Y > it adds\nAssertionError: expected 1 to be 2'); line({ ran: 2, red: ['dotnet|X.Y'] }); process.exit(1); }
            process.exit(2);
            """);
        await File.WriteAllTextAsync(At("gate.mjs"), "import { appendFileSync } from 'node:fs'; import { join } from 'node:path'; appendFileSync(join(process.argv[2], 'gate-calls.log'), `${process.argv[3]}\\n`);");
    }

    public async Task DisposeAsync()
    {
        foreach (var executor in _executors) await executor.DisposeAsync();
        GitSeed.Discard(_folder);
    }

    private string At(string name) => Path.Combine(_folder, name);

    private static string Node(string script, params string[] words) => $"node \"{script}\" {string.Join(' ', words.Select(word => $"\"{word}\""))}";

    private object Declaring(string mode, bool rerun, bool handback = false) => new
    {
        gates = handback
            ? (object[])
            [
                new { name = "reached", run = Node(At("reached.mjs"), mode, _folder, "main"), handback = Node(At("reached.mjs"), mode, _folder, "main"), paths = new[] { "src/" } },
                new { name = "after", run = Node(At("gate.mjs"), _folder, "after"), handback = true, paths = new[] { "src/" } },
            ]
            : (object[])[new { name = "suite", run = "node -e \"\"", heavy = true }],
        reached = new
        {
            run = Node(At("reached.mjs"), mode, _folder, "{base}"),
            rerun = rerun ? Node(At("reached.mjs"), mode, _folder, "{base}") + " --classes {classes}" : null,
            outcome = "reached-classes: ",
        },
    };

    private async Task<(string Commit, string Blob)> RepositoryAsync(object declaration, bool work = false)
    {
        Directory.CreateDirectory(Checkout);
        await GitFixture.GitAsync(Checkout, "init", "-q", "-b", "main");
        await GitFixture.GitAsync(Checkout, "config", "user.email", "test@example.com");
        await GitFixture.GitAsync(Checkout, "config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(Checkout, "daoris.gates.json"), JsonSerializer.Serialize(declaration));
        await GitFixture.GitAsync(Checkout, "add", "-A");
        await GitFixture.GitAsync(Checkout, "commit", "-q", "-m", "gates\n\nCo-Authored-By: Claude (Anthropic, fixture) <noreply@anthropic.com>");
        var blob = (await GitFixture.GitAsync(Checkout, "rev-parse", "HEAD:daoris.gates.json")).Trim();
        if (work)
        {
            await GitFixture.GitAsync(Checkout, "switch", "-q", "-c", "work");
            Directory.CreateDirectory(Path.Combine(Checkout, "src"));
            await File.WriteAllTextAsync(Path.Combine(Checkout, "src", "a.txt"), "work");
            await GitFixture.GitAsync(Checkout, "add", "-A");
            await GitFixture.GitAsync(Checkout, "commit", "-q", "-m", "work\n\nCo-Authored-By: Claude (Anthropic, fixture) <noreply@anthropic.com>");
        }

        return ((await GitFixture.GitAsync(Checkout, "rev-parse", "HEAD")).Trim(), blob);
    }

    /// <summary>
    /// A stand-in load, driven by what the stand-in runner has done: once its first run is logged the free commit reads short for the first
    /// <paramref name="loadedReads"/> readings (the reading at the failure, then the wait for a quiet slot), and after its rerun began it reads
    /// short again where <paramref name="loadedAfter"/> says so. Nothing counts the readings the executor takes before its first run.
    /// </summary>
    private async Task<VerifyResult> RunAsync(
        (string Commit, string Blob) repository, int loadedReads, bool loadedAfter = false, TimeSpan? quietWait = null, bool handback = false)
    {
        var reads = 0;
        var world = new VerifyWorld
        {
            Poll = TimeSpan.FromMilliseconds(50),
            QuietWait = quietWait ?? TimeSpan.FromSeconds(30),
            Memory = () =>
            {
                var calls = File.Exists(At("reached-calls.log")) ? File.ReadAllLines(At("reached-calls.log")).Length : 0;
                var loaded = calls == 1 ? Interlocked.Increment(ref reads) <= loadedReads : calls > 1 && loadedAfter;
                return new MemoryReading(Roomy, loaded ? Tight : Roomy);
            },
        };
        var executor = new HeavyExecutor(Home, world);
        _executors.Add(executor);
        await executor.SendAsync(new VerifySend("v-0000000000000001", "i-a", "engine", Checkout, repository.Commit, repository.Blob)
        {
            Tree = Checkout + "-verify", Base = handback ? null : "main", Reached = handback, AsMerged = handback,
        });
        var path = VerifyFiles.ResultOf(Home, "v-0000000000000001", "i-a");
        await Poll.Until(() => File.Exists(path), () => $"no result at {path}");
        return VerifyResult.Read(path) ?? throw new InvalidOperationException($"the result at {path} does not read");
    }

    private string[] Calls() => File.ReadAllLines(At("reached-calls.log"));

    [Fact]
    public async Task Tests_that_timed_out_under_load_run_again_alone_at_a_quiet_slot_and_pass_without_a_failure_or_a_round()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("timeout", rerun: true)), loadedReads: 3);

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        Assert.Contains("load: X.Y timed out while the machine was loaded", result.Why);
        Assert.Contains("passed run again alone at a quiet slot", result.Why);
        Assert.Contains("counted no round", result.Why);
        Assert.Equal(["timeout main ", "timeout main --classes X.Y"], Calls());
        Assert.Null(result.Failed);
    }

    [Fact]
    public async Task A_hand_back_check_goes_on_with_its_other_gates_after_the_reached_gate_passed_alone_and_waits_on_no_place_of_its_own()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("timeout", rerun: true, handback: true), work: true), loadedReads: 3, handback: true);

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        Assert.Contains("load: X.Y timed out", result.Why);
        Assert.DoesNotContain("when Daoris closed", result.Why);
        Assert.Equal(["reached", "after"], result.Gates.Select(gate => gate.Name));
        Assert.Equal(["timeout main ", "timeout main --classes X.Y"], Calls());
        Assert.Equal(["after"], File.ReadAllLines(At("gate-calls.log")));
    }

    [Fact]
    public async Task A_real_assertion_failure_is_the_work_s_and_is_not_run_again()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("assert", rerun: true)), loadedReads: 99);

        Assert.Equal(VerifyVerdicts.Failed, result.Verdict);
        Assert.Contains("X.Y", result.Why);
        Assert.Equal(["assert main "], Calls());
    }

    [Fact]
    public async Task A_timeout_elsewhere_in_the_log_does_not_make_another_class_s_assertion_failure_a_load_failure()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("other", rerun: true)), loadedReads: 99);

        Assert.Equal(VerifyVerdicts.Failed, result.Verdict);
        Assert.Equal(["other main "], Calls());
    }

    [Fact]
    public async Task A_timeout_while_the_machine_is_quiet_is_the_work_s_and_is_not_run_again()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("timeout", rerun: true)), loadedReads: 0);

        Assert.Equal(VerifyVerdicts.Failed, result.Verdict);
        Assert.Equal(["timeout main "], Calls());
    }

    [Fact]
    public async Task A_declaration_naming_no_rerun_command_keeps_the_failure_as_it_was_and_no_flag_is_guessed()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("timeout", rerun: false)), loadedReads: 3);

        Assert.Equal(VerifyVerdicts.Failed, result.Verdict);
        Assert.Equal(["timeout main "], Calls());
    }

    [Fact]
    public async Task Classes_that_stay_red_run_alone_at_a_quiet_slot_fail_the_check_as_the_work_s()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("stays", rerun: true)), loadedReads: 3);

        Assert.Equal(VerifyVerdicts.Failed, result.Verdict);
        Assert.Contains("failed again run alone at a quiet slot", result.Why);
        Assert.Equal(["stays main ", "stays main --classes X.Y"], Calls());
    }

    [Fact]
    public async Task Classes_that_time_out_again_while_the_machine_reads_loaded_again_are_unknown_with_the_load_code_and_never_the_work_s()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("twice", rerun: true)), loadedReads: 3, loadedAfter: true);

        Assert.Equal((VerifyVerdicts.Unknown, VerifyResult.Load), (result.Verdict, result.Code));
        Assert.Contains("timed out again run alone", result.Why);
    }

    [Fact]
    public async Task A_rerun_that_ran_no_class_is_unknown_with_the_load_code_and_never_a_pass()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("empty", rerun: true)), loadedReads: 3);

        Assert.Equal((VerifyVerdicts.Unknown, VerifyResult.Load), (result.Verdict, result.Code));
        Assert.Contains("ran no class", result.Why);
    }

    [Fact]
    public async Task No_quiet_slot_within_the_bound_is_unknown_with_the_load_code_and_never_a_failure()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("timeout", rerun: true)), loadedReads: int.MaxValue, quietWait: TimeSpan.FromMilliseconds(300));

        Assert.Equal((VerifyVerdicts.Unknown, VerifyResult.Load), (result.Verdict, result.Code));
        Assert.Contains("counts no round", result.Why);
        Assert.Equal(["timeout main "], Calls());
    }
}
