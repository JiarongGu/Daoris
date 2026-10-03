using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAND2b (D145 point 2, design §2, §4, §5, §8): done work lands itself. A session whose record concludes on a done quest,
/// released, under a rule that accepts automatically, is due; the look lands it exactly as the review's Accept does — the
/// branch, its record, the rule's plugin and its tidy — and keeps who accepted it, each try and its code. A plugin that
/// cannot land here leaves the branch made; a refusal is tried again only when the tree's tip or status moves; a done with no
/// commits lands nothing.
/// </summary>
/// <remarks>
/// Real git, as the other tree tests, a stand-in for the service's quests and records, and a plugin faked on the wire's
/// channel: nothing reaches a network or a harness.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class AutoLandingTests : IDisposable
{
    private const string Plugin = "example.lands";

    private readonly string _scratch;
    private readonly string _home;
    private readonly World _world = new();
    private readonly List<object> _frames = [];
    private readonly List<LandingLine> _lines = [];

    public AutoLandingTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "auto-landing", Guid.NewGuid().ToString("N")[..8]);
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
    /// The whole act: a done session in its own tree under the switch is due when it concludes, and the next look lands it as the
    /// press would — the branch the pattern names, recorded as accepted automatically with the rule as it stood, the plugin
    /// told once, the rule's tidy behind D88's proof — and the conversation, the due list and the log each keep the try.
    /// </summary>
    [Fact]
    public async Task A_done_session_under_the_switch_lands_at_the_look_as_the_press_would()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true, Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1");

        var (chosen, said) = await Lander().ChooseAsync(Config());
        Assert.Empty(said);
        var choice = Assert.Single(chosen);
        var line = Assert.Single(await Lander().LandAsync(chosen));

        const string branch = "feature/q1-fix-the-gap";
        Assert.True(await BranchAsync(root, branch), line);
        Assert.StartsWith($"landing  session s1 (#q1 → engine): {LandingRules.AutoAccepted} its work is on `{branch}`.", line);
        var record = new LandedBranches(_home).Landing("s1")!;
        Assert.Equal(AcceptedBy.Auto, record.AcceptedBy);
        Assert.Equal(new LandedRule(Plugin, AutoAccept: true, LandingSource.Repository), record.Rule);
        Assert.True(record.Pushed);
        Assert.Equal("https://example.test/pull/7", record.PullRequest);
        Assert.Single(_frames);
        Assert.False(Directory.Exists(tree), "the rule's tidy took the tree, as it follows a press");

        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.NotNull(entry.Closed);
        var tried = Assert.Single(entry.Tries);
        Assert.Equal((AutoLandingCode.Landed, branch, 1, choice.Tip), (tried.Code, tried.Branch, tried.Commits, tried.Tip));

        var note = Notes("s1").Single(e => e.Text!.StartsWith(LandingRules.AutoAccepted, StringComparison.Ordinal));
        Assert.Equal(NoteCodes.LandingAccepted.Code, note.Parts![0].Code);
        Assert.Contains("Plugin `example.lands`: pushed it.", note.Text);

        var logged = Assert.Single(_lines);
        Assert.Equal("landing.auto", logged.Event);
        Assert.Equal(AutoLandingCode.Landed, Value(logged, "code"));
        Assert.Equal(Plugin, Value(logged, "plugin"));
        Assert.True(Value(logged, "pushed") is true);
        Assert.Equal(1, Value(logged, "commits"));

        // The review's note names who accepted it, and the next look finds nothing more to land.
        var review = await new SessionTrees(_home).LandedReviewAsync(root, record, changes: false);
        Assert.Contains("accepted automatically when its quest was done", LandedReviewWords.Describe("s1", review));
        Assert.Empty((await Lander().ChooseAsync(Config())).Chosen);
    }

    /// <summary>
    /// *To review* lists only what could not land (design §4): with no tidy the tree stays, and its work is on a branch of the
    /// person's now, so D88's proof finds nothing unaccepted in it.
    /// </summary>
    [Fact]
    public async Task A_landed_session_holds_nothing_to_review()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        var tree = await DoneSessionAsync(root, "s1", "q1");
        Assert.True((await new SessionTrees(_home).WorkAsync(tree))!.Holds);

        await LookAsync();

        Assert.True(Directory.Exists(tree));
        Assert.False((await new SessionTrees(_home).WorkAsync(tree))!.Holds);
        Assert.Contains("for the person to push", Instruction(root));
    }

    /// <summary>
    /// 🔴 A plugin that cannot land work here does not stop the branch (D145 point 2): nobody is there to fix it, so the branch is
    /// made and recorded, the plugin is never spoken to, and the sentence names the hand-off that pushes it later.
    /// </summary>
    [Fact]
    public async Task A_plugin_that_cannot_land_here_leaves_the_branch_made_and_names_the_hand_off()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        await DoneSessionAsync(root, "s1", "q1");

        var line = Assert.Single(await LookAsync());

        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"), line);
        Assert.Empty(_frames);
        Assert.Contains("not installed", line);
        Assert.Contains("`daoris-driver trees hand s1`", line);
        var record = new LandedBranches(_home).Landing("s1")!;
        Assert.False(record.Pushed);
        Assert.Equal(AcceptedBy.Auto, record.AcceptedBy);
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.PluginUnready, entry.Last!.Code);
        Assert.NotNull(entry.Closed);
        Assert.Equal(NoteCodes.LandingUnready.Code, Notes("s1").Last().Parts![0].Code);
    }

    /// <summary>A plugin that answers it did not push leaves the branch standing (D100), and the try keeps its code.</summary>
    [Fact]
    public async Task A_plugin_that_did_not_push_leaves_the_branch_and_is_kept_as_its_code()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        await DoneSessionAsync(root, "s1", "q1");

        var line = Assert.Single(await LookAsync(pushes: false));

        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Contains("did not push it", line);
        Assert.Equal(AutoLandingCode.PluginFailed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(Plugin, Value(_lines.Single(), "plugin"));
        Assert.True(Value(_lines.Single(), "pushed") is false);
    }

    /// <summary>
    /// Uncommitted work holds it (design §5), said with its count; nothing changed, no look tries it again; once the tree moves,
    /// the next look lands it.
    /// </summary>
    [Fact]
    public async Task Uncommitted_work_holds_it_and_it_is_tried_again_only_once_the_tree_moves()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        var tree = await DoneSessionAsync(root, "s1", "q1");
        await File.WriteAllTextAsync(Path.Combine(tree, "loose.txt"), "not committed\n");

        var first = await Lander().ChooseAsync(Config());
        Assert.Empty(first.Chosen);
        Assert.Contains("1 uncommitted path(s)", Assert.Single(first.Said));
        var held = new AutoLandings(_home).Of("s1")!;
        Assert.Equal((AutoLandingCode.Uncommitted, 1), (held.Last!.Code, held.Last.Uncommitted));
        Assert.Null(held.Closed);
        // Read back from the record, a count is a whole number as JSON keeps it.
        Assert.Equal(1L, Notes("s1").Last().Parts![0].Value("paths"));

        // Nothing moved: no try, no line, no note.
        var again = await Lander().ChooseAsync(Config());
        Assert.Empty(again.Chosen);
        Assert.Empty(again.Said);
        Assert.Single(new AutoLandings(_home).Of("s1")!.Tries);

        await CommitAsync(tree, "loose.txt", "now committed", "commit the loose file");
        Assert.Single(await LookAsync());
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
    }

    /// <summary>
    /// A done held for the person's yes waits (D133), said once; it lands at the look after the release (design §2).
    /// </summary>
    [Fact]
    public async Task A_held_done_waits_and_lands_at_the_look_after_its_release()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        await DoneSessionAsync(root, "s1", "q1", held: true);

        var first = await Lander().ChooseAsync(Config());
        Assert.Empty(first.Chosen);
        Assert.Contains("held for your yes", Assert.Single(first.Said));
        Assert.Empty((await Lander().ChooseAsync(Config())).Said);
        Assert.Single(new AutoLandings(_home).Of("s1")!.Tries);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));

        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false };
        Assert.Single(await LookAsync());
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
    }

    /// <summary>A done that made no commits lands nothing, and its conversation says so (design §2).</summary>
    [Fact]
    public async Task A_done_with_no_commits_lands_nothing_and_says_so()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        await DoneSessionAsync(root, "s1", "q1", commit: false);

        var line = Assert.Single(await LookAsync());

        Assert.Contains("made no commits", line);
        Assert.Empty((await GitAsync(root, "branch", "--list", "feature/*")).Trim());
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.Nothing, entry.Last!.Code);
        Assert.NotNull(entry.Closed);
        Assert.Null(new LandedBranches(_home).Landing("s1"));
    }

    /// <summary>
    /// A branch of the pattern's name that stands, and that Daoris did not make, is refused and kept (design §5); a look with
    /// nothing moved does not try it again, and a new commit does. One Daoris made moves on instead (the next case, LAND2c).
    /// </summary>
    [Fact]
    public async Task A_standing_branch_is_refused_kept_and_tried_again_only_on_a_change()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        var tree = await DoneSessionAsync(root, "s1", "q1");
        await GitAsync(root, "branch", "feature/q1-fix-the-gap");

        var line = Assert.Single(await LookAsync());
        Assert.Contains("`feature/q1-fix-the-gap` is already a branch", line);
        var refused = new AutoLandings(_home).Of("s1")!;
        Assert.Equal((AutoLandingCode.Exists, "feature/q1-fix-the-gap"), (refused.Last!.Code, refused.Last.Branch));
        Assert.Null(refused.Closed);

        Assert.Empty(await LookAsync());
        Assert.Single(new AutoLandings(_home).Of("s1")!.Tries);

        await CommitAsync(tree, "more.txt", "more work", "more work");
        Assert.Single(await LookAsync());
        Assert.Equal(2, new AutoLandings(_home).Of("s1")!.Tries.Count);
    }

    /// <summary>
    /// LAND2c (D145 §3, D149): a chain's later step whose done comes after the chain's first landing moves the chain's branch on
    /// at the look, kept as <c>advanced</c>; the rule's plugin is told the pull request it opened at the first landing, and who
    /// accepted the work, so the ticket keeps one pull request.
    /// </summary>
    [Fact]
    public async Task A_later_steps_done_moves_the_chains_branch_on_and_is_kept_as_advanced()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var first = await DoneSessionAsync(root, "s1", "q1");
        Assert.Single(await LookAsync());
        const string branch = "feature/q1-fix-the-gap";

        // Its verify step follows q1 and grows from s1's branch, as the planner grows it (CHAIN2).
        await DoneSessionAsync(root, "s2", "q2", parent: "q1", from: $"daoris/{Path.GetFileName(first)}");
        var line = Assert.Single(await LookAsync());

        Assert.StartsWith($"landing  session s2 (#q2 → engine): {LandingRules.AutoAccepted} its work is on `{branch}`.", line);
        Assert.Contains($"moved `{branch}` on from", line);
        var entry = new AutoLandings(_home).Of("s2")!;
        Assert.Equal((AutoLandingCode.Advanced, branch, 1), (entry.Last!.Code, entry.Last.Branch, entry.Last.Commits));
        Assert.NotNull(entry.Closed);
        Assert.Equal(2, _frames.Count);
        var told = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(_frames[1])).RootElement;
        Assert.Equal("https://example.test/pull/7", told.GetProperty("pullRequest").GetString());
        Assert.Equal(AcceptedBy.Auto, told.GetProperty("acceptedBy").GetString());
        var record = new LandedBranches(_home).Landing("s2")!;
        Assert.Equal("s1", record.Session);
        Assert.Equal("s2", Assert.Single(record.Advances).Session);
        Assert.Equal(AutoLandingCode.Advanced, Value(_lines[^1], "code"));
        Assert.Single((await GitAsync(root, "branch", "--list", "feature/*")).Trim().Split('\n'));
    }

    /// <summary>
    /// <c>ReviewableTree</c>'s rule: a live session in the tree is using it, so the look waits; a newer session on it stands for it,
    /// so the entry closes. The switch turned off closes it for the person's press, which, made meanwhile, reads as already landed.
    /// </summary>
    [Fact]
    public async Task A_tree_in_use_waits_a_newer_session_supersedes_and_the_switch_off_or_a_press_closes_it()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        var tree = await DoneSessionAsync(root, "s1", "q1");

        _world.Records.Add(new SessionRecord("s2", "engine", "working") { Quest = "q1", Tree = tree, Created = DateTimeOffset.UtcNow.AddMinutes(1) });
        var waiting = await Lander().ChooseAsync(Config());
        Assert.Empty(waiting.Chosen);
        Assert.Empty(waiting.Said);

        _world.Records[^1] = _world.Records[^1] with { State = "completed" };
        Assert.Contains("a newer session went on in its tree", Assert.Single((await Lander().ChooseAsync(Config())).Said));
        Assert.Equal(AutoLandingCode.Superseded, new AutoLandings(_home).Of("s1")!.Last!.Code);

        // Off: the person's press is the way again.
        _world.Records.RemoveAt(_world.Records.Count - 1);
        var second = await DoneSessionAsync(root, "s3", "q3");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        Assert.Contains("no longer accepts automatically", Assert.Single((await Lander().ChooseAsync(Config())).Said));
        Assert.Equal(AutoLandingCode.Off, new AutoLandings(_home).Of("s3")!.Last!.Code);

        // A press made while it waited lands it: the look reads it as already landed.
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        var fourth = await DoneSessionAsync(root, "s4", "q4");
        var pressed = await new SessionTrees(_home).LandAsync(fourth, new LandingSubject("s4", "q4", "Fix the gap"));
        Assert.True(pressed.Landed, pressed.Message);
        Assert.Equal(AcceptedBy.Person, new LandedBranches(_home).Landing("s4")!.AcceptedBy);
        Assert.Contains("already on a branch of yours", Assert.Single((await Lander().ChooseAsync(Config())).Said));
        Assert.Equal(AutoLandingCode.Already, new AutoLandings(_home).Of("s4")!.Last!.Code);
        Assert.True(Directory.Exists(second));
    }

    /// <summary>
    /// The session is told (D145 point 1): under the switch its work lands when it closes the quest done, pushed where a plugin
    /// is named, and uncommitted work does not land. Without the switch the instruction is byte for byte today's.
    /// </summary>
    [Fact]
    public async Task The_sessions_instruction_says_its_work_lands_when_it_closes_the_quest_done()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();

        var said = Instruction(root);
        Assert.Contains("This work is accepted automatically. When you close the quest done, this tree's branch is put on "
            + "`feature/q9-fix-the-gap`, pushed, and a pull request opened from it, where the work is judged.", said);
        Assert.Contains("uncommitted work does not land", said);

        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin));
        Assert.Contains("This work goes through review. When it is done and the person accepts it", Instruction(root));
    }

    // ——— the world, the plugin and the fixtures

    /// <summary>The service's quests and records, standing in.</summary>
    private sealed class World : IAutoLandingWorld
    {
        public Dictionary<string, QuestView> Quests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<SessionRecord> Records { get; } = [];

        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult(Quests.GetValueOrDefault(id));

        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([.. Records]);

        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<string>>(Records.Where(r => r.Live && r.Tree is not null).Select(r => r.Tree!).ToHashSet());
    }

    private sealed class FakeLander(Func<object, PluginLanding> land) : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.Land];
        public bool Alive => true;
        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);
        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;
        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) => Task.FromResult(land(payload));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private bool _pushes = true;

    private AutoLander Lander() => new(
        _home, _world,
        new SessionTrees(_home, new LandingPlugins(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(new FakeLander(frame =>
        {
            _frames.Add(frame);
            return _pushes
                ? new PluginLanding(Plugin, true, "https://example.test/pull/7", "pushed it.")
                : new PluginLanding(Plugin, false, null, "gh is not signed in — run `gh auth login`.");
        })))),
        new SessionEvents(Path.Combine(_home, "sessions")), _lines.Add);

    /// <summary>One look: what it chose, then what landing those beside it said.</summary>
    private async Task<IReadOnlyList<string>> LookAsync(bool pushes = true)
    {
        _pushes = pushes;
        var lander = Lander();
        var (chosen, said) = await lander.ChooseAsync(Config());
        return [.. said, .. await lander.LandAsync(chosen)];
    }

    private DriverConfig Config() => DriverConfig.Load(Path.Combine(_home, "driver.json"));

    /// <summary>A session that worked in its own tree and concluded on its quest's done, made due as the driver's conclusion makes it.</summary>
    /// <param name="parent">The quest it follows, where it is a chain's later step.</param>
    /// <param name="from">The session branch its tree grows from, as a chain's step grows (CHAIN2).</param>
    private async Task<string> DoneSessionAsync(
        string root, string session, string quest, bool held = false, bool commit = true, string? parent = null, string? from = null)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora", from: from);
        if (commit) await CommitAsync(opened.Path, $"{session}.txt", $"the work of {session}", $"the work of {session}");
        _world.Quests[quest] = new QuestView(quest, "game", "engine", "Fix the gap", "Fix it.", "Done") { Held = held, Parent = parent };
        _world.Records.Add(new SessionRecord(session, "engine", "completed") { Quest = quest, Tree = opened.Path, Created = DateTimeOffset.UtcNow });
        var verdict = AutoLander.Concluded(_home, Config(), new SessionEvents(Path.Combine(_home, "sessions")), session, _world.Quests[quest],
            "Done", "completed", opened.Path, "aurora");
        Assert.Equal(Config().Landings.GetValueOrDefault("engine")?.AutoAccept == true ? AutoConcluded.Due : null, verdict);
        return opened.Path;
    }

    /// <summary>What a session in this repository's tree would be told of its landing, as a start composes it.</summary>
    private string Instruction(string root)
    {
        var trees = new SessionTrees(_home);
        var tree = trees.OpenAsync(root, "engine", "aurora").GetAwaiter().GetResult();
        var plan = trees.PlanAsync(tree.Path, new LandingSubject("s9", "q9", "Fix the gap")).GetAwaiter().GetResult();
        var quest = new QuestView("q9", "game", "engine", "Fix the gap", "Fix it.", "Open");
        return TargetPrompt.Compose(SessionTarget.ForQuest(quest, tree.Path, "http://stand-in") with { LandsOn = plan });
    }

    private IReadOnlyList<SessionEvent> Notes(string session) =>
        [.. new SessionEvents(Path.Combine(_home, "sessions")).After(session, 0).Events.Where(e => e.Kind == SessionEventKind.Note)];

    private static object? Value(LandingLine line, string key) => line.Data.Single(pair => pair.Key == key).Value;

    private void Rule(LandingRule rule) =>
        DriverConfig.Empty.WithLanding("engine", rule).Save(Path.Combine(_home, "driver.json"));

    private void InstallManifest()
    {
        var folder = Path.Combine(_home, "plugins", Plugin);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"),
            """{ "id": "example.lands", "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] } }""");
    }

    private static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

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
        // 🔴 Both streams are read at once, before the wait: read one after the other, a git that writes more than a pipe's
        // worth of warnings to the second blocks on it while the first is still being read (FIX-LOG 2026-10-04).
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
