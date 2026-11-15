using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check's verification tree is prepared as a session's tree is before its gates run: the preparation is asked once with the tree and
/// the repository's checkout, again only where the inputs it stands on moved, and one that does not finish ends the attempt with its own
/// code and sentence, no gate run.
/// </summary>
/// <remarks>Real git and node processes: the Process half's. The preparation is a stub, so no install runs.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class HeavyExecutorPreparationProcessTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "heavy-prepare", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<HeavyExecutor> _executors = [];
    private string Home => Path.Combine(_folder, "home");

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

    private sealed class Stub(string? failure = null) : IVerifyPreparation
    {
        public string Stamp = "lock-1";
        public readonly List<(string Checkout, string Tree, string Repository)> Asked = [];

        public Task<string?> StampAsync(string home, string checkout, string tree, string repository, CancellationToken ct) => Task.FromResult<string?>(Stamp);

        public Task<string?> RunAsync(string home, string checkout, string tree, string repository, CancellationToken ct)
        {
            Asked.Add((checkout, tree, repository));
            return Task.FromResult(failure);
        }
    }

    private async Task<(string Checkout, string Commit, string Blob)> RepositoryAsync()
    {
        var checkout = Path.Combine(_folder, "engine");
        Directory.CreateDirectory(checkout);
        await GitFixture.GitAsync(checkout, "init", "-q", "-b", "main");
        await GitFixture.GitAsync(checkout, "config", "user.email", "test@example.com");
        await GitFixture.GitAsync(checkout, "config", "user.name", "Test");
        var gate = Path.Combine(_folder, "gate.mjs");
        await File.WriteAllTextAsync(gate, "import { appendFileSync } from 'node:fs'; import { join } from 'node:path'; appendFileSync(join(process.argv[2], 'gate-calls.log'), 'ran\\n');");
        var declaration = new { gates = new[] { new { name = "suite", run = $"node \"{gate}\" \"{_folder}\"", heavy = true } } };
        await File.WriteAllTextAsync(Path.Combine(checkout, "daoris.gates.json"), JsonSerializer.Serialize(declaration));
        await GitFixture.GitAsync(checkout, "add", "-A");
        await GitFixture.GitAsync(checkout, "commit", "-q", "-m", "gates");
        return (checkout, (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD")).Trim(), (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD:daoris.gates.json")).Trim());
    }

    private async Task<VerifyResult> RunAsync(Stub preparation, (string Checkout, string Commit, string Blob) repository, int number)
    {
        var executor = new HeavyExecutor(Home, new VerifyWorld { Poll = TimeSpan.FromMilliseconds(50), Preparation = preparation });
        _executors.Add(executor);
        var run = $"v-{number:D16}";
        await executor.SendAsync(new VerifySend(run, $"i-{number}", "engine", repository.Checkout, repository.Commit, repository.Blob) { Tree = repository.Checkout + "-verify" });
        var path = VerifyFiles.ResultOf(Home, run, $"i-{number}");
        await Poll.Until(() => File.Exists(path), () => $"no result at {path}");
        return VerifyResult.Read(path) ?? throw new InvalidOperationException($"the result at {path} does not read");
    }

    [Fact]
    public async Task The_tree_is_prepared_once_before_its_gates_and_again_only_where_the_lockfiles_moved()
    {
        var repository = await RepositoryAsync();
        var stub = new Stub();

        Assert.Equal(VerifyVerdicts.Reached, (await RunAsync(stub, repository, 1)).Verdict);
        Assert.Equal([(repository.Checkout, repository.Checkout + "-verify", "engine")], stub.Asked);
        Assert.True(File.Exists(Path.Combine(_folder, "gate-calls.log")));

        Assert.Equal(VerifyVerdicts.Reached, (await RunAsync(stub, repository, 2)).Verdict);
        Assert.Single(stub.Asked);

        stub.Stamp = "lock-2";
        Assert.Equal(VerifyVerdicts.Reached, (await RunAsync(stub, repository, 3)).Verdict);
        Assert.Equal(2, stub.Asked.Count);
    }

    [Fact]
    public async Task A_preparation_that_does_not_finish_ends_the_attempt_with_its_code_and_runs_no_gate()
    {
        var repository = await RepositoryAsync();

        var result = await RunAsync(new Stub("`npm ci` did not finish: tsc is not recognized"), repository, 1);

        Assert.Equal((VerifyVerdicts.Unknown, VerifyResult.Unprepared), (result.Verdict, result.Code));
        Assert.Contains("tsc is not recognized", result.Why);
        Assert.False(File.Exists(Path.Combine(_folder, "gate-calls.log")));
    }
}
