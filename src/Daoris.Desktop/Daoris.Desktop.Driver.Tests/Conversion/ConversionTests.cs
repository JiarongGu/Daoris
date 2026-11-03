using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOWCLEAN1 1e (the cleanup design §4, §13 point 4): one fixture home holding each old shape, its census, its plan, its apply with each
/// outcome, a restart mid-apply, <c>moved</c>, <i>Undo</i>, and the plan again showing only what is left.
/// </summary>
public sealed class ConversionTests : IDisposable
{
    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "convert-" + Guid.NewGuid().ToString("N")[..8]);

    private const string QaCycle = """{ "nodes": [ { "id": "qa", "kind": "human", "version": 1, "reach": "pressed" } ], "wires": [ { "id": "a", "kind": "after", "from": "handed", "to": "qa" } ], "end": "qa" }""";

    private static readonly RepoView[] Registry =
    [
        new("engine", true, "/f/engine", "aurora"), new("web", true, "/f/web", "aurora"), new("docs", true, "/f/docs", "aurora"),
        new("auto", true, "/f/auto", "default"), new("atlas", true, "/f/atlas", "default"), new("solo", true, "/f/solo", "default"),
        new("legacy", true, "/f/legacy", "default"), new("cyc", true, "/f/cyc", "default"), new("steps-user", true, "/f/steps", "default"),
        new("plain", true, "/f/plain", "default"),
    ];

    private string ConfigPath => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static Dictionary<string, T> Map<T>(params (string Key, T Value)[] entries) =>
        new(entries.ToDictionary(each => each.Key, each => each.Value), StringComparer.OrdinalIgnoreCase);

    /// <summary>Every old shape the design lists, in one driver.json and one folder of workflows.</summary>
    private void Seed()
    {
        Directory.CreateDirectory(_home);
        var config = DriverConfig.Empty with
        {
            Landings = Map(
                // A branch rule with pattern, plugin, key, squash, tidy and cycle.
                ("engine", new LandingRule(LandingForm.Branch, "feat/{ticket}-{slug}", Tidy: true, Plugin: "example-pr", Key: "[A-Z]+-\\d+", Squash: true) { Cycle = QaCycle }),
                // An automatic merge, and one a handed version is open to.
                ("auto", new LandingRule(LandingForm.Merge, AutoAccept: true)),
                ("docs", new LandingRule(LandingForm.Merge, AutoAccept: true)),
                // `landing: rule` over a non-automatic branch.
                ("atlas", new LandingRule(LandingForm.Branch, "work/{quest}-{slug}"))),
            // A workspace rule with `integrate`.
            WorkspaceLandings = Map(("aurora", new LandingRule(LandingForm.Branch, LandNodes.DefaultPattern) { Integrate = new LandingIntegrate(Batch: 2, Gates: LandingIntegrate.Declared, Conflicts: LandingIntegrate.Session) })),
            Access = Map(("atlas", new AccessSetting { Pauses = new Dictionary<string, string> { [AccessKey.Landing] = AccessKey.Rule } })),
            SessionChecks = Map(("solo", "light")),
            Workflows = Map(
                ("legacy", new WorkflowChoice("current", [])), ("web", new WorkflowChoice("old-handed", [])), ("docs", new WorkflowChoice("old-handed", [])),
                ("steps-user", new WorkflowChoice("old-steps", [])), ("plain", new WorkflowChoice("land-you", []))),
            WorkCycles = Map<IReadOnlyDictionary<string, string>>(("cyc", new Dictionary<string, string> { ["quest"] = """{"nodes":[],"wires":[]}""" })),
        };
        config.Save(ConfigPath);
        var store = WorkflowPorts.Store(_home);
        const string at = "2026-10-10T09:00:00Z";
        StoredSteps.Add(store, "old-steps", "Old steps", at, JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"merge","accept":"you"}]""")!);
        StoredSteps.AddGraph(store, "old-handed", "Old handed", at, JsonNode.Parse("""
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"handed","version":1,"reach":"observed"}],
             "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
            """)!);
        StoredSteps.AddGraph(store, "land-you", "Land you", at, LandWithAccept("you"));
        StoredSteps.AddGraph(store, "land-auto", "Land auto", at, LandWithAccept("automatic"));
    }

    private static JsonNode LandWithAccept(string accept) => JsonNode.Parse($$$"""
        {"schemaVersion":2,"entries":["claimed"],"end":"landing",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge","accept":"{{{accept}}}"}}],
         "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"}]}
        """)!;

    private static LegacyRun Run(string run, string repository, string workflow, int? version, bool open = true) =>
        new(new WorkflowRunBinding(run, repository, "default", workflow, "repository", DateTimeOffset.UtcNow.AddHours(-1)) { Version = version }, open, run);

    private List<LegacyRun> Runs { get; } = [];

    private ConvertWorld World() => new(DriverConfig.Load(ConfigPath), _home, Registry, WorkflowPorts.Store(_home), Runs);

    private ConvertDoors Doors(Action<Func<DriverConfig, DriverConfig>>? change = null, Func<IReadOnlySet<string>>? open = null) => new(
        _home, _ => Task.FromResult(World()), change ?? (edit => edit(DriverConfig.Load(ConfigPath)).Save(ConfigPath)), () => DriverConfig.Load(ConfigPath),
        open ?? (() => Runs.Where(each => each.Open && each.Binding.IsCurrent).SelectMany(each => new[] { "repository:" + each.Binding.Repository.ToLowerInvariant() }).ToHashSet()));

    private ConversionPlan Plan(ConversionChoices? choices = null) =>
        ConversionPlans.Plan(World(), choices ?? new ConversionChoices(), new ConversionReceipts(_home).HeldPlaces());

    [Fact]
    public void The_fixture_reads_back_as_it_was_written()
    {
        Seed();
        var config = DriverConfig.Load(ConfigPath);
        Assert.Equal(["atlas", "auto", "docs", "engine"], config.Landings.Keys.Order());
        Assert.Equal(["aurora"], config.WorkspaceLandings.Keys.ToList());
        Assert.Equal(2, config.WorkspaceLandings["aurora"].Integrate!.Batch);
        Assert.Equal("light", config.SessionChecks["solo"]);
        Assert.Equal(["cyc"], config.WorkCycles.Keys);
        Assert.True(config.Access["atlas"].Pauses.ContainsKey(AccessKey.Landing));
        Assert.Equal("old-handed", config.Workflows["web"].Default);
        Assert.Equal("current", config.Workflows["legacy"].Default);
    }

    [Fact]
    public void The_census_finds_each_old_shape_in_its_item()
    {
        Seed();
        Runs.Add(Run("q-steps", "steps-user", "old-steps", 1));
        Runs.Add(Run("q-auto", "auto", "current", null));

        var census = WorkflowLegacy.Census(World());

        Assert.Equal(["old-steps"], census.Steps.Select(each => each.Workflow));
        Assert.Equal(["old-handed"], census.Handed.Select(each => each.Workflow));
        Assert.Equal(["land-auto", "land-you"], census.LandAccept.Select(each => each.Workflow).Order());
        var places = census.Places.Select(each => each.Place).Order().ToList();
        Assert.Contains("engine", places);
        Assert.Contains("aurora", places);
        Assert.Contains("auto", places);
        Assert.Contains("legacy", places);
        Assert.DoesNotContain("web", places);
        Assert.Equal(["cyc"], census.WorkCycles.Select(each => each.Place));
        Assert.Equal(["q-auto", "q-steps"], census.Runs.Select(each => each.Run).Order());
        Assert.All(census.Runs, run => Assert.Contains("quest abandon", run.Door));
        // Docs follows a handed version, so its own merge rule is nothing a place on Current reads.
        Assert.Contains(census.Leftovers, each => each.Place == "docs");
        Assert.False(census.Ended);
    }

    [Fact]
    public void The_plan_converts_each_shape_and_names_what_it_keeps()
    {
        Seed();
        Runs.Add(Run("q-steps", "steps-user", "old-steps", 1));
        Runs.Add(Run("q-auto", "auto", "current", null));

        var plan = Plan();

        // The saved workflows: the next version of each id, a handed open to two rules in one version each.
        var steps = plan.Saves.Single(each => each.Id == "old-steps");
        Assert.Equal(("append", 1), (steps.Mode, steps.Base));
        Assert.DoesNotContain(steps.Graph["nodes"]!.AsArray(), node => (string?)node!["kind"] == NodeKinds.Handed);
        Assert.Contains(plan.Saves, each => each.Id == "old-handed" && each.Mode == "append");
        var second = plan.Saves.Single(each => each.Id.StartsWith("old-handed-", StringComparison.Ordinal));
        Assert.Equal("new", second.Mode);
        Assert.Contains(plan.Uses, each => each.Workflow == second.Id);
        var lifted = new[] { plan.Saves.Single(each => each.Id == "old-handed"), second }
            .Select(each => each.Graph["nodes"]!.AsArray().Single(node => (string?)node!["kind"] == NodeKinds.Land)["options"]!["method"]!.ToString()).Order().ToList();
        Assert.Equal(["branch", "merge"], lifted);
        // `land` with accept: `you` and `automatic` both get the review, and `automatic` says it was never landing.
        foreach (var id in new[] { "land-you", "land-auto" })
        {
            var graph = plan.Saves.Single(each => each.Id == id).Graph;
            Assert.Contains(graph["nodes"]!.AsArray(), node => (string?)node!["kind"] == NodeKinds.Review);
            Assert.All(graph["nodes"]!.AsArray(), node => Assert.Null(node!["options"]?["accept"]));
        }

        Assert.Empty(plan.Lowers);
        // Places: the home default is written, the automatic merge matches its shipped copy, the branch with everything a workflow of its own.
        Assert.Equal("merge-after-accept", plan.DefaultWorkflow);
        Assert.Contains(plan.Uses, each => each.Place == "auto" && each.Workflow == "merge-automatic");
        Assert.DoesNotContain(plan.Saves, each => each.Id == "merge-automatic" && each.Mode == "append");
        var engine = plan.Saves.Single(each => each.Id == "engine-workflow");
        var land = engine.Graph["nodes"]!.AsArray().Single(node => (string?)node!["kind"] == NodeKinds.Land)["options"]!.AsObject();
        Assert.Equal(("pull-request", "example-pr", "feat/{ticket}-{slug}", "[A-Z]+-\\d+", true),
            (land["method"]!.ToString(), land["plugin"]!.ToString(), land["pattern"]!.ToString(), land["key"]!.ToString(), (bool)land["squash"]!));
        Assert.Contains(engine.Graph["nodes"]!.AsArray(), node => (string?)node!["id"] == "qa");
        Assert.Contains(engine.Graph["wires"]!.AsArray(), wire => (string?)wire!["from"] == "landing" && (string?)wire["to"] == "qa");
        // The workspace's rule becomes the workspace's choice, and its integrate a line policy; web follows it with nothing written.
        Assert.Contains(plan.Uses, each => each.IsWorkspace && each.Place == "aurora");
        Assert.Equal(2, plan.Policies.Single(each => each.Place == "aurora").Policy.Batch);
        Assert.Null(plan.Policies.Single(each => each.Place == "aurora").Policy.Request);
        // sessionChecks becomes claimed.checks; landing: rule over a branch converts with no review as it landed.
        var solo = plan.Saves.Single(each => each.Id == "solo-workflow").Graph["nodes"]!.AsArray().Single(node => (string?)node!["kind"] == NodeKinds.Claimed);
        Assert.Equal("light", solo["options"]!["checks"]!.ToString());
        var atlas = plan.Saves.Single(each => each.Id == "atlas-workflow").Graph;
        Assert.DoesNotContain(atlas["nodes"]!.AsArray(), node => (string?)node!["kind"] == NodeKinds.Review);
        // An explicit current with no rule follows the home default again.
        Assert.Contains(plan.Uses, each => each.Place == "legacy" && each.Workflow is null);
        // The work cycle stays; the open Current unit keeps its place's rule; the bound runs are counted.
        Assert.Contains(plan.Kept, each => each.Code == "work-cycle" && each.Subject == "cyc");
        Assert.Contains(plan.Kept, each => each.Code == "open-unit" && each.Subject == "auto");
        Assert.DoesNotContain(plan.Removals, each => each.Place == "auto");
        Assert.Equal(2, plan.Kept.Count(each => each.Code == "bound-run"));
        Assert.Contains(plan.Removals, each => each.Place == "engine" && each.Keys.Contains("landings"));
        Assert.Contains(plan.Removals, each => each.Place == "atlas" && each.Keys.Contains("access.landing"));
        Assert.Contains(plan.Removals, each => each.Place == "solo" && each.Keys.Contains("sessionChecks"));
    }

    [Fact]
    public async Task The_apply_writes_everything_once_and_the_plan_again_shows_only_what_is_left()
    {
        Seed();
        Runs.Add(Run("q-steps", "steps-user", "old-steps", 1));
        Runs.Add(Run("q-auto", "auto", "current", null));
        var plan = Plan();

        var result = await ConversionApplies.ApplyAsync(Doors(), new ConversionChoices(), plan.Digest);

        Assert.True(result.Done, result.Message);
        var config = DriverConfig.Load(ConfigPath);
        Assert.Equal("merge-after-accept", config.DefaultWorkflow);
        Assert.False(config.Landings.ContainsKey("engine"));
        Assert.True(config.Landings.ContainsKey("auto"), "the open Current unit keeps its rule");
        Assert.False(config.WorkspaceLandings.ContainsKey("aurora"));
        Assert.Equal(2, config.WorkspaceLinePolicies["aurora"].Batch);
        Assert.False(config.SessionChecks.ContainsKey("solo"));
        Assert.Equal("engine-workflow", WorkflowSelection.Resolve(config, "engine", "aurora", null).Workflow);
        Assert.Equal(WorkflowLevels.Home, WorkflowSelection.Resolve(config, "legacy", "default", null).Level);
        // The versions are saved and the old ones stand as they were.
        var store = WorkflowPorts.Store(_home);
        Assert.Equal(2, store.Load("old-steps")!.Read.Versions.Count);
        Assert.NotNull(store.Load("old-steps")!.Read.Versions[0].Steps);
        Assert.NotNull(store.Load("engine-workflow")!.Read.Versions[0].Graph);
        Assert.Equal(["merge-after-accept"], new ConversionReceipts(_home).All().Select(each => each.DefaultWorkflow!).ToList());
        Assert.True(ConversionReceipts.Stands(_home));
        Assert.Equal(InstallRollback.Workflows, InstallRollback.Refused(_home, "0.41.0", "0.42.0", ConversionReceipts.Stands)!.Value.Code);

        // Again: only the leftovers, the work cycle and the bound runs remain, and nothing is left to write.
        var again = Plan();
        Assert.True(again.Empty, string.Join("; ", again.Saves.Select(each => each.Id)));
        Assert.Empty(again.Census.Versions);
        Assert.Equal(["cyc"], again.Census.WorkCycles.Select(each => each.Place));
        Assert.Contains(again.Census.Leftovers, each => each.Place == "auto");
        Assert.Equal(2, again.Census.Runs.Count);

        // A start now binds the converted version, and a bound run keeps the version it was bound to.
        var binding = WorkflowRunBindings.Plan(config, store, "q-new", "solo", "default", null, null, [], DateTimeOffset.UtcNow);
        Assert.Equal(("solo-workflow", 1), (binding.Workflow, binding.Version));
        Assert.Null(binding.Problem);
        var autoStart = WorkflowRunBindings.Plan(config, store, "q-new2", "auto", "default", null, null, [], DateTimeOffset.UtcNow);
        Assert.Equal("merge-automatic", autoStart.Workflow);
        // The Current run bound before the conversion lands the old way: its rule is still in force.
        Assert.True(LandingRules.Effective(config, "auto", "default").Rule.AutoAccept);
    }

    [Fact]
    public async Task A_plan_that_moved_since_it_was_read_is_refused_with_nothing_written()
    {
        Seed();
        var plan = Plan();
        DriverConfig.Load(ConfigPath).WithCap(5).WithLanding("plain", new LandingRule(LandingForm.Merge, AutoAccept: true)).Save(ConfigPath);

        var result = await ConversionApplies.ApplyAsync(Doors(), new ConversionChoices(), plan.Digest);

        Assert.False(result.Done);
        Assert.Equal(ConversionRefusals.Moved, result.Refused);
        Assert.NotNull(result.Plan);
        Assert.Null(DriverConfig.Load(ConfigPath).DefaultWorkflow);
        Assert.Empty(new ConversionReceipts(_home).All());
        Assert.Equal(2, WorkflowPorts.Store(_home).Load("old-handed") is { } found ? found.Read.Versions.Count + 1 : 0);
    }

    [Fact]
    public async Task A_rule_edited_between_the_plan_and_the_edit_is_refused_moved_by_the_judge_inside_it()
    {
        Seed();
        var plan = Plan();

        var result = await ConversionApplies.ApplyAsync(Doors(change: edit =>
        {
            // Another writer got in between the plan's read and the edit.
            DriverConfig.Load(ConfigPath).WithLanding("engine", new LandingRule(LandingForm.Merge)).Save(ConfigPath);
            edit(DriverConfig.Load(ConfigPath)).Save(ConfigPath);
        }), new ConversionChoices(), plan.Digest);

        Assert.False(result.Done);
        Assert.Equal(ConversionRefusals.Moved, result.Refused);
        Assert.True(DriverConfig.Load(ConfigPath).Landings.ContainsKey("engine"));
        Assert.Null(DriverConfig.Load(ConfigPath).DefaultWorkflow);
    }

    [Fact]
    public async Task A_unit_that_opened_after_the_plan_keeps_its_places_rules()
    {
        Seed();
        var plan = Plan();

        var result = await ConversionApplies.ApplyAsync(Doors(open: () => new HashSet<string> { "repository:engine" }), new ConversionChoices(), plan.Digest);

        Assert.True(result.Done, result.Message);
        var config = DriverConfig.Load(ConfigPath);
        Assert.True(config.Landings.ContainsKey("engine"));
        Assert.Equal("engine-workflow", WorkflowSelection.Resolve(config, "engine", "aurora", null).Workflow);
        Assert.Contains("repository:engine", new ConversionReceipts(_home).HeldPlaces());
        // Once it has ended the next plan offers the removal, and applying it empties the leftovers.
        var later = Plan();
        Assert.Contains(later.Removals, each => each.Place == "engine");
        var second = await ConversionApplies.ApplyAsync(Doors(), new ConversionChoices(), later.Digest);
        Assert.True(second.Done, second.Message);
        Assert.False(DriverConfig.Load(ConfigPath).Landings.ContainsKey("engine"));
    }

    [Fact]
    public async Task A_restart_after_the_edit_landed_but_its_answer_was_lost_settles_from_the_file()
    {
        Seed();
        var plan = Plan();
        var lost = await ConversionApplies.ApplyAsync(Doors(change: edit =>
        {
            edit(DriverConfig.Load(ConfigPath)).Save(ConfigPath);
            throw new IOException("the answer was lost");
        }), new ConversionChoices(), plan.Digest);

        Assert.False(lost.Done);
        var receipt = new ConversionReceipts(_home).Read(plan.Digest)!;
        Assert.Equal(PlaceOutcomes.Uncertain, receipt.Of("configuration")!.Outcome);

        var again = await ConversionApplies.ApplyAsync(Doors(change: _ => Assert.Fail("the edit is not replayed")), new ConversionChoices(), plan.Digest);

        Assert.True(again.Done, again.Message);
    }

    [Fact]
    public async Task A_restart_between_the_saves_and_the_edit_goes_on_from_the_receipt_without_planning_again()
    {
        Seed();
        var plan = Plan();
        // The saves are made, and the process stops before the edit: the receipt is the only thing that remembers the plan.
        var stopped = await ConversionApplies.ApplyAsync(Doors(change: _ => throw new IOException("stopped")), new ConversionChoices(), plan.Digest);
        Assert.False(stopped.Done);
        Assert.Equal(PlaceOutcomes.Done, new ConversionReceipts(_home).Read(plan.Digest)!.Of("save:old-steps")!.Outcome);
        Assert.Null(DriverConfig.Load(ConfigPath).DefaultWorkflow);

        var resumed = await ConversionApplies.ApplyAsync(Doors(), new ConversionChoices(), plan.Digest);

        Assert.True(resumed.Done, resumed.Message);
        Assert.Equal("merge-after-accept", DriverConfig.Load(ConfigPath).DefaultWorkflow);
        Assert.Equal(2, WorkflowPorts.Store(_home).Load("old-steps")!.Read.Versions.Count);
    }

    [Fact]
    public async Task Undo_puts_the_keys_back_and_the_rollback_refusal_lifts()
    {
        Seed();
        var before = DriverConfig.Load(ConfigPath);
        var plan = Plan();
        await ConversionApplies.ApplyAsync(Doors(), new ConversionChoices(), plan.Digest);
        var undo = ConversionApplies.UndoPlan(Doors());
        Assert.Null(undo.Refusal);

        var result = ConversionApplies.Undo(Doors(), undo.Digest!);

        Assert.True(result.Done, result.Message);
        var config = DriverConfig.Load(ConfigPath);
        Assert.Null(config.DefaultWorkflow);
        Assert.Empty(config.WorkspaceLinePolicies);
        Assert.Equal(before.Landings["engine"], config.Landings["engine"]);
        Assert.Equal(before.WorkspaceLandings["aurora"], config.WorkspaceLandings["aurora"]);
        Assert.Equal("light", config.SessionChecks["solo"]);
        Assert.Equal(AccessKey.Rule, config.Access["atlas"].Pauses[AccessKey.Landing]);
        Assert.Equal("current", config.Workflows["legacy"].Default);
        Assert.False(config.Workflows.ContainsKey("engine"));
        // The versions it saved stay saved, unchosen.
        Assert.NotNull(WorkflowPorts.Store(_home).Load("engine-workflow"));
        Assert.False(ConversionReceipts.Stands(_home));
        Assert.Null(InstallRollback.Refused(_home, "0.41.0", "0.42.0", ConversionReceipts.Stands));
        Assert.NotNull(ConversionApplies.UndoPlan(Doors()).Refusal);
    }

    [Fact]
    public async Task Undo_is_refused_moved_where_a_key_it_wrote_changed_since()
    {
        Seed();
        var plan = Plan();
        await ConversionApplies.ApplyAsync(Doors(), new ConversionChoices(), plan.Digest);
        DriverConfig.Load(ConfigPath).WithLanding("engine", new LandingRule(LandingForm.Merge)).Save(ConfigPath);

        var undo = ConversionApplies.UndoPlan(Doors());

        Assert.StartsWith("moved:", undo.Refusal);
        Assert.Contains("engine", undo.Refusal);
        Assert.True(ConversionReceipts.Stands(_home));
    }

    [Fact]
    public async Task The_rows_choices_lower_nothing_by_default_and_say_so_when_they_do()
    {
        Seed();
        var lowering = Plan(new ConversionChoices(LandAutomatic: ConversionChoices.AsSaved));
        Assert.Contains(lowering.Lowers, each => each.Contains("land-auto"));
        Assert.DoesNotContain(lowering.Saves.Single(each => each.Id == "land-auto").Graph["nodes"]!.AsArray(), node => (string?)node!["kind"] == NodeKinds.Review);
        var keeping = Plan(new ConversionChoices(Composed: ConversionChoices.KeepReview));
        Assert.Contains(keeping.Saves.Single(each => each.Id == "atlas-workflow").Graph["nodes"]!.AsArray(), node => (string?)node!["kind"] == NodeKinds.Review);
        Assert.NotEqual(Plan().Digest, lowering.Digest);
        var bad = await ConversionApplies.ApplyAsync(Doors(), new ConversionChoices(Composed: "maybe"), Plan().Digest);
        Assert.Equal(ConversionRefusals.Choices, bad.Refused);
    }

    [Fact]
    public void Session_checks_are_refused_for_a_place_on_a_saved_workflow_and_still_set_on_current()
    {
        var onGraph = DriverConfig.Empty.WithDefaultWorkflow("merge-after-accept");

        var refused = Assert.Throws<DriverException>(() => SessionChecks.Apply(onGraph, "engine", SessionChecks.Light));

        Assert.Contains("claimed", refused.Message);
        // Taking the rule away is never refused, and a place on Current sets it as it did.
        Assert.Empty(SessionChecks.Apply(onGraph, "engine", null).SessionChecks);
        Assert.Equal(SessionChecks.Light, SessionChecks.Apply(DriverConfig.Empty, "engine", SessionChecks.Light).SessionChecks["engine"]);
    }

    [Fact]
    public void A_handed_lift_reproduces_the_old_reading_over_the_rule()
    {
        // A merge-form `handed` under a `merge --auto-accept` rule: GateSteps read the press as `you` (a merge is always pressed).
        var graph = (JsonObject)JsonNode.Parse("""
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"handed","version":1,"reach":"observed"}],
             "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
            """)!;
        var merge = new LegacyFacts("docs", false, "default", new LegacyRule(LandingForm.Merge, null, null, true, null, false, false, null, null), true, false, false, false, null, false, false, false, "old", "repository");

        var lifted = LegacyGraphs.LiftHanded(graph, HandedResolution.Of(merge), out var refusal)!;

        Assert.Null(refusal);
        Assert.Equal("merge", lifted["nodes"]!.AsArray().Single(node => (string?)node!["kind"] == NodeKinds.Land)["options"]!["method"]!.ToString());
        Assert.Contains(lifted["nodes"]!.AsArray(), node => (string?)node!["kind"] == NodeKinds.Review);
        // The same under a branch rule that accepts automatically is a branch with no review.
        var branch = merge with { Rule = new LegacyRule(LandingForm.Branch, null, null, true, null, false, false, null, null) };
        var auto = LegacyGraphs.LiftHanded(graph, HandedResolution.Of(branch), out _)!;
        Assert.DoesNotContain(auto["nodes"]!.AsArray(), node => (string?)node!["kind"] == NodeKinds.Review);
        Assert.Equal("branch", auto["nodes"]!.AsArray().Single(node => (string?)node!["kind"] == NodeKinds.Land)["options"]!["method"]!.ToString());
        // A merge written with no press never ran in the old engine, so it is kept, not lowered.
        var written = (JsonObject)graph.DeepClone();
        written["nodes"]!.AsArray()[1]!["options"] = new JsonObject { ["form"] = "merge", ["accept"] = "automatic" };
        Assert.Null(LegacyGraphs.LiftHanded(written, HandedResolution.Default, out var kept));
        Assert.Contains("never ran", kept);
    }

    [Fact]
    public void A_drawn_graph_matches_a_shipped_copy_by_its_shape_not_its_ids()
    {
        var plain = new LegacyFacts("plain", false, "default", null, false, false, false, false, null, false, false, false, "current", "current");
        var drawn = LegacyGraphs.OfCurrent(plain, keepReview: false);
        var shipped = WorkflowDefaults.Of("merge-after-accept")!.Graph();

        Assert.Equal(LegacyGraphs.Shape(shipped), LegacyGraphs.Shape(drawn));
        Assert.NotEqual(NodeGraphs.Digest(NodeGraphs.Read(shipped.ToJsonString()).Graph!), NodeGraphs.Digest(NodeGraphs.Read(drawn.ToJsonString()).Graph!));
        var gated = plain with { Opinion = true, Look = true };
        Assert.Equal(LegacyGraphs.Shape(WorkflowPresets.WithGraphGates(shipped, true, true)), LegacyGraphs.Shape(LegacyGraphs.OfCurrent(gated, false)));
    }
}
