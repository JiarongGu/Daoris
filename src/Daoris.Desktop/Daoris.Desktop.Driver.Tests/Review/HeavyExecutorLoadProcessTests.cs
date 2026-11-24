using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A reached run whose tests timed out while the machine ran loaded (a stand-in load: the free commit read short) runs its red
/// classes again alone at a quiet slot before its result counts. Passed alone it is reached and says load; a real assertion failure, or a
/// timeout with the machine quiet, or a failure that stays red alone, is the work's; no quiet slot is unknown with the load code, asked
/// again by the check and never a round.
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
            if (mode === 'timeout' && !again) { console.log('FAIL X.Y > it waits\nError: Test timed out in 10000ms.'); line({ ran: 2, red: ['dotnet|X.Y'] }); process.exit(1); }
            if (mode === 'timeout' && again) { line({ ran: 1 }); process.exit(0); }
            if (mode === 'stays' && !again) { console.log('FAIL X.Y > it waits\nError: Test timed out in 10000ms.'); line({ ran: 2, red: ['dotnet|X.Y'] }); process.exit(1); }
            if (mode === 'stays' && again) { console.log('FAIL X.Y > it waits\nAssertionError: expected 1 to be 2'); line({ ran: 1, red: ['dotnet|X.Y'] }); process.exit(1); }
            if (mode === 'assert') { console.log('FAIL X.Y > it adds\nAssertionError: expected 1 to be 2'); line({ ran: 2, red: ['dotnet|X.Y'] }); process.exit(1); }
            process.exit(2);
            """);
    }

    public async Task DisposeAsync()
    {
        foreach (var executor in _executors) await executor.DisposeAsync();
        GitSeed.Discard(_folder);
    }

    private string At(string name) => Path.Combine(_folder, name);

    private static string Node(string script, params string[] words) => $"node \"{script}\" {string.Join(' ', words.Select(word => $"\"{word}\""))}";

    private async Task<(string Checkout, string Commit, string Blob)> RepositoryAsync(string mode)
    {
        var checkout = At("engine");
        Directory.CreateDirectory(checkout);
        await GitFixture.GitAsync(checkout, "init", "-q", "-b", "main");
        await GitFixture.GitAsync(checkout, "config", "user.email", "test@example.com");
        await GitFixture.GitAsync(checkout, "config", "user.name", "Test");
        var declaration = new
        {
            gates = new[] { new { name = "suite", run = "node -e \"\"", heavy = true } },
            reached = new { run = Node(At("reached.mjs"), mode, _folder, "{base}"), outcome = "reached-classes: " },
        };
        await File.WriteAllTextAsync(Path.Combine(checkout, "daoris.gates.json"), JsonSerializer.Serialize(declaration));
        await GitFixture.GitAsync(checkout, "add", "-A");
        await GitFixture.GitAsync(checkout, "commit", "-q", "-m", "gates");
        return (checkout, (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD")).Trim(), (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD:daoris.gates.json")).Trim());
    }

    /// <summary>A stand-in load: the free commit reads short for the first <paramref name="loadedReads"/> readings, then roomy.</summary>
    private async Task<VerifyResult> RunAsync((string Checkout, string Commit, string Blob) repository, int loadedReads, TimeSpan? quietWait = null)
    {
        var reads = 0;
        var world = new VerifyWorld
        {
            Poll = TimeSpan.FromMilliseconds(50),
            QuietWait = quietWait ?? TimeSpan.FromSeconds(30),
            Memory = () => new MemoryReading(Roomy, Interlocked.Increment(ref reads) <= loadedReads ? Tight : Roomy),
        };
        var executor = new HeavyExecutor(Home, world);
        _executors.Add(executor);
        await executor.SendAsync(new VerifySend("v-0000000000000001", "i-a", "engine", repository.Checkout, repository.Commit, repository.Blob)
        {
            Tree = repository.Checkout + "-verify", Base = "main",
        });
        var path = VerifyFiles.ResultOf(Home, "v-0000000000000001", "i-a");
        await Poll.Until(() => File.Exists(path), () => $"no result at {path}");
        return VerifyResult.Read(path) ?? throw new InvalidOperationException($"the result at {path} does not read");
    }

    private string[] Calls() => File.ReadAllLines(At("reached-calls.log"));

    [Fact]
    public async Task Tests_that_timed_out_under_load_run_again_alone_at_a_quiet_slot_and_pass_without_a_failure_or_a_round()
    {
        var result = await RunAsync(await RepositoryAsync("timeout"), loadedReads: 3);

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        Assert.Contains("load: X.Y timed out while the machine was loaded", result.Why);
        Assert.Contains("passed run again alone at a quiet slot", result.Why);
        Assert.Contains("counted no round", result.Why);
        Assert.Equal(["timeout main ", "timeout main --classes X.Y"], Calls());
        Assert.Null(result.Failed);
    }

    [Fact]
    public async Task A_real_assertion_failure_is_the_work_s_and_is_not_run_again()
    {
        var result = await RunAsync(await RepositoryAsync("assert"), loadedReads: 99);

        Assert.Equal(VerifyVerdicts.Failed, result.Verdict);
        Assert.Contains("X.Y", result.Why);
        Assert.Equal(["assert main "], Calls());
    }

    [Fact]
    public async Task A_timeout_while_the_machine_is_quiet_is_the_work_s_and_is_not_run_again()
    {
        var result = await RunAsync(await RepositoryAsync("timeout"), loadedReads: 0);

        Assert.Equal(VerifyVerdicts.Failed, result.Verdict);
        Assert.Equal(["timeout main "], Calls());
    }

    [Fact]
    public async Task Classes_that_stay_red_run_alone_at_a_quiet_slot_fail_the_check_as_the_work_s()
    {
        var result = await RunAsync(await RepositoryAsync("stays"), loadedReads: 3);

        Assert.Equal(VerifyVerdicts.Failed, result.Verdict);
        Assert.Contains("failed again run alone at a quiet slot", result.Why);
        Assert.Equal(["stays main ", "stays main --classes X.Y"], Calls());
    }

    [Fact]
    public async Task No_quiet_slot_within_the_bound_is_unknown_with_the_load_code_and_never_a_failure()
    {
        var result = await RunAsync(await RepositoryAsync("timeout"), loadedReads: int.MaxValue, quietWait: TimeSpan.FromMilliseconds(300));

        Assert.Equal((VerifyVerdicts.Unknown, VerifyResult.Load), (result.Verdict, result.Code));
        Assert.Contains("counts no round", result.Why);
        Assert.Equal(["timeout main "], Calls());
    }
}
