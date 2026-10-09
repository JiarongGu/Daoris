using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1f (D157 points 4, 6, 10 and 11; the workflow design §2.3, §3.7, §4.4–§4.6, §5): the gate reads the run's bound version.
/// A state table over a named version against Current: each step's process read from the version over what the repository
/// declares (the landing's form, press, pattern and plugin; the look's presence and environment; the opinion's occasions, its
/// switches and which declared reviewers), Current read live as today; a step that cannot start sitting with its reason, at the
/// look, the opinion and the landing; a workflow that cannot be read here holding its run, never swapped; and a kind's paths held
/// where its workflow asks less of the person. Pure, or over a scratch home: the fast half (MOD8). <c>ReviewLandingTests</c> and
/// <c>AutoLandingTests</c> hold the doors over real git.
/// </summary>
public sealed class WorkflowGateTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 9, 9, 12, 30, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-workflow-gate-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Everything declared for `web-app`: a branch rule with its plugin and tidy, two environments, two reviewers.</summary>
    private const string Declared = """
        {"landings":{"web-app":{"form":"branch","pattern":"feature/{quest}","plugin":"example.pull-request","tidy":true}},
         "reviews":{"web-app":{"required":true,"environments":[
           {"name":"local","kind":"local","procedure":"README.md","address":"http://localhost:4200"},
           {"name":"dev","kind":"deployed","procedure":"docs/deploying.md"}]}},
         "opinions":{"web-app":{"reviewers":["codex-acp","dsh"],"on":["landing"],"verify":true,"minutes":30}}}
        """;

    private static DriverConfig Config(string json) => DriverConfig.Parse(json);

    private static IReadOnlyList<NamedStep> Steps(string json)
    {
        using var document = JsonDocument.Parse(json);
        var (steps, problem) = WorkflowNamed.ReadSteps(document.RootElement.Clone(), 2);
        Assert.True(problem is null, problem);
        return steps!;
    }

    private static WorkflowRunBinding Binding(string level = WorkflowLevels.Repository, IReadOnlyList<string>? paths = null) =>
        new("q1", "web-app", "work", "docs-to-pr", level, At)
        {
            Version = 2, Kind = paths is null ? null : "docs", Label = paths is null ? null : "Documentation", Paths = paths ?? [],
        };

    /// <summary>A process in one line: how it lands, rows 5 and 6 of the look, and the opinion, each as read.</summary>
    private static string Summary(WorkflowProcess process)
    {
        var rule = process.Landing.Rule;
        var landing = (rule.Form == LandingForm.Merge ? "merge" : $"branch {rule.Pattern ?? "-"} plugin {rule.Plugin ?? "-"}")
            + (rule.AutoAccept ? " automatic" : " you") + (rule.Tidy ? " tidy" : "");
        var look = process.Look is null ? "rule" : process.Look.Cannot is not null ? "cannot" : process.Look.Environment ?? "none";
        var opinion = process.OpinionCannot is not null ? "cannot"
            : process.Opinion is not { } read ? "none"
            : $"{string.Join(",", read.Rule.Reviewers)} on {string.Join(",", read.Rule.On)}" + (read.Rule.Required ? " required" : "")
              + (read.Rule.Recheck ? "" : " no-recheck") + (read.Rule.Verify ? " verify" : "") + (read.Rule.Minutes is { } minutes ? $" {minutes}min" : "");
        return $"landing {landing} | look {look} | opinion {opinion}" + (process.LandingCannot is not null ? " | landing cannot" : "");
    }

    public static TheoryData<string, string, string?, string> Rows => new()
    {
        {
            "Current: the rules live, as today", Declared, null,
            "landing branch feature/{quest} plugin example.pull-request you tidy | look rule | opinion codex-acp,dsh on landing verify 30min"
        },
        {
            "a pull request with no press: no look and no opinion, whatever is declared", Declared,
            """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic"}]""",
            "landing branch feature/{quest} plugin example.pull-request automatic tidy | look none | opinion none"
        },
        {
            "each step's settings the version's, among the declared reviewers and environments", Declared,
            """
            [{"id":"work","kind":"work"},{"id":"opinion","kind":"opinion","reviewers":["DSH"],"required":true,"steps":true,"recheck":false},
             {"id":"look","kind":"look","environment":"dev"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]
            """,
            "landing merge you tidy | look dev | opinion dsh on landing,steps required no-recheck verify 30min"
        },
        {
            "absent fields read the declarations: the reviewers, the first environment, the rule's plugin unless `none`", Declared,
            """
            [{"id":"work","kind":"work"},{"id":"opinion","kind":"opinion"},{"id":"look","kind":"look"},
             {"id":"land","kind":"landing","form":"branch","accept":"you","pattern":"docs/{quest}","plugin":"none"}]
            """,
            "landing branch docs/{quest} plugin - you tidy | look local | opinion codex-acp,dsh on landing verify 30min"
        },
        {
            "an opinion naming a reviewer the repository does not declare cannot start", Declared,
            """[{"id":"work","kind":"work"},{"id":"opinion","kind":"opinion","reviewers":["claude-code"]},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""",
            "landing merge you tidy | look none | opinion cannot"
        },
        {
            "a look in an environment the repository does not declare cannot start", Declared,
            """[{"id":"work","kind":"work"},{"id":"look","kind":"look","environment":"staging"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""",
            "landing merge you tidy | look cannot | opinion none"
        },
        {
            "a repository's `false` declares no environment, whatever its workspace's says: the look cannot start",
            """{"reviews":{"web-app":false},"workspaceReviews":{"work":{"required":true,"environments":[{"name":"local","kind":"local","procedure":"README.md","address":"http://localhost:4200"}]}}}""",
            """[{"id":"work","kind":"work"},{"id":"look","kind":"look"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""",
            "landing merge you | look cannot | opinion none"
        },
        {
            "nothing declared: each step needing a declaration cannot start, the branch's pattern among them", "{}",
            """[{"id":"work","kind":"work"},{"id":"opinion","kind":"opinion"},{"id":"look","kind":"look"},{"id":"land","kind":"landing","form":"branch","accept":"you"}]""",
            "landing branch - plugin - you | look cannot | opinion cannot | landing cannot"
        },
        {
            "the workspace's declarations reach a repository that sets none", """
            {"workspaceLandings":{"work":{"form":"branch","pattern":"work/{quest}"}},
             "workspaceOpinions":{"work":{"reviewers":["codex-acp"]}}}
            """,
            """[{"id":"work","kind":"work"},{"id":"opinion","kind":"opinion","required":true},{"id":"land","kind":"landing","form":"branch","accept":"automatic"}]""",
            "landing branch work/{quest} plugin - automatic | look none | opinion codex-acp on landing required"
        },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Each_steps_process_is_read_from_the_version_over_what_is_declared(string name, string config, string? steps, string expected)
    {
        var process = steps is null
            ? WorkflowProcesses.Current(Config(config), "web-app", "work")
            : WorkflowProcesses.Of(Config(config), "web-app", "work", Binding(), Steps(steps));

        Assert.True(expected == Summary(process), $"{name}\n  expected {expected}\n  actual   {Summary(process)}");
        Assert.Equal(steps is not null, process.Named);
    }

    [Fact]
    public void A_step_that_cannot_start_says_why_and_names_the_Setup_row_that_declares_what_it_needs()
    {
        var process = WorkflowProcesses.Of(Config("{}"), "web-app", "work", Binding(), Steps(
            """[{"id":"work","kind":"work"},{"id":"opinion","kind":"opinion"},{"id":"look","kind":"look","environment":"local"},{"id":"land","kind":"landing","form":"branch","accept":"you"}]"""));

        Assert.Equal(
            "its second opinion `opinion` reads with reviewers `web-app` declares, and it declares none: `daoris driver opinion web-app "
            + "--reviewers <adapter,adapter>` declares them.",
            process.OpinionCannot);
        Assert.Equal(
            "its look `look` reads the work in `local`, and `web-app` declares no review environment: `daoris driver review web-app local "
            + "--kind local|deployed --procedure <path>` declares one.",
            process.Look!.Cannot);
        Assert.Equal(
            "its landing `land` puts the work on a branch, and neither the step nor `web-app`'s landing rule names a pattern for it: "
            + "`daoris driver landing web-app branch <pattern>` declares one.",
            process.LandingCannot);
        Assert.Equal(
            "It follows `docs-to-pr` v2, and its landing `land` puts the work on a branch, and neither the step nor `web-app`'s landing rule "
            + "names a pattern for it: `daoris driver landing web-app branch <pattern>` declares one. Nothing was landed.",
            WorkflowGate.LandingSays(process));

        var beyond = WorkflowProcesses.Of(Config(Declared), "web-app", "work", Binding(), Steps(
            """[{"id":"work","kind":"work"},{"id":"opinion","kind":"opinion","reviewers":["dsh","claude-code"]},{"id":"look","kind":"look","environment":"staging"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]"""));
        Assert.Equal(
            "its second opinion `opinion` names `claude-code`, which `web-app` does not declare among its reviewers (`codex-acp` and `dsh`): "
            + "a step chooses among them, and `daoris driver opinion web-app --reviewers <adapter,adapter>` declares more.",
            beyond.OpinionCannot);
        Assert.Equal(
            "its look `look` reads the work in `staging`, which `web-app` does not declare (it declares `local` and `dev`): `daoris driver "
            + "review web-app staging --kind local|deployed --procedure <path>` declares it.",
            beyond.Look!.Cannot);
    }

    // ——— rows 5 and 6 of the review's level, read from the version

    private static QuestView Work(string id = "q1") => new(id, "ask #a1", "web-app", "Fix the gap", "", "Done");

    [Fact]
    public async Task The_look_rows_5_and_6_are_the_versions_and_rows_1_to_4_still_decide_first()
    {
        var config = Config(Declared);
        var rule = ReviewRules.Resolve(config, "web-app", "work");

        // Current requires a look; a version with none lands without one, said by its name.
        var none = ReviewGate.Decide([Work()], "web-app", null, rule, new WorkflowLook(null, null), "`docs-to-pr` v2");
        var free = await ReviewGate.JudgeAsync(none, "abc", _ => Task.FromResult<bool?>(true));
        Assert.Equal((ReviewStates.None, ReviewLevels.Workflow), (free.State, free.Decision.Level));
        Assert.True(free.LetsGo);
        Assert.Equal("No review waits for this work: `docs-to-pr` v2 has no look.", free.Says);

        // A version's look in an environment the rule declares: waits as Current's would.
        var looks = ReviewGate.Decide([Work()], "web-app", null, rule, new WorkflowLook("dev", null), "`docs-to-pr` v2");
        Assert.Equal((ReviewLevels.Workflow, "dev"), (looks.Level, looks.Environment));
        Assert.Equal(ReviewStates.NotShown, (await ReviewGate.JudgeAsync(looks, "abc", _ => Task.FromResult<bool?>(true))).State);

        // A look that cannot start sits saying why, the skip named as its floor.
        var cannot = ReviewGate.Decide([Work()], "web-app", null, rule, new WorkflowLook(null, "its look `look` reads the work in `staging`, which it does not declare."), "`docs-to-pr` v2");
        var sits = await ReviewGate.JudgeAsync(cannot, "abc", _ => Task.FromResult<bool?>(true));
        Assert.Equal(ReviewStates.CannotStart, sits.State);
        Assert.False(sits.LetsGo);
        Assert.Equal(
            "Waits for your review, and it cannot start: `docs-to-pr` v2 asks for one, and its look `look` reads the work in `staging`, which "
            + "it does not declare. `daoris-driver quest review q1 skip \"…\"` lets it land without one.",
            sits.Says);

        // Row 1, the person's skip, and row 3, the chain's choice, still decide before the version.
        var skipped = Work() with { Verdicts = [new QuestReviewVerdictView(ReviewVerdicts.Skipped) { Words = "docs only" }] };
        Assert.Equal(ReviewLevels.Skip, ReviewGate.Decide([skipped], "web-app", null, rule, new WorkflowLook(null, cannot.Cannot), "`docs-to-pr` v2").Level);
        var off = Work() with { Review = new QuestReviewChoiceView("off") };
        Assert.Equal((ReviewLevels.Chain, (string?)null), Decided(ReviewGate.Decide([off], "web-app", null, rule, new WorkflowLook("dev", null), "`docs-to-pr` v2")));
        var on = Work() with { Review = new QuestReviewChoiceView("on") };
        Assert.Equal((ReviewLevels.Chain, "local"), Decided(ReviewGate.Decide([on], "web-app", null, rule, new WorkflowLook(null, null), "`docs-to-pr` v2")));

        static (string, string?) Decided(ReviewDecision decision) => (decision.Level, decision.Environment);
    }

    // ——— the second opinion, read from the version

    private static OpinionReads NoOpinions() => new(
        _ => Task.FromResult<OpinionView?>(null), _ => OpinionDelivered.None, _ => Task.FromResult<bool?>(true), _ => Task.FromResult<int?>(0));

    [Fact]
    public async Task An_opinion_that_cannot_start_sits_and_the_persons_own_answers_are_its_floor()
    {
        var process = WorkflowProcesses.Of(Config(Declared), "web-app", "work", Binding(), Steps(
            """[{"id":"work","kind":"work"},{"id":"opinion","kind":"opinion","reviewers":["claude-code"]},{"id":"land","kind":"landing","form":"merge","accept":"you"}]"""));
        var facts = new OpinionGateFacts("web-app", process.Opinion) { Session = "s1", Quest = Work(), Tip = "abc", Cannot = process.OpinionCannot };

        var sits = await OpinionGate.JudgeAsync(facts, NoOpinions());
        Assert.Equal(OpinionGateStates.CannotStart, sits.State);
        Assert.False(sits.LetsGo);
        Assert.StartsWith("Waits for a second opinion, and it cannot start: its second opinion `opinion` names `claude-code`", sits.Says);
        Assert.EndsWith("`daoris-driver opinion anyway s1 \"…\"` lets it land without one, or `daoris-driver opinion myself s1 \"…\"` records "
            + "your own reading.", sits.Says);

        var anyway = await OpinionGate.JudgeAsync(facts with { Person = [new OpinionPersonWord(OpinionPersonSaid.Anyway, "abc", At)] }, NoOpinions());
        Assert.Equal((OpinionGateStates.Anyway, OpinionGateStates.CannotStart), (anyway.State, anyway.Answered));
        Assert.True(anyway.LetsGo);

        // A version with no opinion step asks none, though the rule declared reads at landing.
        var none = WorkflowProcesses.Of(Config(Declared), "web-app", "work", Binding(), Steps(
            """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]"""));
        var asksNone = await OpinionGate.JudgeAsync(
            new OpinionGateFacts("web-app", none.Opinion) { Session = "s1", Quest = Work(), Tip = "abc", Cannot = none.OpinionCannot }, NoOpinions());
        Assert.Equal((OpinionGateStates.None, true), (asksNone.State, asksNone.LetsGo));
        Assert.False(OpinionGate.Reads(none.Opinion, none.OpinionCannot, OpinionRules.Landing));
        Assert.True(OpinionGate.Reads(process.Opinion, process.OpinionCannot, OpinionRules.Landing));
        Assert.False(OpinionGate.Reads(process.Opinion, process.OpinionCannot, OpinionRules.Steps));
    }

    // ——— one gate, in its order

    private static LandingGate Gate(WorkflowGateState workflow, bool opinionHolds = false, bool reviewHolds = false) =>
        new(new OpinionGateState(opinionHolds ? OpinionGateStates.NotAsked : OpinionGateStates.None, "web-app"),
            new ReviewGateState(reviewHolds ? ReviewStates.NotShown : ReviewStates.None,
                new ReviewDecision(ReviewLevels.Workflow, reviewHolds ? "local" : null) { Repository = "web-app" }))
        {
            Workflow = workflow,
        };

    [Fact]
    public void The_runs_own_part_holds_first_and_the_landing_steps_last()
    {
        var steps = Steps("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"you"}]""");
        var named = WorkflowProcesses.Of(Config("{}"), "web-app", "work", Binding(), steps);
        var broken = named with { Problem = "no workflow `docs-to-pr` is saved here any more." };

        var run = Gate(WorkflowGate.Judge(broken), opinionHolds: true, reviewHolds: true);
        Assert.Equal(AutoLandingCode.Refused, run.Refusal!.Refusal);
        Assert.StartsWith("Its run was bound to `docs-to-pr` v2, `web-app`'s default, which cannot start it here:", run.Refusal.Message);
        Assert.True(run.WorkflowHolds);

        var opinion = Gate(WorkflowGate.Judge(named), opinionHolds: true, reviewHolds: true);
        Assert.Equal(AutoLandingCode.Opinion, opinion.Refusal!.Refusal);
        Assert.False(opinion.WorkflowHolds);
        Assert.Equal(AutoLandingCode.Unreviewed, Gate(WorkflowGate.Judge(named), reviewHolds: true).Refusal!.Refusal);

        var landing = Gate(WorkflowGate.Judge(named));
        Assert.Equal(AutoLandingCode.Refused, landing.Refusal!.Refusal);
        Assert.Equal(WorkflowGate.LandingSays(named), landing.Refusal.Message);
        Assert.True(landing.WorkflowHolds);
        Assert.False(landing.LetsGo);

        var current = Gate(WorkflowGate.Judge(WorkflowProcesses.Current(Config("{}"), "web-app", "work")));
        Assert.Null(current.Refusal);
        Assert.Empty(LandingGateWords.Plan(current));
        Assert.Equal(["trees: It follows `docs-to-pr` v2, `web-app`'s default.", $"trees: {WorkflowGate.LandingSays(named)}"], LandingGateWords.Plan(landing));
    }

    // ——— a kind's paths

    public static TheoryData<string, string, bool> Globs => new()
    {
        { "docs/**", "docs/guide.md", true },
        { "docs/**", "docs/deep/in/it.png", true },
        { "docs/**", "src/docs/guide.md", false },
        { "**/*.md", "README.md", true },
        { "**/*.md", "src/app/notes.md", true },
        { "**/*.md", "src/app/report.ts", false },
        { "*.md", "README.md", true },
        { "*.md", "docs/README.md", false },
        { "docs/*.md", "docs/a/b.md", false },
        { "src/?.ts", "src/a.ts", true },
        { "src/?.ts", "src/ab.ts", false },
        { "{docs,guides}/**", "guides/start.md", true },
        { "{docs,guides}/**", "src/start.md", false },
        { "Docs/**", "docs/guide.md", false },
        { "docs/a+b.md", "docs/a+b.md", true },
        { "docs/{a.md", "docs/{a.md", true },
    };

    [Theory]
    [MemberData(nameof(Globs))]
    public void A_kinds_glob_keeps_a_path_as_git_names_it(string glob, string path, bool keeps) =>
        Assert.Equal(keeps, KindPaths.Matches(glob, path));

    public static TheoryData<string, string, string, string> Lowerings => new()
    {
        { "a look dropped", """[{"id":"work","kind":"work"},{"id":"look","kind":"look"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""",
            """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""", "it drops your look" },
        { "an opinion dropped", """[{"id":"work","kind":"work"},{"id":"opinion","kind":"opinion"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""",
            """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""", "it drops the second opinion" },
        { "an opinion no longer required", """[{"id":"work","kind":"work"},{"id":"opinion","kind":"opinion","required":true},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""",
            """[{"id":"work","kind":"work"},{"id":"opinion","kind":"opinion"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""", "it no longer requires the second opinion" },
        { "a landing made automatic", """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"you","pattern":"w/{quest}"}]""",
            """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic","pattern":"w/{quest}"}]""", "it lands with no press of yours" },
        { "a pull request skipped", """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"you","pattern":"w/{quest}"},{"id":"pr","kind":"pull-request"}]""",
            """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"you","pattern":"w/{quest}"}]""", "it skips the pull request" },
        { "a go-ahead removed", """[{"id":"work","kind":"work"},{"id":"ok","kind":"go-ahead","act":"write dev's settings","on":"dev"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""",
            """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""", "it removes the go-ahead \"write dev's settings\"" },
        { "raising is no lowering: a look added, a landing made yours", """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic","pattern":"w/{quest}"}]""",
            """[{"id":"work","kind":"work"},{"id":"look","kind":"look"},{"id":"land","kind":"landing","form":"branch","accept":"you","pattern":"w/{quest}"}]""", "" },
    };

    [Theory]
    [MemberData(nameof(Lowerings))]
    public void Lowering_the_persons_part_is_a_table(string name, string otherwise, string chosen, string lowered) =>
        Assert.True(lowered == string.Join("; ", Involvement.Lowers(Steps(chosen), Steps(otherwise))), name);

    [Fact]
    public void Current_as_steps_lowers_as_its_rules_say()
    {
        var current = Involvement.StepsOf(WorkflowCurrent.Derive(Config(Declared), "web-app", "work", []));
        Assert.Equal(["work", "opinion", "look", "landing", "pull-request"], current.Select(step => step.Kind));
        var docs = Steps("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic","pattern":"docs/{quest}"}]""");
        Assert.Equal(
            ["it drops your look", "it drops the second opinion", "it lands with no press of yours", "it skips the pull request"],
            Involvement.Lowers(docs, current));
    }

    private static WorkflowProcess Kind(WorkflowKept? kept = null) =>
        WorkflowProcesses.Of(Config(Declared), "web-app", "work",
            Binding(WorkflowLevels.WorkspaceKind, ["docs/**", "**/*.md"]) with { Kept = kept },
            Steps("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic"}]"""));

    [Fact]
    public void A_kinds_paths_hold_where_its_workflow_asks_less_of_the_person_until_they_keep_it()
    {
        IReadOnlyList<string> lowered = ["it drops your look", "it lands with no press of yours"];
        Assert.True(WorkflowGate.ChecksPaths(Kind()));
        Assert.False(WorkflowGate.ChecksPaths(Kind() with { Binding = Binding(WorkflowLevels.Repository, ["docs/**"]) }));
        Assert.False(WorkflowGate.ChecksPaths(Kind() with { Binding = Binding(WorkflowLevels.WorkspaceKind, []) }));

        var inside = WorkflowGate.Judge(Kind(), new KindPathsRead(lowered, "`feature` v3", ["docs/guide.md", "README.md"]));
        Assert.Equal(WorkflowGateStates.Follows, inside.State);
        Assert.Equal("It follows `docs-to-pr` v2, chosen by the workspace `work` for Documentation. It keeps to its kind's paths.", inside.Says);

        var outside = WorkflowGate.Judge(Kind(), new KindPathsRead(lowered, "`feature` v3",
            ["docs/guide.md", "src/app/report.ts", "src/app/a.ts", "src/app/b.ts", "src/app/c.ts"])) with { Session = "s1" };
        Assert.Equal(WorkflowGateStates.KindPaths, outside.State);
        Assert.False(outside.LetsGo);
        Assert.Equal(["src/app/report.ts", "src/app/a.ts", "src/app/b.ts", "src/app/c.ts"], outside.Outside);
        Assert.Equal(
            "Holds: this work changed 4 paths outside Documentation's (`docs/**` and `**/*.md`): `src/app/report.ts`, `src/app/a.ts`, "
            + "`src/app/b.ts` and 1 more; and `docs-to-pr` v2, chosen for it, asks less of you than `feature` v3 would without the kind (it "
            + "drops your look; it lands with no press of yours). `daoris-driver workflow keep s1 \"…\"` keeps `docs-to-pr` v2 for this work "
            + "with your words; nothing switches by itself.",
            outside.Says);

        // Lowering nothing, the paths hold nothing; kept, they hold nothing; git that cannot say holds, unread.
        Assert.Equal(WorkflowGateStates.Follows, WorkflowGate.Judge(Kind(), new KindPathsRead([], "Current", ["src/app.ts"])).State);
        var kept = WorkflowGate.Judge(Kind(new WorkflowKept(At, ReviewDoors.Terminal) { Words = "the report is the docs" }),
            new KindPathsRead(lowered, "`feature` v3", ["src/app.ts"]));
        Assert.Equal(WorkflowGateStates.Kept, kept.State);
        Assert.True(kept.LetsGo);
        Assert.EndsWith("you kept it for this work, though it changed paths outside its kind's, saying: \"the report is the docs\".", kept.Says);
        var unread = WorkflowGate.Judge(Kind(), new KindPathsRead(lowered, "`feature` v3", null));
        Assert.Equal((WorkflowGateStates.Unread, false), (unread.State, unread.LetsGo));
    }

    [Fact]
    public void Current_is_todays_and_a_named_workflow_follows_saying_what_chose_it()
    {
        var current = WorkflowGate.Judge(WorkflowProcesses.Current(Config(Declared), "web-app", "work"));
        Assert.Equal((WorkflowGateStates.None, true), (current.State, current.LetsGo));
        Assert.Null(current.Process.Record);

        var named = WorkflowGate.Judge(Kind());
        Assert.Equal(WorkflowGateStates.Follows, named.State);
        Assert.Equal(new LandingWorkflow("docs-to-pr", 2, WorkflowLevels.WorkspaceKind) { Kind = "docs" }, named.Process.Record);

        var unread = WorkflowGate.Judge(WorkflowProcesses.Current(Config(Declared), "web-app", "work") with { Unread = "its chain could not be read." });
        Assert.Equal((WorkflowGateStates.Unread, false), (unread.State, unread.LetsGo));
        Assert.Equal("Whether this work's workflow holds it could not be read: its chain could not be read. Nothing lands until it can be.", unread.Says);
    }

    // ——— read from the home: the binding, then the version it kept

    private static JsonNode Saved(string accept) => JsonNode.Parse(
        $$"""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"{{accept}}","pattern":"docs/{quest}","plugin":"none"}]""")!;

    private void Save(params string[] accepts)
    {
        foreach (var accept in accepts)
        {
            WorkflowStore.Add(_home, new WorkflowVersionAdded("docs-to-pr", "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z", Saved(accept)), kept: []);
        }
    }

    private void Bind(WorkflowRunBinding binding) => Assert.True(WorkflowRunBindings.Bind(_home, binding));

    [Fact]
    public void A_runs_process_is_its_bound_version_and_Current_or_no_binding_reads_the_rules_live()
    {
        var config = Config(Declared);
        Assert.Equal(Summary(WorkflowProcesses.Current(config, "web-app", "work")), Summary(WorkflowProcesses.Read(_home, config, "web-app", "work", "q1")));
        Assert.Null(WorkflowProcesses.Read(_home, config, "web-app", "work", "q1").Binding);

        Bind(WorkflowRunBindings.Plan(config, _home, "q1", "web-app", "work", null, null, [], At));
        var current = WorkflowProcesses.Read(_home, config, "web-app", "work", "q1");
        Assert.False(current.Named);
        Assert.Equal(WorkflowSelection.Current, current.Binding!.Workflow);
        Assert.Null(current.Look);

        // A newer version saved after the run bound changes new work only (design §2.6).
        Save("automatic");
        Bind(WorkflowRunBindings.Plan(Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}"""), _home, "q2", "web-app", "work", null, null, [], At));
        Save("you");
        var named = WorkflowProcesses.Read(_home, config, "web-app", "work", "q2");
        Assert.True(named.Named);
        Assert.Equal(1, named.Binding!.Version);
        Assert.Equal("landing branch docs/{quest} plugin - automatic tidy | look none | opinion none", Summary(named));
    }

    [Fact]
    public void A_bound_workflow_that_cannot_be_read_here_holds_its_run_and_nothing_is_swapped_in()
    {
        var config = Config(Declared);
        var choice = Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}""");

        Bind(WorkflowRunBindings.Plan(choice, _home, "gone", "web-app", "work", null, null, [], At));
        Assert.Equal("no workflow `docs-to-pr` is saved here.", WorkflowProcesses.Read(_home, config, "web-app", "work", "gone").Problem);

        Save("automatic");
        Bind(WorkflowRunBindings.Plan(choice, _home, "q1", "web-app", "work", null, null, [], At));
        File.Delete(WorkflowStore.PathOf(_home, "docs-to-pr"));
        var deleted = WorkflowProcesses.Read(_home, config, "web-app", "work", "q1");
        Assert.Equal("no workflow `docs-to-pr` is saved here any more.", deleted.Problem);
        Assert.Null(deleted.Opinion);
        Assert.Null(deleted.Record);
        Assert.Equal(WorkflowGateStates.CannotStart, WorkflowGate.Judge(deleted).State);

        // A version edited by hand since its run bound it is not the version it bound.
        Save("automatic");
        var path = WorkflowStore.PathOf(_home, "docs-to-pr");
        var file = JsonNode.Parse(File.ReadAllText(path))!;
        file["versions"]![0]!["steps"]![1]!["accept"] = "you";
        File.WriteAllText(path, file.ToJsonString());
        Assert.StartsWith("`docs-to-pr` v1 is not the version its run was bound to", WorkflowProcesses.Read(_home, config, "web-app", "work", "q1").Problem);
    }

    [Fact]
    public async Task A_chain_the_service_cannot_name_holds_only_where_a_named_workflow_is_bound_here()
    {
        var config = Config(Declared);
        var world = new Unanswering();

        Assert.Null((await WorkflowProcesses.ReadAsync(_home, config, world, "q9", "web-app", "work")).Unread);

        Save("automatic");
        Bind(WorkflowRunBindings.Plan(Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}"""), _home, "q1", "web-app", "work", null, null, [], At));
        Assert.True((await WorkflowProcesses.ReadAsync(_home, config, world, "q1", "web-app", "work")).Named, "the quest's own id names its run");
        var unread = await WorkflowProcesses.ReadAsync(_home, config, world, "q9", "web-app", "work");
        Assert.StartsWith("its chain could not be read, so whether a named workflow governs it is not known:", unread.Unread);
        Assert.Null((await WorkflowProcesses.ReadAsync(_home, config, world, "q9", "notes-site", "work")).Unread);
    }

    private sealed class Unanswering : IReviewWorld
    {
        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => throw new HttpRequestException("the service did not answer");

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => throw new HttpRequestException("the service did not answer");
    }

    [Fact]
    public void The_persons_keep_is_kept_on_the_run_whole_and_read_back()
    {
        Save("automatic");
        var binding = WorkflowRunBindings.Plan(Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}"""), _home, "q1", "web-app", "work", null, null, [], At);
        Bind(binding);

        Assert.Null(WorkflowRunBindings.Keep(_home, "q9", new WorkflowKept(At, ReviewDoors.Terminal)));
        var kept = WorkflowRunBindings.Keep(_home, "q1", new WorkflowKept(At.AddMilliseconds(250), ReviewDoors.Terminal) { Words = "it is the docs" })!;

        var read = WorkflowRunBindings.Read(_home, "q1")!;
        Assert.Equal(new WorkflowKept(At, ReviewDoors.Terminal) { Words = "it is the docs" }, read.Kept);
        Assert.Equal(binding with { Kept = read.Kept, Paths = read.Paths, CurrentSteps = read.CurrentSteps }, read);
        Assert.Equal(kept.Kept, read.Kept);
        Assert.Equal([1], WorkflowRunBindings.KeptVersions(_home, "docs-to-pr"));
    }

    [Fact]
    public void The_machine_log_says_which_version_decided_in_codes_and_ids()
    {
        var binding = Binding(WorkflowLevels.WorkspaceKind, ["docs/**"]);
        var chosen = WorkflowLines.Chosen(binding);
        Assert.Equal("workflow.chosen", chosen.Event);
        Assert.Equal(
            [("run", (object?)"q1"), ("repository", "web-app"), ("workspace", "work"), ("level", WorkflowLevels.WorkspaceKind), ("kind", "docs"),
             ("named", true), ("workflow", "docs-to-pr"), ("version", 2), ("problem", false)],
            chosen.Data);

        var gate = WorkflowGate.Judge(Kind(), new KindPathsRead(["it drops your look"], "Current", ["src/a.ts"]));
        var held = WorkflowLines.Held("s1", "web-app", "work", gate, ReviewDoors.Look);
        Assert.Equal("workflow.held", held.Event);
        Assert.Equal(
            [("session", (object?)"s1"), ("repository", "web-app"), ("workspace", "work"), ("code", WorkflowGateStates.KindPaths), ("step", null),
             ("workflow", "docs-to-pr"), ("version", 2), ("outside", 1), ("door", ReviewDoors.Look)],
            held.Data);
        Assert.Equal(WorkflowGateStates.CannotStart, WorkflowLines.Held("s1", "web-app", "work", gate, ReviewDoors.Screen, WorkflowKinds.Landing).Data[3].Value);
    }
}
