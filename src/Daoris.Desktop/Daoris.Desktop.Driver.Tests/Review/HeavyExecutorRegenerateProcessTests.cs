using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check runs the <c>regenerate</c> commands of the declaration it runs under in its verification tree before any gate, on every check:
/// a later tip whose generated inputs' sources moved reads them current, though the tree's preparation stamp did not move (checks once
/// failed on a stale ignored file their sessions' own trees had regenerated). One that does not finish ends the check
/// unknown, naming the command and its output, no gate run; a gate that fails hands the work its log's red lines with the log's path.
/// </summary>
/// <remarks>Real git and node processes: the Process half's. The preparation is a stub, so no install runs.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class HeavyExecutorRegenerateProcessTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "heavy-regenerate", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<HeavyExecutor> _executors = [];
    private string Home => Path.Combine(_folder, "home");
    private string Checkout => Path.Combine(_folder, "engine");
    private string Called => Path.Combine(_folder, "gate-calls.log");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(Home);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        foreach (var executor in _executors) await executor.DisposeAsync();
        GitSeed.Discard(_folder);
    }

    /// <summary>A preparation whose stamp never moves, as a tree prepared once at an earlier tip.</summary>
    private sealed class Prepared : IVerifyPreparation
    {
        public Task<string?> StampAsync(string home, string checkout, string tree, string repository, CancellationToken ct) => Task.FromResult<string?>("lock-1");

        public Task<string?> RunAsync(string home, string checkout, string tree, string repository, CancellationToken ct) => Task.FromResult<string?>(null);
    }

    // The gate reads the generated file the regenerate writes from the tracked source, as `contracts.mjs --check` reads service.ts.
    private const string Regenerate = "const f = require('fs'); f.mkdirSync('generated', { recursive: true }); f.writeFileSync('generated/out.txt', f.readFileSync('source.txt', 'utf8'));";

    private string Gate => $$"""
        const f = require('fs');
        f.appendFileSync({{JsonSerializer.Serialize(Called)}}, 'ran\n');
        const fresh = f.existsSync('generated/out.txt') && f.readFileSync('generated/out.txt', 'utf8') === f.readFileSync('source.txt', 'utf8');
        console.log('== ok  node tools/first.mjs (0.1s)');
        console.log('first is fine');
        if (fresh) process.exit(0);
        console.log('== RED node tools/contracts.mjs --check (0.4s)');
        console.log('generated/out.txt is stale: the contracts changed since it was written');
        console.log('verify:checks: 2 steps in 0.5s, 1 red');
        process.exit(1);
        """;

    private async Task<string> CommitAsync(string source, string regenerate)
    {
        Directory.CreateDirectory(Checkout);
        if (!Directory.Exists(Path.Combine(Checkout, ".git")))
        {
            await GitFixture.GitAsync(Checkout, "init", "-q", "-b", "main");
            await GitFixture.GitAsync(Checkout, "config", "user.email", "test@example.com");
            await GitFixture.GitAsync(Checkout, "config", "user.name", "Test");
        }

        await File.WriteAllTextAsync(Path.Combine(Checkout, ".gitignore"), "generated/\n");
        await File.WriteAllTextAsync(Path.Combine(Checkout, "source.txt"), source);
        await File.WriteAllTextAsync(Path.Combine(Checkout, "regen.cjs"), Regenerate);
        await File.WriteAllTextAsync(Path.Combine(Checkout, "gate.cjs"), Gate);
        var declaration = new { gates = new[] { new { name = "cli", run = "node gate.cjs", heavy = true, regenerate } } };
        await File.WriteAllTextAsync(Path.Combine(Checkout, "daoris.gates.json"), JsonSerializer.Serialize(declaration));
        await GitFixture.GitAsync(Checkout, "add", "-A");
        await GitFixture.GitAsync(Checkout, "commit", "-q", "-m", source);
        return (await GitFixture.GitAsync(Checkout, "rev-parse", "HEAD")).Trim();
    }

    private async Task<VerifyResult> CheckAsync(string commit, int number)
    {
        var blob = (await GitFixture.GitAsync(Checkout, "rev-parse", $"{commit}:daoris.gates.json")).Trim();
        var executor = new HeavyExecutor(Home, new VerifyWorld { Poll = TimeSpan.FromMilliseconds(50), Preparation = new Prepared() });
        _executors.Add(executor);
        var run = $"v-{number:D16}";
        await executor.SendAsync(new VerifySend(run, $"i-{number}", "engine", Checkout, commit, blob) { Tree = Checkout + "-verify" });
        var path = VerifyFiles.ResultOf(Home, run, $"i-{number}");
        await Poll.Until(() => File.Exists(path), () => $"no result at {path}");
        return VerifyResult.Read(path) ?? throw new InvalidOperationException($"the result at {path} does not read");
    }

    [Fact]
    public async Task A_later_check_at_a_tip_whose_sources_moved_regenerates_before_its_gates_though_the_tree_stood_prepared()
    {
        var first = await CommitAsync("contracts one", "node regen.cjs");
        Assert.Equal(VerifyVerdicts.Reached, (await CheckAsync(first, 1)).Verdict);

        var second = await CommitAsync("contracts two", "node regen.cjs");
        var result = await CheckAsync(second, 2);

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
    }

    [Fact]
    public async Task A_regenerate_that_does_not_finish_ends_the_check_unknown_naming_it_and_its_output_and_runs_no_gate()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "broken.cjs"), "console.log('contracts would not build: tsc is not recognized'); process.exit(3);");
        var commit = await CommitAsync("contracts one", $"node \"{Path.Combine(_folder, "broken.cjs")}\"");

        var result = await CheckAsync(commit, 1);

        Assert.Equal((VerifyVerdicts.Unknown, VerifyResult.Unprepared), (result.Verdict, result.Code));
        Assert.Contains("broken.cjs", result.Why);
        Assert.Contains("exited 3", result.Why);
        Assert.Contains("tsc is not recognized", result.Why);
        Assert.False(File.Exists(Called));
    }

    [Fact]
    public async Task A_failed_gate_hands_the_work_its_logs_red_block_and_the_logs_path()
    {
        var commit = await CommitAsync("contracts one", "node -e \"\"");

        var result = await CheckAsync(commit, 1);

        Assert.Equal(VerifyVerdicts.Failed, result.Verdict);
        Assert.Equal("cli", result.Failed);
        Assert.StartsWith("the gate 'cli' failed with exit 1", result.Why);
        Assert.Contains("== RED node tools/contracts.mjs --check (0.4s)\ngenerated/out.txt is stale: the contracts changed since it was written", result.Why);
        Assert.Contains(result.Gates.Single(gate => gate.Name == "cli").Log!, result.Why);
    }
}
