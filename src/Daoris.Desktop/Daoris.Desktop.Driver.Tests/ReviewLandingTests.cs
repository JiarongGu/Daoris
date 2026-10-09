using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWENV1c (D154 point 7; the review environment design §3.1–§3.2, §3.5): the gate at every landing door, over real git. The
/// press (<c>LAND_SESSION_TREE</c> and <c>trees land</c> land through <see cref="SessionTrees.LandAsync"/> with the gate the door
/// read) and the look under <i>Accept automatically</i> each refuse a chain's work the level says to review, until the person's
/// <i>reviewed</i> on a set-up whose commit holds the tip, or their skip; a later commit holds again; a merge rule waits as a branch
/// rule does; a repository set to none lands at once. What let it go is kept on the landing record.
/// </summary>
/// <remarks>
/// Real git, as the other tree tests, and the service's quests stood in: nothing reaches a network or a harness.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ReviewLandingTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;
    private readonly World _world = new();
    private readonly List<LandingLine> _lines = [];

    public ReviewLandingTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "review-landing", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    /// <summary>
    /// The press, under a branch rule: shown and unreviewed, it is refused with the gate's sentence and nothing is made; the
    /// person's <i>reviewed</i> on the set-up at the tree's tip lets it land, and the landing record keeps the review.
    /// </summary>
    [Fact]
    public async Task The_press_waits_for_the_persons_reviewed_and_the_landing_keeps_it()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var tip = await HeadAsync(tree);
        ShowStep(tip);

        var held = await PressAsync(tree);
        Assert.False(held.Landed);
        Assert.Equal(AutoLandingCode.Unreviewed, held.Refusal);
        Assert.StartsWith("Waits for your review in `local`: set-up step `#q2` showed it at", held.Message);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"), "nothing is made while the review waits");

        Review(tip, words: "the column reads right");
        var landed = await PressAsync(tree);
        Assert.True(landed.Landed, landed.Message);
        var record = new LandedBranches(_home).Landing("s1")!;
        Assert.Equal(new LandingReview(ReviewVerdicts.Reviewed, "local", "q2") { Commit = tip, At = Verdicted, Words = "the column reads right" }, record.Review);
    }

    /// <summary>A commit added after the review is work the person has not seen run: the gate holds it again.</summary>
    [Fact]
    public async Task A_later_commit_holds_again()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var tip = await HeadAsync(tree);
        ShowStep(tip);
        Review(tip);

        await CommitAsync(tree, "late.txt", "added after the look", "a late change");
        var held = await PressAsync(tree);

        Assert.False(held.Landed);
        Assert.Contains("what you reviewed does not hold these commits", held.Message);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));
    }

    /// <summary>A merge rule waits as a branch rule does (design §3.1): nothing merges into the line until the review lets it go.</summary>
    [Fact]
    public async Task A_merge_rule_waits_as_a_branch_rule_does()
    {
        var root = await RepositoryAsync("web-app");
        Configure(landing: null, required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var line = await HeadAsync(root);

        var held = await PressAsync(tree);

        Assert.False(held.Landed);
        Assert.Contains("nothing shows it there yet: no set-up step for `web-app` is in its chain", held.Message);
        Assert.Equal(line, await HeadAsync(root));
    }

    /// <summary>The person's skip lets the work land without a review, and the landing record keeps it with their words.</summary>
    [Fact]
    public async Task A_skip_lets_it_land_and_is_kept()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        _world.Quests["q1"] = _world.Quests["q1"] with
        {
            Verdicts = [new QuestReviewVerdictView(ReviewVerdicts.Skipped) { Words = "a readme change", At = Verdicted }],
        };

        var landed = await PressAsync(tree);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(new LandingReview(ReviewVerdicts.Skipped, null, "q1") { At = Verdicted, Words = "a readme change" },
            new LandedBranches(_home).Landing("s1")!.Review);
    }

    /// <summary>A repository set to none has no review environment, whatever its workspace says (design §1.3): it lands at once.</summary>
    [Fact]
    public async Task A_repository_set_to_none_lands_at_once()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: true, repositoryNone: true);
        var tree = await DoneSessionAsync(root, "s1", "q1");

        var landed = await PressAsync(tree);

        Assert.True(landed.Landed, landed.Message);
        Assert.Null(new LandedBranches(_home).Landing("s1")!.Review);
    }

    /// <summary>
    /// <i>Accept automatically</i> (design §3.1): the look keeps a due session due as <c>unreviewed</c>, told once and read again at
    /// every look, and lands it at the first look after the review, as the press would, the review kept on its record.
    /// </summary>
    [Fact]
    public async Task The_look_keeps_it_unreviewed_and_lands_it_at_the_first_look_after_the_review()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true), required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1", due: true);
        var tip = await HeadAsync(tree);
        ShowStep(tip);

        var first = await LookAsync();
        Assert.Contains("Waits for your review in `local`", Assert.Single(first));
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.Unreviewed, entry.Last!.Code);
        Assert.Null(entry.Closed);
        Assert.Equal(NoteCodes.LandingUnreviewed.Code, Notes("s1").Last().Parts![0].Code);
        var logged = Assert.Single(_lines, line => line.Event == "review.held");
        Assert.Equal((ReviewStates.Shown, ReviewLevels.SetUpStep, ReviewDoors.Look),
            (Value(logged, "state"), Value(logged, "level"), Value(logged, "door")));

        // Said once: a look with nothing changed says nothing more, and still lands nothing.
        Assert.Empty(await LookAsync());
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));

        Review(tip);
        Assert.Single(await LookAsync());
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(ReviewVerdicts.Reviewed, new LandedBranches(_home).Landing("s1")!.Review!.Said);
    }

    /// <summary>
    /// The set-up's commit is read, never reported (design §2.6): at a set-up step's end the driver posts its session's said
    /// set-ups with its tree's <c>HEAD</c> and the kind the rule declares, tells the conversation, and logs <c>review.shown</c>. A
    /// quest that is no set-up step posts nothing.
    /// </summary>
    [Fact]
    public async Task A_set_up_steps_end_posts_the_commit_its_tree_holds()
    {
        var root = await RepositoryAsync("web-app");
        Configure(landing: null, required: false);
        var tree = await DoneSessionAsync(root, "s7", "q2");
        var head = await HeadAsync(tree);
        var (service, heard) = ReviewStandIn.Of(ReviewStandIn.Step());
        using var _ = service;
        var lines = new List<LandingLine>();
        service.LandingLined += lines.Add;
        var events = new SessionEvents(Path.Combine(_home, "sessions"));

        Assert.Null(await ReviewSetUps.PostAsync(service, Config(), events, _world.Quests["q2"], "s7", tree, "aurora", DateTimeOffset.UtcNow,
            CancellationToken.None));
        var said = await ReviewSetUps.PostAsync(service, Config(), events, ReviewStandIn.Step(), "s7", tree, "aurora",
            new DateTimeOffset(2026, 10, 8, 8, 59, 0, TimeSpan.Zero), CancellationToken.None);

        Assert.StartsWith($"Daoris read `{head[..8]}` from its tree and posted what it showed in `local`:", said);
        var (path, body) = Assert.Single(heard, each => each.Path.EndsWith("/set-up", StringComparison.Ordinal));
        Assert.Equal("/api/quests/q2/set-up", path);
        using var sent = System.Text.Json.JsonDocument.Parse(body);
        Assert.Equal((head, "local", "s7"),
            (sent.RootElement.GetProperty("commit").GetString(), sent.RootElement.GetProperty("kind").GetString(), sent.RootElement.GetProperty("session").GetString()));
        var shown = Assert.Single(lines);
        Assert.Equal(("review.shown", "local", (object?)60L), (shown.Event, Value(shown, "kind"), Value(shown, "seconds")));
        Assert.Contains(events.After("s7", 0).Events, e => e.Kind == SessionEventKind.Note && e.Text!.Contains("posted what it showed"));
    }

    // ——— the world and the fixtures

    private static readonly DateTimeOffset Verdicted = new(2026, 10, 9, 9, 30, 0, TimeSpan.Zero);

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

        // The second opinion's gate reads the host's opinions (XAGENT1f): no rule names a reviewer here, so none is asked.
        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult<OpinionView?>(null);
    }

    /// <summary>The press, as a door makes it: the gate read for the tree, and handed to the landing.</summary>
    private async Task<TreeLanding> PressAsync(string tree)
    {
        var trees = new SessionTrees(_home);
        var gate = await trees.GateAsync(tree, "q1", _world, "s1");
        return await trees.LandAsync(tree, new LandingSubject("s1", "q1", "Fix the gap"), gate: gate);
    }

    private async Task<IReadOnlyList<string>> LookAsync()
    {
        var lander = new AutoLander(_home, _world, new SessionTrees(_home), new SessionEvents(Path.Combine(_home, "sessions")), _lines.Add);
        var (chosen, said) = await lander.ChooseAsync(Config());
        return [.. said, .. await lander.LandAsync(chosen)];
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
    private void Review(string commit, string? words = null) =>
        _world.Quests["q2"] = _world.Quests["q2"] with
        {
            Held = false,
            Hold = null,
            Verdicts = [new QuestReviewVerdictView(ReviewVerdicts.Reviewed) { SetUpMachine = "desk", SetUpSequence = 41, Commit = commit, Words = words, At = Verdicted }],
        };

    private DriverConfig Config() => DriverConfig.Load(Path.Combine(_home, "driver.json"));

    /// <summary>The landing rule, and a review rule for the workspace and the repository: the repository's own, or its <c>false</c>.</summary>
    private void Configure(LandingRule? landing, bool required, bool repositoryNone = false)
    {
        var rule = new ReviewRule([new ReviewEnvironment("local", "local", "README.md", "http://localhost:4200")], required);
        var config = DriverConfig.Empty with
        {
            Reviews = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase) { ["web-app"] = repositoryNone ? ReviewRule.None : rule },
            WorkspaceReviews = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase) { ["aurora"] = rule },
        };
        (landing is null ? config : config.WithLanding("web-app", landing)).Save(Path.Combine(_home, "driver.json"));
    }

    /// <summary>A session that worked in its own tree and concluded on its quest's done; made due as the conclusion makes it, where asked.</summary>
    private async Task<string> DoneSessionAsync(string root, string session, string quest, bool due = false)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "web-app", "aurora");
        await CommitAsync(opened.Path, $"{session}.txt", $"the work of {session}", $"the work of {session}");
        _world.Quests[quest] = new QuestView(quest, "ask #a1", "web-app", "Fix the gap", "Fix it.", "Done");
        _world.Records.Add(new SessionRecord(session, "web-app", "completed") { Quest = quest, Tree = opened.Path, Created = DateTimeOffset.UtcNow });
        if (due)
        {
            Assert.Equal(AutoConcluded.Due, AutoLander.Concluded(_home, Config(), new SessionEvents(Path.Combine(_home, "sessions")), session,
                _world.Quests[quest], "Done", "completed", opened.Path, "aurora"));
        }

        return opened.Path;
    }

    private IReadOnlyList<SessionEvent> Notes(string session) =>
        [.. new SessionEvents(Path.Combine(_home, "sessions")).After(session, 0).Events.Where(e => e.Kind == SessionEventKind.Note)];

    private static object? Value(LandingLine line, string key) => line.Data.Single(pair => pair.Key == key).Value;

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
