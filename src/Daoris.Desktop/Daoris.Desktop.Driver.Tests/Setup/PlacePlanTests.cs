using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTSPLACE1a2 (the Agents-place design §4 and §7 row 5): a place's plan. The parts are applied to a copy of the configuration through the
/// doors' own edits and read before and after through the resolvers a start uses; a test per delivery over fixtures (a repository on its
/// own, one that declares integration, a remote with a landing plugin, one without, refused a pull request); the work driving releases;
/// several repositories in one plan, each change said once; and nothing written or probed. The verb's words are <c>fixtures/place-plan.json</c>.
/// </summary>
public sealed class PlacePlanTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private const string Blob = "fedcba9876543210fedcba9876543210fedcba98";
    private const string Declaring = """{ "gates": [ { "name": "verify", "run": "npm run verify" } ], "integration": {} }""";
    private const string Plugin = "example.github-pull-request";

    private static readonly DriverConfig Config = (DriverConfig.Empty with { Adapter = "claude-code-acp" })
        .WithDrivable("engine", true).WithTrees("engine", true)
        .WithDrivable("web", true).WithTrees("web", true)
        .WithDrivable("docs", true).WithTrees("docs", true);

    private static readonly SetupPresetsTests.Catalog Plugins = new(new SetupPresetsTests.Entry(Plugin, [HookPoints.Land]));

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "no-home-" + Guid.NewGuid().ToString("N")[..8]);

    private Task<PlacePlanned> PlanAsync(World world, PlaceAsk ask, DriverConfig? config = null, SessionWire door = SessionWire.Acp) =>
        PlacePlans.PlanAsync(world, config ?? Config, _home, Plugins, door, ask);

    private static PlaceAsk Ask(string preset, params string[] repositories) => new(repositories, preset);

    [Fact]
    public async Task A_repository_on_its_own_is_offered_a_merge_and_writes_only_what_differs()
    {
        var plan = await PlanAsync(new World(), Ask(SetupPresets.Merge, "solo") with { Drive = true });

        var solo = Assert.Single(plan.Repositories);
        Assert.Equal(new PlaceSuggestion(SetupPresets.Merge, Adoption: false), solo.Suggested);
        Assert.Equal(new SetupMatch(SetupPresets.NotDriven, []), solo.Before.Match);
        Assert.Equal(new SetupMatch(SetupPresets.Merge, []), solo.After.Match);
        // The default rule already merges after the person's Accept, so no landing rule is written.
        Assert.Equal(["SET_TREES", "SET_DRIVABLE"], solo.Changes.Select(change => change.Key));
        Assert.True(plan.Applies);
        Assert.Empty(solo.Lowers);
        Assert.Equal("daoris-driver place solo --preset merge-after-accept --drive --plan", plan.Terminal);
    }

    [Fact]
    public async Task A_repository_declaring_integration_is_offered_it_with_the_acceptance_its_declaration_is_for()
    {
        var plan = await PlanAsync(new World(), Ask(SetupPresets.Integrated, "engine"));

        var engine = Assert.Single(plan.Repositories);
        Assert.Equal(SetupPresets.Integrated, engine.Suggested.Preset);
        var landing = Assert.Single(engine.Changes);
        Assert.Equal(new PlaceChange("SET_LANDING", PlacePlans.Delivery, "how its work lands", "merged after you accept",
            "on `feature/{quest}-{slug}`, integrated into the line, requested by you"), landing);
        Assert.Equal(new SetupLand("integrate", "you", "none", "owner"), engine.After.Land);
        var declaration = engine.Declaration!;
        Assert.Equal(("main", Commit, Blob, Declaring), (declaration.Line, declaration.Commit, declaration.Blob, declaration.Text));
        var acceptance = Assert.Single(declaration.Acceptances);
        Assert.Equal((PlacePlans.Integration, IntegrationAcceptance.Unaccepted), (acceptance.Purpose, acceptance.Standing));
        Assert.Empty(engine.Lowers);
        Assert.True(plan.Applies);
    }

    [Fact]
    public async Task A_remote_with_a_landing_plugin_is_offered_a_pull_request_opened_by_it()
    {
        var plan = await PlanAsync(new World(), Ask(SetupPresets.PullRequest, "web"));

        var web = Assert.Single(plan.Repositories);
        Assert.Equal(SetupPresets.PullRequest, web.Suggested.Preset);
        Assert.Equal("on `feature/{quest}-{slug}`, a pull request by `example.github-pull-request`", Assert.Single(web.Changes).After);
        Assert.Equal(new SetupLand("pull-request", "you", Plugin), web.After.Land);
        Assert.True(plan.Applies);
    }

    [Fact]
    public async Task A_remote_no_landing_plugin_serves_is_refused_a_pull_request_and_offered_a_merge()
    {
        var plan = await PlanAsync(new World(), Ask(SetupPresets.PullRequest, "docs"));

        var docs = Assert.Single(plan.Repositories);
        Assert.Equal(SetupPresets.Merge, docs.Suggested.Preset);
        var refusal = Assert.Single(plan.Refusals);
        Assert.Equal(("docs", PlacePlans.NoLandingPlugin), (refusal.Repository, refusal.Code));
        Assert.Contains("`gitlab.com`", refusal.Sentence);
        Assert.Empty(docs.Changes);
        Assert.False(plan.Applies);
    }

    /// <summary>The plan's before and after are each resolver's own, over the configuration as it stands and as the parts would leave it.</summary>
    [Fact]
    public async Task Before_and_after_are_each_resolvers_own()
    {
        var world = new World();
        var ask = Ask(SetupPresets.Integrated, "engine");
        var row = world.Registry.Single(each => each.Repository == "engine");

        var plan = await PlanAsync(world, ask);
        var after = PlacePlans.Apply(Config, row, ask, world.Tips["engine"], Plugins, SessionWire.Acp, []).After;

        var engine = Assert.Single(plan.Repositories);
        var store = WorkflowPorts.Store(_home);
        foreach (var (side, config) in new[] { (engine.Before, Config), (engine.After, after) })
        {
            Assert.Equal(PlaceProjection.ValuesAt(config, "engine", "aurora", PlaceSource.RepositoryLevel), side.Values);
            Assert.Equal(SetupPresets.LandOf(WorkflowProcesses.Otherwise(store, config, "engine", "aurora").Steps!), side.Land);
            Assert.Equal(RoleWork.Codes.Select(work => WorkPolicies.Of(config, work, "engine", "aurora").Consent), side.Policies.Select(policy => policy.Consent));
            Assert.Equal(LandingRules.Choose(config, "engine", "aurora").Rule.Form, side.Values.Landing.Value);
        }

        var rule = LandingRules.Choose(after, "engine", "aurora");
        Assert.Equal(("repository", LandingForm.Branch, (string?)null), (rule.Source, rule.Rule.Form, rule.Rule.Integrate!.Request));
        Assert.Null(LandingKey.Problem(rule.Rule));
        Assert.Equal(WorkflowSelection.Current, WorkflowSelection.Resolve(after, "engine", "aurora", task: null).Workflow);
    }

    [Fact]
    public async Task Enabling_driving_lists_the_queued_work_it_releases_by_title()
    {
        var driving = await PlanAsync(new World(), Ask(SetupPresets.Merge, "atlas") with { Drive = true });
        var notDriving = await PlanAsync(new World(), Ask(SetupPresets.Merge, "atlas"));
        var held = await PlanAsync(new World(), Ask(SetupPresets.Merge, "atlas") with { Drive = true }, Config.WithHold("atlas", true));

        Assert.Equal([new PlaceReleased("q1", "Fix the map"), new PlaceReleased("q2", "Draw the coast")], Assert.Single(driving.Repositories).Released);
        Assert.Empty(Assert.Single(notDriving.Repositories).Released);
        Assert.Empty(Assert.Single(held.Repositories).Released);
        Assert.True(Assert.Single(held.Repositories).After.Held);
    }

    [Fact]
    public async Task Several_repositories_make_one_plan_saying_each_identical_change_once()
    {
        var plan = await PlanAsync(new World(), Ask(SetupPresets.Merge, "solo", "atlas", "SOLO") with { Drive = true });

        Assert.Equal(["solo", "atlas"], plan.Repositories.Select(row => row.Repository));
        Assert.Equal(
            [("SET_TREES", "solo,atlas"), ("SET_DRIVABLE", "solo,atlas")],
            plan.Changes.Select(each => (each.Change.Key, string.Join(",", each.Repositories))));
    }

    /// <summary>🔴 A plan writes nothing and probes nothing: it asks its world only to read, and the home it is handed is never made.</summary>
    [Fact]
    public async Task The_plan_writes_nothing_and_asks_only_its_reads()
    {
        var world = new World();

        await PlanAsync(world, Ask(SetupPresets.PullRequest, "web", "atlas") with { Drive = true, Adoption = true, Builds = SetupBuilds.Service });
        var quiet = new World();
        await PlanAsync(quiet, Ask(SetupPresets.Merge, "solo"));

        Assert.Equal(["registry", "quests", "tip web", "tip atlas"], world.Asked);
        Assert.Equal(["registry", "tip solo"], quiet.Asked);
        Assert.False(Directory.Exists(_home));
        Assert.Empty(typeof(IPlaceWorld).GetMethods().Where(method => !method.Name.EndsWith("Async", StringComparison.Ordinal)));
    }

    /// <summary>Adoption is refused by the press's own rules over the configuration the plan would leave: drive it in the same plan, or it is not driven.</summary>
    [Fact]
    public async Task Adoption_is_judged_by_the_presss_rules_over_the_configuration_the_plan_leaves()
    {
        var alone = await PlanAsync(new World(), Ask(SetupPresets.Merge, "atlas") with { Adoption = true, Builds = SetupBuilds.Library });
        var driven = await PlanAsync(new World(), Ask(SetupPresets.Merge, "atlas") with { Adoption = true, Drive = true, Builds = SetupBuilds.Library });

        Assert.Equal([SetupRefusals.NotDriven], alone.Refusals.Select(refusal => refusal.Code));
        Assert.Equal(PlacePlans.Adoption, alone.Refusals[0].Part);
        Assert.True(driven.Applies);
        Assert.Contains(new PlaceChange("SETUP_PRESS", PlacePlans.Adoption, "a set-up asked of its own session", "not asked", "asked, for a library or package"),
            Assert.Single(driven.Repositories).Changes);
        Assert.True(Assert.Single(driven.Repositories).Suggested.Adoption);
    }

    [Fact]
    public async Task A_second_opinion_is_never_guessed_and_one_left_off_is_written_none_and_said_as_lowering()
    {
        var reviewed = OpinionRules.Apply(Config, new OpinionEdit { Workspace = "aurora", Set = new OpinionSet { Reviewers = ["codex-acp"] } });

        var guessed = await PlanAsync(new World(), Ask(SetupPresets.Merge, "solo") with { Opinion = true });
        var dropped = await PlanAsync(new World(), Ask(SetupPresets.Merge, "engine"), reviewed);
        var kept = await PlanAsync(new World(), Ask(SetupPresets.Merge, "engine") with { Opinion = true }, reviewed);

        Assert.Equal(["no-reviewers"], guessed.Refusals.Select(refusal => refusal.Code));
        var engine = Assert.Single(dropped.Repositories);
        Assert.Equal(new PlaceChange("SET_OPINION", PlacePlans.Delivery, "a second opinion", "`codex-acp`", "none"), Assert.Single(engine.Changes));
        Assert.Equal(["it drops the second opinion"], engine.Lowers);
        Assert.Empty(Assert.Single(kept.Repositories).Changes);
    }

    [Theory]
    [InlineData("nowhere", false, false, null, false, "preset")]
    [InlineData("not-driven", true, false, null, false, "not-driven")]
    [InlineData("not-driven", false, true, null, false, "not-driven")]
    [InlineData("merge-after-accept", false, false, "library", false, "builds")]
    [InlineData("merge-after-accept", false, false, "game", true, "builds")]
    public async Task The_asks_own_refusals_are_said_before_any_repository_is_changed(
        string preset, bool drive, bool opinion, string? builds, bool adoption, string code)
    {
        var plan = await PlanAsync(new World(), Ask(preset, "engine") with { Drive = drive, Opinion = opinion, Builds = builds, Adoption = adoption });

        Assert.Contains(code, plan.Refusals.Select(refusal => refusal.Code));
        Assert.Null(plan.Refusals.First(refusal => refusal.Code == code).Repository);
        Assert.All(plan.Repositories, row => Assert.Empty(row.Changes));
    }

    [Fact]
    public async Task Not_driven_takes_a_driven_repository_off_driving_and_nothing_else()
    {
        var plan = await PlanAsync(new World(), Ask(SetupPresets.NotDriven, "engine"));

        Assert.Equal(new PlaceChange("SET_DRIVABLE", PlacePlans.Delivery, "driven here", "on", "off"), Assert.Single(plan.Changes).Change);
        Assert.Equal(new SetupMatch(SetupPresets.NotDriven, []), Assert.Single(plan.Repositories).After.Match);
    }

    /// <summary>The verb's words, case by case, held to <c>fixtures/place-plan.json</c>: the same plan from the same code, and the usage.</summary>
    [Theory]
    [MemberData(nameof(Words))]
    public async Task The_verb_says_the_plan_in_the_fixtures_words(string name, string[] args, int exit, string[] lines)
    {
        var output = new StringWriter();

        var code = await PlaceCommand.RunAsync(args, output, new World(), Config, _home, Plugins, SessionWire.Acp, instance: null);

        Assert.True(lines.SequenceEqual(output.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n')), $"{name}:\n{output}");
        Assert.Equal(exit, code);
    }

    public static TheoryData<string, string[], int, string[]> Words()
    {
        var data = new TheoryData<string, string[], int, string[]>();
        var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "place-plan.json")));
        foreach (var each in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            data.Add(
                each.GetProperty("name").GetString()!, [.. each.GetProperty("args").EnumerateArray().Select(arg => arg.GetString()!)],
                each.GetProperty("exit").GetInt32(), [.. each.GetProperty("lines").EnumerateArray().Select(line => line.GetString()!)]);
        }

        return data;
    }

    /// <summary>
    /// The world a plan reads, as a stand-in: <c>solo</c> on its own, <c>engine</c> declaring integration, <c>web</c> on a platform a landing plugin
    /// serves, <c>docs</c> on one none does, <c>atlas</c> not adopted with two queued quests. Each read is recorded.
    /// </summary>
    internal sealed class World : IPlaceWorld
    {
        public List<RepoView> Registry { get; } =
        [
            new("solo", true, "/fixture/solo", "default"),
            new("engine", true, "/fixture/engine", "aurora"),
            new("web", true, "/fixture/web", "aurora"),
            new("docs", true, "/fixture/docs", "aurora"),
            new("atlas", false, "/fixture/atlas", "aurora"),
        ];

        public Dictionary<string, PlaceTip> Tips { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["solo"] = new("main", Commit, "none", null, null),
            ["engine"] = new("main", Commit, Blob, Declaring, null),
            ["web"] = new("main", Commit, "none", null, "github.com"),
            ["docs"] = new("main", Commit, "none", null, "gitlab.com"),
            ["atlas"] = new("main", Commit, "none", null, null),
        };

        public List<QuestView> Quests { get; } =
        [
            new("q1", "work", "atlas", "Fix the map", "", "Open"),
            new("q2", "work", "atlas", "Draw the coast", "", "Open"),
            new("q3", "work", "atlas", "Done already", "", "Closed"),
            new("q4", "work", "engine", "Not atlas's", "", "Open"),
        ];

        public List<string> Asked { get; } = [];

        public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct)
        {
            Asked.Add("registry");
            return Task.FromResult<IReadOnlyList<RepoView>>(Registry);
        }

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct)
        {
            Asked.Add("quests");
            return Task.FromResult<IReadOnlyList<QuestView>>(Quests);
        }

        public Task<PlaceTip> TipAsync(RepoView repository, DriverConfig config, CancellationToken ct)
        {
            Asked.Add($"tip {repository.Repository}");
            return Task.FromResult(Tips[repository.Repository]);
        }
    }
}
