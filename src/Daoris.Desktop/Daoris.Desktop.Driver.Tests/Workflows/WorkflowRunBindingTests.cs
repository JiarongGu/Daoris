using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1e (D157 point 11, the workflow design §4.5, §5.1, §2.6): a run bound at its first start to the workflow that chose it,
/// with what chose it, kept once under the home; and a workflow's file keeping every version a kept run names.
/// </summary>
public sealed class WorkflowRunBindingTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 9, 9, 12, 30, 250, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-workflow-runs-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static DriverConfig Config(string json) => DriverConfig.Parse(json);

    private void Save(string id, params JsonNode[] steps)
    {
        foreach (var each in steps)
        {
            WorkflowStore.Add(_home, new WorkflowVersionAdded(id, "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z", each), kept: []);
        }
    }

    private static JsonNode Steps(string accept) => JsonNode.Parse(
        $$"""[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"branch","accept":"{{accept}}","pattern":"docs/{quest}","plugin":"none"}]""")!;

    [Fact]
    public void Nothing_set_binds_Current_with_the_graph_it_drew()
    {
        var binding = WorkflowRunBindings.Plan(Config("{}"), _home, "a1b2c3", "web-app", "work", null, null, [], At);

        Assert.True(binding.IsCurrent);
        Assert.Equal(WorkflowLevels.Current, binding.Level);
        Assert.Null(binding.Version);
        var current = WorkflowCurrent.Derive(Config("{}"), "web-app", "work", []);
        Assert.Equal(current.Version, binding.Digest);
        Assert.Equal(current.Steps.Select(WorkflowCurrent.ToJson), binding.CurrentSteps);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 9, 12, 30, TimeSpan.Zero), binding.At);
    }

    [Fact]
    public void A_named_workflow_binds_its_newest_version_with_the_level_and_kind_that_chose_it()
    {
        Save("docs-to-pr", Steps("you"), Steps("automatic"));
        var config = Config("""
            {"workspaceWorkflows":{"work":{"kinds":{"docs":{"label":"Documentation","paths":["docs/**"],"workflow":"docs-to-pr"}}}}}
            """);

        var binding = WorkflowRunBindings.Plan(config, _home, "a1b2c3", "web-app", "work", "f00d01", new WorkflowTaskChoice("docs", null), [], At);

        Assert.Equal("docs-to-pr", binding.Workflow);
        Assert.Equal(2, binding.Version);
        Assert.Equal(WorkflowStore.Load(_home, "docs-to-pr")!.Read.Versions[^1].Digest, binding.Digest);
        Assert.Equal(WorkflowLevels.WorkspaceKind, binding.Level);
        Assert.Equal("docs", binding.Kind);
        Assert.Equal("Documentation", binding.Label);
        Assert.Equal(["docs/**"], binding.Paths);
        Assert.Equal("f00d01", binding.Ask);
        Assert.Null(binding.Problem);
        Assert.Empty(binding.CurrentSteps);
        Assert.Equal(
            "workflow  #a1b2c3 → web-app: its run follows `docs-to-pr` v2, chosen by the workspace `work` for Documentation.",
            WorkflowRunBindings.Said(binding, "a1b2c3"));
    }

    /// <summary>
    /// FABRIC-PIPELINE1a (the pipeline design §2.1): a development route naming a workflow marked a verification pipeline (set by hand, or
    /// by a build before the mark) is judged at its bind: the run keeps what chose it, with why it cannot follow it, and follows nothing
    /// else.
    /// </summary>
    [Fact]
    public void A_development_route_naming_a_verification_pipeline_is_held_at_its_bind_with_why()
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded("web-verify", "Verify the line", "terminal", "2026-10-21T09:00:00Z", null, JsonNode.Parse(
            """{"schemaVersion":2,"entries":["gates"],"end":"gates","nodes":[{"id":"gates","kind":"check","version":1,"reach":"performed"}],"wires":[]}"""))
        {
            Pipeline = WorkflowPipelines.Verification,
        }, kept: []);

        var binding = WorkflowRunBindings.Plan(
            Config("""{"workflows":{"web-app":{"default":"web-verify"}}}"""), _home, "a1b2c3", "web-app", null, null, null, [], At);

        Assert.Equal(("web-verify", WorkflowLevels.Repository), (binding.Workflow, binding.Level));
        Assert.Equal(1, binding.Version);
        Assert.Equal(WorkflowSelection.VerificationElsewhere("web-verify"), binding.Problem);
    }

    [Fact]
    public void A_workflow_chosen_and_not_saved_here_is_kept_with_why_never_swapped_for_another()
    {
        var binding = WorkflowRunBindings.Plan(
            Config("""{"workflows":{"web-app":{"default":"release-train"}}}"""), _home, "a1b2c3", "web-app", null, null, null, [], At);

        Assert.Equal("release-train", binding.Workflow);
        Assert.Equal(WorkflowLevels.Repository, binding.Level);
        Assert.Null(binding.Version);
        Assert.Equal("no workflow `release-train` is saved here.", binding.Problem);
        Assert.Equal(
            "workflow  #a1b2c3 → web-app: its run was bound to `release-train`, `web-app`'s default, which cannot start it: "
            + "no workflow `release-train` is saved here.",
            WorkflowRunBindings.Said(binding, "a1b2c3"));
    }

    [Fact]
    public void A_newest_version_that_does_not_read_here_is_bound_with_its_problem()
    {
        Save("docs-to-pr", Steps("you"));
        var path = WorkflowStore.PathOf(_home, "docs-to-pr");
        var file = JsonNode.Parse(File.ReadAllText(path))!;
        file["versions"]!.AsArray().Add(JsonNode.Parse("""{"version":2,"at":"2026-10-09T10:00:00Z","door":"terminal","steps":[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"queue","accept":"you"}]}"""));
        File.WriteAllText(path, file.ToJsonString());

        var binding = WorkflowRunBindings.Plan(
            Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}"""), _home, "a1b2c3", "web-app", null, null, null, [], At);

        Assert.Equal(2, binding.Version);
        Assert.Null(binding.Digest);
        Assert.StartsWith("`docs-to-pr` v2 cannot be read here: ", binding.Problem);
    }

    [Fact]
    public void The_ask_s_latest_choice_is_the_task_s_and_one_naming_neither_is_none()
    {
        var at = DateTimeOffset.UnixEpoch;
        var ask = new AskView("f00d01", "work", "words", "Published", "named")
        {
            WorkflowChoices = [new AskWorkflowChoiceView("docs", null, at), new AskWorkflowChoiceView("docs", "hotfix", at)],
        };

        Assert.Equal(new WorkflowTaskChoice("docs", "hotfix"), WorkflowRunBindings.TaskOf(ask));
        Assert.Null(WorkflowRunBindings.TaskOf(ask with { WorkflowChoices = [.. ask.WorkflowChoices, new AskWorkflowChoiceView(null, null, at)] }));
        Assert.Null(WorkflowRunBindings.TaskOf(ask with { WorkflowChoices = [] }));
        Assert.Null(WorkflowRunBindings.TaskOf(null));
    }

    /// <summary>
    /// WORKFLOW1e2: a quest no ask published takes its own latest choice, else the nearest of its chain above it with any; the nearest
    /// with any decides, so a clear there is none, and a parent the quests do not hold ends the walk.
    /// </summary>
    [Fact]
    public void A_quests_own_latest_choice_is_the_tasks_else_its_nearest_parents()
    {
        var at = DateTimeOffset.UnixEpoch;
        QuestView Quest(string id, string? parent, params AskWorkflowChoiceView[] choices) =>
            new(id, "daoris", "web-app", "t", "b", "Open") { Parent = parent, WorkflowChoices = choices };
        var root = Quest("a1", null, new AskWorkflowChoiceView("docs", null, at), new AskWorkflowChoiceView("docs", "hotfix", at));
        var middle = Quest("b2", "a1");
        var leaf = Quest("c3", "b2");
        var own = Quest("d4", "a1", new AskWorkflowChoiceView(null, "current", at));
        var cleared = Quest("e5", "a1", new AskWorkflowChoiceView(null, null, at));
        IReadOnlyList<QuestView> every = [root, middle, leaf, own, cleared];

        Assert.Equal(new WorkflowTaskChoice("docs", "hotfix"), WorkflowRunBindings.TaskOf(root, every));
        Assert.Equal(new WorkflowTaskChoice("docs", "hotfix"), WorkflowRunBindings.TaskOf(leaf, every));
        Assert.Equal(new WorkflowTaskChoice(null, "current"), WorkflowRunBindings.TaskOf(own, every));
        Assert.Null(WorkflowRunBindings.TaskOf(cleared, every));
        Assert.Null(WorkflowRunBindings.TaskOf(leaf, [leaf]));
        Assert.Null(WorkflowRunBindings.TaskOf(middle, null));
    }

    [Fact]
    public void A_run_is_named_by_its_chain_s_first_quest_in_the_repository()
    {
        QuestView Quest(string id, string to, string? parent) => new(id, "ask #f00d01", to, "t", "b", "Open") { Parent = parent };
        var chain = new[] { Quest("a1", "web-app", null), Quest("b2", "api", "a1"), Quest("c3", "web-app", "b2") };

        Assert.Equal("a1", WorkflowRunBindings.RunOf(chain, "WEB-APP"));
        Assert.Equal("b2", WorkflowRunBindings.RunOf(chain, "api"));
        Assert.Null(WorkflowRunBindings.RunOf(chain, "notes-site"));
    }

    [Fact]
    public void A_run_is_bound_once_and_read_back_whole()
    {
        Save("docs-to-pr", Steps("automatic"));
        var config = Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}""");
        var first = WorkflowRunBindings.Plan(config, _home, "a1b2c3", "web-app", "work", null, null, [], At);

        Assert.True(WorkflowRunBindings.Bind(_home, first));
        Assert.False(WorkflowRunBindings.Bind(_home, first with { Workflow = "other" }));

        var read = WorkflowRunBindings.Read(_home, "a1b2c3")!;
        Assert.Equal(first with { Paths = read.Paths, CurrentSteps = read.CurrentSteps }, read);
        var text = File.ReadAllText(WorkflowRunBindings.PathOf(_home, "a1b2c3"));
        Assert.DoesNotContain("\r", text);
        Assert.EndsWith("}\n", text);
        Assert.Contains("\"at\": \"2026-10-09T09:12:30Z\"", text);
    }

    /// <summary>
    /// FABRIC-CUTOVER1g (design §8, *one first binding*): starts run beside one another, so two binds of one run can race. The first
    /// binding is moved into place without overwriting: exactly one bind is written, whole, and every other reads it as bound already.
    /// </summary>
    [Fact]
    public async Task Binds_of_one_run_at_once_leave_the_first_binding_whole()
    {
        Save("docs-to-pr", Steps("automatic"));
        var config = Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}""");
        const int Racers = 8;
        for (var round = 0; round < 10; round++)
        {
            var run = $"r{round}";
            using var gate = new Barrier(Racers);
            var binds = Enumerable.Range(0, Racers)
                .Select(racer => Task.Run(() =>
                {
                    var binding = WorkflowRunBindings.Plan(config, _home, run, "web-app", "work", null, null, [], At) with { Label = $"racer {racer}" };
                    gate.SignalAndWait();
                    return (Written: WorkflowRunBindings.Bind(_home, binding), binding.Label);
                }))
                .ToArray();
            var came = await Task.WhenAll(binds);

            var written = Assert.Single(came, each => each.Written);
            Assert.Equal(written.Label, WorkflowRunBindings.Read(_home, run)!.Label);
        }

        Assert.Empty(Directory.GetFiles(WorkflowRunBindings.FolderOf(_home), "*.writing"));
    }

    /// <summary>
    /// FABRIC-CUTOVER1g (design §8): two starts of one run at once each bind it before their take; one makes the binding and the other
    /// reads it as bound already, so both take under the one binding.
    /// </summary>
    [Fact]
    public async Task Two_starts_of_one_run_at_once_both_take_under_one_binding()
    {
        Save("docs-to-pr", Steps("automatic"));
        var config = Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}""");
        // The chain came here from `api`, so its first quest here, q1, names the run both steps are.
        var before = new QuestView("q0", "daoris", "api", "Its endpoint", "", "Done");
        var first = new QuestView("q1", "daoris", "web-app", "Docs", "", "Open") { Parent = "q0" };
        var second = new QuestView("q2", "daoris", "web-app", "More docs", "", "Open") { Parent = "q1" };
        var world = new Gated([before, first, second]);

        var starts = new[] { first, second }
            .Select(quest => Task.Run(() => WorkflowRunStart.AtStartAsync(
                new WorkflowStarted(quest, "work"), world, _home, config, [], () => At, CancellationToken.None)))
            .ToArray();
        var came = await Task.WhenAll(starts);

        Assert.Equal([WorkflowBindCame.Bound, WorkflowBindCame.Already], came.Select(each => each.Came).Order());
        Assert.All(came, each => Assert.Equal(("q1", "docs-to-pr", (int?)1), (each.Run, each.Binding!.Workflow, each.Binding.Version)));
        Assert.Single(world.Said);
    }

    /// <summary>A service that answers each start's read of the chain only once both have asked, so the two binds meet.</summary>
    private sealed class Gated(IReadOnlyList<QuestView> quests) : IWorkflowStartWorld
    {
        private readonly Barrier _both = new(2);

        public List<LandingLine> Said { get; } = [];

        public Task<IReadOnlyList<QuestView>> EveryQuestAsync(CancellationToken ct)
        {
            _both.SignalAndWait(TimeSpan.FromSeconds(10));
            return Task.FromResult(quests);
        }

        public Task<AskView?> FindAskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        public void Chosen(LandingLine line)
        {
            lock (Said) Said.Add(line);
        }
    }

    /// <summary>
    /// BINDRACE1: the start that loses the race reads the winner's binding as it lands, while Windows' move still holds the file open for
    /// its deletion (<see cref="MoveHold"/>), a moment in which a reader not sharing deletion is refused. Every read of a binding shares it,
    /// so the start takes under the binding there, where it read as kept and unread and held; the version it names stays kept, and another
    /// bind is told the run is bound.
    /// </summary>
    [Fact]
    public async Task A_start_reads_the_binding_its_move_into_place_still_holds()
    {
        if (!OperatingSystem.IsWindows()) return;
        Save("docs-to-pr", Steps("automatic"));
        var config = Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}""");
        // The chain's first quest names its run without the chain being read, so the gate is never met.
        var quest = new QuestView("q1", "daoris", "web-app", "Docs", "", "Open");
        var world = new Gated([quest]);
        Task<WorkflowBind> Start() =>
            WorkflowRunStart.AtStartAsync(new WorkflowStarted(quest, "work"), world, _home, config, [], () => At, CancellationToken.None);
        var first = await Start();
        Assert.Equal(WorkflowBindCame.Bound, first.Came);
        var path = WorkflowRunBindings.PathOf(_home, "q1");

        WorkflowBind second;
        bool again;
        IReadOnlyList<int> kept;
        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            second = await Start();
            again = WorkflowRunBindings.Bind(_home, first.Binding! with { Label = "later" });
            kept = WorkflowRunBindings.KeptVersions(_home, "docs-to-pr");
        }

        Assert.Equal(WorkflowBindCame.Already, second.Came);
        Assert.Equal(("q1", "docs-to-pr", (int?)1, first.Binding!.Label), (second.Run, second.Binding!.Workflow, second.Binding.Version, second.Binding.Label));
        Assert.False(again);
        Assert.Equal([1], kept);
        Assert.Single(world.Said);
    }

    /// <summary>BINDRACE1: arming's read of the runs bound to graph versions finds a run whose binding its move into place still holds.</summary>
    [Fact]
    public void A_graph_run_is_found_while_the_move_into_place_still_holds_its_binding()
    {
        if (!OperatingSystem.IsWindows()) return;
        var digest = RunHome.SaveGraph(_home, """
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"handed","version":1,"reach":"observed"}],
             "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
            """);
        RunHome.Bind(_home, "q1", digest, At);

        using (MoveHold.Take(WorkflowRunBindings.PathOf(_home, "q1")))
        {
            Assert.Equal(digest, new GraphRuns(new NodeRecordFiles(_home)).Find("q1")?.Binding.Digest);
        }
    }

    [Fact]
    public void A_Current_binding_reads_back_its_graph()
    {
        var binding = WorkflowRunBindings.Plan(Config("{}"), _home, "a1b2c3", "web-app", "work", null, null, [], At);
        WorkflowRunBindings.Bind(_home, binding);

        var read = WorkflowRunBindings.Read(_home, "a1b2c3")!;

        Assert.Equal(binding.CurrentSteps.Select(step => JsonNode.Parse(step)!.ToJsonString()), read.CurrentSteps.Select(step => JsonNode.Parse(step)!.ToJsonString()));
        Assert.Equal(binding.Digest, read.Digest);
    }

    [Fact]
    public void A_run_s_id_names_no_path_and_a_file_that_does_not_read_is_no_binding()
    {
        Assert.False(WorkflowRunBindings.IsRunId("../a1"));
        Assert.Throws<DriverException>(() => WorkflowRunBindings.PathOf(_home, "../a1"));
        Assert.Null(WorkflowRunBindings.Read(_home, "../a1"));

        Directory.CreateDirectory(WorkflowRunBindings.FolderOf(_home));
        File.WriteAllText(WorkflowRunBindings.PathOf(_home, "a1b2c3"), "{ not json");
        Assert.Null(WorkflowRunBindings.Read(_home, "a1b2c3"));
        Assert.Empty(WorkflowRunBindings.KeptVersions(_home, "docs-to-pr"));
    }

    /// <summary>§2.6: a workflow keeps 20 versions, a version a kept run names always among them (WORKFLOW1d's plan, now handed the runs).</summary>
    [Fact]
    public void A_version_a_kept_run_names_outlives_the_newest_twenty()
    {
        Save("docs-to-pr", Steps("you"));
        var config = Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}""");
        WorkflowRunBindings.Bind(_home, WorkflowRunBindings.Plan(config, _home, "a1b2c3", "web-app", null, null, null, [], At));

        for (var n = 0; n < 21; n++)
        {
            WorkflowStore.Add(_home, new WorkflowVersionAdded("docs-to-pr", null, "terminal", "2026-10-09T09:00:00Z", Steps(n % 2 == 0 ? "automatic" : "you")));
        }

        var versions = WorkflowStore.Load(_home, "docs-to-pr")!.Read.Versions.Select(each => each.Version).ToList();
        Assert.Equal(1, versions[0]);
        Assert.Equal(20, versions.Count);
        Assert.Equal(4, versions[1]);
        Assert.Equal(22, versions[^1]);
        Assert.Equal([1], WorkflowRunBindings.KeptVersions(_home, "docs-to-pr"));
    }
}
