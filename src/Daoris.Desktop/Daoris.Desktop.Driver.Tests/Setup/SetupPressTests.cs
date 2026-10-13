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
    private static readonly DriverConfig Driven = (DriverConfig.Empty with { Adapter = "claude-code-acp" })
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
            SetupPress.Verbs);
        Assert.Equal(SetupPress.Verbs.Select(verb => $"Bash({verb})"), SetupPress.Rules);
        Assert.All(SetupPress.Rules, rule => Assert.Null(PermissionRules.Refusal(rule)));
        Assert.DoesNotContain(SetupPress.Rules, rule => rule.Contains('*') || rule.Contains("npx")
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
    public async Task A_repository_already_set_up_is_refused()
    {
        var plan = await PlanAsync(new World { Facts = Clean(declares: true) });

        Refused(plan, SetupRefusals.AlreadySetUp, "already set up");
        Assert.Null(plan.Title);
    }

    [Fact]
    public async Task A_repository_not_on_this_machines_registry_is_refused_naming_both_doors()
    {
        var plan = await PlanAsync(new World(), "nobody");

        Refused(plan, SetupRefusals.NotOnRegistry, "Repositories → *Add a repository*");
        Assert.Contains("`daoris import <folder> --workspace <name>`", plan.Refusals[0].Sentence);
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

    [Fact]
    public async Task A_repository_not_driven_here_is_refused_naming_its_setup_and_the_command()
    {
        var plan = await PlanAsync(new World(), config: Driven.WithDrivable("reports", false));

        // HELPSETUP1: driving is the repository's own, on its Setup (UX6f); Settings → Driver never held it.
        Refused(plan, SetupRefusals.NotDriven, "Repositories → the repository's page → Setup → Driving");
        Assert.DoesNotContain("Settings → Driver", Find(plan, SetupRefusals.NotDriven).Sentence);
        Assert.Contains("`daoris driver drive reports`", Find(plan, SetupRefusals.NotDriven).Sentence);
    }

    /// <summary>An unadopted repository is carried only by a session on the protocol door (D70); an adopter rides either.</summary>
    [Fact]
    public async Task An_unadopted_repository_on_the_pipe_door_is_refused_and_an_adopter_is_not()
    {
        var unadopted = await PlanAsync(new World(), door: SessionWire.Pipe);
        var adopted = await PlanAsync(new World { Facts = Adopter("{}", """{"entries":[]}""") }, "billing", door: SessionWire.Pipe);

        Refused(unadopted, SetupRefusals.NotDriven, "protocol door");
        Assert.Empty(adopted.Refusals);
    }

    [Fact]
    public async Task A_repository_whose_sessions_run_in_its_checkout_is_refused_naming_trees()
    {
        var plan = await PlanAsync(new World(), config: Driven.WithTrees("reports", false));

        Refused(plan, SetupRefusals.NoOwnTree, "`daoris driver trees reports on`");
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

        Assert.Equal([SetupRefusals.NotDriven, SetupRefusals.NoOwnTree, SetupRefusals.NoTool], plan.Refusals.Select(refusal => refusal.Code));
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
        Assert.Equal(SetupPress.Rules, outcome.Added);
        Assert.Equal(SetupPress.Rules, PermissionRules.Load(_home).Repositories["reports"].Allow);
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

    /// <summary>🔴 <c>--plan</c> prints what was read, the refusals, the rule, the landing, the agent and the quest's text, and publishes nothing.</summary>
    [Fact]
    public async Task Plan_prints_everything_and_publishes_nothing()
    {
        var world = new World();
        var output = new StringWriter();

        var code = await SetupCommand.RunAsync(["reports", "--plan"], output, world, Driven, SessionWire.Acp, _home, Day);

        Assert.Equal(0, code);
        Assert.Empty(world.Published);
        Assert.False(File.Exists(PermissionRules.PathOf(_home)));
        var printed = output.ToString();
        Assert.Contains("Set up this repository for every agent (2026-10-01)", printed);
        Assert.Contains("Bash(daoris sync --dry-run)", printed);
        Assert.Contains("merged into its line", printed);
        Assert.Contains("claude-code-acp", printed);
        Assert.Contains("nothing was published", printed);
        Assert.Contains("**The steps**", printed);
    }

    [Fact]
    public async Task A_refused_plan_says_why_and_exits_one()
    {
        var output = new StringWriter();

        var code = await SetupCommand.RunAsync(["reports", "--plan"], output, new World(), Driven.WithTrees("reports", false), SessionWire.Acp, _home, Day);

        Assert.Equal(1, code);
        Assert.Contains("`daoris driver trees reports on`", output.ToString());
    }

    /// <summary>The landing rule it will land by is said before it is pressed (D117 §6.3): as a branch for review, or merged.</summary>
    [Fact]
    public async Task The_plan_says_how_it_will_land()
    {
        var output = new StringWriter();
        var branch = Driven.WithLanding("reports", new LandingRule(LandingForm.Branch, "daoris/{quest}-{slug}"));

        await SetupCommand.RunAsync(["reports", "--plan"], output, new World(), branch, SessionWire.Acp, _home, Day);

        Assert.Contains("as a branch, for your review", output.ToString());
    }

    [Fact]
    public async Task A_press_from_the_terminal_publishes_and_says_the_rule_it_added()
    {
        var world = new World();
        var output = new StringWriter();

        var code = await SetupCommand.RunAsync(["reports"], output, world, Driven, SessionWire.Acp, _home, Day);

        Assert.Equal(0, code);
        Assert.Single(world.Published);
        Assert.Contains("Asked as `#a1b2c3`", output.ToString());
        // HELPSETUP1: a repository's rules are on its Setup → Reach (UX6f).
        Assert.Contains("taken back in Repositories → the repository's page → Setup → Reach, or `daoris agent rules remove", output.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("--plan")]
    [InlineData("reports billing")]
    [InlineData("reports --force")]
    public async Task Anything_but_one_repository_and_plan_is_the_usage(string words)
    {
        var output = new StringWriter();
        var args = words.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, await SetupCommand.RunAsync(args, output, new World(), Driven, SessionWire.Acp, _home, Day));
        Assert.Contains("daoris-driver setup <repository> [--plan]", output.ToString());
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

    private static LayoutFacts Clean(bool declares) => new LayoutReaderTests.Scratch()
        .File("daoris.json", declares
            ? """{ "harness": "agents", "target": ".agents", "domain": { "summary": "Reports." } }"""
            : """{ "harness": "agents", "target": ".agents" }""")
        .File("daoris.lock", """{ "harness": "agents", "target": ".agents", "entries": [] }""")
        .File("AGENTS.md", "<!-- daoris:rules -->\n# Doctrine\n<!-- /daoris:rules -->\n")
        .File("CLAUDE.md", "<!-- daoris:import -->\n@AGENTS.md\n<!-- /daoris:import -->\n")
        .Read();

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
