using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check regenerates what its gates' declaration says before them, every check and not only where its tree's preparation moved, and
/// holds one place in the machine's admission for the regeneration and every gate (or the reached runner) it runs; a reached runner
/// runs the fast tier unless the step names <c>heavy</c>.
/// </summary>
/// <remarks>Real git, node and job processes: the Process half's.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class VerifyExecutionRegenerateProcessTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "verify-regenerate", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<HeavyExecutor> _executors = [];
    private int _sent;
    private string Home => Path.Combine(_folder, "home");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Home);
        // Each stand-in notes what it is and the admission hold it runs inside, in the order the check runs them.
        const string note = "import { appendFileSync } from 'node:fs'; import { join } from 'node:path'; const note = (words) => appendFileSync(join(process.argv[2], 'calls.log'), `${words} ${process.env.DAORIS_ADMISSION_HOLD ?? 'none'}\\n`);";
        await File.WriteAllTextAsync(At("regen.mjs"), note + " note('regen'); process.exit(Number(process.argv[3] ?? 0));");
        await File.WriteAllTextAsync(At("gate.mjs"), note + " note(`gate:${process.argv[3]}`);");
        await File.WriteAllTextAsync(At("reached.mjs"), note + " note(`reached:${process.argv[3]}:${process.argv[4]}`); console.log('reached-classes: {\"ran\":1,\"replayed\":0,\"flaked\":[],\"mainRed\":[],\"red\":[]}');");
    }

    public async Task DisposeAsync()
    {
        foreach (var executor in _executors) await executor.DisposeAsync();
        GitSeed.Discard(_folder);
    }

    private string At(string name) => Path.Combine(_folder, name);

    private static string Node(string script, params string[] words) => $"node \"{script}\" {string.Join(' ', words.Select(word => $"\"{word}\""))}";

    private IReadOnlyList<(string What, string Hold)> Calls() => File.Exists(At("calls.log"))
        ? [.. File.ReadAllLines(At("calls.log")).Select(line => line.Split(' ')).Select(words => (words[0], words[1]))]
        : [];

    private async Task<(string Checkout, string Commit, string Blob)> RepositoryAsync(int regenerateExit, bool reached)
    {
        var checkout = At("engine");
        Directory.CreateDirectory(checkout);
        await GitFixture.GitAsync(checkout, "init", "-q", "-b", "main");
        await GitFixture.GitAsync(checkout, "config", "user.email", "test@example.com");
        await GitFixture.GitAsync(checkout, "config", "user.name", "Test");
        var regenerate = Node(At("regen.mjs"), _folder, regenerateExit.ToString());
        var declaration = new
        {
            gates = new[]
            {
                new { name = "a", run = Node(At("gate.mjs"), _folder, "a"), heavy = true, regenerate },
                new { name = "b", run = Node(At("gate.mjs"), _folder, "b"), heavy = true, regenerate },
            },
            reached = reached ? new { run = Node(At("reached.mjs"), _folder, "{base}", "{tier}"), outcome = "reached-classes: " } : null,
        };
        await File.WriteAllTextAsync(Path.Combine(checkout, "daoris.gates.json"), JsonSerializer.Serialize(declaration));
        await GitFixture.GitAsync(checkout, "add", "-A");
        await GitFixture.GitAsync(checkout, "commit", "-q", "-m", "gates");
        return (checkout, (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD")).Trim(), (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD:daoris.gates.json")).Trim());
    }

    private async Task<VerifyResult> RunAsync((string Checkout, string Commit, string Blob) repository, string? baseRef = null, bool heavy = false)
    {
        var executor = new HeavyExecutor(Home, new VerifyWorld() with { Poll = TimeSpan.FromMilliseconds(50) });
        _executors.Add(executor);
        var number = ++_sent;
        var run = $"v-{number:D16}";
        await executor.SendAsync(new VerifySend(run, $"i-{number}", "engine", repository.Checkout, repository.Commit, repository.Blob)
        {
            Tree = repository.Checkout + "-verify", Base = baseRef, Heavy = heavy,
        });
        var path = VerifyFiles.ResultOf(Home, run, $"i-{number}");
        await Poll.Until(() => File.Exists(path), () => $"no result at {path}");
        return VerifyResult.Read(path) ?? throw new InvalidOperationException($"the result at {path} does not read");
    }

    [Fact]
    public async Task Each_check_regenerates_once_before_its_gates_and_runs_them_all_inside_one_place()
    {
        var repository = await RepositoryAsync(0, reached: false);

        var one = await RunAsync(repository);
        Assert.True(one.Verdict == VerifyVerdicts.Reached, one.Why);
        var first = Calls();
        Assert.Equal(["regen", "gate:a", "gate:b"], first.Select(call => call.What));
        Assert.NotEqual("none", first[0].Hold);
        Assert.Single(first.Select(call => call.Hold).Distinct());

        // The preparation's stamp has not moved, yet the next check regenerates again.
        var two = await RunAsync(repository);
        Assert.True(two.Verdict == VerifyVerdicts.Reached, two.Why);
        Assert.Equal(["regen", "gate:a", "gate:b", "regen", "gate:a", "gate:b"], Calls().Select(call => call.What));
    }

    [Fact]
    public async Task A_regeneration_that_fails_fails_the_check_naming_it_and_runs_no_gate()
    {
        var result = await RunAsync(await RepositoryAsync(3, reached: false));

        Assert.Equal((VerifyVerdicts.Failed, VerifyExecution.RegenerateGate), (result.Verdict, result.Failed));
        Assert.Contains("failed with exit 3", result.Why);
        Assert.Equal(["regen"], Calls().Select(call => call.What));
        Assert.Equal([VerifyExecution.RegenerateGate, "a", "b"], result.Gates.Select(gate => gate.Name));
    }

    [Theory]
    [InlineData(false, "fast")]
    [InlineData(true, "all")]
    public async Task A_reached_runner_runs_after_the_regeneration_in_the_checks_one_place_and_the_fast_tier_unless_the_step_names_heavy(bool heavy, string tier)
    {
        var result = await RunAsync(await RepositoryAsync(0, reached: true), baseRef: "main", heavy: heavy);

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        var calls = Calls();
        Assert.Equal(["regen", $"reached:main:{tier}"], calls.Select(call => call.What));
        Assert.Single(calls.Select(call => call.Hold).Distinct());
    }
}
