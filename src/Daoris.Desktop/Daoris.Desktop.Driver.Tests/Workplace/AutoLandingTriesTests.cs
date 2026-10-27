using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAND2b (D145 point 2, design §2, §4, §5, §8): done work lands itself. A session whose record concludes on a done quest,
/// released, under a rule that accepts automatically, is due; the look lands it exactly as the review's Accept does — the
/// branch, its record, the rule's plugin and its tidy — and keeps who accepted it, each try and its code. A plugin that
/// cannot land here leaves the branch made; a refusal is tried again only when the tree's tip or status moves; a done with no
/// commits lands nothing. This class holds the look's landing and its tries (PROCSPLIT1 split it from the one class).
/// </summary>
/// <remarks>
/// Real git, as the other tree tests, a stand-in for the service's quests and records, and a plugin faked on the wire's
/// channel: nothing reaches a network or a harness.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class AutoLandingTriesTests : AutoLandingFixture
{
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
    /// AUTONOMY1c2 (D184 points 2 and 3): where the repository's access setup says <c>landing: rule</c>, a branch rule whose own switch
    /// is off lands as the switch lands it. The session is told so, its done is due, and the look lands it with its plugin told once; its
    /// record says the setup accepted it, and the file's rule keeps its own switch, as the settings page reads it.
    /// </summary>
    [Fact]
    public async Task A_done_session_under_the_setups_rule_lands_at_the_look_and_its_record_says_the_setup_accepted_it()
    {
        var root = await RepositoryAsync("engine");
        (DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin)) with
        {
            WorkspaceAccess = new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase)
            {
                ["aurora"] = new() { Pauses = new Dictionary<string, string>(StringComparer.Ordinal) { [AccessRules.Landing] = AccessRules.Rule } },
            },
        }).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        InstallManifest();
        Assert.Contains("This work is accepted automatically.", Instruction(root));
        await DoneSessionAsync(root, "s1", "q1");

        var (chosen, said) = await Lander().ChooseAsync(Config());
        Assert.Empty(said);
        var line = Assert.Single(await Lander().LandAsync(chosen));

        const string branch = "feature/q1-fix-the-gap";
        Assert.True(await BranchAsync(root, branch), line);
        Assert.StartsWith($"landing  session s1 (#q1 → engine): {LandingRules.AutoAccepted} its work is on `{branch}`.", line);
        var record = new LandedBranches(_home).Landing("s1")!;
        Assert.Equal(AcceptedBy.Auto, record.AcceptedBy);
        Assert.Equal(new LandedRule(Plugin, AutoAccept: true, LandingSource.Repository) { Access = AccessRules.FromWorkspace }, record.Rule);
        Assert.Single(_frames);
        Assert.False(Config().Landings["engine"].AutoAccept, "the file's rule keeps its own switch");
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

    // ——— FABRIC-CLOSE1h17: an opinion holding an ordinary automatic landing is asked by the loop (D173's note)

    /// <summary>
    /// The row's proof. A done's landing held at the look by a second opinion nobody asked makes one owed where the look reads its gate, at
    /// the landing's occasion, as a push held back does (FABRIC-CLOSE1h15): a rule that started reading after the done owed none, and one
    /// whose due the loop closed as off before the rule came back owes none any more. The loop's opinion look keeps it owed and asks it, its
    /// gate reading not asked; a look while it holds owes it once, not again. Once it settles, the first look lands it once, and the opinion
    /// look then closes it as landed.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_landing_held_by_an_opinion_nobody_asked_owes_it_at_the_look_and_lands_once_it_settles(bool closedOff)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var tip = await HeadAsync(tree);
        ReadsAnOpinion();
        if (closedOff)
        {
            // Owed at the done, closed as off while the rule read none, and the rule came back.
            Assert.Equal(OpinionRules.Landing, Assert.Single(OpinionLook.Concluded(_home, Config(), "s1", _world.Quests["q1"], "Done", "completed", tree)));
            new OpinionDues(_home).Close("s1", OpinionRules.Landing, OpinionDueClosed.Off, DateTimeOffset.UtcNow);
        }

        Assert.Empty(new OpinionDues(_home).Open());

        var first = await LookAsync();

        Assert.Contains("it is asked at the driver's next look", Assert.Single(first));
        Assert.Empty(_frames);
        Assert.False(await BranchAsync(root, PushBranch));
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal((AutoLandingCode.Opinion, (DateTimeOffset?)null), (entry.Last!.Code, entry.Closed));
        var due = Assert.Single(new OpinionDues(_home).Open());
        Assert.Equal(("s1", "q1", "engine", "aurora", tree, OpinionRules.Landing),
            (due.Session, due.Quest, due.Repository, due.Workspace, due.Tree, due.Occasion));
        // The loop's opinion look keeps it owed, and asks it: its gate reads not asked, with no hold of the quest's before it.
        Assert.Null(await ClosesAsync(due));
        var gate = await new SessionTrees(_home).OpinionAsync(due.Tree, due.Repository, due.Workspace, due.Quest, due.Session, _world, null, due.Occasion);
        Assert.Equal((OpinionGateStates.NotAsked, false), (gate.State, gate.Held));

        Assert.Empty(await LookAsync());
        Assert.Equal(due, Assert.Single(new OpinionDues(_home).All()));

        Settled("o1", tip);
        Assert.Single(await LookAsync());

        Assert.Single(_frames);
        Assert.True(await BranchAsync(root, PushBranch));
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(OpinionDueClosed.Landed, await ClosesAsync(due));
    }

    /// <summary>
    /// The guards, each green before the row: a due already owed for the landing's occasion (the rule read at the done) is left as it was,
    /// not owed again; and one the loop closed as landed since the done is not opened again, since its look would only close it again.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_landing_held_by_an_opinion_owed_at_its_done_owes_nothing_more_at_the_look(bool closedLanded)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        ReadsAnOpinion();
        var tree = await DoneSessionAsync(root, "s1", "q1");
        Assert.Equal(OpinionRules.Landing, Assert.Single(OpinionLook.Concluded(_home, Config(), "s1", _world.Quests["q1"], "Done", "completed", tree)));
        if (closedLanded) new OpinionDues(_home).Close("s1", OpinionRules.Landing, OpinionDueClosed.Landed, DateTimeOffset.UtcNow);
        var owed = Assert.Single(new OpinionDues(_home).All());

        Assert.Contains("it is asked at the driver's next look", Assert.Single(await LookAsync()));

        Assert.Equal(AutoLandingCode.Opinion, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(owed, Assert.Single(new OpinionDues(_home).All()));
        Assert.False(await BranchAsync(root, PushBranch));
    }

    // ——— FABRIC-CLOSE1h18: an opinion's due closes as landed only on a landing made since it was owed (D173's note)

    /// <summary>
    /// The row's proof. A session landed, its words went on (D137), and it concluded on its done again with more work, now under a rule
    /// that reads an opinion before landing: owed at the done, or at the look where its done owed none (FABRIC-CLOSE1h17). Its landing,
    /// older than the due, lands none of the work done since, so the loop's opinion look keeps it owed and asks it, rather than closing it
    /// as landed and leaving the landing to the person's ask. Once it settles, the next look moves the made branch on once, and the
    /// advance, made since the due, closes it as landed.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_session_landed_that_went_on_keeps_its_later_dones_opinion_owed_and_lands_once_it_settles(bool owedAtTheDone)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1");
        Assert.Single(await LookAsync());
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);

        ReadsAnOpinion();
        await CommitAsync(tree, "more.txt", "the work after its landing", "the work after its landing");
        var head = await HeadAsync(tree);
        ConcludeAgain("s1", "q1", tree);
        if (owedAtTheDone)
        {
            Assert.Equal(OpinionRules.Landing, Assert.Single(OpinionLook.Concluded(_home, Config(), "s1", _world.Quests["q1"], "Done", "completed", tree)));
        }

        Assert.Contains("it is asked at the driver's next look", Assert.Single(await LookAsync()));

        Assert.Single(_frames);
        Assert.Equal(AutoLandingCode.Opinion, new AutoLandings(_home).Of("s1")!.Last!.Code);
        var due = Assert.Single(new OpinionDues(_home).Open());
        Assert.Equal(("s1", OpinionRules.Landing), (due.Session, due.Occasion));
        Assert.True(new LandedBranches(_home).Landing("s1")!.AcceptedAt("s1") < due.At, "the landing is older than the due");
        // The loop's opinion look keeps it owed, and asks it: its gate reads not asked, with no hold of the quest's before it.
        Assert.Null(await ClosesAsync(due));
        var gate = await new SessionTrees(_home).OpinionAsync(due.Tree, due.Repository, due.Workspace, due.Quest, due.Session, _world, null, due.Occasion);
        Assert.Equal((OpinionGateStates.NotAsked, false), (gate.State, gate.Held));

        Settled("o1", head);
        Assert.Single(await LookAsync());

        Assert.Equal(2, _frames.Count);
        Assert.Equal(head, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        Assert.Equal(AutoLandingCode.Advanced, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(OpinionDueClosed.Landed, await ClosesAsync(due));
    }

    /// <summary>
    /// FABRIC-CLOSE1h19 (D173's CLOSELAND1 note), confirmed red first. A session landed, then concluded again with nothing new, under an
    /// opinion rule that started reading after its landing: its due closes as landed at once, its tree being on the branch made
    /// (CLOSE1h18 point 2), and CLOSE1h17 does not owe it again, so the gate's <c>opinion</c> would hold an entry with nothing to land
    /// until the person's ask. The look reads the tree on its landed branch before the gate, and closes the entry <c>already</c>.
    /// </summary>
    [Fact]
    public async Task A_done_already_on_its_landed_branch_closes_its_entry_before_the_opinion_gate()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1");
        Assert.Single(await LookAsync());
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);

        ReadsAnOpinion();
        ConcludeAgain("s1", "q1", tree);
        Assert.Equal(OpinionRules.Landing, Assert.Single(OpinionLook.Concluded(_home, Config(), "s1", _world.Quests["q1"], "Done", "completed", tree)));
        var due = Assert.Single(new OpinionDues(_home).Open());
        // The loop's opinion look closes it as landed at once: the tree is on the branch made.
        Assert.Equal(OpinionDueClosed.Landed, await ClosesAsync(due));
        new OpinionDues(_home).Close("s1", OpinionRules.Landing, OpinionDueClosed.Landed, DateTimeOffset.UtcNow);

        var said = await LookAsync();

        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.Already, entry.Last!.Code);
        Assert.NotNull(entry.Closed);
        Assert.Contains("already on a branch of yours", Assert.Single(said));
        Assert.Single(_frames);
        Assert.Empty(new OpinionDues(_home).Open());
    }

    /// <summary>
    /// The reading by git behind it. A landing of the session's recorded before its due lands only the work it holds: a tree gone on past
    /// it keeps the due owed; one still at the commit its branch was made at, or at the commit it squashed (its record's own word,
    /// PROCESS1b-ii), closes it as landed, as any landing closed it before (a push held back's recovery owes its due after the landing,
    /// FABRIC-CLOSE1h15).
    /// </summary>
    [Theory]
    [InlineData(false, false, OpinionDueClosed.Landed)]
    [InlineData(false, true, OpinionDueClosed.Landed)]
    [InlineData(true, false, null)]
    [InlineData(true, true, null)]
    public async Task A_landing_older_than_its_due_closes_it_only_where_it_holds_the_trees_work(bool wentOn, bool squash, string? closed)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        ReadsAnOpinion();
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var tip = await HeadAsync(tree);
        // Its landing made the branch at the tree's tip, or at one commit squashing the tree's work onto the line, an hour before the due.
        var made = squash ? (await GitAsync(root, "commit-tree", $"{tip}^{{tree}}", "-p", $"{tip}~1", "-m", "Fix the gap")).Trim() : tip;
        await GitAsync(root, "branch", PushBranch, made);
        var at = DateTimeOffset.UtcNow;
        new LandedBranches(_home).Record(new LandedBranch("engine", "aurora", PushBranch, "main", made, "s1", "q1", "Fix the gap", at.AddHours(-1))
        {
            Squashed = squash ? tip : null,
        });
        if (wentOn) await CommitAsync(tree, "more.txt", "the work after its landing", "the work after its landing");

        Assert.Equal(closed, await ClosesAsync(new OpinionDue("s1", "q1", "engine", "aurora", tree, OpinionRules.Landing, at)));
    }
}
