using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run bound at its first start to the workflow that chose it,
/// with what chose it, kept once under the home; and a workflow's file keeping every version a kept run names.
/// </summary>
public sealed class WorkflowRunBindingTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 9, 9, 12, 30, 250, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-workflow-runs-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static DriverConfig Config(string json) => DriverConfig.Parse(json);

    private void Save(string id, params JsonNode[] graphs)
    {
        foreach (var each in graphs)
        {
            StoredSteps.AddGraph(WorkflowPorts.Store(_home), id, "Documentation to a pull request", "2026-10-09T09:00:00Z", each);
        }
    }

    /// <summary>A branch landing under <c>docs/{quest}</c>, the person's Accept before it where <paramref name="accept"/> is <c>you</c>.</summary>
    private static JsonNode Steps(string accept) => JsonNode.Parse(accept == "you"
        ? """
          {"schemaVersion":2,"entries":["work"],"end":"landing","nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},
            {"id":"review","kind":"review","version":1,"reach":"pressed"},
            {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch","pattern":"docs/{quest}"}}],
           "wires":[{"id":"a","kind":"after","from":"work","to":"review"},{"id":"b","kind":"after","from":"review","to":"landing"}]}
          """
        : """
          {"schemaVersion":2,"entries":["work"],"end":"landing","nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},
            {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch","pattern":"docs/{quest}"}}],
           "wires":[{"id":"a","kind":"after","from":"work","to":"landing"}]}
          """)!;

    [Fact]
    public void Nothing_set_binds_no_workflow_and_holds_its_run()
    {
        var binding = WorkflowRunBindings.Plan(Config("{}"), _home, "a1b2c3", "web-app", "work", null, null, [], At);

        Assert.True(binding.IsCurrent);
        Assert.Equal(WorkflowLevels.Current, binding.Level);
        Assert.Null(binding.Version);
        Assert.Equal(WorkflowProcess.NoWorkflow("web-app"), binding.Problem);
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
        Assert.Equal(
            "workflow  #a1b2c3 → web-app: its run follows `docs-to-pr` v2, chosen by the workspace `work` for Documentation.",
            WorkflowRunBindings.Said(binding, "a1b2c3"));
    }

    /// <summary>
    /// A development route naming a workflow marked a verification pipeline (set by hand, or
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

    /// <summary>
    /// A chat run binds with no ask and no task, so the repository's default chooses, never a
    /// workspace's kind; its line says the workflow and who chose it, once, and a run bound already is read back and said no more.
    /// </summary>
    [Fact]
    public void A_chat_run_binds_under_the_repository_s_default_never_by_a_kind()
    {
        Save("docs-to-pr", Steps("you"));
        var config = Config("""
            {"workflows":{"web-app":{"default":"docs-to-pr"}},
             "workspaceWorkflows":{"work":{"kinds":{"docs":{"label":"Documentation","paths":["docs/**"],"workflow":"release-train"}}}}}
            """);

        var bound = ChatRuns.Bind(config, _home, "chat-c1a2b3c4", "web-app", "work", [], At);

        Assert.True(bound.Wrote);
        Assert.Equal(("docs-to-pr", 1, WorkflowLevels.Repository, (string?)null), (bound.Binding!.Workflow, bound.Binding.Version, bound.Binding.Level, bound.Binding.Kind));
        Assert.Equal(bound.Binding, WorkflowRunBindings.Read(_home, "chat-c1a2b3c4"));
        Assert.Equal("This conversation's work follows `docs-to-pr` v1, the workflow `web-app` chose.", ChatRuns.Line(bound.Binding)!.Note);
        Assert.Equal(ChatNoteCodes.ChatWorkflow.Code, ChatRuns.Line(bound.Binding)!.Parts.Single().Code);

        var again = ChatRuns.Bind(config, _home, "chat-c1a2b3c4", "web-app", "work", [], At.AddHours(1));
        Assert.False(again.Wrote);
        Assert.Equal(bound.Binding, again.Binding);

        // Only a workspace's kind names a workflow: a chat has no kind, so Current binds, and says nothing.
        var current = ChatRuns.Bind(Config("""{"workspaceWorkflows":{"work":{"kinds":{"docs":{"label":"Documentation","paths":["docs/**"],"workflow":"docs-to-pr"}}}}}"""),
            _home, "chat-d1a2b3c4", "web-app", "work", [], At);
        Assert.True(current.Binding!.IsCurrent);
        Assert.Null(ChatRuns.Line(current.Binding));
    }

    /// <summary>
    /// A choice that is a graph version binds as any other does; a named workflow that cannot
    /// be read is bound with its problem, which holds the press by it; a home that cannot keep the binding fails the bind, saying why.
    /// </summary>
    [Fact]
    public void A_chat_run_binds_a_graph_version_keeps_an_unreadable_workflow_s_problem_and_fails_only_on_a_home_it_cannot_write()
    {
        // A graph with a `handed`, as an older build stored it: no door saves one now, and a stored one still binds.
        StoredSteps.AddGraph(WorkflowPorts.Store(_home), "web-graph", "A graph", "2026-10-21T09:00:00Z", JsonNode.Parse(
            """{"schemaVersion":2,"entries":["claimed"],"end":"handed","nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},"""
            + """{"id":"handed","kind":"land","version":1,"reach":"performed"}],"wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}""")!);
        var graph = Config("""{"workflows":{"web-app":{"default":"web-graph"}}}""");

        var graphed = ChatRuns.Bind(graph, _home, "chat-c1a2b3c4", "web-app", null, [], At);
        Assert.Null(graphed.Failed);
        Assert.True(GraphRun.Names(graphed.Binding!));
        Assert.True(WorkflowRunBindings.Bound(_home, "chat-c1a2b3c4"));

        var unread = ChatRuns.Bind(Config("""{"workflows":{"web-app":{"default":"release-train"}}}"""), _home, "chat-e1a2b3c4", "web-app", null, [], At);
        Assert.True(unread.Wrote);
        Assert.Equal("no workflow `release-train` is saved here.", unread.Binding!.Problem);
        Assert.Equal(ChatNoteCodes.ChatWorkflowUnread.Code, ChatRuns.Line(unread.Binding)!.Parts.Single().Code);

        // The runs' folder is a file: the home cannot keep a binding there.
        var runs = WorkflowRunBindings.FolderOf(_home);
        Directory.Delete(runs, recursive: true);
        File.WriteAllText(runs, "not a folder");
        var failed = ChatRuns.Bind(Config("{}"), _home, "chat-d1a2b3c4", "web-app", null, [], At);
        Assert.Null(failed.Binding);
        Assert.NotNull(failed.Failed);
        Assert.Equal(ChatNoteCodes.ChatWorkflowFailed.Code, ChatRuns.FailedLine(failed.Failed!).Parts.Single().Code);
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
        Assert.Equal(BindingProblem.VersionUnread, binding.Coded!.Code);

        // The process a run held on it is read from carries the same code for the page to word.
        var process = WorkflowProcesses.Bound(_home, Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}"""), "web-app", null, "a1b2c3", binding);
        Assert.Equal(BindingProblem.VersionUnread, process.Coded!.Code);
        Assert.Equal(binding.Problem, process.Problem);
    }

    [Fact]
    public void A_run_whose_workflow_is_no_longer_saved_is_held_on_a_coded_problem()
    {
        var binding = new WorkflowRunBinding("a1b2c3", "web-app", "default", "gone-flow", WorkflowLevels.Repository, At) { Version = 1, Digest = "gph-x" };

        var process = WorkflowProcesses.Bound(_home, Config("{}"), "web-app", null, "a1b2c3", binding);

        Assert.Equal(BindingProblem.NotSaved, process.Coded!.Code);
        Assert.Equal("gone-flow", process.Coded.Values["workflow"]);
        Assert.Equal(process.Coded.Said, process.Problem);
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
        Assert.Equal(first with { Paths = read.Paths }, read);
        var text = File.ReadAllText(WorkflowRunBindings.PathOf(_home, "a1b2c3"));
        Assert.DoesNotContain("\r", text);
        Assert.EndsWith("}\n", text);
        Assert.Contains("\"at\": \"2026-10-09T09:12:30Z\"", text);
    }

    /// <summary>
    /// Starts run beside one another, so two binds of one run can race. The first
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
    /// Two starts of one run at once each bind it before their take; one makes the binding and the other
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

        var held = StartHold.TryTake(_home, Path.Combine(_home, "web-app"))!;
        using var holding = held.Lock;
        var starts = new[] { first, second }
            .Select(quest => Task.Run(() => WorkflowRunStart.AtStartAsync(
                new WorkflowStarted(quest, "work"), world, _home, config, [], () => At, held, CancellationToken.None)))
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
    /// The start that loses the race reads the winner's binding while the move that landed it still holds it
    /// (<see cref="MoveHold"/>), and takes under it where it read it as unread and held. The version it names stays kept, and another
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
        var held = StartHold.TryTake(_home, Path.Combine(_home, "web-app"))!;
        using var holding = held.Lock;
        Task<WorkflowBind> Start() =>
            WorkflowRunStart.AtStartAsync(new WorkflowStarted(quest, "work"), world, _home, config, [], () => At, held, CancellationToken.None);
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

    /// <summary>Arming's read of the runs bound to graph versions finds a run whose binding its move into place still holds.</summary>
    [Fact]
    public void A_graph_run_is_found_while_the_move_into_place_still_holds_its_binding()
    {
        if (!OperatingSystem.IsWindows()) return;
        var digest = RunHome.SaveGraph(_home, """
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"}],
             "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
            """);
        RunHome.Bind(_home, "q1", digest, At);

        using (MoveHold.Take(WorkflowRunBindings.PathOf(_home, "q1")))
        {
            Assert.Equal(digest, new GraphRuns(new NodeRecordFiles(_home)).Find("q1")?.Binding.Digest);
        }
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

    /// <summary>A workflow keeps 20 versions, a version a kept run names always among them.</summary>
    [Fact]
    public void A_version_a_kept_run_names_outlives_the_newest_twenty()
    {
        Save("docs-to-pr", Steps("you"));
        var config = Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}""");
        WorkflowRunBindings.Bind(_home, WorkflowRunBindings.Plan(config, _home, "a1b2c3", "web-app", null, null, null, [], At));

        for (var n = 0; n < 21; n++)
        {
            WorkflowStore.Add(_home, new WorkflowVersionAdded("docs-to-pr", null, "terminal", "2026-10-09T09:00:00Z", null,
                JsonNode.Parse(WorkflowDefaults.Of(n % 2 == 0 ? "merge-automatic" : "merge-after-accept")!.GraphJson)));
        }

        var versions = WorkflowStore.Load(_home, "docs-to-pr")!.Read.Versions.Select(each => each.Version).ToList();
        Assert.Equal(1, versions[0]);
        Assert.Equal(20, versions.Count);
        Assert.Equal(4, versions[1]);
        Assert.Equal(22, versions[^1]);
        Assert.Equal([1], WorkflowRunBindings.KeptVersions(_home, "docs-to-pr"));
    }

    /// <summary>
    /// A <c>land</c> the version leaves open is resolved from the derived defaults at the bind, never
    /// from a rule, kept in the run's file, and read from there.
    /// </summary>
    [Fact]
    public void A_land_left_open_is_resolved_from_the_derived_defaults_at_the_bind_and_kept_in_the_runs_file()
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded("open-land", "An open land", "terminal", "2026-11-01T09:00:00Z", null, JsonNode.Parse(
            """
            {"schemaVersion":2,"entries":["work"],"end":"pull-request","nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},
              {"id":"review","kind":"review","version":1,"reach":"pressed"},
              {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch"}},
              {"id":"pushed","kind":"push","version":1,"reach":"performed"},{"id":"pull-request","kind":"acme.github/pull-request","version":1,"reach":"performed"}],
             "wires":[{"id":"a","kind":"after","from":"work","to":"review"},{"id":"r","kind":"after","from":"review","to":"landing"},
                      {"id":"b","kind":"after","from":"landing","to":"pushed"},{"id":"c","kind":"after","from":"pushed","to":"pull-request"}]}
            """)), kept: []);
        var config = Config("""{"workflows":{"web-app":{"default":"open-land"}}}""");

        var planned = WorkflowRunBindings.Plan(config, WorkflowPorts.Store(_home), "a1b2c3", "web-app", null, null, null, Plugins, At);
        Assert.True(WorkflowRunBindings.Bind(_home, planned));
        var bound = WorkflowRunBindings.Read(_home, "a1b2c3")!;

        Assert.Null(bound.Problem);
        Assert.Equal(
            new Dictionary<string, string> { ["pattern"] = LandNodes.DefaultPattern }.OrderBy(each => each.Key),
            bound.Land.OrderBy(each => each.Key));

        // A pattern the version writes with {ticket} resolves the default key, not the rule's.
        WorkflowStore.Add(_home, new WorkflowVersionAdded("ticketed", "Ticketed", "terminal", "2026-11-01T09:00:00Z", null, JsonNode.Parse(
            """
            {"schemaVersion":2,"entries":["work"],"end":"pull-request","nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},
              {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch","pattern":"t/{ticket}"}},
              {"id":"pushed","kind":"push","version":1,"reach":"performed"},{"id":"pull-request","kind":"acme.github/pull-request","version":1,"reach":"performed"}],
             "wires":[{"id":"a","kind":"after","from":"work","to":"landing"},{"id":"b","kind":"after","from":"landing","to":"pushed"},
                      {"id":"c","kind":"after","from":"pushed","to":"pull-request"}]}
            """)), kept: []);
        var ticketed = Config("""{"workflows":{"api":{"default":"ticketed"}}}""");
        var keyed = WorkflowRunBindings.Plan(ticketed, WorkflowPorts.Store(_home), "d4e5f6", "api", null, null, null, Plugins, At);
        Assert.Equal(BranchPatterns.DefaultKey, keyed.Land["key"]);
        Assert.False(keyed.Land.ContainsKey("pattern"));
    }

    private static readonly IReadOnlyList<WorkflowPlugin> Plugins = [new("acme.github", [HookPoints.Perform])];

    /// <summary>A pull request whose plugin neither the version nor the rule names is held at the bind with why.</summary>
    /// <summary>A graph whose work and opinion steps name the role that takes each.</summary>
    private void SaveStaffed(string work, string opinion) =>
        WorkflowStore.Add(_home, new WorkflowVersionAdded("staffed", "Staffed steps", "terminal", "2026-11-01T09:00:00Z", null, JsonNode.Parse(
            $$$"""
            {"schemaVersion":2,"entries":["work"],"end":"pull-request","nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed","options":{"role":"{{{work}}}"}},
              {"id":"second","kind":"opinion","version":1,"reach":"observed","options":{"role":"{{{opinion}}}"}},
              {"id":"review","kind":"review","version":1,"reach":"pressed"},
              {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch"}},
              {"id":"pushed","kind":"push","version":1,"reach":"performed"},{"id":"pull-request","kind":"acme.github/pull-request","version":1,"reach":"performed"}],
             "wires":[{"id":"a","kind":"after","from":"work","to":"second"},{"id":"o","kind":"after","from":"second","to":"review"},
                      {"id":"r","kind":"after","from":"review","to":"landing"},{"id":"l","kind":"after","from":"second","to":"landing"},
                      {"id":"b","kind":"after","from":"landing","to":"pushed"},{"id":"c","kind":"after","from":"pushed","to":"pull-request"}]}
            """)), kept: []);

    private static DriverConfig Staffed => Config("""
        {"workflows":{"web-app":{"default":"staffed"}}}
        """);

    private static RoleCatalog Roles(params RoleRecord[] roles) => RoleCatalog.Of([.. roles.Select(role => RoleCatalogTests.Stored(role, 4))]);

    /// <summary>Each agent step's role is judged at the bind and kept with its record's revision, through the run's file.</summary>
    [Fact]
    public void A_bound_step_s_role_is_kept_with_its_revision_and_read_back_from_the_run_s_file()
    {
        SaveStaffed("frontend", "security");
        var roles = Roles(
            RoleCatalogTests.Working("frontend", "build", new RoleScope(RoleScope.Repository, "web-app")), RoleCatalogTests.Working("security", "review"));

        var planned = WorkflowRunBindings.Plan(Staffed, WorkflowPorts.Store(_home), "a1b2c3", "web-app", "work", null, null, Plugins, At, roles: () => roles);
        Assert.True(WorkflowRunBindings.Bind(_home, planned));
        var bound = WorkflowRunBindings.Read(_home, "a1b2c3")!;

        Assert.Null(bound.Problem);
        Assert.Equal([new WorkflowStepRole("work", NodeKinds.Claimed, "frontend", 4), new WorkflowStepRole("second", NodeKinds.Opinion, "security", 4)], bound.Roles);
        Assert.Equal("frontend", bound.RoleOf(NodeKinds.Claimed)?.Role);
        Assert.Equal("security", bound.RoleOf(NodeKinds.Opinion)?.Role);

        // A binding whose steps name no role writes none, so every file from before reads and writes as it did.
        var current = WorkflowRunBindings.Plan(Config("{}"), _home, "d4e5f6", "web-app", "work", null, null, [], At);
        Assert.Empty(current.Roles);
        Assert.DoesNotContain("roles", WorkflowRunFiles.ToJson(current), StringComparison.Ordinal);
    }

    /// <summary>A role that cannot take its step is the binding's problem, said with why; the step still names it, never another.</summary>
    [Fact]
    public void A_step_s_role_that_cannot_take_it_is_the_binding_s_problem_and_no_other_role_is_put_in_its_place()
    {
        SaveStaffed("frontend", "security");
        var roles = Roles(RoleCatalogTests.Working("frontend", "build"), RoleCatalogTests.Working("security", "build"));

        var planned = WorkflowRunBindings.Plan(Staffed, WorkflowPorts.Store(_home), "a1b2c3", "web-app", "work", null, null, Plugins, At, roles: () => roles);

        Assert.Equal(
            "`staffed` v1's step `second` names the role `security`, which cannot take it: The role `security` does not take `review`, so the starts "
            + "naming it hold until it does: Agents → Roles, or `daoris role`.",
            planned.Problem);
        Assert.Equal("security", planned.RoleOf(NodeKinds.Opinion)?.Role);

        var unknown = WorkflowRunBindings.Plan(Staffed, WorkflowPorts.Store(_home), "a1b2c3", "web-app", "work", null, null, Plugins, At, roles: () => RoleCatalog.Of([]));
        Assert.StartsWith("`staffed` v1's step `work` names the role `frontend`, which cannot take it: No role `frontend` is in the catalogue", unknown.Problem);
        Assert.Equal("frontend", unknown.RoleOf(NodeKinds.Claimed)?.Role);
    }
}
