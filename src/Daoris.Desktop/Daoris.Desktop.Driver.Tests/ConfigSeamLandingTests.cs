using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONFIGSEAM1 (D63's per-file override), over real git: a home whose choices are a file of another name, as
/// <c>DAORIS_DRIVER_CONFIG</c> names it, beside a <c>driver.json</c> that says nothing. What <c>trees land --plan</c> asks
/// (<see cref="SessionTrees.PlanAsync"/> and the gate, <see cref="SessionTrees.ReviewAsync"/>) and what the press and every
/// landing door run (<see cref="SessionTrees.LandAsync"/>) read the override's branch rule and review rule, as the loop and the
/// planner do. Before, the trees read the home's <c>driver.json</c>: the plan said a merge with no gate line, and the work merged
/// into the line while the planner sat a review step for it.
/// </summary>
/// <remarks>
/// The variable is the process's, and set here for the class's life: the class runs in the Process half, one class at a time, and
/// every other door holding a home ignores an override in another one (<see cref="ConfigSeamTests"/>).
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ConfigSeamLandingTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;
    private readonly string? _overrideBefore;
    private readonly World _world = new();

    public ConfigSeamLandingTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "config-seam", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
        _overrideBefore = Environment.GetEnvironmentVariable(DriverConfig.PathVariable);
        Environment.SetEnvironmentVariable(DriverConfig.PathVariable, Override);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(DriverConfig.PathVariable, _overrideBefore);
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    /// <summary>The override: a file of another name, in the home.</summary>
    private string Override => Path.Combine(_home, "landing.json");

    [Fact]
    public async Task The_plan_the_gate_and_the_landing_read_the_file_the_override_names()
    {
        // The home's own driver.json says nothing: a merge, and no review. Read, it would merge into the line at once.
        DriverConfig.Empty.Save(Path.Combine(_home, "driver.json"));
        var rule = new ReviewRule([new ReviewEnvironment("local", "local", "README.md", "http://localhost:4200")], Required: true);
        (DriverConfig.Empty with
        {
            Reviews = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase) { ["web-app"] = rule },
            WorkspaceReviews = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase) { ["aurora"] = rule },
        }).WithLanding("web-app", new LandingRule(LandingForm.Branch, "review/{quest}")).Save(Override);

        var root = await RepositoryAsync("web-app");
        var line = await HeadAsync(root);
        var tree = await DoneSessionAsync(root);
        var tip = await HeadAsync(tree);
        ShowStep(tip);
        var trees = new SessionTrees(_home);
        var subject = new LandingSubject("s1", "q1", "Fix the gap");

        // `trees land --plan`: the override's branch rule, and the gate's line.
        var plan = await trees.PlanAsync(tree, subject);
        Assert.Equal((LandingForm.Branch, "review/q1", LandingSource.Repository), (plan.Form, plan.Target, plan.Source));
        var review = await trees.ReviewAsync(tree, "q1", _world);
        Assert.Equal(ReviewStates.Shown, review.State);

        // The press: held by the override's review rule, with nothing made and nothing merged into the line.
        var held = await trees.LandAsync(tree, subject, gate: await trees.GateAsync(tree, "q1", _world, "s1"));
        Assert.Equal(AutoLandingCode.Unreviewed, held.Refusal);
        Assert.False(await BranchAsync(root, "review/q1"), "nothing is made while the review waits");
        Assert.Equal(line, await HeadAsync(root));

        // Reviewed, it lands where the override's rule says: on its branch, the line where it was.
        Review(tip);
        var landed = await trees.LandAsync(tree, subject, gate: await trees.GateAsync(tree, "q1", _world, "s1"));
        Assert.True(landed.Landed, landed.Message);
        Assert.True(await BranchAsync(root, "review/q1"), landed.Message);
        Assert.Equal(line, await HeadAsync(root));
        Assert.Equal(ReviewVerdicts.Reviewed, new LandedBranches(_home).Landing("s1")!.Review!.Said);
    }

    // ——— the world and the fixtures, as ReviewLandingTests has them

    /// <summary>The service's quests and records, standing in.</summary>
    private sealed class World : IAutoLandingWorld
    {
        public Dictionary<string, QuestView> Quests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<SessionRecord> Records { get; } = [];

        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult(Quests.GetValueOrDefault(id));

        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([.. Records]);

        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([.. Quests.Values]);

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult<OpinionView?>(null);
    }

    /// <summary>The set-up step that follows q1, done, having shown its work at <paramref name="commit"/>.</summary>
    private void ShowStep(string commit) =>
        _world.Quests["q2"] = new QuestView("q2", "ask #a1", "web-app", "Show #q1 in `local` for review", "", "Done")
        {
            Parent = "q1",
            SetUpIn = "local",
            Held = true,
            Hold = EvidenceCodes.Unreviewed,
            SetUps = [new QuestSetUpView(commit) { Look = "http://localhost:4200/reports", Shows = "the new column", Session = "s2", Local = true, Machine = "desk", Sequence = 41 }],
        };

    /// <summary>The person's reviewed of the set-up step's set-up.</summary>
    private void Review(string commit) =>
        _world.Quests["q2"] = _world.Quests["q2"] with
        {
            Held = false,
            Hold = null,
            Verdicts = [new QuestReviewVerdictView(ReviewVerdicts.Reviewed) { SetUpMachine = "desk", SetUpSequence = 41, Commit = commit, At = DateTimeOffset.UtcNow }],
        };

    /// <summary>A session that worked in its own tree and concluded on its quest's done.</summary>
    private async Task<string> DoneSessionAsync(string root)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "web-app", "aurora");
        await CommitAsync(opened.Path, "s1.txt", "the work of s1", "the work of s1");
        _world.Quests["q1"] = new QuestView("q1", "ask #a1", "web-app", "Fix the gap", "Fix it.", "Done");
        _world.Records.Add(new SessionRecord("s1", "web-app", "completed") { Quest = "q1", Tree = opened.Path, Created = DateTimeOffset.UtcNow });
        return opened.Path;
    }

    private static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    private static async Task<string> HeadAsync(string tree) => (await GitAsync(tree, "rev-parse", "HEAD")).Trim();

    private async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await CommitAsync(root, "README.md", $"# {name}", "first");
        return root;
    }

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        // Both streams read at once, before the wait, so a git that writes a pipe's worth to one never blocks on it.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        return await stdout;
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git"))
               && !File.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
