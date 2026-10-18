using System.Diagnostics;
using System.Text.Json;
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
[Trait(Category.Parallel, Category.Git)]
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
    /// ACCEPTALL1f (D167's 2026-10-15 amendment point 2): the look lands a part of an entry with others as the press does. Two parts of one
    /// entry that ran alongside, each in a tree of its own, each done: both land on the entry's one branch, named for its first part, the
    /// other joining it by a replay onto its tip, so the entry has one pull request. Before, each made a branch for its own quest.
    /// </summary>
    [Fact]
    public async Task An_entrys_alongside_parts_land_at_done_on_the_entrys_one_branch()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        await DoneSessionAsync(root, "s1", "q1", entry: "ask:a3/engine");
        await DoneSessionAsync(root, "s2", "q2", entry: "ask:a3/engine");

        var lines = await LookAsync();

        const string branch = "feature/q1-fix-the-gap";
        Assert.Equal(2, lines.Count);
        Assert.Equal(branch, (await GitAsync(root, "branch", "--list", "feature/*")).Trim().TrimStart('*', ' '));
        Assert.Equal("2", (await GitAsync(root, "rev-list", "--count", $"main..{branch}")).Trim());
        var due = new AutoLandings(_home);
        Assert.Equal([AutoLandingCode.Advanced, AutoLandingCode.Landed], new[] { "s1", "s2" }.Select(id => due.Of(id)!.Last!.Code).Order(StringComparer.Ordinal));
        Assert.All(new[] { "s1", "s2" }, id => Assert.Equal(branch, new LandedBranches(_home).Landing(id)!.Branch));
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

    /// <summary>
    /// SQUASHTIDY1f: a due session whose work a squash merge put on the line before the look lands nothing. The landing the look
    /// runs is the press's own, which refuses it by its own code in the head's clause; the code closes the entry, the look's line
    /// says the sentence, and no branch, record or plugin call is made. Its tree stays, for its discard.
    /// </summary>
    [Fact]
    public async Task A_due_session_the_line_holds_by_content_is_not_landed_and_its_entry_closes()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var branch = (await GitAsync(tree, "rev-parse", "--abbrev-ref", "HEAD")).Trim();
        await GitAsync(root, "merge", "--squash", branch);
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", $"{branch} (squashed)");

        var line = Assert.Single(await LookAsync());

        Assert.Equal(
            "landing  session s1 (#q1 → engine): Its work is on `main` by content (a squash merge), so it is not landed again: that "
            + "would make a second copy of it. Discard its tree instead.",
            line);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Null(new LandedBranches(_home).Landing("s1"));
        Assert.Empty(_frames);
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.NotNull(entry.Closed);
        Assert.Equal(AutoLandingCode.Carried, entry.Last!.Code);
        Assert.Equal(AutoLandingCode.Carried, Value(Assert.Single(_lines), "code"));
        Assert.True(Directory.Exists(tree));
        Assert.Empty((await Lander().ChooseAsync(Config())).Chosen);
    }

    // ——— under a named workflow (WORKFLOW1f)

    /// <summary>
    /// WORKFLOW1f (the workflow design §2.3, §4.5): whether done work lands with no press is its run's bound version's landing step. A
    /// version that accepts automatically makes the done due and the look lands it, where Current's rule waits for the person's
    /// press; the record keeps the version, the rule as the version made it, and who accepted it.
    /// </summary>
    [Fact]
    public async Task A_version_that_accepts_automatically_lands_at_the_look_where_Current_waits_for_your_press()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        Follow("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic"}]""");
        var tree = await NamedDoneAsync(root, "s1", "q1");

        var said = await LookAsync();

        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"), string.Join("\n", said));
        var record = new LandedBranches(_home).Landing("s1")!;
        Assert.Equal(AcceptedBy.Auto, record.AcceptedBy);
        Assert.Equal(new LandedRule(null, AutoAccept: true, LandingSource.Repository), record.Rule);
        Assert.Equal(new LandingWorkflow("docs-to-pr", 1, WorkflowLevels.Repository), record.Workflow);
        Assert.Contains("It landed as `docs-to-pr` v1 says, `engine`'s default.", Assert.Single(said));
        Assert.True(Directory.Exists(tree));
    }

    /// <summary>
    /// WORKFLOW1f (design §2.3, §4.6): a version requiring a second opinion holds the look's landing where Current, whose rule reads
    /// only before each next step, would land it; the session stays due as <c>opinion</c>, said once.
    /// </summary>
    [Fact]
    public async Task A_version_requiring_an_opinion_holds_where_Current_would_land()
    {
        var root = await RepositoryAsync("engine");
        (DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true)) with
        {
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase) { ["engine"] = new(["codex-acp"], [OpinionRules.Steps]) },
        }).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        Follow("""[{"id":"work","kind":"work"},{"id":"opinion","kind":"opinion","required":true},{"id":"land","kind":"landing","form":"branch","accept":"automatic"}]""");
        await NamedDoneAsync(root, "s1", "q1");

        var first = await LookAsync();

        Assert.StartsWith("landing  session s1 (#q1 → engine): not accepted automatically: its work waits for a second opinion", Assert.Single(first));
        Assert.Equal(AutoLandingCode.Opinion, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Empty(await LookAsync());
    }

    /// <summary>
    /// WORKFLOW1f (design §4.4): a kind's workflow that lands with no press, where the work would otherwise wait for the person's,
    /// checks the paths the chain changed against the kind's. Outside them the look holds it, said once, until the person keeps the
    /// workflow for this work (`workflow keep`); then the next look lands it. Nothing switches by itself.
    /// </summary>
    [Fact]
    public async Task A_kinds_paths_hold_the_look_until_you_keep_its_workflow()
    {
        var root = await RepositoryAsync("engine");
        var config = WorkflowSelection.Apply(
            DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}")),
            new WorkflowDeclare("docs", "aurora", "Documentation", ["docs/**"]));
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        Follow("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic"}]""", kind: "docs");
        var tree = await NamedDoneAsync(root, "s1", "q1");

        var held = Assert.Single(await LookAsync());
        Assert.StartsWith("landing  session s1 (#q1 → engine): not accepted automatically: the landing was refused, as follows. It waits for "
            + "your review. Holds: this work changed 1 path outside Documentation's (`docs/**`): `s1.txt`; and `docs-to-pr` v1, chosen for it, "
            + "asks less of you than Current would without the kind (it lands with no press of yours).", held);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));
        var logged = Assert.Single(_lines, line => line.Event == "workflow.held");
        Assert.Equal((WorkflowGateStates.KindPaths, (object?)1), (Value(logged, "code"), Value(logged, "outside")));
        Assert.Empty(await LookAsync());

        var (done, message) = await WorkflowKeepCommand.KeepAsync(
            _home, "s1", tree, "q1", _world, "the gap is in the docs build", ReviewDoors.Terminal, DateTimeOffset.UtcNow);
        Assert.True(done, message);
        Assert.Contains("you kept it for this work, though it changed paths outside its kind's, saying: \"the gap is in the docs build\"", message);

        Assert.Single(await LookAsync());
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Equal(new LandingWorkflow("docs-to-pr", 1, WorkflowLevels.WorkspaceKind) { Kind = "docs" }, new LandedBranches(_home).Landing("s1")!.Workflow);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Keep_refuses_when_the_binding_disappears_or_breaks_after_the_gate_read(bool corrupt)
    {
        var root = await RepositoryAsync("engine");
        WorkflowSelection.Apply(
            DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}")),
            new WorkflowDeclare("docs", "aurora", "Documentation", ["docs/**"]))
            .Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        Follow("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic"}]""", kind: "docs");
        var tree = await NamedDoneAsync(root, "s1", "q1");
        var binding = WorkflowRunBindings.PathOf(_home, "q1");
        var reads = 0;
        _world.ReadingQuests = () =>
        {
            // Process read first; the review reads the chain after the workflow's path hold was judged.
            if (++reads != 2) return;
            if (corrupt) File.WriteAllText(binding, "{broken");
            else File.Delete(binding);
        };

        var (done, message) = await WorkflowKeepCommand.KeepAsync(
            _home, "s1", tree, "q1", _world, "keep these paths", ReviewDoors.Terminal, DateTimeOffset.UtcNow);

        Assert.False(done, message);
        Assert.Contains("nothing was kept", message);
        Assert.Contains("Holds: this work changed 1 path outside Documentation's", message);
        Assert.Null(WorkflowRunBindings.Read(_home, "q1"));
        Assert.Equal(corrupt, File.Exists(binding));
        if (corrupt) Assert.Equal("{broken", File.ReadAllText(binding));
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));
    }

    /// <summary>
    /// A named workflow `docs-to-pr` saved here with one version and chosen as `engine`'s default (or, with a kind, the workspace's for
    /// it), and run q1 bound to it as its first start binds it (WORKFLOW1e), over the rule <see cref="Rule"/> wrote.
    /// </summary>
    private void Follow(string steps, string? kind = null)
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded(
            "docs-to-pr", "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z", System.Text.Json.Nodes.JsonNode.Parse(steps)), kept: []);
        var use = kind is null
            ? new WorkflowUse("docs-to-pr", "engine", null, null)
            : new WorkflowUse("docs-to-pr", null, "aurora", kind);
        WorkflowSelection.Apply(Config(), use).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var task = kind is null ? null : new WorkflowTaskChoice(kind, null);
        Assert.True(WorkflowRunBindings.Bind(_home, WorkflowRunBindings.Plan(Config(), _home, "q1", "engine", "aurora", null, task, [], DateTimeOffset.UtcNow)));
    }

    /// <summary>A done session in its own tree, concluded as the driver concludes one: its run's process read, then the switch.</summary>
    private async Task<string> NamedDoneAsync(string root, string session, string quest)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, $"{session}.txt", $"the work of {session}", $"the work of {session}");
        _world.Quests[quest] = new QuestView(quest, "game", "engine", "Fix the gap", "Fix it.", "Done");
        _world.Records.Add(new SessionRecord(session, "engine", "completed") { Quest = quest, Tree = opened.Path, Created = DateTimeOffset.UtcNow });
        var process = await AutoLander.ProcessAsync(_home, Config(), _world, _world.Quests[quest], opened.Path, "aurora");
        Assert.True(process!.Named);
        Assert.Equal(AutoConcluded.Due, AutoLander.Concluded(_home, Config(), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), session,
            _world.Quests[quest], "Done", "completed", opened.Path, "aurora", process: process));
        return opened.Path;
    }

    // ——— FABRIC-END7's review: what lands is what was judged

    /// <summary>
    /// Finding 1: each chosen entry is judged again from fresh facts just before its own landing. A look chooses two; the person holds the
    /// second's departure before the pass reaches it, so the first lands and the second is kept as held, with no branch made for it.
    /// </summary>
    [Fact]
    public async Task A_chosen_entry_is_judged_again_just_before_its_own_landing()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        await DoneSessionAsync(root, "s1", "q1");
        await DoneSessionAsync(root, "s2", "q2");
        var lander = Lander();
        var (chosen, said) = await lander.ChooseAsync(Config());
        Assert.Empty(said);
        Assert.Equal(2, chosen.Count);

        _world.Quests["q2"] = _world.Quests["q2"] with { Held = true };
        var landed = await lander.LandAsync(chosen);

        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.False(await BranchAsync(root, "feature/q2-fix-the-gap"), "a hold that came after the choice holds it");
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(AutoLandingCode.Held, new AutoLandings(_home).Of("s2")!.Last!.Code);
        Assert.Contains(landed, line => line.StartsWith("landing  session s2", StringComparison.Ordinal));
    }

    /// <summary>
    /// Finding 1's pin: a landing whose gate names the tip it was judged at lands exactly that commit. A commit the tree gained after the
    /// judgement is refused, not landed unseen, and nothing is made; pinned at the tip it stands at, it lands that commit.
    /// </summary>
    [Fact]
    public async Task A_landing_pinned_to_the_tip_judged_refuses_a_tree_that_moved_past_it()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var trees = new SessionTrees(_home);
        var judged = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        var gate = await trees.GateAsync(tree, "q1", _world, "s1") with { Tip = judged };
        await CommitAsync(tree, "late.txt", "made after the judgement", "a late change");

        var moved = await trees.LandAsync(tree, new LandingSubject("s1", "q1", "Fix the gap"), acceptedBy: AcceptedBy.Auto, gate: gate);

        Assert.False(moved.Landed, moved.Message);
        Assert.Equal(AutoLandingCode.Refused, moved.Refusal);
        Assert.Contains("moved", moved.Message);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"), "nothing is made from a tip nobody judged");

        var now = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        var landed = await trees.LandAsync(tree, new LandingSubject("s1", "q1", "Fix the gap"), acceptedBy: AcceptedBy.Auto, gate: gate with { Tip = now });
        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(now, (await GitAsync(root, "rev-parse", "feature/q1-fix-the-gap")).Trim());
    }

    /// <summary>
    /// LANDRACE1 (D145 point 2, D173's FABRIC-END7 note): the person presses Accept while the automatic landing of the same due entry
    /// is still in its plugin. The press waits for the entry's hold, finds the landing recorded while it waited, and says so: the plugin
    /// is spoken to once, and one landing is recorded. A tree on a branch of its own (TREEFOLLOW1) is the case git's ref writes never
    /// kept apart, since no ref is made for it: both doors landed it, and both pushed.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_press_beside_the_automatic_landing_of_one_entry_waits_for_it_and_lands_nothing_more(bool ownBranch)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", commit: !ownBranch);
        if (ownBranch) await OwnBranchAsync(tree);

        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        Assert.Single(chosen);
        Task<TreeLanding>? press = null;
        var waits = new List<bool>();
        // The plugin's first frame: the person presses while the automatic landing's plugin is still pushing. The press is its own
        // request, so nothing of the landing's flow (its look, its table of git's answers) flows into it.
        _landing = () =>
        {
            _landing = null;
            using (ExecutionContext.SuppressFlow())
            {
                press = Task.Run(() =>
                {
                    // LANDHOLD2: the press says it waits behind the landing, and that it has stopped.
                    using var told = LandingHoldNotice.Told(waiting => { lock (waits) waits.Add(waiting); });
                    return new SessionTrees(_home, Plugins()).LandAsync(tree, new LandingSubject("s1", "q1", "Fix the gap"));
                });
            }

            SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, tree) > 0 || press.IsCompleted, TimeSpan.FromSeconds(30));
        };

        await lander.LandAsync(chosen);
        var pressed = await press!;

        lock (waits) Assert.Equal([true, false], waits);
        Assert.Single(_frames);
        Assert.Single(new LandedBranches(_home).Entries(), entry => entry.Names("s1"));
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.False(pressed.Landed, pressed.Message);
        Assert.Equal(AutoLandingCode.Already, pressed.Refusal);
        Assert.Contains($"`{(ownBranch ? "feature/own" : "feature/q1-fix-the-gap")}`", pressed.Message);
        Assert.Contains("accepted automatically", pressed.Message);
    }

    /// <summary>
    /// LANDRACE1's review, finding 1: a press that waited is `already` only where the commit it would land is the one landed. The agent
    /// commits again on its own branch while the automatic landing's plugin pushes the first commit; the person presses for the new
    /// one, waits, and lands it: the plugin is told twice, each time its own commit.
    /// </summary>
    [Fact]
    public async Task A_press_for_a_later_commit_lands_it_after_the_landing_it_waited_behind()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", commit: false);
        await OwnBranchAsync(tree);
        var first = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        Task<TreeLanding>? press = null;
        _landing = () =>
        {
            _landing = null;
            CommitAsync(tree, "more.txt", "made while the plugin pushed", "more work").GetAwaiter().GetResult();
            using (ExecutionContext.SuppressFlow())
            {
                press = Task.Run(() => new SessionTrees(_home, Plugins()).LandAsync(tree, new LandingSubject("s1", "q1", "Fix the gap")));
            }

            SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, tree) > 0 || press.IsCompleted, TimeSpan.FromSeconds(30));
        };

        await lander.LandAsync(chosen);
        var pressed = await press!;

        var second = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        Assert.True(pressed.Landed, pressed.Message);
        Assert.Equal([first, second], _frames.Select(frame => Json(frame).GetProperty("tip").GetString()));
        Assert.Equal(second, new LandedBranches(_home).Landing("s1")!.Tip);
    }

    /// <summary>
    /// LANDRACE1's review, finding 1: a press that waited behind a landing of the same commit is told what that landing's plugin answered,
    /// its failure included, rather than a bare `already`: the branch stands, and the sentence says how it is pushed.
    /// </summary>
    [Fact]
    public async Task A_press_behind_a_landing_whose_plugin_failed_is_told_the_failure()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        Task<TreeLanding>? press = null;
        _answer = () => new PluginLanding(Plugin, false, null, "gh is not signed in — run `gh auth login`", Failed: true);
        _landing = () =>
        {
            _landing = null;
            using (ExecutionContext.SuppressFlow())
            {
                press = Task.Run(() => new SessionTrees(_home, Plugins()).LandAsync(tree, new LandingSubject("s1", "q1", "Fix the gap")));
            }

            SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, tree) > 0 || press.IsCompleted, TimeSpan.FromSeconds(30));
        };

        await lander.LandAsync(chosen);
        var pressed = await press!;

        Assert.Single(_frames);
        Assert.False(pressed.Landed, pressed.Message);
        Assert.Equal(AutoLandingCode.Already, pressed.Refusal);
        Assert.True(pressed.Plugin is { Failed: true }, pressed.Message);
        Assert.Contains("gh is not signed in", pressed.Message);
        Assert.Contains("`feature/q1-fix-the-gap`", pressed.Message);
    }

    /// <summary>
    /// LANDRACE1's review, finding 4: a hand-off of the branch a landing just recorded, while that landing's plugin still pushes, waits for
    /// the push and hands nothing more on: the plugin is told once, and the hand-off says the branch was pushed at this commit. Its
    /// re-check: a terminal's hand-off from a linked worktree of the repository (a registry may name one) takes the same hold as the
    /// landing, which names the repository by its main checkout.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_hand_off_beside_a_landings_push_waits_for_it_and_hands_nothing_more_on(bool fromLinked)
    {
        var main = await RepositoryAsync("engine");
        var root = main;
        if (fromLinked)
        {
            root = Path.Combine(_scratch, "engine-linked");
            await GitAsync(main, "worktree", "add", "--quiet", "--detach", root);
        }

        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        await DoneSessionAsync(main, "s1", "q1");
        const string branch = "feature/q1-fix-the-gap";
        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        Task<TreeHand>? hand = null;
        _landing = () =>
        {
            _landing = null;
            var recorded = new LandedBranches(_home).Of("engine", branch)!;
            using (ExecutionContext.SuppressFlow())
            {
                hand = Task.Run(() => new SessionTrees(_home, Plugins()).HandAsync(root, recorded));
            }

            SpinWait.SpinUntil(() => LandingHolds.WaitingOn(_home, root, branch) > 0 || hand.IsCompleted, TimeSpan.FromSeconds(30));
        };

        await lander.LandAsync(chosen);
        var handed = await hand!;

        Assert.Single(_frames);
        Assert.False(handed.Handed, handed.Message);
        Assert.Contains("pushed", handed.Message);
        Assert.True(new LandedBranches(_home).Of("engine", branch)!.Pushed);
    }

    /// <summary>
    /// LANDPIN2 (D164 point 4, D173's FABRIC-END7 note): a tree on a branch of its own has no ref made for it, so its plugin is handed the
    /// commit judged to push, not the branch's name alone. Its agent commits again after the judgement, as the plugin starts: the frame
    /// still names the commit judged, which the record keeps as the one landed.
    /// </summary>
    [Fact]
    public async Task A_tree_on_its_own_branch_hands_its_plugin_the_commit_judged_not_the_branch_as_it_stands()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", commit: false);
        await OwnBranchAsync(tree);
        var judged = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        // The plugin's process starting: the agent commits on its branch after the landing judged it, before its plugin pushes.
        _starting = () =>
        {
            _starting = null;
            CommitAsync(tree, "late.txt", "made after the judgement", "a late change").GetAwaiter().GetResult();
        };

        await lander.LandAsync(chosen);

        var frame = Json(Assert.Single(_frames));
        Assert.Equal("feature/own", frame.GetProperty("branch").GetString());
        Assert.Equal(judged, frame.GetProperty("tip").GetString());
        Assert.NotEqual(judged, (await GitAsync(tree, "rev-parse", "HEAD")).Trim());
        Assert.Equal(judged, new LandedBranches(_home).Landing("s1")!.Tip);
    }

    // ——— FABRIC-CLOSE1h: the landing on an unheld submission (the fabric design §E5, D173's note)

    /// <summary>
    /// The row's proof. In a closure graph Daoris writes no Done before the graph's end, so the landing starts at the agent's
    /// <c>ready</c>: a bound quest's unheld submission is due at its conclusion and lands its branch at the look, the quest still taken,
    /// and a held one lands nothing.
    /// </summary>
    [Fact]
    public async Task A_bound_quests_unheld_submission_lands_its_branch_and_a_held_one_does_not()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        await DoneSessionAsync(root, "s1", "q1", submitted: true);
        await DoneSessionAsync(root, "s2", "q2", held: true, submitted: true);

        var said = await LookAsync();

        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"), string.Join("\n", said));
        Assert.False(await BranchAsync(root, "feature/q2-fix-the-gap"), "a held submission lands nothing");
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(AutoLandingCode.Held, new AutoLandings(_home).Of("s2")!.Last!.Code);
        Assert.Equal(AcceptedBy.Auto, new LandedBranches(_home).Landing("s1")!.AcceptedBy);
    }

    /// <summary>
    /// Each hold the service computes on a submission holds its landing as it holds a done's (§E5: departed, evidence unread or missing),
    /// said once; its release lets the work go on, and it lands with no Done written.
    /// </summary>
    [Theory]
    [InlineData(EvidenceCodes.Departed)]
    [InlineData(EvidenceCodes.Unread)]
    [InlineData(EvidenceCodes.MissingHold)]
    public async Task A_held_submission_waits_and_lands_at_the_look_after_its_release_with_no_done(string hold)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        await DoneSessionAsync(root, "s1", "q1", held: true, submitted: true);
        _world.Quests["q1"] = _world.Quests["q1"] with { Hold = hold };

        Assert.Single(await LookAsync());
        Assert.Empty(await LookAsync());
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));

        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false, Hold = null };
        Assert.Single(await LookAsync());
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Equal(("Taken", 1L), (_world.Quests["q1"].Status, _world.Quests["q1"].Submission!.Sequence));
    }

    /// <summary>
    /// A hold racing the landing (the pre-mortem's first trap): the landing is judged again once it holds its tree, since LANDRACE1's wait
    /// for another landing of the tree may be long. The person's hold, a reopen of the submission, or a later <c>ready</c> that came while
    /// it waited stops it before anything is made: no branch, no push, no record. A hold on a plain done that waited is judged again too.
    /// A later <c>ready</c> keeps no try, so the next look judges the new submission from the start, and lands it.
    /// </summary>
    [Theory]
    [InlineData("held-done")]
    [InlineData("held")]
    [InlineData("reopened")]
    [InlineData("replaced")]
    public async Task A_hold_or_reopen_while_the_landing_waits_for_its_tree_stops_it_before_anything_is_made(string change)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", submitted: change != "held-done");
        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        Assert.Single(chosen);

        Task<IReadOnlyList<string>> landing;
        using (var holding = await LandingHolds.TakeAsync(_home, tree))
        {
            Assert.NotNull(holding);
            using (ExecutionContext.SuppressFlow())
            {
                landing = Task.Run(() => lander.LandAsync(chosen));
            }

            Assert.True(SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, tree) > 0, TimeSpan.FromSeconds(30)), "the landing waits for its tree");
            var quest = _world.Quests["q1"];
            _world.Quests["q1"] = change switch
            {
                "reopened" => quest with { Submission = null },
                "replaced" => quest with { Submission = Made(2) },
                _ => quest with { Held = true, Hold = EvidenceCodes.Departed },
            };
        }

        var said = await landing;

        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"), string.Join("\n", said));
        Assert.Empty(_frames);
        Assert.Null(new LandedBranches(_home).Landing("s1"));
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(change switch { "reopened" => AutoLandingCode.Undone, "replaced" => null, _ => AutoLandingCode.Held }, entry.Last?.Code);
        if (change != "replaced") return;

        Assert.Null(entry.Closed);
        Assert.Single(await LookAsync());
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Single(_frames);
    }

    /// <summary>
    /// The reverse order, against the landing's commitment point (the parent's default, D173's note): a hold that comes once the landing
    /// was judged again under its tree's hold finds it going on. The branch is made and pushed, once; the hold applies to what follows,
    /// and nothing already pushed is taken back or pushed again.
    /// </summary>
    [Fact]
    public async Task A_hold_after_the_landing_was_judged_under_its_hold_applies_to_what_follows_and_retracts_nothing()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        await DoneSessionAsync(root, "s1", "q1", submitted: true);
        _landing = () =>
        {
            _landing = null;
            _world.Quests["q1"] = _world.Quests["q1"] with { Held = true, Hold = EvidenceCodes.Departed };
        };

        await LookAsync();

        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Single(_frames);
        Assert.True(new LandedBranches(_home).Landing("s1")!.Pushed);
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.Landed, entry.Last!.Code);
        Assert.NotNull(entry.Closed);
        Assert.Empty((await Lander().ChooseAsync(Config())).Chosen);
        Assert.Single(_frames);
    }

    /// <summary>
    /// A landing never implies completion (the pre-mortem's fourth trap): the branch is made and its push fails. The quest stays taken at
    /// its submission, since a landing writes no quest state (D46), the push's way on stays the person's hand-off, and a restart's look
    /// lands nothing twice and keeps the landing's record.
    /// </summary>
    [Fact]
    public async Task A_submissions_landing_whose_push_fails_leaves_the_quest_taken_and_a_restart_lands_nothing_twice()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        await DoneSessionAsync(root, "s1", "q1", submitted: true);

        var line = Assert.Single(await LookAsync(pushes: false));

        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"), line);
        Assert.Equal(AutoLandingCode.PluginFailed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(("Taken", 1L), (_world.Quests["q1"].Status, _world.Quests["q1"].Submission!.Sequence));
        Assert.False(new LandedBranches(_home).Landing("s1")!.Pushed);

        // A restart: a new lander over the same home.
        Assert.Empty((await Lander().ChooseAsync(Config())).Chosen);
        Assert.Single(_frames);
        Assert.Equal("feature/q1-fix-the-gap", new LandedBranches(_home).Landing("s1")!.Branch);
    }

    // ——— FABRIC-CLOSE1h2: a hold between a landing's branch and its push (D173's note)

    /// <summary>
    /// The row's proof. The automatic landing is judged once more just before its plugin is told, since the owner's default (D173's
    /// FABRIC-CLOSE1h note) is that a hold stops a landing up to the moment its push begins. A hold that comes while the push waits
    /// behind another push of its branch leaves the branch made and recorded and tells the plugin nothing: the entry stays open as
    /// <c>push-held</c>, says nothing more while the hold stands, and the first look after the release hands the branch on, one push,
    /// never a second branch. A plain done is judged there only where its push waited; a submission every time.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_hold_while_the_push_waits_for_its_branch_leaves_the_branch_made_and_pushes_it_once_after_the_release(bool submitted)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        await DoneSessionAsync(root, "s1", "q1", submitted: submitted);
        var said = await HeldAtThePushAsync(root, quest => quest with { Held = true, Hold = EvidenceCodes.Departed });

        Assert.True(await BranchAsync(root, PushBranch), string.Join("\n", said));
        Assert.Empty(_frames);
        Assert.False(new LandedBranches(_home).Landing("s1")!.Pushed);
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.PushHeld, entry.Last!.Code);
        Assert.Null(entry.Closed);
        Assert.Contains("nothing was pushed", Assert.Single(said));

        // While the hold stands, a look hands nothing on and says nothing more.
        Assert.Empty(await LookAsync());
        Assert.Empty(_frames);

        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false, Hold = null };
        var handed = Assert.Single(await LookAsync());

        var frame = Json(Assert.Single(_frames));
        Assert.Equal(PushBranch, frame.GetProperty("branch").GetString());
        Assert.True(new LandedBranches(_home).Landing("s1")!.Pushed, handed);
        entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.Landed, entry.Last!.Code);
        Assert.NotNull(entry.Closed);
        Assert.Equal([$"refs/heads/{PushBranch}"], await FeatureBranchesAsync(root));
        Assert.Empty((await Lander().ChooseAsync(Config())).Chosen);
        Assert.Single(_frames);
    }

    /// <summary>
    /// A reopen that comes as the push waits holds it the same way, and the work that goes on after it is not lost to a hand-off of the
    /// branch as it was made: once the quest stands at a later submission and its tree has moved past the commit landed, the look lands
    /// it as any later done of the session lands, moving the made branch on, and its plugin is told once, at the tree's tip.
    /// </summary>
    [Fact]
    public async Task A_reopen_while_the_push_waits_holds_it_and_the_work_that_went_on_moves_the_made_branch_with_one_push()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", submitted: true);
        await HeldAtThePushAsync(root, quest => quest with { Submission = null });

        Assert.True(await BranchAsync(root, PushBranch));
        Assert.Empty(_frames);
        Assert.Equal(AutoLandingCode.PushHeld, new AutoLandings(_home).Of("s1")!.Last!.Code);

        // Taken back: a look waits for its next submission, and hands nothing on.
        Assert.Empty(await LookAsync());
        Assert.Empty(_frames);

        await CommitAsync(tree, "more.txt", "the work after the reopen", "the work after the reopen");
        var head = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        _world.Quests["q1"] = _world.Quests["q1"] with { Submission = Made(2) };
        var said = await LookAsync();

        var frame = Json(Assert.Single(_frames));
        Assert.Equal(head, frame.GetProperty("tip").GetString());
        Assert.Equal(head, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        Assert.Equal(AutoLandingCode.Advanced, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal([$"refs/heads/{PushBranch}"], await FeatureBranchesAsync(root));
        Assert.True(new LandedBranches(_home).Landing("s1")!.Pushed, string.Join("\n", said));
    }

    /// <summary>
    /// FABRIC-CLOSE1h6 (D173's FABRIC-CLOSE1h note, *Enrolled and kept due*): a reopen takes the submission back and the look closes the
    /// entry <c>undone</c>; the agent's next <c>ready</c> makes its session due again, and the next look judges it afresh and lands it,
    /// the earlier try kept.
    /// </summary>
    [Fact]
    public async Task A_submission_reopened_and_readied_again_lands_at_the_next_look()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        var tree = await DoneSessionAsync(root, "s1", "q1", submitted: true);
        _world.Quests["q1"] = _world.Quests["q1"] with { Submission = null };
        Assert.Single(await LookAsync());
        Assert.Equal(AutoLandingCode.Undone, new AutoLandings(_home).Of("s1")!.Last!.Code);

        await CommitAsync(tree, "more.txt", "the work after the reopen", "the work after the reopen");
        _world.Quests["q1"] = _world.Quests["q1"] with { Submission = Made(2) };
        ConcludeAgain("s1", "q1", tree);
        var said = await LookAsync();

        Assert.True(await BranchAsync(root, PushBranch), string.Join("\n", said));
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal([AutoLandingCode.Undone, AutoLandingCode.Landed], entry.Tries.Select(each => each.Code));
        Assert.Equal(2, entry.Last!.Commits);
        Assert.NotNull(entry.Closed);
        Assert.Equal(("Taken", 2L), (_world.Quests["q1"].Status, _world.Quests["q1"].Submission!.Sequence));
    }

    /// <summary>
    /// FABRIC-CLOSE1h6 (D137, LAND2c): the ordinary automatic landing, no closure graph. A session landed, words went on with it, and it
    /// concluded on its done again with more work: the next look moves the branch its landing made on, its plugin told once at the
    /// tree's tip, kept as <c>advanced</c> beside the first try. Concluded once more with nothing new, it lands nothing twice.
    /// </summary>
    [Fact]
    public async Task A_done_after_words_went_on_past_its_landing_moves_the_made_branch_on_with_one_push()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1");
        Assert.Single(await LookAsync());
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Single(_frames);

        await CommitAsync(tree, "more.txt", "the work after its landing", "the work after its landing");
        var head = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        ConcludeAgain("s1", "q1", tree);
        var said = await LookAsync();

        Assert.True(_frames.Count == 2, string.Join("\n", said));
        Assert.Equal(head, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        Assert.Equal(head, Json(_frames[1]).GetProperty("tip").GetString());
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal([AutoLandingCode.Landed, AutoLandingCode.Advanced], entry.Tries.Select(each => each.Code));
        Assert.NotNull(entry.Closed);
        Assert.Equal("s1", Assert.Single(new LandedBranches(_home).Landing("s1")!.Advances).Session);
        Assert.Equal([$"refs/heads/{PushBranch}"], await FeatureBranchesAsync(root));

        // Concluded again with nothing new: judged afresh, its work is on the branch already, so nothing lands twice.
        ConcludeAgain("s1", "q1", tree);
        Assert.Single(await LookAsync());
        Assert.Equal(2, _frames.Count);
        Assert.Equal(AutoLandingCode.Already, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(head, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
    }

    // ——— FABRIC-CLOSE1h3: the person's press and an entry's Accept judged again under their holds (D173's note)

    /// <summary>
    /// The row's proof. A press waits behind another landing of its tree (LANDRACE1, bounded only at minutes), and the result it was
    /// made on moves meanwhile: a reopen, a later <c>ready</c> at the same commit, one on another machine, or a plain done turned down.
    /// It makes nothing: no branch, no record, no plugin told, and says why. A hold on the same result does not stop it (the parent's
    /// decision 1: the press is the person's own word), and a press on an unfinished quest or on no quest holds to nothing and lands as
    /// before (decision 3).
    /// </summary>
    [Theory]
    [InlineData("reopened", false)]
    [InlineData("again", false)]
    [InlineData("elsewhere", false)]
    [InlineData("turned-down", false)]
    [InlineData("held", true)]
    [InlineData("unfinished", true)]
    [InlineData("conversation", true)]
    public async Task A_press_waiting_for_its_tree_makes_nothing_where_the_result_it_was_made_on_moved(string change, bool lands)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", submitted: change is "reopened" or "again" or "elsewhere" or "held");
        if (change == "unfinished") _world.Quests["q1"] = _world.Quests["q1"] with { Status = "Taken" };

        Task<TreeLanding> press;
        using (var holding = await LandingHolds.TakeAsync(_home, tree))
        {
            Assert.NotNull(holding);
            using (ExecutionContext.SuppressFlow())
            {
                press = Task.Run(() => PressAsync(tree, change == "conversation" ? null : "q1"));
            }

            Assert.True(SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, tree) > 0, TimeSpan.FromSeconds(30)), "the press waits for its tree");
            var quest = _world.Quests["q1"];
            _world.Quests["q1"] = change switch
            {
                "reopened" => quest with { Submission = null },
                "again" => quest with { Submission = Made(2) },
                "elsewhere" => quest with { Submission = quest.Submission! with { Machine = "laptop" } },
                "turned-down" => quest with { Status = "Taken" },
                _ => quest with { Held = true, Hold = EvidenceCodes.Departed },
            };
        }

        var pressed = await press;

        Assert.Equal(lands, pressed.Landed);
        if (lands)
        {
            Assert.Single(_frames);
            return;
        }

        Assert.False(await BranchAsync(root, PushBranch), pressed.Message);
        Assert.Empty(_frames);
        Assert.Null(new LandedBranches(_home).Landing("s1"));
        Assert.Contains(change is "reopened" or "turned-down" ? "its quest was taken back" : "its quest's submission was made again", pressed.Message);
    }

    /// <summary>
    /// The same at the push (the parent's decision 4): a press whose push waits behind another push of its branch, its result moved
    /// meanwhile, keeps the branch made and recorded, tells no plugin, and says why and that the hand-off pushes it; the verb exits 1. A
    /// hold on the same result there pushes, once.
    /// </summary>
    [Theory]
    [InlineData("reopened", false)]
    [InlineData("again", false)]
    [InlineData("held", true)]
    public async Task A_press_waiting_for_its_push_keeps_its_branch_and_tells_no_plugin_where_its_result_moved(string change, bool pushes)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", submitted: true);

        Task<TreeLanding> press;
        using (var pushing = await LandingHolds.BranchAsync(_home, root, PushBranch))
        {
            Assert.NotNull(pushing);
            using (ExecutionContext.SuppressFlow())
            {
                press = Task.Run(() => PressAsync(tree));
            }

            Assert.True(SpinWait.SpinUntil(() => LandingHolds.WaitingOn(_home, root, PushBranch) > 0 || press.IsCompleted,
                TimeSpan.FromSeconds(60)), "the push waits for its branch");
            var quest = _world.Quests["q1"];
            _world.Quests["q1"] = change switch
            {
                "reopened" => quest with { Submission = null },
                "again" => quest with { Submission = Made(2) },
                _ => quest with { Held = true, Hold = EvidenceCodes.Departed },
            };
        }

        var pressed = await press;

        Assert.True(pressed.Landed, pressed.Message);
        Assert.True(await BranchAsync(root, PushBranch));
        Assert.Equal(pushes, new LandedBranches(_home).Landing("s1")!.Pushed);
        if (pushes)
        {
            Assert.Single(_frames);
            Assert.Null(pressed.Withheld);
            return;
        }

        Assert.Empty(_frames);
        Assert.NotNull(pressed.Withheld);
        Assert.Contains("its push was not tried", pressed.Message);
        Assert.Contains("`daoris-driver trees hand s1`", pressed.Message);
        Assert.Equal(1, TreesWords.Land(pressed, []).Code);
    }

    /// <summary>
    /// An entry's Accept at the push: its first part's result moves while that part's push waits for its branch. Its branch stays made
    /// with no plugin told, and the part after it is not tried, since its push would carry the first part's work on the entry's one
    /// branch; the press says it did not land all it was asked to.
    /// </summary>
    [Fact]
    public async Task An_entrys_part_whose_result_moved_at_its_push_keeps_its_branch_and_stops_the_parts_after_it()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin));
        InstallManifest();
        var first = await DoneSessionAsync(root, "s1", "q1", entry: "ask:a3/engine", submitted: true);
        var second = await DoneSessionAsync(root, "s2", "q2", entry: "ask:a3/engine", submitted: true);
        var reads = new PressQuests(_world.QuestAsync);
        EntryLandingPart[] parts =
        [
            new("s1", first) { Pressed = await reads.PressedAsync("s1", "q1", late: true) },
            new("s2", second) { Pressed = await reads.PressedAsync("s2", "q2", late: true) },
        ];

        Task<EntryLanding> landing;
        using (var pushing = await LandingHolds.BranchAsync(_home, root, PushBranch))
        {
            Assert.NotNull(pushing);
            using (ExecutionContext.SuppressFlow())
            {
                landing = Task.Run(() => new SessionTrees(_home, Plugins()).LandEntryAsync(
                    parts, new LandingSubject("s1", "q1", "Fix the gap"), new HashSet<string>(StringComparer.Ordinal)));
            }

            Assert.True(SpinWait.SpinUntil(() => LandingHolds.WaitingOn(_home, root, PushBranch) > 0 || landing.IsCompleted,
                TimeSpan.FromSeconds(60)), "the first part's push waits for its branch");
            _world.Quests["q1"] = _world.Quests["q1"] with { Submission = null };
        }

        var landed = await landing;

        Assert.False(landed.Landed, landed.Message);
        Assert.Equal([EntryPartOutcome.Landed, EntryPartOutcome.NotTried], landed.Parts.Select(part => part.Outcome));
        Assert.NotNull(landed.Parts[0].Landing!.Withheld);
        Assert.Empty(_frames);
        Assert.Equal("1", (await GitAsync(root, "rev-list", "--count", $"main..{PushBranch}")).Trim());
        Assert.False(new LandedBranches(_home).Landing("s1")!.Pushed);
        Assert.Contains("`s1`'s push was held back", landed.Message);
        Assert.Contains("`daoris-driver trees hand s1`", landed.Parts[0].Landing!.Message);
    }

    /// <summary>
    /// FABRIC-CLOSE1h8 (D173's note): a person's hand-off is judged again under its branch's hold, just before its plugin is told, against
    /// the quest of the session that last moved the branch (the parent's decision 1). s2's done moved s1's branch on; the hand-off waits
    /// behind another push of it while s2's submission is reopened, replaced, readied again at the same commit or readied on another
    /// machine: nothing is pushed, no plugin is told, nothing is recorded, and the answer names `trees hand s2`; the verb exits 1. A hold
    /// on s2's quest does not stop it, since the hand-off is the person's own word (decision 2), nor does s1's result moving, which is
    /// not the branch's last.
    /// </summary>
    [Theory]
    [InlineData("reopened", false)]
    [InlineData("replaced", false)]
    [InlineData("same-commit", false)]
    [InlineData("elsewhere", false)]
    [InlineData("held", true)]
    [InlineData("first-reopened", true)]
    public async Task A_hand_off_waiting_for_its_push_tells_no_plugin_where_the_last_result_on_its_branch_moved(string change, bool pushes)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var first = await DoneSessionAsync(root, "s1", "q1", submitted: true);
        Assert.Single(await LookAsync(pushes: false));
        await DoneSessionAsync(root, "s2", "q2", parent: "q1", from: $"daoris/{Path.GetFileName(first)}", submitted: true);
        Assert.Single(await LookAsync(pushes: false));
        var recorded = new LandedBranches(_home).Of("engine", PushBranch)!;
        Assert.Equal(("s1", "s2"), (recorded.Session, recorded.LastSession()));
        Assert.False(recorded.Pushed);
        lock (_frames) _frames.Clear();
        _pushes = true;

        // As the doors make it: the branch's last session's quest, read where the door names the branch.
        var pressed = await PressQuests.HandedAsync(recorded, SessionQuestAsync, _world.QuestAsync);
        Task<TreeHand> hand;
        using (var pushing = await LandingHolds.BranchAsync(_home, root, PushBranch))
        {
            Assert.NotNull(pushing);
            using (ExecutionContext.SuppressFlow())
            {
                hand = Task.Run(() => new SessionTrees(_home, Plugins()).HandAsync(root, recorded, beforePush: pressed?.BeforePush));
            }

            Assert.True(SpinWait.SpinUntil(() => LandingHolds.WaitingOn(_home, root, PushBranch) > 0 || hand.IsCompleted,
                TimeSpan.FromSeconds(60)), "the hand-off waits for its branch");
            var quest = change == "first-reopened" ? "q1" : "q2";
            var was = _world.Quests[quest];
            _world.Quests[quest] = change switch
            {
                "reopened" or "first-reopened" => was with { Submission = null },
                "replaced" => was with { Submission = Made(2) with { Commit = "a later commit" } },
                "same-commit" => was with { Submission = Made(2) },
                "elsewhere" => was with { Submission = new QuestSubmissionView("laptop", 1, DateTimeOffset.UtcNow, "ready") },
                _ => was with { Held = true, Hold = EvidenceCodes.Departed },
            };
        }

        var handed = await hand;

        Assert.Equal(pushes, handed.Handed);
        Assert.Equal(pushes, new LandedBranches(_home).Of("engine", PushBranch)!.Pushed);
        Assert.True(await BranchAsync(root, PushBranch));
        if (pushes)
        {
            Assert.Single(_frames);
            Assert.Null(handed.Withheld);
            return;
        }

        Assert.Empty(_frames);
        Assert.Null(handed.Plugin);
        Assert.NotNull(handed.Withheld);
        Assert.Contains("its push was not tried", handed.Message);
        Assert.Contains("`daoris-driver trees hand s2`", handed.Message);
        Assert.Equal(1, TreesWords.Hand(handed, []).Code);
    }

    /// <summary>
    /// FABRIC-CLOSE1h8: the look's own hand-off of a push held back keeps the look's rule (the parent's decision 3). Released, the look
    /// hands the made branch on; that hand-off waits behind another push of the branch while the quest is held again: nothing is pushed,
    /// no plugin is told, and the entry stays <c>push-held</c>, not refused, so the first look after the release hands it on, once.
    /// </summary>
    [Fact]
    public async Task The_looks_hand_off_held_again_while_it_waits_for_its_push_stays_push_held_and_pushes_once_after_the_release()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        await DoneSessionAsync(root, "s1", "q1", submitted: true);
        await HeldAtThePushAsync(root, quest => quest with { Held = true, Hold = EvidenceCodes.Departed });
        Assert.Equal(AutoLandingCode.PushHeld, new AutoLandings(_home).Of("s1")!.Last!.Code);

        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false, Hold = null };
        var said = await HeldAtThePushAsync(root, quest => quest with { Held = true, Hold = EvidenceCodes.Departed });

        Assert.Empty(_frames);
        Assert.False(new LandedBranches(_home).Landing("s1")!.Pushed);
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.PushHeld, entry.Last!.Code);
        Assert.Null(entry.Closed);
        Assert.Contains("nothing was pushed", Assert.Single(said));

        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false, Hold = null };
        Assert.Single(await LookAsync());

        Assert.Single(_frames);
        Assert.True(new LandedBranches(_home).Landing("s1")!.Pushed);
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal([$"refs/heads/{PushBranch}"], await FeatureBranchesAsync(root));
    }

    /// <summary>The quest a session serves, as the service answers it from its records (<c>ServiceClient.SessionQuestAsync</c>).</summary>
    private Task<string?> SessionQuestAsync(string id, CancellationToken ct) =>
        Task.FromResult(_world.Records.FirstOrDefault(record => string.Equals(record.Id, id, StringComparison.OrdinalIgnoreCase))?.Quest);

    /// <summary>
    /// The person's press as its doors make it (FABRIC-CLOSE1h3): the quest read where the door names its landing, then the landing
    /// judged again under its holds on that read.
    /// </summary>
    private async Task<TreeLanding> PressAsync(string tree, string? quest = "q1")
    {
        var reads = new PressQuests(_world.QuestAsync);
        if (quest is not null) await reads.FindAsync(quest);
        var pressed = await reads.PressedAsync("s1", quest);
        return await new SessionTrees(_home, Plugins()).LandAsync(
            tree, new LandingSubject("s1", quest, "Fix the gap"), judged: pressed?.Judged, beforePush: pressed?.BeforePush);
    }

    private const string PushBranch = "feature/q1-fix-the-gap";

    /// <summary>
    /// One look's landing of the one due entry whose push waits behind another push of its branch, held here, while the quest moves as
    /// <paramref name="change"/> says; then the branch's hold is let go and the landing ends. What the landing said comes back.
    /// </summary>
    private async Task<IReadOnlyList<string>> HeldAtThePushAsync(string root, Func<QuestView, QuestView> change)
    {
        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        Assert.Single(chosen);
        Task<IReadOnlyList<string>> landing;
        using (var pushing = await LandingHolds.BranchAsync(_home, root, PushBranch))
        {
            Assert.NotNull(pushing);
            using (ExecutionContext.SuppressFlow())
            {
                landing = Task.Run(() => lander.LandAsync(chosen));
            }

            Assert.True(SpinWait.SpinUntil(() => LandingHolds.WaitingOn(_home, root, PushBranch) > 0 || landing.IsCompleted,
                TimeSpan.FromSeconds(60)), "the push waits for its branch");
            _world.Quests["q1"] = change(_world.Quests["q1"]);
        }

        return await landing;
    }

    private static async Task<IReadOnlyList<string>> FeatureBranchesAsync(string root) =>
        (await GitAsync(root, "for-each-ref", "--format=%(refname)", "refs/heads/feature/"))
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private const string Graph = "sha256:closure";

    /// <summary>A submission made on this machine at <paramref name="sequence"/> (FABRIC-CLOSE1a).</summary>
    private static QuestSubmissionView Made(long sequence) => new("desk", sequence, DateTimeOffset.UtcNow, "ready");

    /// <summary>The agent moves its tree onto a branch of its own and commits its work there (TREEFOLLOW1, D164 point 4).</summary>
    private static async Task OwnBranchAsync(string tree)
    {
        await GitAsync(tree, "checkout", "--quiet", "-b", "feature/own");
        await CommitAsync(tree, "s1.txt", "the work of s1", "the work of s1");
    }

    // ——— the world, the plugin and the fixtures

    /// <summary>The service's quests and records, standing in.</summary>
    private sealed class World : IAutoLandingWorld
    {
        public Action? ReadingQuests { get; set; }
        public Dictionary<string, QuestView> Quests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<SessionRecord> Records { get; } = [];

        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult(Quests.GetValueOrDefault(id));

        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([.. Records]);

        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<string>>(Records.Where(r => r.Live && r.Tree is not null).Select(r => r.Tree!).ToHashSet());

        // The review's gate reads the chain and its ask (REVIEWENV1c): no rule names a review here, so these decide nothing.
        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct)
        {
            ReadingQuests?.Invoke();
            return Task.FromResult<IReadOnlyList<QuestView>>([.. Quests.Values]);
        }

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        // The second opinion's gate reads the host's opinions (XAGENT1f): no rule names a reviewer here, so none is asked.
        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult<OpinionView?>(null);
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

    // What happens as the plugin's process starts, before its frame (LANDPIN2), and as it lands, before it answers (LANDRACE1).
    private Action? _starting;
    private Action? _landing;

    // The plugin's next answer, once, where a test names one (LANDRACE1's review).
    private Func<PluginLanding>? _answer;

    private AutoLander Lander() => new(
        _home, _world, new SessionTrees(_home, Plugins()), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add);

    /// <summary>The rule's plugin, faked on its channel: every frame it is told is kept, whichever door told it.</summary>
    private LandingPlugins Plugins() => new(_home, start: (_, _, _) =>
    {
        _starting?.Invoke();
        return Task.FromResult<IHookChannel>(new FakeLander(frame =>
        {
            lock (_frames) _frames.Add(frame);
            _landing?.Invoke();
            if (_answer is { } answer)
            {
                _answer = null;
                return answer();
            }

            return _pushes
                ? new PluginLanding(Plugin, true, "https://example.test/pull/7", "pushed it.")
                : new PluginLanding(Plugin, false, null, "gh is not signed in — run `gh auth login`.");
        }));
    });

    private static JsonElement Json(object frame) => JsonDocument.Parse(JsonSerializer.Serialize(frame)).RootElement.Clone();

    /// <summary>One look: what it chose, then what landing those beside it said.</summary>
    private async Task<IReadOnlyList<string>> LookAsync(bool pushes = true)
    {
        _pushes = pushes;
        var lander = Lander();
        var (chosen, said) = await lander.ChooseAsync(Config());
        return [.. said, .. await lander.LandAsync(chosen)];
    }

    private DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>A session that worked in its own tree and concluded on its quest's done, made due as the driver's conclusion makes it.</summary>
    /// <param name="parent">The quest it follows, where it is a chain's later step.</param>
    /// <param name="from">The session branch its tree grows from, as a chain's step grows (CHAIN2).</param>
    /// <param name="entry">The entry its record names (SESSONE1b), shared by an entry's parts; none is its own line of work.</param>
    /// <param name="submitted">Bound to a closure graph and at its submission, taken, rather than done (FABRIC-CLOSE1h, §E5).</param>
    private async Task<string> DoneSessionAsync(
        string root, string session, string quest, bool held = false, bool commit = true, string? parent = null, string? from = null,
        string? entry = null, bool submitted = false)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora", from: from);
        if (commit) await CommitAsync(opened.Path, $"{session}.txt", $"the work of {session}", $"the work of {session}");
        var view = new QuestView(quest, "game", "engine", "Fix the gap", "Fix it.", submitted ? "Taken" : "Done") { Held = held, Parent = parent };
        // FABRIC-CLOSE1h: a closure graph's quest stays taken at its agent's `ready`, its submission current (§E5).
        _world.Quests[quest] = submitted ? view with { Closure = Graph, Submission = Made(1) } : view;
        _world.Records.Add(new SessionRecord(session, "engine", "completed")
        {
            Quest = quest, Tree = opened.Path, Created = DateTimeOffset.UtcNow, Entry = entry,
        });
        // The status the driver's conclusion hands on, as it reads the quest after the run (Driver.ConcludedForLandingAsync's callers).
        var verdict = AutoLander.Concluded(_home, Config(), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), session, _world.Quests[quest],
            QuestResults.Status(_world.Quests[quest]), "completed", opened.Path, "aurora");
        Assert.Equal(Config().Landings.GetValueOrDefault("engine")?.AutoAccept == true ? AutoConcluded.Due : null, verdict);
        return opened.Path;
    }

    /// <summary>
    /// The session's record concluding again on its quest as it stands now, as the driver's conclusion of a carried-on run hands it on
    /// (D137's words that went on, or the agent's next <c>ready</c>): due again.
    /// </summary>
    private void ConcludeAgain(string session, string quest, string tree) =>
        Assert.Equal(AutoConcluded.Due, AutoLander.Concluded(_home, Config(), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), session,
            _world.Quests[quest], QuestResults.Status(_world.Quests[quest]), "completed", tree, "aurora"));

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
        [.. new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(session, 0).Events.Where(e => e.Kind == SessionEventKind.Note)];

    private static object? Value(LandingLine line, string key) => line.Data.Single(pair => pair.Key == key).Value;

    private void Rule(LandingRule rule) =>
        DriverConfig.Empty.WithLanding("engine", rule).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    private void InstallManifest()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins,Plugin);
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
        using var process = Process.Start(TestGit.Past(info))!;
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
