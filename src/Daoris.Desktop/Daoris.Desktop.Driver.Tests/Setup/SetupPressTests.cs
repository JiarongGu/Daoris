using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAYOUT7 (D117 §6.1–§6.3, as D124 §2.1 and §2.4 amend them): the set-up press. What it finds is said, each refusal
/// with its door; a press publishes one ask to one repository, as the person's, and adds the rule its session needs,
/// in exact verbs; <c>--plan</c> prints all of it and publishes nothing. Fast: the world the press reads is a stand-in.
/// </summary>
public sealed class SetupPressTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 1);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-setup-" + Guid.NewGuid().ToString("N")[..8]);

    public SetupPressTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Drivable here, its sessions in trees of their own, on an agent that rides the protocol door.</summary>
    private static readonly DriverConfig Driven = (DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code-acp") })
        .WithDrivable("reports", true).WithTrees("reports", true)
        .WithDrivable("billing", true).WithTrees("billing", true);

    /// <summary>🔴 D124 §2.4: exact verbs, never a runner, never `upstream`, `connect` or any management verb.</summary>
    [Fact]
    public void The_rule_is_the_doctrine_tools_exact_verbs()
    {
        Assert.Equal(
            [
                "daoris --version", "daoris init --harness agents", "daoris analyze --json", "daoris sync --dry-run",
                "daoris sync", "daoris sync --force", "daoris check", "daoris status --json", "daoris doctor",
            ],
            SetupPressRules.Verbs);
        Assert.Equal(SetupPressRules.Verbs.Select(verb => $"Bash({verb})"), SetupPressRules.Rules);
        Assert.All(SetupPressRules.Rules, rule => Assert.Null(PermissionRules.Refusal(rule)));
        Assert.DoesNotContain(SetupPressRules.Rules, rule => rule.Contains('*') || rule.Contains("npx")
            || rule.Contains("upstream") || rule.Contains("connect") || rule.Contains("import") || rule.Contains("retire"));
    }

    [Fact]
    public async Task An_unadopted_repository_is_the_set_ups_own_case()
    {
        var plan = await PlanAsync(new World());

        Assert.Empty(plan.Refusals);
        Assert.True(plan.Pressable);
        Assert.Equal(SetupCase.Whole, plan.Case);
        Assert.Equal("Set up this repository for every agent (2026-10-01)", plan.Title);
        Assert.Equal("work", plan.Workspace);
        Assert.Equal($"{plan.Title}\n\n{plan.Body}", plan.Sentence);
        Assert.Contains("It prints `0.0.1`.", plan.Body);
    }

    /// <summary>D124 §2.1: an adopted repository that declares nothing is not refused; it is asked the knowledge alone.</summary>
    [Fact]
    public async Task An_adopter_declaring_nothing_is_asked_the_knowledge_alone_and_one_on_the_older_layout_to_move()
    {
        var declaring = await PlanAsync(new World { Facts = Clean(declares: false) });
        var moving = await PlanAsync(new World { Facts = Adopter("{}", """{"entries":[]}""") });

        Assert.Equal(SetupCase.Declare, declaring.Case);
        Assert.StartsWith(SetupQuests.Declare, declaring.Title);
        Assert.Equal(SetupCase.Move, moving.Case);
        Assert.StartsWith(SetupQuests.Move, moving.Title);
    }

    [Fact]
    public async Task A_repository_already_set_up_and_naming_its_index_is_refused()
    {
        var plan = await PlanAsync(new World { Facts = Clean(declares: true, index: "docs/index/README.md") });

        Refused(plan, SetupRefusals.AlreadySetUp, "already set up");
        Assert.Contains("`docs/index/README.md`", plan.Refusals[0].Sentence);
        Assert.Null(plan.Title);
    }

    /// <summary>
    /// ORIENT2c (D151 point 5): a repository set up and naming no index is asked its index alone, the generator named from
    /// what it is built with, and the rule the press would add is the doctrine tool's few verbs and the generator's two.
    /// </summary>
    [Fact]
    public async Task A_repository_set_up_and_naming_no_index_is_asked_its_index_with_its_generator_named()
    {
        var plan = await PlanAsync(new World { Facts = Clean(declares: true), Description = Built("React", ".NET") });

        Assert.Empty(plan.Refusals);
        Assert.Equal(SetupCase.Index, plan.Case);
        Assert.Equal("Give this repository an index of where things are (2026-10-01)", plan.Title);
        Assert.Equal(new IndexForm("Node", "tools/index.mjs", "node tools/index.mjs", "node tools/index.mjs --check"), plan.Index);
        Assert.Equal(
            [
                "Bash(daoris --version)", "Bash(daoris sync)", "Bash(daoris check)", "Bash(daoris status --json)",
                "Bash(node tools/index.mjs)", "Bash(node tools/index.mjs --check)",
            ],
            SetupPressRules.RulesOf(plan));
        Assert.All(SetupPressRules.RulesOf(plan), rule => Assert.Null(PermissionRules.Refusal(rule)));
        Assert.Contains("`tools/index.mjs`", plan.Body);
    }

    /// <summary>
    /// D230: a reconcile is a case of the press, asked of a repository the registry marks adopted. The plan holds what the doctrine tool's
    /// <c>status --json</c> said of its checkout, which the body says (one that finds nothing still closes done). Its quest carries the
    /// standing answer's lines among its items, its rule is the set-up's verbs with <c>reconcile</c> and <c>upstream --all</c>, and a press
    /// publishes it.
    /// </summary>
    [Fact]
    public async Task A_reconcile_is_the_press_case_for_an_adopted_repository_carrying_the_standing_answers_rule_line()
    {
        var world = new World { Facts = Clean(declares: true, index: "docs/index/README.md") };
        var config = Driven.WithStanding("billing", "Run the whole test suite before every commit.\nTest in staging.", DateTimeOffset.UnixEpoch);

        var plan = await SetupPress.PlanReconcileAsync(world, config, SessionWire.Acp, "billing", Day);

        Assert.Empty(plan.Refusals);
        Assert.Equal(SetupCase.Reconcile, plan.Case);
        Assert.Equal(world.Doctrine.Status, plan.Doctrine);
        Assert.Equal(["billing"], world.Statused);
        Assert.Equal("Reconcile this repository with the shared doctrine (2026-10-01)", plan.Title);
        Assert.Contains("said its canon differs from the one this repository synced from at the same version, `0.0.1`", plan.Body);
        Assert.Contains("a sync would add 1 file.", plan.Body);
        Assert.Contains("Run `daoris reconcile`", plan.Body);
        Assert.Contains("> Run the whole test suite before every commit.", plan.Body);
        Assert.Contains("> Test in staging.", plan.Body);
        Assert.Contains("a rule about this repository's own development", plan.Body);
        Assert.Equal(SetupPressRules.ReconcileVerbs.Select(verb => $"Bash({verb})"), SetupPressRules.RulesOf(plan));
        Assert.All(SetupPressRules.RulesOf(plan), rule => Assert.Null(PermissionRules.Refusal(rule)));

        var outcome = await SetupPress.ApplyAsync(plan, world, _home);
        Assert.True(outcome.Published);
        var (_, sentence, to) = Assert.Single(world.Published);
        Assert.Equal("billing", to);
        Assert.StartsWith(plan.Title + "\n\n", sentence);
        Assert.Contains("Run the whole test suite before every commit.", sentence);
    }

    /// <summary>D230: one open reconcile per repository, and none beside an open set-up; one the registry does not mark adopted is refused.</summary>
    [Fact]
    public async Task A_reconcile_is_refused_beside_an_open_one_and_for_a_repository_not_adopted()
    {
        var world = new World { Facts = Clean(declares: true, index: "docs/index/README.md") };
        world.Quests.Add(new QuestView("q7", "ask #a7", "billing", "Reconcile this repository with the shared doctrine (2026-09-30)", "", "Taken"));

        Refused(await SetupPress.PlanReconcileAsync(world, Driven, SessionWire.Acp, "billing", Day), SetupRefusals.Open, "`#q7`");

        var unadopted = await SetupPress.PlanReconcileAsync(new World(), Driven, SessionWire.Acp, "reports", Day);
        Refused(unadopted, SetupRefusals.NotAdopted, "has not taken up the shared doctrine");
        Assert.Null(unadopted.Title);
    }

    /// <summary>
    /// D230, WSSETUP4: a lock a newer canon wrote is never reconciled back, and a status the tool could not read is said; where no tool runs,
    /// the status is not asked. Each is refused before a quest is composed.
    /// </summary>
    [Fact]
    public async Task A_reconcile_is_refused_over_a_newer_lock_and_a_status_the_tool_could_not_read()
    {
        var adopted = Clean(declares: true, index: "docs/index/README.md");
        var newer = new World { Facts = adopted, Doctrine = new(new DoctrineStatus { Synced = true, Carried = "0.0.1", NewerLock = "9.9.9" }, null) };
        var unread = new World { Facts = adopted, Doctrine = new(null, "`daoris status --json` said: daoris.json is not valid JSON") };
        var toolless = new World { Facts = adopted, Tools = new("node", "v22.11.0", null, null, null, null) };

        var refused = await SetupPress.PlanReconcileAsync(newer, Driven, SessionWire.Acp, "billing", Day);
        Refused(refused, SetupRefusals.NewerLock, "9.9.9");
        Assert.Null(refused.Title);
        Assert.Equal("9.9.9", refused.Doctrine?.NewerLock);
        Refused(await SetupPress.PlanReconcileAsync(unread, Driven, SessionWire.Acp, "billing", Day), SetupRefusals.StatusUnread, "daoris.json is not valid JSON");

        var none = await SetupPress.PlanReconcileAsync(toolless, Driven, SessionWire.Acp, "billing", Day);
        Refused(none, SetupRefusals.NoTool, "no `daoris`");
        Assert.Empty(toolless.Statused);
        Assert.Null(none.Doctrine);
    }

    /// <summary>ORIENT2c: each stack the press knows has one form, the first the reading names.</summary>
    [Theory]
    [InlineData(".NET", "tools/index.cs", "dotnet run tools/index.cs", "dotnet run tools/index.cs -- --check")]
    [InlineData("Python", "tools/index.py", "python tools/index.py", "python tools/index.py --check")]
    [InlineData("Vue", "tools/index.mjs", "node tools/index.mjs", "node tools/index.mjs --check")]
    public void Each_known_stack_names_one_generator(string stack, string path, string write, string check)
    {
        var (form, problem) = IndexForms.For(["Terraform", stack], _ => false);

        Assert.Null(problem);
        Assert.Equal(path, form!.Generator);
        Assert.Equal(write, form.Write);
        Assert.Equal(check, form.Check);
    }

    /// <summary>ORIENT2c (the design §2.3): a name already on the line is never named over.</summary>
    [Fact]
    public async Task A_generator_name_already_on_the_line_is_passed_over_and_both_taken_refuse()
    {
        var one = await PlanAsync(new World { Facts = Clean(declares: true, tools: ["tools/index.mjs"]), Description = Built("Node") });
        var both = await PlanAsync(new World
        {
            Facts = Clean(declares: true, tools: ["tools/index.mjs", "tools/orient-index.mjs"]), Description = Built("Node"),
        });

        Assert.Equal("tools/orient-index.mjs", one.Index!.Generator);
        Refused(both, SetupRefusals.NoIndexForm, "already on its line");
        Assert.Null(both.Title);
    }

    /// <summary>ORIENT2c (the design §2.2): a stack the press does not know gets no quest, and the press says so.</summary>
    [Fact]
    public async Task A_stack_the_press_does_not_know_is_refused_naming_the_ask_by_hand()
    {
        var go = await PlanAsync(new World { Facts = Clean(declares: true), Description = Built("Go") });
        var silent = await PlanAsync(new World { Facts = Clean(declares: true) });

        Refused(go, SetupRefusals.NoIndexForm, "built with Go");
        Assert.Contains("*Give this repository an index of where things are*", go.Refusals[0].Sentence);
        Refused(silent, SetupRefusals.NoIndexForm, "nothing it is built with could be read");
        Assert.False(go.Pressable);
        Assert.Null(go.Title);
    }

    /// <summary>ORIENT2c: the press grants the index quest's commands, exactly, as it grants a set-up's verbs.</summary>
    [Fact]
    public async Task Pressing_an_index_quest_adds_its_own_rule_and_publishes_it()
    {
        var world = new World { Facts = Clean(declares: true), Description = Built("Python") };
        var plan = await PlanAsync(world);

        var outcome = await SetupPress.ApplyAsync(plan, world, _home);

        Assert.True(outcome.Published);
        Assert.Equal(SetupPressRules.IndexRules(plan.Index!), outcome.Added);
        Assert.Equal(SetupPressRules.IndexRules(plan.Index!), PermissionRules.Load(_home).Repositories["reports"].Allow);
        Assert.StartsWith(SetupQuests.Index, Assert.Single(world.Published).Sentence);
    }

    [Fact]
    public async Task A_repository_not_on_this_machines_registry_is_refused_naming_both_doors()
    {
        var plan = await PlanAsync(new World(), "nobody");

        Refused(plan, SetupRefusals.NotOnRegistry, "Repositories → *Add a repository*");
        Assert.Contains("`daoris repository import <folder> --workspace <name>`", plan.Refusals[0].Sentence);
        Assert.Single(plan.Refusals);
    }

    [Fact]
    public async Task A_teammates_registration_with_no_checkout_here_is_refused()
    {
        var world = new World();
        world.Registry.Add(new RepoView("elsewhere", false, null, "work"));

        var plan = await PlanAsync(world, "elsewhere");

        Refused(plan, SetupRefusals.NoCheckout, "no checkout here");
        Assert.Single(plan.Refusals);
    }

    /// <summary>
    /// 🔴 AGENTSPLACE1b: a repository not driven here is pressed all the same, its quest admitted alone by its id in the driver's
    /// file, so its session starts while nothing else of the repository does; one driven here is admitted by its driving, as before.
    /// </summary>
    [Fact]
    public async Task A_repository_not_driven_here_is_pressed_and_its_quest_admitted_alone()
    {
        var world = new World();
        var plan = await PlanAsync(world, config: Driven.WithDrivable("reports", false));
        var driven = await PlanAsync(new World());

        var outcome = await SetupPress.ApplyAsync(plan, world, _home);

        Assert.Empty(plan.Refusals);
        Assert.True(plan.Alone);
        Assert.False(driven.Alone);
        Assert.True(outcome.Published);
        Assert.Equal("q123", outcome.QuestId);
        var file = DriverConfig.Load(DriverConfig.ResolvePath(_home));
        Assert.Equal(new Dictionary<string, string> { ["q123"] = "reports" }, file.Admitted);
        Assert.Empty(file.Drivable);
        Assert.True(file.Admits(new QuestView("q123", "ask #a1b2c3", "reports", plan.Title!, "", "Open")));
    }

    /// <summary>A press where it is driven writes no admission, and one the service refused admits nothing.</summary>
    [Fact]
    public async Task A_driven_press_and_a_refused_one_admit_nothing()
    {
        var refused = new World { Answer = new AskAnswer(false, "`reports` cannot be asked: it is not addressable here.", null, null) };

        await SetupPress.ApplyAsync(await PlanAsync(new World()), new World(), _home);
        await SetupPress.ApplyAsync(await PlanAsync(refused, config: Driven.WithDrivable("reports", false)), refused, _home);

        Assert.False(File.Exists(DriverConfig.ResolvePath(_home)));
    }

    /// <summary>
    /// An admission that cannot be written leaves the quest published and says so: it waits, as the repository's other work does, until it
    /// is driven or the file reads again.
    /// </summary>
    [Fact]
    public async Task An_admission_that_cannot_be_written_is_said_with_the_quest_it_left_waiting()
    {
        var path = DriverConfig.ResolvePath(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");
        var world = new World();

        var outcome = await SetupPress.ApplyAsync(await PlanAsync(world, config: Driven.WithDrivable("reports", false)), world, _home);

        Assert.True(outcome.Published);
        Assert.Equal("q123", outcome.QuestId);
        Assert.StartsWith("Asked as `#a1b2c3` in `work`. Its admission could not be written", outcome.Message);
        Assert.Contains("`#q123` waits as the rest of `reports`'s work does until it is driven here", outcome.Message);
        Assert.Equal("{ not json", File.ReadAllText(path));
    }

    /// <summary>
    /// An unadopted repository is carried only by a session on the protocol door (D70), driven or not; an adopter rides either.
    /// </summary>
    [Fact]
    public async Task An_unadopted_repository_on_the_pipe_door_is_refused_and_an_adopter_is_not()
    {
        var unadopted = await PlanAsync(new World(), door: SessionWire.Pipe);
        var alone = await PlanAsync(new World(), config: Driven.WithDrivable("reports", false), door: SessionWire.Pipe);
        var adopted = await PlanAsync(new World { Facts = Adopter("{}", """{"entries":[]}""") }, "billing", door: SessionWire.Pipe);

        Refused(unadopted, SetupRefusals.NotDriven, "protocol door");
        Refused(alone, SetupRefusals.NotDriven, "protocol door");
        Assert.Single(alone.Refusals);
        Assert.Empty(adopted.Refusals);
    }

    [Fact]
    public async Task A_repository_whose_sessions_run_in_its_checkout_is_refused_naming_trees()
    {
        var plan = await PlanAsync(new World(), config: Driven.WithTrees("reports", false));

        Refused(plan, SetupRefusals.NoOwnTree, "`daoris place trees reports on`");
        // HELPSETUP1: a tree per session is on the repository's Setup (UX6f).
        Assert.Contains("Repositories → the repository's page → Setup → Driving", Find(plan, SetupRefusals.NoOwnTree).Sentence);
    }

    [Fact]
    public async Task A_line_with_no_history_is_refused_with_what_git_said()
    {
        var plan = await PlanAsync(new World { Line = new LineReading(null, "its line `main` names no commit here") });

        Refused(plan, SetupRefusals.NoHistory, "its line `main` names no commit here");
        Assert.Null(plan.Title);
    }

    [Theory]
    [InlineData(null, null, "`node` is not on this machine's PATH")]
    [InlineData("node", "v20.11.1", "older than 22")]
    [InlineData("node", "not a version", "did not answer its version")]
    public async Task No_node_or_an_older_one_is_refused_naming_tools(string? node, string? version, string said)
    {
        var tools = Tools() with { Node = node, NodeVersion = version, NodeProblem = node is null ? "`node` is not on this machine's PATH" : null };

        var plan = await PlanAsync(new World { Tools = tools });

        Refused(plan, SetupRefusals.NoNode, said);
        Assert.Contains("Settings → Tools", Find(plan, SetupRefusals.NoNode).Sentence);
        Assert.Contains("`daoris tool use node …`", Find(plan, SetupRefusals.NoNode).Sentence);
    }

    [Theory]
    [InlineData(null, null, "no `daoris` on the PATH")]
    [InlineData("daoris", "", "did not answer its version")]
    [InlineData("daoris", "Usage: daoris <command>", "did not answer its version")]
    public async Task No_doctrine_tool_on_a_childs_path_is_refused_saying_what_was_found(string? daoris, string? version, string said)
    {
        var tools = Tools() with { Daoris = daoris, DaorisVersion = version };

        var plan = await PlanAsync(new World { Tools = tools });

        Refused(plan, SetupRefusals.NoTool, "the doctrine tool cannot run here");
        Assert.Contains(said, Find(plan, SetupRefusals.NoTool).Sentence);
    }

    /// <summary>A set-up open or taken for the repository is named; a closed one, another repository's and an ordinary quest are not set-ups open here.</summary>
    [Fact]
    public async Task A_set_up_already_open_is_refused_naming_it()
    {
        var world = new World();
        world.Quests.AddRange(
        [
            new QuestView("q1", "ask #a1", "reports", "Set up this repository for every agent (2026-09-30)", "", "Done"),
            new QuestView("q2", "ask #a2", "billing", "Set up this repository for every agent (2026-10-01)", "", "Open"),
            new QuestView("q3", "ask #a3", "reports", "Fix the totals", "", "Open"),
        ]);
        Assert.Empty((await PlanAsync(world)).Refusals);

        world.Quests.Add(new QuestView("q9", "ask #a9", "REPORTS", "Move this repository to the agents layout (2026-09-29)", "", "Taken"));
        var plan = await PlanAsync(world);

        Refused(plan, SetupRefusals.Open, "`#q9`");
    }

    /// <summary>Every refusal is said at once, so a person fixes them in one pass rather than one press at a time.</summary>
    [Fact]
    public async Task Every_refusal_that_applies_is_said_together()
    {
        var plan = await PlanAsync(new World { Tools = Tools() with { Daoris = null } }, config: Driven.WithDrivable("reports", false).WithTrees("reports", false));

        Assert.Equal([SetupRefusals.NoOwnTree, SetupRefusals.NoTool], plan.Refusals.Select(refusal => refusal.Code));
        Assert.False(plan.Pressable);
    }

    /// <summary>D124 §2.2: the index's entries for it, the workspace's other names, and its README, all from the world the press reads.</summary>
    [Fact]
    public async Task The_quest_carries_the_index_the_neighbours_and_the_readme()
    {
        var world = new World
        {
            Entries = ["docs/DECISIONS.md", "docs/DECISIONS.md"],
            Description = new RepositoryDescription("Reports — the monthly figures.", [".NET"], "README.md"),
        };
        world.Registry.Add(new RepoView("ledger", true, "/fixture/ledger", "work"));
        world.Registry.Add(new RepoView("faraway", true, "/fixture/faraway", "home"));

        var plan = await PlanAsync(world);

        Assert.Contains("holds 2 entries for it, from 1 file: `docs/DECISIONS.md` (2)", plan.Body);
        Assert.Contains("`billing`, `ledger`", plan.Body);
        Assert.DoesNotContain("faraway", plan.Body);
        Assert.Contains("Reports — the monthly figures.", plan.Body);
    }

    /// <summary>
    /// A press: the rule into the repository's own scope, then one ask to one repository, in its workspace, as the
    /// person's — the title its first line, so the quest is titled by it.
    /// </summary>
    [Fact]
    public async Task A_press_adds_the_rule_to_the_repository_and_publishes_one_ask_to_it()
    {
        var world = new World();
        var plan = await PlanAsync(world);

        var outcome = await SetupPress.ApplyAsync(plan, world, _home);

        Assert.True(outcome.Published);
        var (workspace, sentence, to) = Assert.Single(world.Published);
        Assert.Equal(("work", "reports"), (workspace, to));
        Assert.Equal(plan.Sentence, sentence);
        Assert.Equal(plan.Title, sentence.Split('\n')[0]);
        Assert.Equal(SetupPressRules.Rules, outcome.Added);
        Assert.Equal(SetupPressRules.Rules, PermissionRules.Load(_home).Repositories["reports"].Allow);
    }

    /// <summary>A press the service refused takes back the rules it added, and keeps one the person already had.</summary>
    [Fact]
    public async Task A_refused_press_takes_back_only_the_rules_it_added()
    {
        PermissionRules.Save(_home, PermissionRules.Add(PermissionFile.Empty, RuleScope.Repository, "reports", RuleList.Allow, "Bash(daoris check)"));
        var world = new World { Answer = new AskAnswer(false, "`reports` cannot be asked: it is not addressable here.", null, null) };
        var plan = await PlanAsync(world);

        var outcome = await SetupPress.ApplyAsync(plan, world, _home);

        Assert.False(outcome.Published);
        Assert.Contains("cannot be asked", outcome.Message);
        Assert.Equal(["Bash(daoris check)"], PermissionRules.Load(_home).Repositories["reports"].Allow);
    }

    /// <summary>A refused plan is never applied: nothing is written and nothing published.</summary>
    [Fact]
    public async Task A_refused_plan_publishes_nothing_and_writes_no_rule()
    {
        var world = new World { Facts = Clean(declares: true) };
        var plan = await PlanAsync(world);

        var outcome = await SetupPress.ApplyAsync(plan, world, _home);

        Assert.False(outcome.Published);
        Assert.Empty(world.Published);
        Assert.False(File.Exists(PermissionRules.PathOf(_home)));
    }

    /// <summary>AGENTSPLACE1a2: what it builds rides the plan and shapes the body's safe work.</summary>
    [Fact]
    public async Task What_it_builds_shapes_the_quests_safe_work()
    {
        var plan = await SetupPress.PlanAsync(new World(), Driven, SessionWire.Acp, "reports", Day, SetupBuilds.Service);

        Assert.Equal(SetupBuilds.Service, plan.Builds);
        Assert.Contains(SetupBuilds.Checks(SetupBuilds.Service), plan.Body);
        Assert.Null((await PlanAsync(new World())).Builds);
    }

    /// <summary>The plan says where its quest starts alone.</summary>
    [Fact]
    public async Task The_plan_says_where_its_quest_starts_alone()
    {
        Assert.True((await PlanAsync(new World(), config: Driven.WithDrivable("reports", false))).Alone);
        Assert.False((await PlanAsync(new World())).Alone);
    }

    [Fact]
    public async Task A_refused_plan_says_why()
    {
        var plan = await PlanAsync(new World(), config: Driven.WithTrees("reports", false));

        Assert.False(plan.Pressable);
        Assert.Contains("`daoris place trees reports on`", plan.Refusals[0].Sentence);
    }

    private Task<SetupPlan> PlanAsync(
        World world, string repository = "reports", DriverConfig? config = null, SessionWire door = SessionWire.Acp) =>
        SetupPress.PlanAsync(world, config ?? Driven, door, repository, Day);

    private static void Refused(SetupPlan plan, string code, string said)
    {
        var refusal = Find(plan, code);
        Assert.Contains(said, refusal.Sentence);
        Assert.False(plan.Pressable);
    }

    private static SetupRefusal Find(SetupPlan plan, string code) =>
        plan.Refusals.FirstOrDefault(refusal => refusal.Code == code)
        ?? throw new Xunit.Sdk.XunitException($"no `{code}` refusal among: {string.Join("; ", plan.Refusals.Select(r => $"{r.Code}: {r.Sentence}"))}");

    private static SetupTools Tools() => new("node", "v22.11.0", null, "daoris", "0.0.1", null);

    private static LayoutFacts Adopter(string manifest, string lockText) =>
        new LayoutReaderTests.Scratch().File("daoris.json", manifest).File("daoris.lock", lockText).Read();

    private static LayoutFacts Clean(bool declares, string? index = null, string[]? tools = null)
    {
        var documents = index is null ? "" : $$""", "documents": { "index": "{{index}}" }""";
        var scratch = new LayoutReaderTests.Scratch()
            .File("daoris.json", declares
                ? $$"""{ "harness": "agents", "target": ".agents", "domain": { "summary": "Reports." }{{documents}} }"""
                : """{ "harness": "agents", "target": ".agents" }""")
            .File("daoris.lock", """{ "harness": "agents", "target": ".agents", "entries": [] }""")
            .File("AGENTS.md", "<!-- daoris:rules -->\n# Doctrine\n<!-- /daoris:rules -->\n")
            .File("CLAUDE.md", "<!-- daoris:import -->\n@AGENTS.md\n<!-- /daoris:import -->\n");
        foreach (var tool in tools ?? []) scratch = scratch.File(tool, "// a tool of its own\n");
        return scratch.Read();
    }

    private static RepositoryDescription Built(params string[] stack) => new(null, stack, null);

    /// <summary>The world a press reads, as a stand-in: the registry, the line, the tools, the quests, the index, and the ask door.</summary>
    private sealed class World : ISetupWorld
    {
        public List<RepoView> Registry { get; } =
        [
            new("reports", false, "/fixture/reports", "work"),
            new("billing", true, "/fixture/billing", "work"),
        ];

        public LayoutFacts Facts { get; init; } = new LayoutReaderTests.Scratch().File("README.md", "# Reports\n").Read();

        public LineReading? Line { get; init; }

        public SetupTools Tools { get; init; } = SetupPressTests.Tools();

        public List<QuestView> Quests { get; } = [];

        public IReadOnlyList<string> Entries { get; init; } = [];

        public RepositoryDescription? Description { get; init; }

        public AskAnswer Answer { get; init; } = new(true, "Asked as `#a1b2c3` in `work`.", "a1b2c3", "q123");

        public List<(string Workspace, string Sentence, string To)> Published { get; } = [];

        public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RepoView>>(Registry);

        public Task<LineReading> ReadLineAsync(RepoView repository, DriverConfig config, CancellationToken ct) =>
            Task.FromResult(Line ?? new LineReading(Facts, null));

        public Task<SetupTools> ToolsAsync(CancellationToken ct) => Task.FromResult(Tools);

        /// <summary>What the doctrine tool says of a checkout: by default, a canon that differs and one file it adds.</summary>
        public DoctrineReading Doctrine { get; init; } =
            new(new DoctrineStatus { Synced = true, Update = DoctrineUpdates.Differs, Carried = "0.0.1", Locked = "0.0.1", Added = 1 }, null);

        /// <summary>Each repository whose status was asked.</summary>
        public List<string> Statused { get; } = [];

        public Task<DoctrineReading> StatusAsync(RepoView repository, CancellationToken ct)
        {
            Statused.Add(repository.Repository);
            return Task.FromResult(Doctrine);
        }

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>(Quests);

        public Task<IReadOnlyList<string>> EntriesAsync(string repository, CancellationToken ct) => Task.FromResult(Entries);

        public RepositoryDescription? Describe(string root) => Description;

        public Task<AskAnswer> PublishAsync(string workspace, string sentence, string to, CancellationToken ct)
        {
            Published.Add((workspace, sentence, to));
            return Task.FromResult(Answer);
        }
    }
}
