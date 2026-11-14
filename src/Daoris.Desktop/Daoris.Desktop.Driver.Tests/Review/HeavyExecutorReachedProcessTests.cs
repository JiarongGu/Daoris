using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check over a repository that declares a reached runner runs that runner for its base, never its gates, and reads the runner's
/// verdict; one that declares none, or a check with no base to judge against, runs its gates.
/// </summary>
/// <remarks>Real git, node and job processes: the Process half's.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class HeavyExecutorReachedProcessTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "heavy-reached", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<HeavyExecutor> _executors = [];
    private string Home => Path.Combine(_folder, "home");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Home);
        // A stand-in reached runner: notes its arguments, prints the outcome line its mode names and exits as the mode says.
        await File.WriteAllTextAsync(At("reached.mjs"), """
            import { appendFileSync } from 'node:fs';
            import { join } from 'node:path';
            const [mode, folder, base] = process.argv.slice(2);
            appendFileSync(join(folder, 'reached-calls.log'), `${mode} ${base}\n`);
            const line = (outcome) => console.log(`reached-classes: ${JSON.stringify({ flaked: [], mainRed: [], red: [], ...outcome })}`);
            if (mode === 'pass') { line({ ran: 2, replayed: 7 }); process.exit(0); }
            if (mode === 'flake') { line({ ran: 2, replayed: 0, flaked: ['dotnet|A.B'] }); process.exit(0); }
            if (mode === 'red') { line({ ran: 2, replayed: 1, red: ['dotnet|X.Y'] }); process.exit(1); }
            process.exit(2);
            """);
        await File.WriteAllTextAsync(At("gate.mjs"), "import { appendFileSync } from 'node:fs'; import { join } from 'node:path'; appendFileSync(join(process.argv[2], 'gate-calls.log'), 'ran\\n');");
    }

    public async Task DisposeAsync()
    {
        foreach (var executor in _executors) await executor.DisposeAsync();
        GitSeed.Discard(_folder);
    }

    private string At(string name) => Path.Combine(_folder, name);

    private static string Node(string script, params string[] words) => $"node \"{script}\" {string.Join(' ', words.Select(word => $"\"{word}\""))}";

    private async Task<(string Checkout, string Commit, string Blob)> RepositoryAsync(object declaration)
    {
        var checkout = At("engine");
        Directory.CreateDirectory(checkout);
        await GitFixture.GitAsync(checkout, "init", "-q", "-b", "main");
        await GitFixture.GitAsync(checkout, "config", "user.email", "test@example.com");
        await GitFixture.GitAsync(checkout, "config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(checkout, "daoris.gates.json"), JsonSerializer.Serialize(declaration));
        await GitFixture.GitAsync(checkout, "add", "-A");
        await GitFixture.GitAsync(checkout, "commit", "-q", "-m", "gates");
        return (checkout, (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD")).Trim(), (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD:daoris.gates.json")).Trim());
    }

    private object Declaring(string mode, bool reached = true) => new
    {
        gates = new[] { new { name = "suite", run = Node(At("gate.mjs"), _folder), heavy = true } },
        reached = reached ? new { run = Node(At("reached.mjs"), mode, _folder, "{base}"), outcome = "reached-classes: " } : null,
    };

    private async Task<VerifyResult> RunAsync((string Checkout, string Commit, string Blob) repository, string? baseRef = "main")
    {
        var executor = new HeavyExecutor(Home, new VerifyWorld() with { Poll = TimeSpan.FromMilliseconds(50) });
        _executors.Add(executor);
        await executor.SendAsync(new VerifySend("v-0000000000000001", "i-a", "engine", repository.Checkout, repository.Commit, repository.Blob)
        {
            Tree = repository.Checkout + "-verify", Base = baseRef,
        });
        var path = VerifyFiles.ResultOf(Home, "v-0000000000000001", "i-a");
        await Poll.Until(() => File.Exists(path), () => $"no result at {path}");
        return VerifyResult.Read(path) ?? throw new InvalidOperationException($"the result at {path} does not read");
    }

    [Fact]
    public async Task A_declared_reached_runner_runs_for_the_base_instead_of_the_gates_and_its_pass_is_the_verdict()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("pass")));

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        Assert.Contains("2 classes run, 7 replayed, 0 red", result.Why);
        Assert.Equal("pass main", File.ReadAllText(At("reached-calls.log")).Trim());
        Assert.False(File.Exists(At("gate-calls.log")), "the gates did not run");
    }

    [Fact]
    public async Task A_flake_is_reached_and_named_and_a_red_class_fails_the_check()
    {
        var flaked = await RunAsync(await RepositoryAsync(Declaring("flake")));
        Assert.Equal(VerifyVerdicts.Reached, flaked.Verdict);
        Assert.Contains("dotnet|A.B", flaked.Why);
    }

    [Fact]
    public async Task A_red_class_fails_the_check_naming_it()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("red")));

        Assert.Equal(VerifyVerdicts.Failed, result.Verdict);
        Assert.Contains("dotnet|X.Y", result.Why);
        Assert.False(File.Exists(At("gate-calls.log")));
    }

    [Fact]
    public async Task A_declaration_naming_no_reached_runner_runs_its_gates()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("pass", reached: false)));

        Assert.Equal(VerifyVerdicts.Reached, result.Verdict);
        Assert.Equal(["suite"], result.Gates.Select(gate => gate.Name));
        Assert.True(File.Exists(At("gate-calls.log")));
        Assert.False(File.Exists(At("reached-calls.log")));
    }

    [Fact]
    public async Task A_check_with_no_base_to_judge_against_runs_the_gates_even_where_a_runner_is_declared()
    {
        var result = await RunAsync(await RepositoryAsync(Declaring("pass")), baseRef: null);

        Assert.Equal(["suite"], result.Gates.Select(gate => gate.Name));
        Assert.False(File.Exists(At("reached-calls.log")));
    }
}
