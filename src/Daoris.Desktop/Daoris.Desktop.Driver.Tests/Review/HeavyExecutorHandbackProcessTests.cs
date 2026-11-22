using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's check judges the work as merged onto its line's tip. One whose choice is <c>reached</c> reads the hand-back from git first
/// (the line it is behind, the commits without a maker's trailer, the merge) and a fault there is its verdict, run no further; else it
/// runs the declaration's required hand-back commands its change reaches, except any a check finished green at the very commit it judges.
/// A line that moved is no fault: a clean merge is checked as merged and a conflict goes back naming its paths. A line it cannot find is
/// no verdict at all: it is unknown, coded <c>unrun</c>.
/// </summary>
/// <remarks>Real git, node and job processes: the Process half's.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class HeavyExecutorHandbackProcessTests : IAsyncLifetime
{
    private const string Trailer = "\n\nCo-Authored-By: Claude (Anthropic, fixture) <noreply@anthropic.com>";

    private static readonly DateTimeOffset Moment = new MovedClock().GetUtcNow();

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
    private async Task<string> RepositoryAsync(string line = "main", bool admission = false, bool redTypes = false)
    {
        Directory.CreateDirectory(Checkout);
        await GitAsync("init", "-q", "-b", line);
        await GitAsync("config", "user.email", "test@example.com");
        await GitAsync("config", "user.name", "Test");
        object[] gates =
        [
            new { name = "fast-src", run = Gate("fast-src", "plain"), handback = Gate("fast-src", "required"), paths = new[] { "src/" } },
            new { name = "fast-docs", run = Gate("fast-docs"), handback = true, paths = new[] { "docs/" } },
            new { name = "slow", run = Gate("slow"), heavy = true },
            .. admission ? (object[])[new { name = "lint", run = Gate("lint") }, new { name = "types", run = redTypes ? "node -e \"process.exit(3)\"" : Gate("types") }] : [],
        ];
        var declaration = new Dictionary<string, object> { ["gates"] = gates };
        if (admission)
        {
            declaration["admission"] = new
            {
                always = new[] { "lint" },
                suites = Array.Empty<string>(),
                byPaths = new[] { new { gate = "types", paths = new[] { "web/**" }, why = "the page's typecheck" } },
            };
        }

        await File.WriteAllTextAsync(Path.Combine(Checkout, "daoris.gates.json"), JsonSerializer.Serialize(declaration));
        await GitAsync("add", "-A");
        await GitAsync("commit", "-q", "-m", "gates" + Trailer);
        await GitAsync("switch", "-q", "-c", "work");
        return (await GitFixture.GitAsync(Checkout, "rev-parse", "HEAD:daoris.gates.json")).Trim();
    }

    private string Tree => Checkout + "-verify";

    private async Task<string> TipAsync(string at = "work") => (await GitFixture.GitAsync(Checkout, "rev-parse", at)).Trim();

    private async Task<VerifyResult> RunAsync(string blob, IReadOnlyList<string>? gates = null, string invocation = "i-a")
    {
        var commit = await TipAsync("HEAD");
        var executor = new HeavyExecutor(Home, new VerifyWorld() with { Poll = TimeSpan.FromMilliseconds(50) });
        _executors.Add(executor);
        await executor.SendAsync(new VerifySend("q7", invocation, "engine", Checkout, commit, blob) { Tree = Tree, Reached = gates is null, Gates = gates, AsMerged = true });
        var path = VerifyFiles.ResultOf(Home, "q7", invocation);
        await Poll.Until(() => File.Exists(path), () => $"no result at {path}");
        return VerifyResult.Read(path) ?? throw new InvalidOperationException($"the result at {path} does not read");
    }

    private async Task<(string Work, string Line)> BehindAsync(string path, string moved)
    {
        await CommitAsync(path, "work" + Trailer);
        await GitAsync("switch", "-q", "main");
        await CommitAsync(moved, "moved" + Trailer);
        await GitAsync("switch", "-q", "work");
        return (await TipAsync(), await TipAsync("main"));
    }

    [Fact]
    public async Task A_signed_work_on_the_line_runs_only_the_required_commands_its_paths_reach_on_the_work_as_it_stands()
    {
        var blob = await RepositoryAsync();
        await CommitAsync("src/a.txt", "work" + Trailer);

        var result = await RunAsync(blob);

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        Assert.Equal(["fast-src"], result.Gates.Select(gate => gate.Name));
        Assert.Equal("fast-src required", File.ReadAllText(Ran).Trim());
        Assert.Contains("up to date with the line", result.Why);
        Assert.DoesNotContain("merged", result.Why);
        Assert.Equal(await TipAsync(), (await GitFixture.GitAsync(Tree, "rev-parse", "HEAD")).Trim());
    }

    [Fact]
    public async Task Work_behind_a_moved_line_with_no_conflict_is_checked_on_its_merge_onto_the_lines_tip_and_its_branch_is_left_as_it_is()
    {
        var blob = await RepositoryAsync();
        var (work, line) = await BehindAsync("src/a.txt", "later.txt");

        var result = await RunAsync(blob);

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        Assert.Equal(work, result.Commit);
        Assert.Contains("1 commit behind `main`", result.Why);
        Assert.Contains("checked as merged onto `main`", result.Why);
        Assert.Equal("fast-src required", File.ReadAllText(Ran).Trim());
        Assert.Equal($"{line} {work}", (await GitFixture.GitAsync(Tree, "log", "-1", "--format=%P", "HEAD")).Trim());
        Assert.True(File.Exists(Path.Combine(Tree, "later.txt")));
        Assert.Equal(work, await TipAsync());
    }

    [Fact]
    public async Task Work_behind_a_moved_line_that_conflicts_goes_back_to_the_work_naming_the_paths_and_runs_no_gate()
    {
        var blob = await RepositoryAsync();
        var (work, _) = await BehindAsync("src/a.txt", "src/a.txt");

        var result = await RunAsync(blob);

        Assert.Equal((VerifyVerdicts.Failed, HandbackReading.Gate), (result.Verdict, result.Failed));
        Assert.Contains("conflicts in src/a.txt: merge `main` into the branch, resolve that path and commit again", result.Why);
        Assert.False(File.Exists(Ran));
        Assert.Equal(work, await TipAsync());
    }

    [Fact]
    public async Task A_named_gate_check_of_work_behind_a_moved_line_runs_on_its_merge_and_a_conflict_runs_none()
    {
        var blob = await RepositoryAsync();
        var (work, line) = await BehindAsync("src/a.txt", "later.txt");

        var result = await RunAsync(blob, ["slow"]);

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        Assert.Contains("checked as merged onto `main`", result.Why);
        Assert.Equal("slow -", File.ReadAllText(Ran).Trim());
        Assert.Equal($"{line} {work}", (await GitFixture.GitAsync(Tree, "log", "-1", "--format=%P", "HEAD")).Trim());

        File.Delete(Ran);
        await GitAsync("switch", "-q", "main");
        await CommitAsync("src/a.txt", "moved again" + Trailer);
        await GitAsync("switch", "-q", "work");

        var conflicted = await RunAsync(blob, ["slow"], "i-b");

        Assert.Equal((VerifyVerdicts.Failed, HandbackReading.Gate), (conflicted.Verdict, conflicted.Failed));
        Assert.Contains("conflicts in src/a.txt", conflicted.Why);
        Assert.False(File.Exists(Ran));
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
            files.WriteAsk(new CheckAsk(id, "s1", gate, null, Moment));
            files.WriteState(new CheckState(id, CheckStates.Reached, Moment) { Tip = tip });
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
        files.WriteAsk(new CheckAsk("k1", "s1", "fast-docs", null, Moment));
        files.WriteState(new CheckState("k1", CheckStates.Reached, Moment) { Tip = "0000000000000000000000000000000000000000" });

        var result = await RunAsync(blob);

        Assert.Equal(["fast-docs"], result.Gates.Where(gate => gate.Verdict == RunnerVerdicts.Pass).Select(gate => gate.Name));
        Assert.Equal("fast-docs -", File.ReadAllText(Ran).Trim());
    }

    [Fact]
    public async Task A_gate_the_admission_names_runs_on_work_no_required_command_reaches_and_one_it_does_not_reach_is_left_out()
    {
        var blob = await RepositoryAsync(admission: true);
        await CommitAsync("web/a.ts", "work" + Trailer);

        var result = await RunAsync(blob);

        Assert.True(result.Verdict == VerifyVerdicts.Reached, result.Why);
        Assert.Equal(["lint", "types"], result.Gates.Select(gate => gate.Name));
        Assert.Equal(["lint -", "types -"], File.ReadAllLines(Ran).Select(line => line.Trim()).Where(line => line.Length > 0));

        File.Delete(Ran);
        await CommitAsync("docs/b.txt", "more work" + Trailer);

        var docs = await RunAsync(blob, invocation: "i-b");

        Assert.True(docs.Verdict == VerifyVerdicts.Reached, docs.Why);
        Assert.Equal(["fast-docs", "lint", "types"], docs.Gates.Select(gate => gate.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_full_access_work_that_predates_an_admission_the_line_added_is_held_to_it_on_its_merge()
    {
        DriverConfig.Empty.WithAccess(new AccessEdit { Repository = "engine", Set = [new(AccessRules.Permissions, AccessRules.Unrestricted)] })
            .Save(DriverConfig.ResolvePath(Home));
        var blob = await RepositoryAsync();
        await CommitAsync("web/a.ts", "work" + Trailer);
        await GitAsync("switch", "-q", "main");
        await File.WriteAllTextAsync(Path.Combine(Checkout, "daoris.gates.json"), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["gates"] = new object[] { new { name = "types", run = "node -e \"process.exit(3)\"" } },
            ["admission"] = new { always = new[] { "types" }, suites = Array.Empty<string>(), byPaths = Array.Empty<object>() },
        }));
        await GitAsync("add", "-A");
        await GitAsync("commit", "-q", "-m", "the line adds an admission" + Trailer);
        await GitAsync("switch", "-q", "work");

        var result = await RunAsync(blob);

        Assert.Equal((VerifyVerdicts.Failed, "types"), (result.Verdict, result.Failed));
    }

    [Fact]
    public async Task A_red_admission_gate_holds_the_work_on_the_check_naming_the_gate_though_no_required_command_reaches_it()
    {
        var blob = await RepositoryAsync(admission: true, redTypes: true);
        await CommitAsync("web/a.ts", "work" + Trailer);

        var result = await RunAsync(blob);

        Assert.Equal((VerifyVerdicts.Failed, "types"), (result.Verdict, result.Failed));
        Assert.Contains("types", result.Why);
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
    public async Task A_repository_with_no_line_to_read_against_is_unrun_never_failed()
    {
        var blob = await RepositoryAsync(line: "trunk");
        await CommitAsync("src/a.txt", "work" + Trailer);

        var result = await RunAsync(blob);

        Assert.Equal((VerifyVerdicts.Unknown, VerifyResult.Unrun), (result.Verdict, result.Code));
        Assert.False(File.Exists(Ran));
    }
}
