using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check holds one place in the machine's admission for its regeneration and every gate (or the reached runner) it runs, which a machine
/// that admits one run at a time proves: a second place would wait on the first for ever. The regeneration's children are told the place,
/// as each gate's are, so one that asks the admission takes none of its own. A reached runner runs the fast tier unless the step names
/// <c>heavy</c>.
/// </summary>
/// <remarks>Real git, node and job processes: the Process half's.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class VerifyExecutionPlaceProcessTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "verify-place", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<HeavyExecutor> _executors = [];
    private int _sent;
    private string Home => Path.Combine(_folder, "home");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Home);
        const string note = "import { appendFileSync } from 'node:fs'; import { join } from 'node:path'; const note = (words) => appendFileSync(join(process.argv[2], 'calls.log'), `${words} ${process.env.DAORIS_ADMISSION_HOLD ?? 'none'}\\n`);";
        await File.WriteAllTextAsync(At("regen.mjs"), note + " note('regen');");
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

    private async Task<(string Checkout, string Commit, string Blob)> RepositoryAsync(bool reached)
    {
        var checkout = At("engine");
        Directory.CreateDirectory(checkout);
        await GitFixture.GitAsync(checkout, "init", "-q", "-b", "main");
        await GitFixture.GitAsync(checkout, "config", "user.email", "test@example.com");
        await GitFixture.GitAsync(checkout, "config", "user.name", "Test");
        var regenerate = Node(At("regen.mjs"), _folder);
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
        var world = new VerifyWorld { Poll = TimeSpan.FromMilliseconds(50), Processors = 8, Capacity = 1, Memory = () => new MemoryReading(20L << 30, 20L << 30) };
        var executor = new HeavyExecutor(Home, world);
        _executors.Add(executor);
        var number = ++_sent;
        var run = $"v-{900 + number:D16}";
        await executor.SendAsync(new VerifySend(run, $"i-place-{number}", "engine", repository.Checkout, repository.Commit, repository.Blob)
        {
            Tree = repository.Checkout + "-verify", Base = baseRef, Heavy = heavy,
        });
        var path = VerifyFiles.ResultOf(Home, run, $"i-place-{number}");
        await Poll.Until(() => File.Exists(path), () => $"no result at {path}");
        return VerifyResult.Read(path) ?? throw new InvalidOperationException($"the result at {path} does not read");
    }

    [Fact]
    public async Task A_check_regenerates_then_runs_every_gate_inside_one_place_on_a_machine_that_admits_one()
    {
        var result = await RunAsync(await RepositoryAsync(reached: false));

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        var calls = Calls();
        Assert.Equal(["regen", "gate:a", "gate:b"], calls.Select(call => call.What));
        Assert.NotEqual("none", calls[1].Hold);
        Assert.Equal(calls[1].Hold, calls[2].Hold);
        Assert.Equal(calls[1].Hold, calls[0].Hold);
    }

    [Theory]
    [InlineData(false, "fast")]
    [InlineData(true, "all")]
    public async Task A_reached_runner_runs_after_the_regeneration_in_the_checks_one_place_and_the_fast_tier_unless_the_step_names_heavy(bool heavy, string tier)
    {
        var result = await RunAsync(await RepositoryAsync(reached: true), baseRef: "main", heavy: heavy);

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        var calls = Calls();
        Assert.Equal(["regen", $"reached:main:{tier}"], calls.Select(call => call.What));
        Assert.NotEqual("none", calls[1].Hold);
        Assert.Equal(calls[1].Hold, calls[0].Hold);
    }
}
