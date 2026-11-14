using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check whose choice is <c>reached</c> reads the hand-back from git first (the line it is behind, the commits without a maker's
/// trailer) and a fault there is its verdict, run no further; else it runs the declaration's required hand-back commands its change reaches,
/// except any a check finished green at this very commit. A line it cannot find is no verdict at all: it is unknown, coded <c>unrun</c>.
/// </summary>
/// <remarks>Real git, node and job processes: the Process half's.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class HeavyExecutorHandbackProcessTests : IAsyncLifetime
{
    private const string Trailer = "\n\nCo-Authored-By: Claude (Anthropic, fixture) <noreply@anthropic.com>";

    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "heavy-handback", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<HeavyExecutor> _executors = [];
    private string Home => Path.Combine(_folder, "home");
    private string Checkout => Path.Combine(_folder, "engine");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(Home);
        // A stand-in gate: appends its name and its argument to its folder's marker.
        File.WriteAllText(Path.Combine(_folder, "gate.mjs"),
            "import { appendFileSync } from 'node:fs'; import { join } from 'node:path';"
            + " appendFileSync(join(process.argv[2], 'ran.log'), `${process.argv[3]} ${process.argv[4] ?? '-'}\n`);");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        foreach (var executor in _executors) await executor.DisposeAsync();
        GitSeed.Discard(_folder);
    }

    private string Ran => Path.Combine(_folder, "ran.log");

    private string Gate(string name, string? word = null) => $"node \"{Path.Combine(_folder, "gate.mjs")}\" \"{_folder}\" {name}" + (word is null ? "" : " " + word);

    private async Task GitAsync(params string[] arguments) => await GitFixture.GitAsync(Checkout, arguments);

    private async Task CommitAsync(string path, string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Checkout, path))!);
        await File.WriteAllTextAsync(Path.Combine(Checkout, path), message + Guid.NewGuid());
        await GitAsync("add", "-A");
        await GitAsync("commit", "-q", "-m", message);
    }

    /// <summary>A line (<paramref name="line"/>) with a declaration of two required gates, one reached by <c>src/</c> and one of them required by a command of its own, and a slow one; then a branch <c>work</c> off it.</summary>
    private async Task<string> RepositoryAsync(string line = "main")
    {
        Directory.CreateDirectory(Checkout);
        await GitAsync("init", "-q", "-b", line);
        await GitAsync("config", "user.email", "test@example.com");
        await GitAsync("config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(Checkout, "daoris.gates.json"), JsonSerializer.Serialize(new
        {
            gates = new object[]
            {
                new { name = "fast-src", run = Gate("fast-src", "plain"), handback = Gate("fast-src", "required"), paths = new[] { "src/" } },
                new { name = "fast-docs", run = Gate("fast-docs"), handback = true, paths = new[] { "docs/" } },
                new { name = "slow", run = Gate("slow"), heavy = true },
            },
        }));
        await GitAsync("add", "-A");
        await GitAsync("commit", "-q", "-m", "gates" + Trailer);
        await GitAsync("switch", "-q", "-c", "work");
        return (await GitFixture.GitAsync(Checkout, "rev-parse", "HEAD:daoris.gates.json")).Trim();
    }

    private async Task<VerifyResult> RunAsync(string blob)
    {
        var commit = (await GitFixture.GitAsync(Checkout, "rev-parse", "HEAD")).Trim();
        var executor = new HeavyExecutor(Home, new VerifyWorld() with { Poll = TimeSpan.FromMilliseconds(50) });
        _executors.Add(executor);
        await executor.SendAsync(new VerifySend("q7", "i-a", "engine", Checkout, commit, blob) { Tree = Checkout + "-verify", Reached = true });
        var path = VerifyFiles.ResultOf(Home, "q7", "i-a");
        await Poll.Until(() => File.Exists(path), () => $"no result at {path}");
        return VerifyResult.Read(path) ?? throw new InvalidOperationException($"the result at {path} does not read");
    }

    [Fact]
    public async Task A_signed_work_on_the_line_runs_only_the_required_commands_its_paths_reach()
    {
        var blob = await RepositoryAsync();
        await CommitAsync("src/a.txt", "work" + Trailer);

        var result = await RunAsync(blob);

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        Assert.Equal(["fast-src"], result.Gates.Select(gate => gate.Name));
        Assert.Equal("fast-src required", File.ReadAllText(Ran).Trim());
    }

    [Fact]
    public async Task A_required_gate_a_check_finished_green_at_this_very_commit_is_kept_and_not_run_again_and_a_gate_required_by_another_command_is_run_anyway()
    {
        var blob = await RepositoryAsync();
        await CommitAsync("src/a.txt", "work" + Trailer);
        await CommitAsync("docs/a.txt", "more work" + Trailer);
        var tip = (await GitFixture.GitAsync(Checkout, "rev-parse", "HEAD")).Trim();
        var files = new CheckFiles(Home);
        foreach (var (id, gate) in new[] { ("k1", "fast-docs"), ("k2", "fast-src") })
        {
            files.WriteAsk(new CheckAsk(id, "s1", gate, null, DateTimeOffset.UtcNow));
            files.WriteState(new CheckState(id, CheckStates.Reached, DateTimeOffset.UtcNow) { Tip = tip });
        }

        var result = await RunAsync(blob);

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        Assert.Equal([("fast-src", RunnerVerdicts.Pass), ("fast-docs", VerifyGate.Kept)], result.Gates.Select(gate => (gate.Name, gate.Verdict)).OrderByDescending(gate => gate.Name));
        Assert.Equal("fast-src required", File.ReadAllText(Ran).Trim());
    }

    [Fact]
    public async Task A_verdict_at_an_earlier_commit_keeps_nothing()
    {
        var blob = await RepositoryAsync();
        await CommitAsync("docs/a.txt", "work" + Trailer);
        var files = new CheckFiles(Home);
        files.WriteAsk(new CheckAsk("k1", "s1", "fast-docs", null, DateTimeOffset.UtcNow));
        files.WriteState(new CheckState("k1", CheckStates.Reached, DateTimeOffset.UtcNow) { Tip = "0000000000000000000000000000000000000000" });

        var result = await RunAsync(blob);

        Assert.Equal(["fast-docs"], result.Gates.Where(gate => gate.Verdict == RunnerVerdicts.Pass).Select(gate => gate.Name));
        Assert.Equal("fast-docs -", File.ReadAllText(Ran).Trim());
    }

    [Fact]
    public async Task A_commit_without_its_makers_trailer_fails_the_hand_back_and_runs_no_gate()
    {
        var blob = await RepositoryAsync();
        await CommitAsync("src/a.txt", "work with no trailer");

        var result = await RunAsync(blob);

        Assert.Equal((VerifyVerdicts.Failed, HandbackReading.Gate), (result.Verdict, result.Failed));
        Assert.Contains("work with no trailer", result.Why);
        Assert.Contains("Co-Authored-By", result.Why);
        Assert.False(File.Exists(Ran));
    }

    [Fact]
    public async Task Work_the_line_has_moved_past_fails_the_hand_back_and_runs_no_gate()
    {
        var blob = await RepositoryAsync();
        await CommitAsync("src/a.txt", "work" + Trailer);
        await GitAsync("switch", "-q", "main");
        await CommitAsync("later.txt", "moved" + Trailer);
        await GitAsync("switch", "-q", "work");

        var result = await RunAsync(blob);

        Assert.Equal((VerifyVerdicts.Failed, HandbackReading.Gate), (result.Verdict, result.Failed));
        Assert.Contains("1 commit behind `main`", result.Why);
        Assert.False(File.Exists(Ran));
    }

    [Fact]
    public async Task A_repository_with_no_line_to_read_against_is_unrun_never_failed()
    {
        var blob = await RepositoryAsync(line: "trunk");
        await CommitAsync("src/a.txt", "work" + Trailer);

        var result = await RunAsync(blob);

        Assert.Equal((VerifyVerdicts.Unknown, VerifyResult.Unrun), (result.Verdict, result.Code));
        Assert.False(File.Exists(Ran));
    }
}
