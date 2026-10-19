using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAND2b (D145 point 2, design §2, §5, §8): the due list's pure rules and its file. A driven session in a tree of its own
/// whose record concludes on a done quest, under a rule that accepts automatically, is due; a refusal is tried again only
/// when its tree's tip or status moves; each try keeps its code, and the facts it was made at. Files and tables only, so the
/// suite's fast half; the landing itself over real git is <see cref="AutoLandingTests"/>.
/// </summary>
public sealed class AutoLandingRulesTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-auto-landing-" + Guid.NewGuid().ToString("N")[..8]);

    public AutoLandingRulesTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly DateTimeOffset At = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly LandingRule Automatic = new(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true, AutoAccept: true);

    private static AutoLanding Due(string session = "s1", string quest = "q1") =>
        new(session, quest, "engine", "aurora", $"trees/aurora/engine/{session}", At);

    // ——— when a concluded record is due

    /// <summary>
    /// Only a rule that accepts automatically makes anything due, and then only a done in a tree of its own; a done in the
    /// repository's own checkout, and an end that is not a done, are each said, and a session still waiting is not an end.
    /// </summary>
    [Theory]
    [InlineData(true, "Done", true, "completed", AutoConcluded.Due)]
    [InlineData(true, "Done", false, "completed", AutoConcluded.NoTree)]
    [InlineData(true, "Done", true, "failed", AutoConcluded.Due)]
    [InlineData(true, "Open", true, "declined", AutoConcluded.NotDone)]
    [InlineData(true, "Taken", true, "failed", AutoConcluded.NotDone)]
    [InlineData(true, "Taken", true, "stood-down", AutoConcluded.NotDone)]
    [InlineData(true, "Taken", true, "stopped", AutoConcluded.NotDone)]
    [InlineData(true, "Taken", true, "completed", null)]
    [InlineData(true, "Taken", true, "awaiting-person", null)]
    [InlineData(false, "Done", true, "completed", null)]
    public void A_concluded_record_is_due_only_on_a_done_in_its_own_tree_under_the_switch(
        bool autoAccept, string status, bool ownTree, string state, string? expected)
    {
        var rule = Automatic with { AutoAccept = autoAccept };

        Assert.Equal(expected, AutoLandingRules.Concluded(rule, status, ownTree, state));
    }

    /// <summary>
    /// The conclusion acts on its verdict: due, the session joins the due list keyed on the tree's layout; a done in the
    /// repository's own checkout, and an end that is not a done, are said in its conversation by code; under a rule with the
    /// switch off nothing is written anywhere.
    /// </summary>
    [Fact]
    public void A_conclusion_under_the_switch_is_due_or_said_and_without_it_writes_nothing()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var quest = new QuestView("q1", "game", "engine", "Fix the gap", "Fix it.", "Done") { Workspace = "aurora" };
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees,"aurora", "engine", "s-1a2b3c4d");
        var config = DriverConfig.Empty.WithWorkspaceLanding("aurora", Automatic);

        Assert.Equal(AutoConcluded.Due, AutoLander.Concluded(_home, config, events, "s1", quest, "Done", "completed", tree, "aurora", At));
        var due = Assert.Single(new AutoLandings(_home).Open());
        Assert.Equal(("s1", "q1", "engine", "aurora", tree, At), (due.Session, due.Quest, due.Repository, due.Workspace, due.Tree, due.DueAt));

        var checkout = Path.Combine(_home, "checkouts", "engine");
        Assert.Equal(AutoConcluded.NoTree, AutoLander.Concluded(_home, config, events, "s2", quest, "Done", "completed", checkout, "aurora"));
        Assert.Equal(NoteCodes.LandingNoTree.Code, events.After("s2", 0).Events.Single().Parts![0].Code);
        Assert.Equal(AutoConcluded.NotDone,
            AutoLander.Concluded(_home, config, events, "s3", quest with { Status = "Open" }, "Open", "declined", tree, "aurora"));
        Assert.Equal(NoteCodes.LandingNotDone.Code, events.After("s3", 0).Events.Single().Parts![0].Code);
        Assert.Single(new AutoLandings(_home).All());

        Assert.Null(AutoLander.Concluded(_home, DriverConfig.Empty, events, "s4", quest, "Done", "completed", tree, "aurora"));
        Assert.False(File.Exists(DaorisHome.Entry(_home, HomeEntry.Sessions,"s4.events.jsonl")));
        Assert.Single(new AutoLandings(_home).All());
    }

    /// <summary>
    /// 🔴 A landing at done reads its rule again where it lands, not only where the look chose it: one changed meanwhile to a
    /// merge, or with the switch off, lands nothing, before git is asked anything, and is kept as <c>off</c> (D51 rule 6).
    /// </summary>
    [Theory]
    [InlineData("merge")]
    [InlineData("branch")]
    public async Task A_landing_at_done_under_a_rule_changed_meanwhile_lands_nothing(string form)
    {
        var rule = form == "merge" ? LandingRule.Merge : new LandingRule(LandingForm.Branch, "feature/{quest}");
        DriverConfig.Empty.WithWorkspaceLanding("aurora", rule).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees,"aurora", "engine", "s-1a2b3c4d");

        var landed = await new SessionTrees(_home).LandAsync(tree, new LandingSubject("s1", "q1", "Fix"), acceptedBy: AcceptedBy.Auto);

        Assert.False(landed.Landed);
        Assert.Equal(AutoLandingCode.Off, AutoLandingRules.CodeOf(landed));
        Assert.Contains("no longer accepts automatically", landed.Message);
    }

    /// <summary>A merge never accepts automatically, whatever a hand-written file says (D145 point 1).</summary>
    [Fact]
    public void A_merge_rule_makes_nothing_due() =>
        Assert.Null(AutoLandingRules.Concluded(new LandingRule(LandingForm.Merge, AutoAccept: true), "Done", ownTree: true, "completed"));

    // ——— the access setup's landing (AUTONOMY1c2, D184 points 2, 3 and 5)

    /// <summary>The setup at one level: full access, or its <c>landing</c> pause alone.</summary>
    private static AccessSetting LandingPause(string value) => value == AccessRules.FullWord
        ? AccessSetting.FullAccess
        : new() { Pauses = new Dictionary<string, string>(StringComparer.Ordinal) { [AccessRules.Landing] = value } };

    /// <summary><paramref name="config"/> with the setup set for <c>engine</c>, for <c>aurora</c> and for this machine, where each is named.</summary>
    private static DriverConfig Setup(DriverConfig config, string? repository = null, string? workspace = null, string? machine = null) =>
        config with
        {
            Access = repository is null
                ? config.Access
                : new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase) { ["engine"] = LandingPause(repository) },
            WorkspaceAccess = workspace is null
                ? config.WorkspaceAccess
                : new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase) { ["aurora"] = LandingPause(workspace) },
            MachineAccess = machine is null ? config.MachineAccess : LandingPause(machine),
        };

    /// <summary>
    /// One effective rule: the chosen rule, its switch on where it is a branch rule and the repository's <c>landing</c> resolves
    /// <c>rule</c> at whichever level, full access among them, naming where the setup was set. <c>person</c>, today's, and the rule's own
    /// switch leave it as the rule says. A merge rule is left a merge with no switch, the setup named beside it (D51 rule 6). The file's
    /// rule is never changed, so what the settings page reads stays the rule's own; the gate's process under Current reads the same.
    /// </summary>
    [Theory]
    [InlineData(LandingForm.Branch, false, null, null, null, false, null)]
    [InlineData(LandingForm.Branch, false, AccessRules.Rule, null, null, true, AccessRules.FromRepository)]
    [InlineData(LandingForm.Branch, false, null, AccessRules.Rule, null, true, AccessRules.FromWorkspace)]
    [InlineData(LandingForm.Branch, false, null, null, AccessRules.FullWord, true, AccessRules.FromMachine)]
    [InlineData(LandingForm.Branch, false, AccessRules.Person, null, AccessRules.FullWord, false, null)]
    [InlineData(LandingForm.Branch, true, null, null, AccessRules.FullWord, true, null)]
    [InlineData(LandingForm.Branch, true, AccessRules.Person, null, null, true, null)]
    [InlineData(LandingForm.Merge, false, AccessRules.Rule, null, null, false, AccessRules.FromRepository)]
    [InlineData(LandingForm.Merge, false, null, null, AccessRules.FullWord, false, AccessRules.FromMachine)]
    public void The_effective_rule_accepts_automatically_where_the_setup_says_rule_over_a_branch_rule(
        string form, bool own, string? repository, string? workspace, string? machine, bool accepts, string? access)
    {
        var rule = form == LandingForm.Merge ? LandingRule.Merge : Automatic with { AutoAccept = own };
        var config = Setup(DriverConfig.Empty.WithWorkspaceLanding("aurora", rule), repository, workspace, machine);

        var effective = LandingRules.Effective(config, "engine", "aurora");

        Assert.Equal((form, accepts, access, LandingSource.Workspace), (effective.Rule.Form, effective.Rule.AutoAccept, effective.Access, effective.Source));
        Assert.Equal(rule with { AutoAccept = accepts }, effective.Rule);
        Assert.Equal(rule, config.WorkspaceLandings["aurora"]);
        Assert.Equal(rule, LandingRules.Choose(config, "engine", "aurora").Rule);
        Assert.Equal(effective, WorkflowProcess.Current(config, "engine", "aurora").Landing);
    }

    /// <summary>
    /// The conclusion reads the effective rule: under the setup's <c>rule</c>, or full access, a done in its own tree is due under a branch
    /// rule whose own switch is off; under <c>person</c> nothing is written. A merge rule makes nothing due whatever the setup (D51 rule 6,
    /// D145), and its conversation says why its work waits for Accept…; an end that is not a done keeps today's review, said nothing more.
    /// </summary>
    [Fact]
    public void A_conclusion_under_the_setups_rule_is_due_and_under_a_merge_rule_says_why_Accept_stays()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var quest = new QuestView("q1", "game", "engine", "Fix the gap", "Fix it.", "Done") { Workspace = "aurora" };
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, "aurora", "engine", "s-1a2b3c4d");
        var pressed = DriverConfig.Empty.WithWorkspaceLanding("aurora", Automatic with { AutoAccept = false });

        Assert.Null(AutoLander.Concluded(_home, Setup(pressed, workspace: AccessRules.Person), events, "s1", quest, "Done", "completed", tree, "aurora", At));
        Assert.Empty(new AutoLandings(_home).All());
        Assert.Equal(AutoConcluded.Due,
            AutoLander.Concluded(_home, Setup(pressed, workspace: AccessRules.Rule), events, "s2", quest, "Done", "completed", tree, "aurora", At));
        Assert.Equal(AutoConcluded.Due,
            AutoLander.Concluded(_home, Setup(pressed, machine: AccessRules.FullWord), events, "s3", quest, "Done", "completed", tree, "aurora", At));
        Assert.Equal(["s2", "s3"], new AutoLandings(_home).Open().Select(entry => entry.Session).Order());

        var merges = Setup(DriverConfig.Empty.WithWorkspaceLanding("aurora", LandingRule.Merge), machine: AccessRules.FullWord);
        Assert.Equal(AutoConcluded.Merges, AutoLander.Concluded(_home, merges, events, "s4", quest, "Done", "completed", tree, "aurora", At));
        var said = Assert.Single(events.After("s4", 0).Events);
        Assert.Equal(SessionEventKind.Note, said.Kind);
        Assert.Contains("this machine's access setup lands work by its rule", said.Text);
        Assert.Contains("merges", said.Text);
        Assert.Contains("Accept", said.Text);
        // ACCESSWORDS1: a coded note, the page words it, with the level that set the setup.
        Assert.Equal(NoteCodes.LandingMerges.Code, said.Parts![0].Code);
        Assert.Equal(AccessRules.FromMachine, said.Parts[0].Value("access"));
        Assert.Equal(NoteCodes.LandingMerges.Code, AutoLandingNotes.Concluded(AutoConcluded.Merges, AccessRules.FromRepository).Parts![0].Code);
        Assert.Null(AutoLander.Concluded(_home, merges, events, "s5", quest with { Status = "Open" }, "Open", "declined", tree, "aurora"));
        Assert.Empty(events.After("s5", 0).Events);
        Assert.Equal(2, new AutoLandings(_home).All().Count);
    }

    /// <summary>
    /// The pure verdict over the effective landing: a merge rule under the setup's <c>rule</c> is said only for a done in a tree of its
    /// own, whose work Accept… would land; a done in the checkout is there already, and a parked record or another end is not said.
    /// </summary>
    [Theory]
    [InlineData(AccessRules.FromWorkspace, "Done", true, "completed", AutoConcluded.Merges)]
    [InlineData(AccessRules.FromWorkspace, "Done", false, "completed", null)]
    [InlineData(AccessRules.FromWorkspace, "Open", true, "declined", null)]
    [InlineData(AccessRules.FromWorkspace, "Done", true, "awaiting-person", null)]
    [InlineData(null, "Done", true, "completed", null)]
    public void A_merge_rule_under_the_setups_rule_is_said_only_for_a_done_in_its_own_tree(
        string? access, string status, bool ownTree, string state, string? expected) =>
        Assert.Equal(expected,
            AutoLandingRules.Concluded(new Landing(LandingRule.Merge, LandingSource.Workspace) { Access = access }, status, ownTree, state));

    /// <summary>
    /// The landing reads the effective rule where it lands, as the look does: under the setup's <c>rule</c>, or full access, a branch rule
    /// whose own switch is off is not refused as off (here it goes on, and finds no tree); under <c>person</c> it is, and a merge rule
    /// lands nothing by itself under any setup (D51 rule 6), refused before git is asked anything.
    /// </summary>
    [Theory]
    [InlineData(LandingForm.Branch, AccessRules.Rule, false)]
    [InlineData(LandingForm.Branch, AccessRules.FullWord, false)]
    [InlineData(LandingForm.Branch, AccessRules.Person, true)]
    [InlineData(LandingForm.Merge, AccessRules.Rule, true)]
    [InlineData(LandingForm.Merge, AccessRules.FullWord, true)]
    public async Task A_landing_at_done_reads_the_setup_where_it_lands(string form, string landing, bool off)
    {
        var rule = form == LandingForm.Merge ? LandingRule.Merge : new LandingRule(LandingForm.Branch, "feature/{quest}");
        Setup(DriverConfig.Empty.WithWorkspaceLanding("aurora", rule), workspace: landing).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, "aurora", "engine", "s-1a2b3c4d");

        var landed = await new SessionTrees(_home).LandAsync(tree, new LandingSubject("s1", "q1", "Fix"), acceptedBy: AcceptedBy.Auto);

        Assert.False(landed.Landed);
        Assert.Equal(off, AutoLandingRules.CodeOf(landed) == AutoLandingCode.Off);
        if (!off) Assert.StartsWith("there is no tree at", landed.Message);
    }

    /// <summary>
    /// The session's landing plan reads the effective rule, so its instruction says the work is accepted automatically where the setup
    /// says <c>rule</c>, or full access, and goes through review under <c>person</c>.
    /// </summary>
    [Theory]
    [InlineData(AccessRules.Person, false)]
    [InlineData(AccessRules.Rule, true)]
    [InlineData(AccessRules.FullWord, true)]
    public async Task The_sessions_plan_reads_the_setups_landing(string landing, bool accepts)
    {
        Setup(DriverConfig.Empty.WithWorkspaceLanding("aurora", new LandingRule(LandingForm.Branch, "feature/{quest}")), workspace: landing)
            .Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, "aurora", "engine", "s-1a2b3c4d");

        var plan = await new SessionTrees(_home).PlanAsync(tree, new LandingSubject("s1", "q1", "Fix"));

        Assert.Equal((LandingForm.Branch, "feature/q1", accepts), (plan.Form, plan.Target, plan.AutoAccept));
    }

    /// <summary>
    /// D184 point 5: the setup accepts as the switch does, and is held as the switch is. A done held for the person's yes or evidence's
    /// verdict waits under <c>rule</c>, said once and read again at every look; a done turned down closes it, and nothing lands.
    /// </summary>
    [Fact]
    public async Task A_held_done_stays_held_under_the_setups_rule()
    {
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, "aurora", "engine", "s-1a2b3c4d");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "work.txt"), "the work");
        var due = new AutoLandings(_home);
        due.Due(new AutoLanding("s1", "q1", "engine", "aurora", tree, At));
        var world = new World();
        world.Quests["q1"] = new QuestView("q1", "game", "engine", "Fix the gap", "Fix it.", "Done") { Held = true, Hold = EvidenceCodes.MissingHold };
        var config = Setup(DriverConfig.Empty.WithWorkspaceLanding("aurora", new LandingRule(LandingForm.Branch, "feature/{quest}")),
            workspace: AccessRules.Rule);
        var lander = new AutoLander(_home, world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)));

        var (chosen, said) = await lander.ChooseAsync(config);

        Assert.Empty(chosen);
        Assert.StartsWith("landing  session s1 (#q1 → engine): its quest is done and held for your yes", Assert.Single(said));
        Assert.Equal(AutoLandingCode.Held, due.Of("s1")!.Last!.Code);
        var again = await lander.ChooseAsync(config);
        Assert.Empty(again.Chosen);
        Assert.Empty(again.Said);
        Assert.Null(due.Of("s1")!.Closed);

        world.Quests["q1"] = world.Quests["q1"] with { Status = "Open", Held = false, Hold = null };
        Assert.Contains("its quest is no longer done", Assert.Single((await lander.ChooseAsync(config)).Said));
        Assert.Equal(AutoLandingCode.Undone, due.Of("s1")!.Last!.Code);
        Assert.Empty(new LandedBranches(_home).All());
    }

    /// <summary>
    /// The landing's record says the setup accepted it: its rule's <c>access</c>, where the setup turned a branch rule's switch on, kept
    /// beside the rule's own source and read back. A record from before, and one the rule's own switch accepted, read none.
    /// </summary>
    [Fact]
    public void The_landing_record_says_the_setup_accepted_it()
    {
        var landings = new LandedBranches(_home);
        landings.Record(new LandedBranch("engine", "aurora", "feature/q1", "main", "tip00001", "s1", "q1", "Fix the gap", At)
        {
            AcceptedBy = AcceptedBy.Auto,
            Rule = new LandedRule(null, AutoAccept: true, LandingSource.Workspace) { Access = AccessRules.FromMachine },
        });
        landings.Record(new LandedBranch("engine", "aurora", "feature/q2", "main", "tip00002", "s2", "q2", "Fix the gap", At)
        {
            AcceptedBy = AcceptedBy.Auto,
            Rule = new LandedRule(null, AutoAccept: true, LandingSource.Workspace),
        });

        Assert.Matches("\"source\": \"workspace\",\\s*\"access\": \"machine\"", File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Landings)));
        var again = new LandedBranches(_home);
        Assert.Equal(new LandedRule(null, true, LandingSource.Workspace) { Access = AccessRules.FromMachine }, again.Landing("s1")!.Rule);
        Assert.Null(again.Landing("s2")!.Rule!.Access);
    }

    /// <summary>The service's quests and records, standing in: none of its records holds the tree.</summary>
    private sealed class World : IAutoLandingWorld
    {
        public Dictionary<string, QuestView> Quests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult(Quests.GetValueOrDefault(id));

        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([]);

        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([.. Quests.Values]);

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult<OpinionView?>(null);
    }

    // ——— when a due session is tried again

    /// <summary>
    /// A refusal is tried again only when the tree's tip or status changed since it (design §2); a hold is read again at every
    /// look, since its release is the quest's and not the tree's; a first try always runs.
    /// </summary>
    [Theory]
    [InlineData(null, "tip1", "clean", "tip1", "clean", true)]
    [InlineData(AutoLandingCode.Held, "tip1", "clean", "tip1", "clean", true)]
    // FABRIC-CLOSE1h2: a push held back waits on its quest, not its tree, as a hold does.
    [InlineData(AutoLandingCode.PushHeld, "tip1", "clean", "tip1", "clean", true)]
    [InlineData(AutoLandingCode.Uncommitted, "tip1", "sha256:aaaa", "tip1", "sha256:aaaa", false)]
    [InlineData(AutoLandingCode.Uncommitted, "tip1", "sha256:aaaa", "tip1", "sha256:bbbb", true)]
    [InlineData(AutoLandingCode.Uncommitted, "tip1", "sha256:aaaa", "tip2", "clean", true)]
    [InlineData(AutoLandingCode.Exists, "tip1", "clean", "tip1", "clean", false)]
    [InlineData(AutoLandingCode.Exists, "tip1", "clean", "tip2", "clean", true)]
    [InlineData(AutoLandingCode.Refused, "tip1", "clean", "tip1", "clean", false)]
    [InlineData(AutoLandingCode.Refused, "tip1", "clean", "tip1", "sha256:cccc", true)]
    public void A_refusal_is_tried_again_only_when_the_trees_tip_or_status_moves(
        string? last, string? triedTip, string triedStatus, string? tip, string status, bool expected)
    {
        var entry = Due();
        if (last is not null) entry = entry with { Tries = [new AutoTry(At, last) { Tip = triedTip, Status = triedStatus }] };

        Assert.Equal(expected, AutoLandingRules.ShouldTry(entry, tip, status));
    }

    /// <summary>The tree's status as a fact a try is kept with: clean, or a fingerprint of what git listed, never the paths.</summary>
    [Fact]
    public void A_trees_status_is_kept_as_clean_or_a_fingerprint_and_never_its_paths()
    {
        Assert.Equal("clean", AutoLandingRules.Fingerprint(""));
        Assert.Equal("clean", AutoLandingRules.Fingerprint("  \n"));
        var dirty = AutoLandingRules.Fingerprint(" M src/secret-plan.ts\n?? notes.txt\n");
        Assert.StartsWith("sha256:", dirty);
        Assert.DoesNotContain("secret", dirty);
        Assert.Equal(dirty, AutoLandingRules.Fingerprint(" M src/secret-plan.ts\r\n?? notes.txt"));
        Assert.NotEqual(dirty, AutoLandingRules.Fingerprint(" M src/secret-plan.ts\n"));
    }

    /// <summary>
    /// What a landing came to, by the code its try keeps (design §8): landed, a plugin that could not land here, a plugin that
    /// failed or did not push, and each refusal by the landing's own code.
    /// </summary>
    [Fact]
    public void A_landings_outcome_is_kept_by_its_code()
    {
        Assert.Equal(AutoLandingCode.Landed, AutoLandingRules.CodeOf(new TreeLanding(true, "put the work on `feature/x`.", "feature/x")));
        Assert.Equal(AutoLandingCode.Landed, AutoLandingRules.CodeOf(new TreeLanding(true, "", "feature/x",
            new PluginLanding("acme.lands", Pushed: true, "https://example.test/pull/7", "pushed"))));
        Assert.Equal(AutoLandingCode.PluginFailed, AutoLandingRules.CodeOf(new TreeLanding(true, "", "feature/x",
            new PluginLanding("acme.lands", Pushed: false, null, "gh is not signed in"))));
        Assert.Equal(AutoLandingCode.PluginFailed, AutoLandingRules.CodeOf(new TreeLanding(true, "", "feature/x",
            new PluginLanding("acme.lands", Pushed: false, null, "did not answer", Failed: true))));
        Assert.Equal(AutoLandingCode.PluginUnready,
            AutoLandingRules.CodeOf(new TreeLanding(true, "", "feature/x") { Unready = "plugin `acme.lands` is switched off" }));
        // FABRIC-CLOSE1h2: the branch made, its push held back just before the plugin was told.
        Assert.Equal(AutoLandingCode.PushHeld,
            AutoLandingRules.CodeOf(new TreeLanding(true, "", "feature/x") { Withheld = "its quest was held before its plugin was told" }));
        Assert.Equal(AutoLandingCode.Exists, AutoLandingRules.CodeOf(new TreeLanding(false, "already a branch") { Refusal = AutoLandingCode.Exists }));
        Assert.Equal(AutoLandingCode.Uncommitted, AutoLandingRules.CodeOf(new TreeLanding(false, "") { Refusal = AutoLandingCode.Uncommitted }));
        Assert.Equal(AutoLandingCode.Nothing, AutoLandingRules.CodeOf(new TreeLanding(false, "") { Refusal = AutoLandingCode.Nothing }));
        // SQUASHTIDY1f: work the line holds by content is refused at every door, the look's among them, by its own code.
        Assert.Equal(AutoLandingCode.Carried, AutoLandingRules.CodeOf(new TreeLanding(false, "") { Refusal = AutoLandingCode.Carried }));
        Assert.Equal(AutoLandingCode.Refused, AutoLandingRules.CodeOf(new TreeLanding(false, "git would not make it")));
    }

    /// <summary>Which codes end an entry, and which leave it waiting for a change, a release or the person's press.</summary>
    [Theory]
    [InlineData(AutoLandingCode.Landed, true)]
    [InlineData(AutoLandingCode.Nothing, true)]
    [InlineData(AutoLandingCode.PluginUnready, true)]
    [InlineData(AutoLandingCode.PluginFailed, true)]
    [InlineData(AutoLandingCode.Already, true)]
    [InlineData(AutoLandingCode.Superseded, true)]
    [InlineData(AutoLandingCode.Gone, true)]
    [InlineData(AutoLandingCode.Undone, true)]
    [InlineData(AutoLandingCode.Off, true)]
    // SQUASHTIDY1f: its work is on the line or a branch of the person's already, so no later look lands it; its discard is the way.
    [InlineData(AutoLandingCode.Carried, true)]
    [InlineData(AutoLandingCode.Held, false)]
    // FABRIC-CLOSE1h2: its branch waits to be handed on once its quest stands again.
    [InlineData(AutoLandingCode.PushHeld, false)]
    [InlineData(AutoLandingCode.Uncommitted, false)]
    [InlineData(AutoLandingCode.Exists, false)]
    [InlineData(AutoLandingCode.Refused, false)]
    public void A_code_ends_its_entry_or_leaves_it_waiting(string code, bool closes) =>
        Assert.Equal(closes, AutoLandingCode.Closes(code));

    // ——— the due list's file

    /// <summary>
    /// <c>&lt;home&gt;/sessions/auto-landings.json</c>, written whole beside and renamed, LF, BOM-less: when each became due, each try's
    /// tip, status and code, and whether it closed. Read back whole.
    /// </summary>
    [Fact]
    public void The_due_list_keeps_when_each_became_due_and_each_try_and_reads_back_whole()
    {
        var due = new AutoLandings(_home);
        due.Due(Due("s1"));
        due.Due(Due("s2", "q2"));

        due.Tried("s1", new AutoTry(At.AddMinutes(1), AutoLandingCode.Uncommitted) { Tip = "abc1234", Status = "sha256:aaaa", Uncommitted = 2 }, close: false);
        due.Tried("s2", new AutoTry(At.AddMinutes(2), AutoLandingCode.Landed) { Tip = "def5678", Status = "clean", Branch = "feature/q2-fix", Commits = 3 }, close: true);

        var path = DaorisHome.Entry(_home, HomeEntry.Sessions,AutoLandings.FileName);
        Assert.Equal(path, due.FilePath);
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("\r", text);
        Assert.False(File.ReadAllBytes(path).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Contains("\"dueAt\"", text);
        Assert.Empty(Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Sessions), "*.writing"));

        var again = new AutoLandings(_home);
        Assert.Equal(["s1"], again.Open().Select(entry => entry.Session));
        var first = again.Of("s1")!;
        Assert.Equal(("engine", "aurora", "q1", At), (first.Repository, first.Workspace, first.Quest, first.DueAt));
        var tried = Assert.Single(first.Tries);
        Assert.Equal((AutoLandingCode.Uncommitted, "abc1234", "sha256:aaaa", 2), (tried.Code, tried.Tip, tried.Status, tried.Uncommitted));
        var second = again.Of("s2")!;
        Assert.NotNull(second.Closed);
        Assert.Equal(("feature/q2-fix", 3), (second.Tries[0].Branch, second.Tries[0].Commits));
    }

    /// <summary>
    /// A session concluded again (words sent to it after its landing, D137) is due again, its earlier tries kept: what it
    /// came to before is a fact of its own.
    /// </summary>
    [Fact]
    public void A_session_due_again_reopens_with_its_earlier_tries_kept()
    {
        var due = new AutoLandings(_home);
        due.Due(Due("s1"));
        due.Tried("s1", new AutoTry(At, AutoLandingCode.Landed) { Branch = "feature/q1-fix" }, close: true);

        due.Due(Due("s1") with { DueAt = At.AddHours(1) });

        var entry = Assert.Single(due.Open());
        Assert.Equal(At.AddHours(1), entry.DueAt);
        Assert.Equal([AutoLandingCode.Landed], entry.Tries.Select(each => each.Code));
    }

    /// <summary>
    /// FABRIC-CLOSE1h6 (D137; D173's FABRIC-CLOSE1h note, *Enrolled and kept due*): a session due again after a try that closed its
    /// entry is judged afresh, whatever that try's code, with its tree as it stood: the closed try was the earlier due's. Every wake
    /// looks at it, as at a first try. Its earlier tries stay as history.
    /// </summary>
    [Theory]
    [InlineData(AutoLandingCode.Landed)]
    [InlineData(AutoLandingCode.Advanced)]
    [InlineData(AutoLandingCode.Nothing)]
    [InlineData(AutoLandingCode.PluginUnready)]
    [InlineData(AutoLandingCode.PluginFailed)]
    [InlineData(AutoLandingCode.Already)]
    [InlineData(AutoLandingCode.Superseded)]
    [InlineData(AutoLandingCode.Gone)]
    [InlineData(AutoLandingCode.Undone)]
    [InlineData(AutoLandingCode.Off)]
    [InlineData(AutoLandingCode.Carried)]
    public void A_session_due_again_after_a_try_that_closed_it_is_judged_afresh(string code)
    {
        var due = new AutoLandings(_home);
        due.Due(Due("s1"));
        due.Tried("s1", new AutoTry(At, code) { Tip = "tip1", Status = "clean" }, close: AutoLandingCode.Closes(code));
        Assert.False(due.Of("s1")!.Reopened, "a closed entry is no due one");

        due.Due(Due("s1") with { DueAt = At.AddHours(1) });

        var entry = Assert.Single(due.Open());
        Assert.True(entry.Reopened);
        Assert.True(AutoLandingRules.ShouldTry(entry, "tip1", "clean"));
        Assert.Equal(LandingWaits.First, LandingWaits.Of(entry));
        Assert.Equal([code], entry.Tries.Select(each => each.Code));
    }

    /// <summary>
    /// An entry still due on its own last try keeps today's rule when its session concludes again (FABRIC-CLOSE1h6): a refusal waits for
    /// its tree to move, and a hold or a push held back is read at every look, as before.
    /// </summary>
    [Theory]
    [InlineData(AutoLandingCode.Uncommitted, false)]
    [InlineData(AutoLandingCode.Exists, false)]
    [InlineData(AutoLandingCode.Completed, false)]
    [InlineData(AutoLandingCode.Refused, false)]
    [InlineData(AutoLandingCode.Held, true)]
    [InlineData(AutoLandingCode.PushHeld, true)]
    public void An_entry_still_due_on_its_last_try_keeps_its_rule_when_due_again(string code, bool tried)
    {
        var due = new AutoLandings(_home);
        due.Due(Due("s1"));
        due.Tried("s1", new AutoTry(At, code) { Tip = "tip1", Status = "clean" }, close: AutoLandingCode.Closes(code));

        due.Due(Due("s1") with { DueAt = At.AddHours(1) });

        var entry = Assert.Single(due.Open());
        Assert.False(entry.Reopened);
        Assert.Equal(tried, AutoLandingRules.ShouldTry(entry, "tip1", "clean"));
    }

    /// <summary>A file that does not read is no due list, never a failure; and a closed entry beyond the bound is dropped, an open one never.</summary>
    [Fact]
    public void A_file_that_does_not_read_is_none_and_closed_entries_are_bounded()
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Sessions,AutoLandings.FileName), "{ not json");
        Assert.Empty(new AutoLandings(_home).All());

        var due = new AutoLandings(_home);
        due.Due(Due("open"));
        for (var i = 0; i < AutoLandings.ClosedKept + 5; i++)
        {
            due.Due(Due($"s{i}"));
            due.Tried($"s{i}", new AutoTry(At.AddMinutes(i), AutoLandingCode.Nothing), close: true);
        }

        var all = new AutoLandings(_home).All();
        Assert.Equal(AutoLandings.ClosedKept, all.Count(entry => entry.Closed is not null));
        Assert.Contains(all, entry => entry.Session == "open" && entry.Closed is null);
        Assert.DoesNotContain(all, entry => entry.Session == "s0");
        Assert.Contains(all, entry => entry.Session == $"s{AutoLandings.ClosedKept + 4}");
    }

    // ——— what the conversation keeps of each, by code

    /// <summary>
    /// Each try the conversation is told of is a note whose lead-in is a code (LANG1a), worded in both catalogues, with the
    /// landing's own sentence beneath it as a part of its own; an acceptance reads as one, for the trace.
    /// </summary>
    [Fact]
    public void Each_try_is_a_coded_note_and_an_acceptance_reads_as_one()
    {
        var landed = new TreeLanding(true, "put the work on `feature/q1-fix` — 2 commit(s) from `main`.", "feature/q1-fix");

        var note = AutoLandingNotes.Of(AutoLandingCode.Landed, landed);

        Assert.Equal(SessionEventKind.Note, note.Kind);
        Assert.StartsWith(LandingRules.AutoAccepted, note.Text);
        Assert.EndsWith(landed.Message, note.Text);
        Assert.Equal(NoteCodes.LandingAccepted.Code, note.Parts![0].Code);
        Assert.Equal("feature/q1-fix", note.Parts[0].Value("branch"));
        Assert.Equal((landed.Message, NoteBy.Program), (note.Parts[1].Words, note.Parts[1].By));
        Assert.True(LandingRules.IsAcceptance(note.Text!));

        var unready = AutoLandingNotes.Of(AutoLandingCode.PluginUnready,
            new TreeLanding(true, "put the work on `feature/q1-fix`.", "feature/q1-fix") { Unready = "plugin `acme.lands` is switched off on this machine." },
            plugin: "acme.lands");
        Assert.Equal(("feature/q1-fix", "acme.lands"), (unready.Parts![0].Value("branch") as string, unready.Parts[0].Value("plugin") as string));
        Assert.Contains("`trees hand`", unready.Text);
        Assert.True(LandingRules.IsAcceptance(unready.Text!));

        var dirty = AutoLandingNotes.Of(AutoLandingCode.Uncommitted, landed: null, uncommitted: 3);
        Assert.Equal(3, dirty.Parts![0].Value("paths"));
        Assert.Single(dirty.Parts);
        Assert.False(LandingRules.IsAcceptance(dirty.Text!));

        Assert.Equal(NoteCodes.LandingNothing.Code, AutoLandingNotes.Of(AutoLandingCode.Nothing, landed: null).Parts![0].Code);
        Assert.Equal(NoteCodes.LandingHeld.Code, AutoLandingNotes.Of(AutoLandingCode.Held, landed: null).Parts![0].Code);
        Assert.Equal(NoteCodes.LandingNoTree.Code, AutoLandingNotes.Concluded(AutoConcluded.NoTree).Parts![0].Code);
        Assert.Equal(NoteCodes.LandingNotDone.Code, AutoLandingNotes.Concluded(AutoConcluded.NotDone).Parts![0].Code);
    }

    /// <summary>Every code a try can keep that the conversation is told of has a note code; the rest stay the due list's and the log's.</summary>
    [Fact]
    public void The_codes_a_note_says_each_have_a_note_code()
    {
        foreach (var code in new[]
                 {
                     AutoLandingCode.Landed, AutoLandingCode.PluginUnready, AutoLandingCode.PluginFailed, AutoLandingCode.Nothing,
                     AutoLandingCode.Held, AutoLandingCode.Uncommitted, AutoLandingCode.Exists, AutoLandingCode.Refused,
                     AutoLandingCode.PushHeld,
                 })
        {
            Assert.True(AutoLandingNotes.Says(code), code);
            var part = AutoLandingNotes.Of(code, new TreeLanding(code is AutoLandingCode.Landed or AutoLandingCode.PluginUnready or AutoLandingCode.PluginFailed, "said.", "feature/x"), plugin: "acme.lands", uncommitted: 1).Parts![0];
            Assert.Contains(NoteCodes.All, each => each.Code == part.Code);
        }

        // FABRIC-CLOSE1h2: a push held back is said by a code of its own, naming the branch and the plugin not told.
        var held = AutoLandingNotes.Of(AutoLandingCode.PushHeld, new TreeLanding(true, "said.", "feature/x"), plugin: "acme.lands").Parts![0];
        Assert.Equal(NoteCodes.LandingPushHeld.Code, held.Code);

        foreach (var code in new[] { AutoLandingCode.Already, AutoLandingCode.Superseded, AutoLandingCode.Gone, AutoLandingCode.Undone, AutoLandingCode.Off })
        {
            Assert.False(AutoLandingNotes.Says(code), code);
        }
    }

    /// <summary>The machine log's line (D94): codes and counts, the plugin's id, never a sentence, a branch's words or a path.</summary>
    [Fact]
    public void The_logs_line_is_codes_and_counts_and_never_words()
    {
        var line = LandingLine.Auto("s1", "engine", "aurora", AutoLandingCode.PluginFailed, commits: 2, uncommitted: null, plugin: "acme.lands", pushed: false);

        Assert.Equal("landing.auto", line.Event);
        Assert.Equal(["session", "repository", "workspace", "code", "commits", "uncommitted", "plugin", "pushed"], line.Data.Select(pair => pair.Key));
        Assert.Equal("acme.lands", line.Data.Single(pair => pair.Key == "plugin").Value);
        Assert.Null(LandingLine.Auto("s1", "engine", "aurora", AutoLandingCode.Landed, null, null, plugin: "not a plugin's name", pushed: null)
            .Data.Single(pair => pair.Key == "plugin").Value);
    }
}
