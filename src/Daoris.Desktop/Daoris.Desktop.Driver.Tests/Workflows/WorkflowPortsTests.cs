using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STRUCT3b (D166; the code-structure design §3): Workflows' use cases are written against the ports its Application declares,
/// so a stand-in over memory drives each with no file: a version added and trimmed by the versions the runs name, a run bound to
/// the newest version saved, the person's keep written on its run, a run's process read and held, and a run's records read beside
/// the service. The files behind the ports are held where they always were, over a scratch home: <c>WorkflowNamedTests</c>,
/// <c>WorkflowRunBindingTests</c>, <c>WorkflowGateTests</c>, <c>WorkflowRunTests</c> and <c>WorkflowRunCommandTests</c>.
/// </summary>
public sealed class WorkflowPortsTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 9, 9, 12, 30, TimeSpan.Zero);

    private static readonly DriverConfig Choice = DriverConfig.Parse("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}""");

    private static JsonNode Steps(string accept) => JsonNode.Parse(
        $$"""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"{{accept}}","pattern":"docs/{quest}","plugin":"none"}]""")!;

    private static WorkflowVersionAdded Added(string accept) =>
        new("docs-to-pr", "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z", Steps(accept));

    [Fact]
    public void A_version_is_added_through_the_store_and_the_newest_twenty_kept_with_every_version_a_run_names()
    {
        var store = new MemoryStore();
        var runs = new MemoryRuns();
        runs.Kept["docs-to-pr"] = [1];

        Assert.Equal(1, WorkflowStore.Add(store, runs, Added("automatic")));
        for (var version = 2; version <= 22; version++) Assert.Equal(version, WorkflowStore.Add(store, runs, Added("you")));

        var read = store.Load("docs-to-pr")!.Read;
        Assert.Null(read.Problem);
        Assert.Equal([1, .. Enumerable.Range(4, 19)], read.Versions.Select(each => each.Version));
        Assert.Equal(22, store.Writes);
    }

    [Fact]
    public void A_file_the_store_cannot_read_is_never_written_over()
    {
        var store = new MemoryStore();
        store.Unreadable.Add("docs-to-pr");

        var refused = Assert.Throws<DriverException>(() => WorkflowStore.Add(store, new MemoryRuns(), Added("you")));

        Assert.StartsWith("nothing was written: the workflow's file cannot be read", refused.Message);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void A_run_is_bound_to_the_newest_version_the_store_holds_and_one_it_does_not_hold_is_kept_with_why()
    {
        var store = new MemoryStore();
        var runs = new MemoryRuns();

        var gone = WorkflowRunBindings.Plan(Choice, store, "q0", "web-app", "work", null, null, [], At);
        Assert.Equal(("docs-to-pr", "no workflow `docs-to-pr` is saved here."), (gone.Workflow, gone.Problem));

        WorkflowStore.Add(store, runs, Added("automatic"));
        WorkflowStore.Add(store, runs, Added("you"));
        var binding = WorkflowRunBindings.Plan(Choice, store, "q1", "web-app", "work", null, null, [], At);

        Assert.Equal((2, (string?)null, WorkflowLevels.Repository), (binding.Version, binding.Problem, binding.Level));
        Assert.Equal(store.Load("docs-to-pr")!.Read.Versions[^1].Digest, binding.Digest);
    }

    [Fact]
    public void The_persons_keep_is_written_through_the_runs_port_to_the_second_and_a_run_with_no_binding_keeps_nothing()
    {
        var runs = new MemoryRuns();
        var binding = new WorkflowRunBinding("q1", "web-app", "work", "docs-to-pr", WorkflowLevels.Repository, At) { Version = 1 };
        Assert.True(runs.Bind(binding));

        Assert.Null(WorkflowRunBindings.Keep(runs, "q9", new WorkflowKept(At, ReviewDoors.Terminal)));
        var kept = WorkflowRunBindings.Keep(runs, "q1", new WorkflowKept(At.AddMilliseconds(250), ReviewDoors.Terminal) { Words = "it is the docs" });

        Assert.Equal(binding with { Kept = new WorkflowKept(At, ReviewDoors.Terminal) { Words = "it is the docs" } }, kept);
        Assert.Equal(kept, runs.Read("q1"));
        Assert.False(runs.Bound("q9"));
    }

    /// <summary>The choice of `docs-to-pr` for `web-app`, and its branch rule, which a named landing step chooses within.</summary>
    private static readonly DriverConfig Declared = DriverConfig.Parse("""
        {"workflows":{"web-app":{"default":"docs-to-pr"}},
         "landings":{"web-app":{"form":"branch","pattern":"feature/{quest}","tidy":true}}}
        """);

    [Fact]
    public void A_runs_process_is_read_through_the_ports_its_bound_version_and_a_binding_that_does_not_read_holds_it()
    {
        var store = new MemoryStore();
        var runs = new MemoryRuns();

        var none = WorkflowProcesses.Read(store, runs, Declared, "web-app", "work", "q1");
        Assert.Equal(("q1", (WorkflowRunBinding?)null, "feature/{quest}"), (none.Run, none.Binding, none.Landing.Rule.Pattern));

        WorkflowStore.Add(store, runs, Added("automatic"));
        Assert.True(runs.Bind(WorkflowRunBindings.Plan(Declared, store, "q1", "web-app", "work", null, null, [], At)));
        WorkflowStore.Add(store, runs, Added("you"));

        var named = WorkflowProcesses.Read(store, runs, Declared, "web-app", "work", "q1");
        Assert.True(named.Named);
        Assert.Equal((1, "docs/{quest}", true, true), (named.Binding!.Version, named.Landing.Rule.Pattern, named.Landing.Rule.AutoAccept, named.Landing.Rule.Tidy));

        runs.Damage("q2");
        var damaged = WorkflowProcesses.Read(store, runs, Declared, "web-app", "work", "q2");
        Assert.Equal(("its stored run binding could not be read.", (WorkflowRunBinding?)null), (damaged.Unread, damaged.Binding));

        var (name, steps) = WorkflowProcesses.Otherwise(store, Declared, "web-app", "work");
        Assert.Equal(("`docs-to-pr` v2", 2), (name, steps!.Count));
    }

    /// <summary>
    /// FABRIC-CUTOVER1e (the cutover design §11.1): what work would follow without its kind takes the newest version that reads, a graph's
    /// among them, its person part read from its gate and go-ahead nodes; a newer version that does not read is passed over as before.
    /// </summary>
    [Fact]
    public void What_work_would_follow_otherwise_takes_a_graph_newest_its_person_part_read_from_its_nodes()
    {
        var store = new MemoryStore();
        var runs = new MemoryRuns();
        WorkflowStore.Add(store, runs, Added("you"));
        WorkflowStore.Add(store, runs, new WorkflowVersionAdded("docs-to-pr", null, "terminal", "2026-10-10T09:00:00Z", null, JsonNode.Parse("""
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"ok","kind":"go-ahead","version":1,"reach":"pressed","options":{"act":"publish the docs","on":"dev"}},
                      {"id":"handed","kind":"handed","version":1,"reach":"observed","options":{"form":"branch","accept":"automatic"}}],
             "wires":[{"id":"c>o","kind":"after","from":"claimed","to":"ok"},{"id":"o>h","kind":"after","from":"ok","to":"handed"}]}
            """)));

        var (name, steps) = WorkflowProcesses.Otherwise(store, Declared, "web-app", "work");

        Assert.Equal("`docs-to-pr` v2", name);
        Assert.Equal([("claimed", WorkflowKinds.Work), ("ok", WorkflowKinds.GoAhead), ("handed", WorkflowKinds.Landing)], steps!.Select(step => (step.Id, step.Kind)));
        Assert.Equal(["it lands with no press of yours"], Involvement.Lowers(steps!, Involvement.StepsOf(Assert.Single(
            WorkflowNamed.Read(JsonDocument.Parse("""
                {"id":"plain","name":"Plain","versions":[{"version":1,"at":"2026-10-09T09:00:00Z","door":"terminal","schema":2,"graph":
                  {"schemaVersion":2,"entries":["claimed"],"end":"handed",
                   "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                            {"id":"ok","kind":"go-ahead","version":1,"reach":"pressed","options":{"act":"publish the docs","on":"dev"}},
                            {"id":"handed","kind":"handed","version":1,"reach":"observed","options":{"form":"branch","accept":"you"}}],
                   "wires":[{"id":"c>o","kind":"after","from":"claimed","to":"ok"},{"id":"o>h","kind":"after","from":"ok","to":"handed"}]}}]}
                """).RootElement).Versions).Graph!)));
    }

    [Fact]
    public async Task A_chain_the_service_cannot_name_holds_through_the_ports_only_where_a_named_run_is_bound()
    {
        var store = new MemoryStore();
        var runs = new MemoryRuns();
        var world = new Unanswering();

        Assert.Null((await WorkflowProcesses.ReadAsync(store, runs, Declared, world, "q9", "web-app", "work")).Unread);

        WorkflowStore.Add(store, runs, Added("automatic"));
        runs.Bind(WorkflowRunBindings.Plan(Declared, store, "q1", "web-app", "work", null, null, [], At));
        Assert.True((await WorkflowProcesses.ReadAsync(store, runs, Declared, world, "q1", "web-app", "work")).Named, "the quest's own id names its run");
        Assert.StartsWith(
            "its chain could not be read, so whether a named workflow governs it is not known:",
            (await WorkflowProcesses.ReadAsync(store, runs, Declared, world, "q9", "web-app", "work")).Unread);
        Assert.Null((await WorkflowProcesses.ReadAsync(store, runs, Declared, world, "q9", "notes-site", "work")).Unread);
    }

    /// <summary>
    /// A run's records beside the service come through their port: the landing records and the due list read once per read, the
    /// acceptances once per run, and whether the work's tree stands asked of it. Here none is on disk: the home names no folder.
    /// </summary>
    [Fact]
    public async Task A_runs_records_are_read_through_their_port_once_per_read()
    {
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new StandInService()));
        var sources = new WorkflowRunSources(service, Path.Combine(AppContext.BaseDirectory, "no-home-" + Guid.NewGuid().ToString("N")[..8]),
            DriverConfig.Empty, [], _ => "aurora");
        var records = new MemoryRecords();
        records.Accepted["s1"] = At;

        var read = await WorkflowRunReader.ReadAsync(new WorkflowRunAsk(Quest: "q1"), sources, records);

        var landing = read.Runs.Single().Steps.Single(step => step.Step.Kind == WorkflowKinds.Landing);
        Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Merge, (DateTimeOffset?)At), (landing.State, landing.Detail, landing.At));
        Assert.Equal((1, 1, 1), (records.LandingsRead, records.AutoLandingsRead, records.AcceptancesRead));
        Assert.Equal(["no-such-tree"], records.Asked);
        Assert.False(Directory.Exists(sources.Home));
    }

    /// <summary>
    /// FABRIC-CUTOVER1e: a run bound to a graph version is read from its record through the port, never written, and drawn from it whatever its
    /// gates still hold: its nodes as its steps, no problem, its branch named by this machine's session branches, read once.
    /// </summary>
    [Fact]
    public async Task A_run_bound_to_a_graph_version_is_drawn_from_its_record_read_through_the_port()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-graph-run-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            RunHome.Bind(home, "q1", RunHome.SaveGraph(home, """
                {"schemaVersion":2,"entries":["claimed"],"end":"handed",
                 "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"handed","version":1,"reach":"observed"}],
                 "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
                """), At);
            using var service = new ServiceClient("http://stand-in", null, new HttpClient(new StandInService()));
            var sources = new WorkflowRunSources(service, home, DriverConfig.Empty, [], _ => "aurora");
            var unit = RunUnits.IdOf("q1");
            var records = new MemoryRecords();
            records.Records[unit] = new NodeRecord(unit)
            {
                Statuses = new Dictionary<string, NodeStatus>
                {
                    ["handed"] = new(unit, "handed", NodeStates.Active)
                    {
                        Attempt = 1, Binding = new NodeAcceptance(unit, "abcdef0123456789", "gph", "handed", 1, 1) { Subject = "unit-b" },
                    },
                },
            };
            records.Grown.Add(new GrownBranch("engine", "aurora", "daoris/q1-work", "main", "base", null, At) { Unit = "unit-b" });
            var before = Directory.GetFiles(home, "*", SearchOption.AllDirectories).Length;

            var read = await WorkflowRunReader.ReadAsync(new WorkflowRunAsk(Quest: "q1"), sources, records);

            var run = Assert.Single(read.Runs);
            Assert.Null(run.Problem);
            // The run's branch is its newest session's, by its tree's name; the landing names the branch its binding observes.
            Assert.Equal(("q1", "daoris/no-such-tree"), (run.Graph!.Run, run.Graph.Branch));
            Assert.Equal([("claimed", WorkflowRunStates.NotReached), ("handed", WorkflowRunStates.Working)], run.Steps.Select(step => (step.Step.Id, step.State)));
            Assert.Equal("Observed on `daoris/q1-work` at `abcdef01`.", WorkflowRunWords.Said(run.Steps[1]));
            Assert.Equal(1, records.BranchesRead);
            Assert.Equal(before, Directory.GetFiles(home, "*", SearchOption.AllDirectories).Length);
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// A run is bound at its first start through the ports: the chain's first quest names it, a named workflow's newest version is
    /// kept and said, a later quest of the same run binds nothing, and a service that does not answer binds nothing and says why.
    /// </summary>
    [Fact]
    public async Task A_run_is_bound_at_its_first_start_through_the_ports_and_said()
    {
        var store = new MemoryStore();
        var runs = new MemoryRuns();
        WorkflowStore.Add(store, runs, Added("automatic"));
        var first = new QuestView("q1", "daoris", "web-app", "Docs", "", "Open");
        var second = new QuestView("q2", "daoris", "web-app", "More docs", "", "Open") { Parent = "q1" };
        var world = new StartWorld([first, second]);
        var events = new List<string>();

        await WorkflowRunStart.BindAsync([new WorkflowStarted(first, "work")], world, store, runs, Choice, [], () => At, events, CancellationToken.None);
        await WorkflowRunStart.BindAsync([new WorkflowStarted(second, "work")], world, store, runs, Choice, [], () => At, events, CancellationToken.None);

        Assert.Equal((1, At), (runs.Read("q1")!.Version, runs.Read("q1")!.At));
        Assert.False(runs.Bound("q2"));
        Assert.Equal(["workflow  #q1 → web-app: its run follows `docs-to-pr` v1, `web-app`'s default."], events);
        Assert.Equal(["workflow.chosen"], world.Said.Select(line => line.Event));

        var unanswered = new List<string>();
        await WorkflowRunStart.BindAsync(
            [new WorkflowStarted(new QuestView("q3", "daoris", "web-app", "Later", "", "Open") { Parent = "q1" }, "work")],
            new StartWorld(null), store, runs, Choice, [], () => At, unanswered, CancellationToken.None);
        Assert.Equal(["workflow  #q3 → web-app: its run was not bound to a workflow at this start: the service did not answer"], unanswered);
    }

    /// <summary>
    /// WORKFLOW1e2 (the workflow design §4.1 row 1): a quest no ask published binds the person's choice made on it, at the task's level,
    /// over its repository's default; a chain's step in another repository with none of its own follows its parent's; and a quest whose
    /// latest choice is a clear follows its repository.
    /// </summary>
    [Fact]
    public async Task A_quest_with_no_ask_binds_its_own_choice_and_its_chains_steps_follow_it()
    {
        var store = new MemoryStore();
        var runs = new MemoryRuns();
        WorkflowStore.Add(store, runs, Added("automatic"));
        var config = DriverConfig.Parse("""{"workflows":{"web-app":{"default":"current"},"api":{"default":"current"}}}""");
        var chosen = new AskWorkflowChoiceView(null, "docs-to-pr", At, "docs only");
        var first = new QuestView("q1", "daoris", "web-app", "Docs", "", "Open") { WorkflowChoices = [chosen] };
        var step = new QuestView("q2", "daoris", "api", "Its endpoint", "", "Open") { Parent = "q1" };
        var cleared = new QuestView("q3", "daoris", "web-app", "Later", "", "Open")
        {
            WorkflowChoices = [chosen, new AskWorkflowChoiceView(null, null, At.AddMinutes(1))],
        };
        var world = new StartWorld([first, step, cleared]);
        var events = new List<string>();

        await WorkflowRunStart.BindAsync(
            [new WorkflowStarted(first, "work"), new WorkflowStarted(step, "work"), new WorkflowStarted(cleared, "work")],
            world, store, runs, config, [], () => At, events, CancellationToken.None);

        Assert.Equal(("docs-to-pr", WorkflowLevels.Task, 1), (runs.Read("q1")!.Workflow, runs.Read("q1")!.Level, runs.Read("q1")!.Version));
        Assert.Equal(("docs-to-pr", WorkflowLevels.Task), (runs.Read("q2")!.Workflow, runs.Read("q2")!.Level));
        Assert.Equal((WorkflowSelection.Current, WorkflowLevels.Repository), (runs.Read("q3")!.Workflow, runs.Read("q3")!.Level));
        Assert.Null(runs.Read("q1")!.Ask);
    }

    /// <summary>
    /// FABRIC-CUTOVER1g (D190's binding moment, the cutover design §8): a start binds its run before its take and says what the bind came
    /// to: bound by it, bound already with the binding kept, or not bound with why, which holds the start. The look says a binding a start
    /// made, once, and binds nothing again; a service that answers at the next start binds the run then.
    /// </summary>
    [Fact]
    public async Task A_start_binds_its_run_before_its_take_and_says_what_the_bind_came_to()
    {
        var store = new MemoryStore();
        var runs = new MemoryRuns();
        WorkflowStore.Add(store, runs, Added("automatic"));
        var first = new QuestView("q1", "daoris", "web-app", "Docs", "", "Open");
        var second = new QuestView("q2", "daoris", "web-app", "More docs", "", "Open") { Parent = "q1" };
        var world = new StartWorld([first, second]);

        var made = await WorkflowRunStart.AtStartAsync(new WorkflowStarted(first, "work"), world, store, runs, Choice, [], () => At, CancellationToken.None);
        var kept = await WorkflowRunStart.AtStartAsync(new WorkflowStarted(second, "work"), world, store, runs, Choice, [], () => At, CancellationToken.None);

        Assert.Equal((WorkflowBindCame.Bound, "q1", false), (made.Came, made.Run, made.Holds));
        Assert.Equal(("docs-to-pr", (int?)1), (made.Binding!.Workflow, made.Binding.Version));
        Assert.Equal((WorkflowBindCame.Already, "q1", false), (kept.Came, kept.Run, kept.Holds));
        Assert.Same(made.Binding, kept.Binding);
        Assert.False(runs.Bound("q2"));
        Assert.Equal(["workflow.chosen"], world.Said.Select(line => line.Event));

        // The look says the binding a start made, once; one a start found bound is not said again, and nothing is bound twice.
        var events = new List<string>();
        await WorkflowRunStart.BindAsync(
            [new WorkflowStarted(first, "work"), new WorkflowStarted(second, "work")], world, store, runs, Choice, [], () => At, events,
            CancellationToken.None, made: [made, kept]);
        Assert.Equal(["workflow  #q1 → web-app: its run follows `docs-to-pr` v1, `web-app`'s default."], events);
        Assert.Equal(["workflow.chosen"], world.Said.Select(line => line.Event));

        // A service that does not answer binds nothing, and the start holds saying why; answered at the next start, the run is bound.
        var step = new QuestView("q3", "daoris", "web-app", "Its page", "", "Open") { Parent = "q8" };
        var unanswered = await WorkflowRunStart.AtStartAsync(
            new WorkflowStarted(step, "work"), new StartWorld(null), store, runs, Choice, [], () => At, CancellationToken.None);
        Assert.Equal((WorkflowBindCame.Failed, true, (WorkflowRunBinding?)null), (unanswered.Came, unanswered.Holds, unanswered.Binding));
        Assert.Equal(
            "its run was not bound to a workflow, and its quest is taken only once it is: the service did not answer", unanswered.Why);
        Assert.False(runs.Bound("q3"));

        var answered = await WorkflowRunStart.AtStartAsync(
            new WorkflowStarted(step, "work"), new StartWorld([new QuestView("q8", "daoris", "api", "Its endpoint", "", "Done"), step]),
            store, runs, Choice, [], () => At, CancellationToken.None);
        Assert.Equal((WorkflowBindCame.Bound, "q3", false), (answered.Came, answered.Run, answered.Holds));
        Assert.True(runs.Bound("q3"));
    }

    /// <summary>
    /// FABRIC-CUTOVER1g (design §8, *the choice read once*): a start that holds after its bind keeps its run bound to what it chose, so a
    /// choice the person changes on the quest since applies to the next run, never to this one; a binding that does not read holds the
    /// start, since the workflow its take would name is not known.
    /// </summary>
    [Fact]
    public async Task A_choice_changed_after_its_run_is_bound_applies_to_the_next_run_and_an_unread_binding_holds()
    {
        var store = new MemoryStore();
        var runs = new MemoryRuns();
        WorkflowStore.Add(store, runs, Added("automatic"));
        var config = DriverConfig.Parse("""{"workflows":{"web-app":{"default":"current"}}}""");
        var chosen = new AskWorkflowChoiceView(null, "docs-to-pr", At, "docs only");
        var quest = new QuestView("q1", "daoris", "web-app", "Docs", "", "Open") { WorkflowChoices = [chosen] };
        var world = new StartWorld([quest]);

        var held = await WorkflowRunStart.AtStartAsync(new WorkflowStarted(quest, "work"), world, store, runs, config, [], () => At, CancellationToken.None);
        var changed = quest with { WorkflowChoices = [chosen, new AskWorkflowChoiceView(null, WorkflowSelection.Current, At.AddMinutes(1))] };
        var again = await WorkflowRunStart.AtStartAsync(new WorkflowStarted(changed, "work"), world, store, runs, config, [], () => At, CancellationToken.None);
        var next = new QuestView("q2", "daoris", "web-app", "More docs", "", "Open") { WorkflowChoices = changed.WorkflowChoices };
        var later = await WorkflowRunStart.AtStartAsync(new WorkflowStarted(next, "work"), world, store, runs, config, [], () => At, CancellationToken.None);

        Assert.Equal((WorkflowBindCame.Bound, "docs-to-pr", WorkflowLevels.Task), (held.Came, held.Binding!.Workflow, held.Binding.Level));
        Assert.Equal((WorkflowBindCame.Already, "docs-to-pr"), (again.Came, again.Binding!.Workflow));
        Assert.Equal("docs-to-pr", runs.Read("q1")!.Workflow);
        Assert.Equal((WorkflowBindCame.Bound, WorkflowSelection.Current), (later.Came, later.Binding!.Workflow));

        runs.Damage("q1");
        var unread = await WorkflowRunStart.AtStartAsync(new WorkflowStarted(quest, "work"), world, store, runs, config, [], () => At, CancellationToken.None);
        Assert.Equal((WorkflowBindCame.Unread, true, (WorkflowRunBinding?)null), (unread.Came, unread.Holds, unread.Binding));
        Assert.Equal(
            "its run's binding could not be read here, so the workflow its quest is taken under is not known, and none is put in its place.",
            unread.Why);
    }

    // ——— stand-ins

    /// <summary>The service a start reads, in memory: its quests, or none where it does not answer; and the machine log's lines said.</summary>
    private sealed class StartWorld(IReadOnlyList<QuestView>? quests) : IWorkflowStartWorld
    {
        public List<LandingLine> Said { get; } = [];

        public Task<IReadOnlyList<QuestView>> EveryQuestAsync(CancellationToken ct) =>
            quests is null ? throw new HttpRequestException("the service did not answer") : Task.FromResult(quests);

        public Task<AskView?> FindAskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        public void Chosen(LandingLine line) => Said.Add(line);
    }

    /// <summary>What a run's read reads beside the service, in memory, counting each read.</summary>
    private sealed class MemoryRecords : IWorkflowRunRecords
    {
        public Dictionary<string, DateTimeOffset> Accepted { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Asked { get; } = [];

        public int LandingsRead { get; private set; }

        public int AutoLandingsRead { get; private set; }

        public int AcceptancesRead { get; private set; }

        public IReadOnlyList<LandedBranch> Landings()
        {
            LandingsRead++;
            return [];
        }

        public IReadOnlyList<AutoLanding> AutoLandings()
        {
            AutoLandingsRead++;
            return [];
        }

        public IReadOnlyDictionary<string, DateTimeOffset> Acceptances(IReadOnlyList<TracedSession> sessions)
        {
            AcceptancesRead++;
            return Accepted;
        }

        public bool Stands(string tree)
        {
            Asked.Add(tree);
            return false;
        }

        public bool Holds(string tree) => false;

        /// <summary>The node records by unit; a unit bound to null is a file that does not read, and one not here is a record not written yet.</summary>
        public Dictionary<string, NodeRecord?> Records { get; } = new(StringComparer.Ordinal);

        public List<GrownBranch> Grown { get; } = [];

        public int BranchesRead { get; private set; }

        public NodeRecord? Record(string unit) => Records.TryGetValue(unit, out var record) ? record : new NodeRecord(unit);

        public IReadOnlyList<GrownBranch> Branches()
        {
            BranchesRead++;
            return Grown;
        }
    }

    /// <summary>A stand-in service: quest <c>q1</c> done in <c>engine</c> by session <c>s1</c>, whose tree is gone. Every other door answers an empty list.</summary>
    private sealed class StandInService : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.PathAndQuery switch
            {
                "/api/quests?includeClosed=true" =>
                    """[{"id":"q1","from":"daoris","to":"engine","title":"Quest q1","body":"","status":"Done","updated":"2026-10-09T09:30:00Z"}]""",
                "/api/sessions?includeClosed=true" => """
                    [{"id":"s1","repository":"engine","state":"completed","quest":"q1","adapter":"claude-code","tree":"no-such-tree",
                      "created":"2026-10-09T09:00:00Z","updated":"2026-10-09T09:20:00Z","evidence":"commits landed:\nabc1234 the change"}]
                    """,
                _ => "[]",
            };
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class Unanswering : IReviewWorld
    {
        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => throw new HttpRequestException("the service did not answer");

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => throw new HttpRequestException("the service did not answer");
    }

    /// <summary>The named workflows in memory, each file as written; a name in <see cref="Unreadable"/> is a file that is not JSON.</summary>
    private sealed class MemoryStore : IWorkflowStore
    {
        private readonly Dictionary<string, JsonNode> _files = new(StringComparer.Ordinal);

        public HashSet<string> Unreadable { get; } = new(StringComparer.Ordinal);

        public int Writes { get; private set; }

        public WorkflowFileFound? Load(string id)
        {
            if (Unreadable.Contains(id)) return new WorkflowFileFound(id, null, new NamedWorkflowRead(null, null, [], $"`{id}.json` is not readable JSON."));
            if (!_files.TryGetValue(id, out var file)) return null;
            using var document = JsonDocument.Parse(file.ToJsonString());
            return new WorkflowFileFound(id, file.DeepClone(), WorkflowNamed.Read(document.RootElement.Clone()));
        }

        public IReadOnlyList<WorkflowFileFound> List() => [.. _files.Keys.Order(StringComparer.Ordinal).Select(id => Load(id)!)];

        public void Write(string id, JsonNode file)
        {
            _files[id] = file.DeepClone();
            Writes++;
        }
    }

    /// <summary>The runs' bindings in memory; a run bound to null is a file that does not read as a binding.</summary>
    private sealed class MemoryRuns : IWorkflowRunStore
    {
        private readonly Dictionary<string, WorkflowRunBinding?> _files = new(StringComparer.Ordinal);

        public Dictionary<string, IReadOnlyList<int>> Kept { get; } = new(StringComparer.Ordinal);

        public void Damage(string run) => _files[run] = null;

        public bool Bound(string run) => _files.ContainsKey(run);

        public WorkflowRunBinding? Read(string run) => _files.GetValueOrDefault(run);

        public bool Bind(WorkflowRunBinding binding) => _files.TryAdd(binding.Run, binding);

        public void Write(string run, WorkflowRunBinding binding) => _files[run] = binding;

        public bool AnyNamed(string repository) => _files.Values.Any(binding =>
            binding is null || (!binding.IsCurrent && string.Equals(binding.Repository, repository, StringComparison.OrdinalIgnoreCase)));

        public IReadOnlyList<int> KeptVersions(string workflow) => Kept.GetValueOrDefault(workflow) ?? [];
    }
}
