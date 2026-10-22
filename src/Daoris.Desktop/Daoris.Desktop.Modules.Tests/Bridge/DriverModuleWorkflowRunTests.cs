using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Where a piece of work stands in its workflow, over the bridge (WORKFLOW1c; D157 point 11, the workflow design §5.1–§5.2, §7):
/// `WORKFLOW_RUN` answers <see cref="WorkflowRunReader"/> read from the service's quests, sessions and ask and this machine's
/// landing record, each step Current's cell beside where it stands; the same run for a session, its quest and its ask; a chat's
/// no run, with the driver's sentence; and the refusals.
/// </summary>
public sealed class DriverModuleWorkflowRunTests : DriverModuleBridge
{
    public DriverModuleWorkflowRunTests() : base("workflow-runs") { }
    private static readonly DateTimeOffset T = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("kind-paths")]
    [InlineData("cannot-start")]
    [InlineData("unread")]
    [Trait(Category.Name, Category.Process)]
    public async Task The_landing_preview_and_refused_press_carry_the_same_workflow_hold(string state)
    {
        var config = DriverConfig.Parse("""
            {"landings":{"engine":{"form":"branch","pattern":"feature/{quest}"}}}
            """);
        WorkflowStore.Add(Home, new WorkflowVersionAdded("release", "Release", "terminal", T.ToString("O"),
            System.Text.Json.Nodes.JsonNode.Parse("""
                [{"id":"write","kind":"work"},{"id":"publish","kind":"landing","form":"branch","accept":"automatic"}]
                """)), kept: []);
        config = WorkflowSelection.Apply(config, new WorkflowDeclare("docs", "aurora", "Documentation", ["docs/**"]));
        config = WorkflowSelection.Apply(config, new WorkflowUse("release", null, "aurora", "docs"));
        config.Save(DriverConfigPath);
        Assert.True(WorkflowRunBindings.Bind(Home, WorkflowRunBindings.Plan(config, Home, "q1", "engine", "aurora", null,
            new WorkflowTaskChoice("docs", null), [], T)));
        var root = Path.Combine(Home, "fixture-root");
        Directory.CreateDirectory(root);
        try
        {
            await GitAsync(root, "init", "--quiet", "-b", "main");
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "Fixture\n");
            await GitAsync(root, "add", "README.md");
            await GitAsync(root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.test", "commit", "--quiet", "-m", "base");
            var tree = (await new SessionTrees(Home).OpenAsync(root, "engine", "aurora")).Path;
            await File.WriteAllTextAsync(Path.Combine(tree, "outside.txt"), "work\n");
            await GitAsync(tree, "add", "outside.txt");
            await GitAsync(tree, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.test", "commit", "--quiet", "-m", "work");
            if (state == "cannot-start") File.Delete(Path.Combine(WorkflowStore.FolderOf(Home), "release.json"));
            if (state == "unread") File.WriteAllText(WorkflowRunBindings.PathOf(Home, "q1"), "{broken");
            var (module, _) = await UpAsync(tree);

            var preview = await AnswerAsync(module, "LANDING", new { id = "s1" });
            var press = await AnswerAsync(module, "LAND_SESSION_TREE", new { id = "s1" });

            Assert.Equal(state, preview.GetProperty("workflow").GetProperty("state").GetString());
            Assert.True(preview.GetProperty("workflow").GetProperty("holds").GetBoolean());
            Assert.Equal(preview.GetProperty("workflow").GetRawText(), press.GetProperty("workflow").GetRawText());
            Assert.False(press.GetProperty("done").GetBoolean());
            // RPC1b3: the refusal is also said in `trees land`'s words, exit 1, beside the page's fields.
            Assert.Equal([$"trees: {press.GetProperty("message").GetString()}"], press.GetProperty("said").GetProperty("out").EnumerateArray().Select(line => line.GetString()));
            Assert.Equal(1, press.GetProperty("said").GetProperty("code").GetInt32());
            Assert.Empty(new LandedBranches(Home).Entries());
            var branches = await GitAsync(root, "branch", "--list", "feature/*");
            Assert.True(string.IsNullOrWhiteSpace(branches));
            if (state == "kind-paths")
            {
                Assert.Contains("workflow keep s1", preview.GetProperty("workflow").GetProperty("says").GetString());
                Assert.Equal("outside.txt", Assert.Single(preview.GetProperty("workflow").GetProperty("outside").EnumerateArray()).GetString());
                var run = (await AnswerAsync(module, "WORKFLOW_RUN", new { session = "s1" })).GetProperty("runs")[0];
                Assert.Equal(preview.GetProperty("workflow").GetRawText(), run.GetProperty("workflowGate").GetRawText());
                Assert.Equal("cannot-start", run.GetProperty("steps")[1].GetProperty("state").GetString());
            }
        }
        finally
        {
            // Git's object files are read-only on Windows; the bridge owns and removes this fixture home.
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        }
    }

    private static async Task<string> GitAsync(string root, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git")
        { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(TestGit.Past(info))!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(output, error);
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await error);
        return await output;
    }

    [Fact]
    public async Task A_named_runs_bound_steps_survive_Current_changes_and_a_newer_saved_version()
    {
        var config = DriverConfig.Parse("""
            {"reviews":{"engine":{"required":false,"environments":[
              {"name":"local","kind":"local","procedure":"README.md","address":"http://localhost:4200"}]}}}
            """);
        WorkflowStore.Add(Home, new WorkflowVersionAdded("release", "Release with a look", "terminal", T.ToString("O"),
            System.Text.Json.Nodes.JsonNode.Parse("""
                [{"id":"write","kind":"work"},{"id":"inspect","kind":"look","environment":"local"},
                 {"id":"publish","kind":"landing","form":"merge","accept":"you"}]
                """)), kept: []);
        var binding = WorkflowRunBindings.Plan(config, Home, "q1", "engine", "aurora", "a1",
            new WorkflowTaskChoice(null, "release"), [], T);
        Assert.True(WorkflowRunBindings.Bind(Home, binding));
        WorkflowStore.Add(Home, new WorkflowVersionAdded("release", "Release with a look", "terminal", T.AddMinutes(1).ToString("O"),
            System.Text.Json.Nodes.JsonNode.Parse("""
                [{"id":"write","kind":"work"},{"id":"publish","kind":"landing","form":"merge","accept":"you"}]
                """)), kept: [1]);
        DriverConfig.Parse("""
            {"reviews":{"engine":{"required":true,"environments":[
              {"name":"dev","kind":"deployed","procedure":"README.md"},
              {"name":"local","kind":"local","procedure":"README.md","address":"http://localhost:4200"}]}}}
            """).Save(DriverConfigPath);
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_RUN", new { quest = "q1" });
        var run = Assert.Single(answer.GetProperty("runs").EnumerateArray());
        var workflow = run.GetProperty("workflow");
        Assert.Equal("release", workflow.GetProperty("id").GetString());
        Assert.Equal(1, workflow.GetProperty("boundVersion").GetInt32());
        Assert.Equal(binding.Digest, workflow.GetProperty("version").GetString());
        // FABRIC-PIPELINE1a: the run's view says which route chose its pipeline, as its binding keeps it.
        var route = workflow.GetProperty("route");
        Assert.Equal((WorkflowLevels.Task, JsonValueKind.Null, JsonValueKind.Null), (route.GetProperty("level").GetString(), route.GetProperty("kind").ValueKind, route.GetProperty("label").ValueKind));
        var steps = run.GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(["write", "inspect", "publish"], steps.Select(step => step.GetProperty("step").GetProperty("id").GetString()));
        Assert.Equal("local", steps[1].GetProperty("step").GetProperty("settings").GetProperty("environment").GetString());
        Assert.Equal("waiting-on-you", steps[1].GetProperty("state").GetString());
    }

    [Theory]
    [InlineData("version")]
    [InlineData("binding")]
    public async Task An_unreadable_bound_workflow_is_not_drawn_as_Current_or_done(string broken)
    {
        WorkflowStore.Add(Home, new WorkflowVersionAdded("release", "Release", "terminal", T.ToString("O"),
            System.Text.Json.Nodes.JsonNode.Parse("""
                [{"id":"write","kind":"work"},{"id":"publish","kind":"landing","form":"merge","accept":"you"}]
                """)), kept: []);
        var binding = WorkflowRunBindings.Plan(DriverConfig.Empty, Home, "q1", "engine", "aurora", null,
            new WorkflowTaskChoice(null, "release"), [], T);
        Assert.True(WorkflowRunBindings.Bind(Home, binding));
        var path = broken == "binding" ? WorkflowRunBindings.PathOf(Home, "q1") : Path.Combine(WorkflowStore.FolderOf(Home), "release.json");
        File.WriteAllText(path, "{broken");
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_RUN", new { quest = "q1" });
        var run = Assert.Single(answer.GetProperty("runs").EnumerateArray());
        Assert.False(string.IsNullOrWhiteSpace(run.GetProperty("problem").GetString()));
        Assert.Empty(run.GetProperty("steps").EnumerateArray());
    }

    [Theory]
    [InlineData(false, "cannot-start")]
    [InlineData(true, "not-known")]
    public async Task A_bound_look_with_no_declaration_is_never_skipped_even_when_the_ask_is_unreadable(bool askUnread, string state)
    {
        WorkflowStore.Add(Home, new WorkflowVersionAdded("release", "Release", "terminal", T.ToString("O"),
            System.Text.Json.Nodes.JsonNode.Parse("""
                [{"id":"write","kind":"work"},{"id":"inspect","kind":"look"},
                 {"id":"publish","kind":"landing","form":"merge","accept":"you"}]
                """)), kept: []);
        Assert.True(WorkflowRunBindings.Bind(Home, WorkflowRunBindings.Plan(DriverConfig.Empty, Home, "q1", "engine", "aurora", null,
            new WorkflowTaskChoice(null, "release"), [], T)));
        var (module, _) = await UpAsync(askUnread: askUnread);

        var answer = await AnswerAsync(module, "WORKFLOW_RUN", new { quest = "q1" });
        var look = answer.GetProperty("runs")[0].GetProperty("steps")[1];

        Assert.Equal("inspect", look.GetProperty("step").GetProperty("id").GetString());
        Assert.Equal(state, look.GetProperty("state").GetString());
        Assert.False(string.IsNullOrWhiteSpace(look.GetProperty("words").GetString()));
    }

    /// <summary>
    /// FABRIC-CUTOVER1e (D190 points 2 and 3): a run bound to a graph version is answered from its record, never as a problem: its nodes as its
    /// steps, each with its own kind, its detail its record's state and the subject it observes; its graph's entries, end and wires, a fork and its
    /// join among them; and what its record says of it. The page draws it whole, so every field is named here.
    /// </summary>
    [Fact]
    public async Task A_run_bound_to_a_graph_version_is_answered_from_its_record_with_its_wires()
    {
        WorkflowStore.Add(Home, new WorkflowVersionAdded("docs", "Docs", "terminal", T.ToString("O"), null, System.Text.Json.Nodes.JsonNode.Parse("""
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"build","kind":"acme.ci/built","version":1,"reach":"observed"},{"id":"lint","kind":"acme.ci/linted","version":1,"reach":"observed"},
                      {"id":"both","kind":"all","version":1,"reach":"observed"},
                      {"id":"handed","kind":"handed","version":1,"reach":"observed","options":{"form":"merge","accept":"you"}}],
             "wires":[{"id":"c>b","kind":"after","from":"claimed","to":"build"},{"id":"c>l","kind":"after","from":"claimed","to":"lint"},
                      {"id":"b>a","kind":"after","from":"build","to":"both"},{"id":"l>a","kind":"after","from":"lint","to":"both"},
                      {"id":"a>h","kind":"after","from":"both","to":"handed"}]}
            """)), kept: []);
        var binding = WorkflowRunBindings.Plan(DriverConfig.Empty, Home, "q1", "engine", "aurora", null, new WorkflowTaskChoice(null, "docs"), [], T);
        Assert.StartsWith("gph-", binding.Digest);
        Assert.True(WorkflowRunBindings.Bind(Home, binding));
        var unit = RunUnits.IdOf("q1");
        new NodeRecordFiles(Home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["claimed"] = new(unit, "claimed", NodeStates.Reached)
                {
                    Attempt = 1, Acceptance = new NodeAcceptance(unit, "moment", binding.Digest!, "claimed", 1, 1) { Subject = "quest:q1" },
                },
            },
        });
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_RUN", new { quest = "q1" });

        var run = Assert.Single(answer.GetProperty("runs").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, run.GetProperty("problem").ValueKind);
        Assert.Equal(("docs", 1), (run.GetProperty("workflow").GetProperty("id").GetString(), run.GetProperty("workflow").GetProperty("boundVersion").GetInt32()));
        var steps = run.GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(
            [("claimed", "work", "claimed", "done", "reached"), ("build", "acme.ci/built", "acme.ci/built", "not-reached", "blocked"),
             ("lint", "acme.ci/linted", "acme.ci/linted", "not-reached", "blocked"), ("both", "all", "all", "not-reached", "blocked"),
             ("handed", "landing", "handed", "not-reached", "blocked")],
            steps.Select(step => (step.GetProperty("step").GetProperty("id").GetString(), step.GetProperty("step").GetProperty("kind").GetString(),
                step.GetProperty("node").GetString(), step.GetProperty("state").GetString(), step.GetProperty("detail").GetString())));
        Assert.Equal(("quest", "q1"), (steps[0].GetProperty("on").GetString(), steps[0].GetProperty("quest").GetString()));
        Assert.Equal("build", run.GetProperty("at").GetString());

        var graph = run.GetProperty("graph");
        Assert.Equal(("q1", "handed", "q1", true, false), (graph.GetProperty("run").GetString(), graph.GetProperty("end").GetString(),
            graph.GetProperty("quest").GetString(), graph.GetProperty("judged").GetBoolean(), graph.GetProperty("unread").GetBoolean()));
        Assert.Equal(["claimed"], graph.GetProperty("entries").EnumerateArray().Select(entry => entry.GetString()));
        Assert.Equal(
            [("claimed", "build", "after"), ("claimed", "lint", "after"), ("build", "both", "after"), ("lint", "both", "after"), ("both", "handed", "after")],
            graph.GetProperty("wires").EnumerateArray().Select(wire => (wire.GetProperty("from").GetString(), wire.GetProperty("to").GetString(), wire.GetProperty("kind").GetString())));
        Assert.Equal(JsonValueKind.Null, graph.GetProperty("ended").ValueKind);
    }

    [Theory]
    [InlineData("opinion")]
    [InlineData("landing")]
    public async Task A_bound_step_that_lacks_its_declaration_cannot_start(string kind)
    {
        var steps = kind == "opinion"
            ? """[{"id":"write","kind":"work"},{"id":"inspect","kind":"opinion"},{"id":"publish","kind":"landing","form":"merge","accept":"you"}]"""
            : """[{"id":"write","kind":"work"},{"id":"publish","kind":"landing","form":"branch","accept":"you"}]""";
        WorkflowStore.Add(Home, new WorkflowVersionAdded("release", "Release", "terminal", T.ToString("O"),
            System.Text.Json.Nodes.JsonNode.Parse(steps)), kept: []);
        Assert.True(WorkflowRunBindings.Bind(Home, WorkflowRunBindings.Plan(DriverConfig.Empty, Home, "q1", "engine", "aurora", null,
            new WorkflowTaskChoice(null, "release"), [], T)));
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_RUN", new { quest = "q1" });
        var step = answer.GetProperty("runs")[0].GetProperty("steps")[1];

        Assert.Equal("cannot-start", step.GetProperty("state").GetString());
        Assert.False(string.IsNullOrWhiteSpace(step.GetProperty("words").GetString()));
    }

    /// <summary>
    /// A chain's work done in a repository whose branch rule names a plugin that lands and answers its state, landed here on a
    /// branch whose pull request is open: the work done, the landing done on its branch, the pull request waiting for the merge.
    /// </summary>
    [Fact]
    public async Task A_run_is_read_from_the_records_each_step_beside_Current_s_cell()
    {
        Plugin("example.pull-request", ["work/land", "work/state"]);
        File.WriteAllText(DriverConfigPath, """
            { "landings": { "engine": { "form": "branch", "pattern": "work/{quest}", "plugin": "example.pull-request" } } }
            """);
        new LandedBranches(Home).Record(new LandedBranch("engine", "aurora", "work/q1", "main", Commit, "s1", "q1", "Quest q1", T)
        {
            Plugin = "example.pull-request", Pushed = true, PullRequest = "https://example.test/pull/7",
        });
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_RUN", new { quest = "q1" });

        var run = Assert.Single(answer.GetProperty("runs").EnumerateArray());
        Assert.Equal("engine", run.GetProperty("repository").GetString());
        Assert.Equal("aurora", run.GetProperty("workspace").GetString());
        Assert.Equal("a1", run.GetProperty("ask").GetString());
        Assert.Equal(["q1"], run.GetProperty("quests").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal("s1", run.GetProperty("session").GetString());
        Assert.Equal("pull-request", run.GetProperty("at").GetString());

        var current = WorkflowCurrent.Derive(
            DriverConfig.Load(DriverConfigPath), "engine", "aurora", WorkflowCurrent.PluginsOf(PluginCatalog.Load(Home, AdapterSet.Built().Names)));
        Assert.Equal(current.Version, run.GetProperty("workflow").GetProperty("version").GetString());
        var steps = run.GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(current.Steps.Select(WorkflowCurrent.ToJson), steps.Select(step => step.GetProperty("step").GetRawText()));
        Assert.Equal(
            [("done", "finished"), ("done", "branch"), ("waiting-on-you", "merge")],
            steps.Select(step => (step.GetProperty("state").GetString(), step.GetProperty("detail").GetString())));
        Assert.Equal("work/q1", steps[1].GetProperty("branch").GetString());
        Assert.Equal("https://example.test/pull/7", steps[2].GetProperty("pullRequest").GetString());
        Assert.Equal("claude-code", steps[0].GetProperty("agent").GetString());
        // The pull request's limit, in the driver's words, for a page with none of its own for it.
        Assert.Equal(
            WorkflowLimits.Says(WorkflowLimits.PullRequestAtCleanUp),
            run.GetProperty("workflow").GetProperty("limits").GetProperty(WorkflowLimits.PullRequestAtCleanUp).GetString());
    }

    /// <summary>A session's run is its quest's, and an ask's is each run of each chain it asked: the same records, read the same.</summary>
    [Fact]
    public async Task A_session_and_an_ask_read_the_same_run_as_the_quest()
    {
        var (module, _) = await UpAsync();

        var byQuest = await AnswerAsync(module, "WORKFLOW_RUN", new { quest = "q1" });
        var bySession = await AnswerAsync(module, "WORKFLOW_RUN", new { session = "s1" });
        var byAsk = await AnswerAsync(module, "WORKFLOW_RUN", new { ask = "a1" });

        Assert.Equal(byQuest.GetProperty("runs").GetRawText(), bySession.GetProperty("runs").GetRawText());
        Assert.Equal(byQuest.GetProperty("runs").GetRawText(), byAsk.GetProperty("runs").GetRawText());
        // Nothing landed it here: the merge waits for the person's Accept.
        var landing = byQuest.GetProperty("runs")[0].GetProperty("steps")[1];
        Assert.Equal(("waiting-on-you", "accept"), (landing.GetProperty("state").GetString(), landing.GetProperty("detail").GetString()));
    }

    /// <summary>A conversation serves no quest: no run, and the driver's sentence says why, which is an answer and not a refusal.</summary>
    [Fact]
    public async Task A_chat_has_no_run_and_says_so()
    {
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_RUN", new { session = "c1" });

        Assert.Empty(answer.GetProperty("runs").EnumerateArray());
        Assert.Equal("session `c1` serves no quest, so no workflow runs for it.", answer.GetProperty("problem").GetString());
    }

    /// <summary>Run <c>q1</c> bound to v1 of a workflow whose graph holds a go-ahead before its landing, its claim reached and its go-ahead under way.</summary>
    private NodeRecordFiles BoundWithAGoAhead()
    {
        WorkflowStore.Add(Home, new WorkflowVersionAdded("docs", "Docs", "terminal", T.ToString("O"), null, System.Text.Json.Nodes.JsonNode.Parse("""
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"go","kind":"go-ahead","version":1,"reach":"pressed","options":{"act":"deploy the release"}},
                      {"id":"handed","kind":"handed","version":1,"reach":"observed"}],
             "wires":[{"id":"c>g","kind":"after","from":"claimed","to":"go"},{"id":"g>h","kind":"after","from":"go","to":"handed"}]}
            """)), kept: []);
        var binding = WorkflowRunBindings.Plan(DriverConfig.Empty, Home, "q1", "engine", "aurora", null, new WorkflowTaskChoice(null, "docs"), [], T);
        Assert.True(WorkflowRunBindings.Bind(Home, binding));
        var unit = RunUnits.IdOf("q1");
        var files = new NodeRecordFiles(Home, () => true);
        files.Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["claimed"] = new(unit, "claimed", NodeStates.Reached)
                {
                    Attempt = 1, Acceptance = new NodeAcceptance(unit, "moment", binding.Digest!, "claimed", 1, 1) { Subject = "quest:q1" },
                },
                ["go"] = new(unit, "go", NodeStates.Active) { Attempt = 1, Binding = new NodeAcceptance(unit, null, binding.Digest!, "go", 1, 1) },
            },
        });
        return files;
    }

    /// <summary>
    /// FABRIC-CUTOVER1f, additive: each graph run's node answers what the person may say of it now, <c>acts</c>, and the word kept on it and not
    /// applied yet, <c>asked</c>, which then takes the place of its acts. A steps run's steps carry neither. Seen failing first.
    /// </summary>
    [Fact]
    public async Task A_graph_runs_nodes_answer_the_persons_acts_and_a_word_kept_on_one()
    {
        var files = BoundWithAGoAhead();
        var (module, _) = await UpAsync();

        var steps = Assert.Single((await AnswerAsync(module, "WORKFLOW_RUN", new { quest = "q1" })).GetProperty("runs").EnumerateArray())
            .GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(
            [("claimed", JsonValueKind.Null), ("go", JsonValueKind.Array), ("handed", JsonValueKind.Null)],
            steps.Select(step => (step.GetProperty("step").GetProperty("id").GetString(), step.GetProperty("acts").ValueKind)));
        Assert.Equal(["skip", "stop"], steps[1].GetProperty("acts").EnumerateArray().Select(act => act.GetString()));
        Assert.Equal(JsonValueKind.Null, steps[1].GetProperty("asked").ValueKind);

        files.Write(files.Read(RunUnits.IdOf("q1"))! with { Pending = [new NodeWord("go", NodeWordActs.Skip, "nobody raises it", T)] });
        var go = Assert.Single((await AnswerAsync(module, "WORKFLOW_RUN", new { quest = "q1" })).GetProperty("runs").EnumerateArray())
            .GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("step").GetProperty("id").GetString() == "go");
        var asked = go.GetProperty("asked");
        Assert.Equal(("go", "skip", "nobody raises it", T), (asked.GetProperty("node").GetString(), asked.GetProperty("act").GetString(),
            asked.GetProperty("words").GetString(), asked.GetProperty("at").GetDateTimeOffset()));
        Assert.Equal(JsonValueKind.Null, go.GetProperty("acts").ValueKind);
    }

    /// <summary>
    /// FABRIC-CUTOVER1f: <c>WORKFLOW_NODE</c>, the person's skip or stop of a run's node. Before the driver is up it is the cold-start sentence; a
    /// call naming no node is malformed; a process that does not hold the home keeps nothing and says so, as an answer with its code. Asked for its
    /// words (<c>said: true</c>) by <c>workflow skip</c> sent over the pipe, it answers the lines and the exit code the alone path prints over the
    /// same home. Seen failing first.
    /// </summary>
    [Fact]
    public async Task A_word_on_a_runs_node_is_refused_cold_malformed_or_without_the_home_and_its_said_is_the_alone_paths()
    {
        var asked = new { run = "q1", node = "go", act = "skip", words = "nobody raises it" };
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "WORKFLOW_NODE", asked));
        BoundWithAGoAhead();
        var (module, _) = await UpAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(module, "WORKFLOW_NODE", new { run = "q1", act = "stop" }));

        var answer = await AnswerAsync(module, "WORKFLOW_NODE", asked);

        Assert.Equal(("q1", "go", "skip", false, "not-held"), (answer.GetProperty("run").GetString(), answer.GetProperty("node").GetString(),
            answer.GetProperty("act").GetString(), answer.GetProperty("done").GetBoolean(), answer.GetProperty("refused").GetString()));
        Assert.Contains("does not hold the home", answer.GetProperty("message").GetString());
        Assert.Empty(new NodeRecordFiles(Home, () => true).Read(RunUnits.IdOf("q1"))!.Pending);

        var said = (await AnswerAsync(module, "WORKFLOW_NODE", new { asked.run, asked.node, asked.act, asked.words, said = true })).GetProperty("said");
        var output = new StringWriter { NewLine = "\n" };
        var code = await WorkflowNodeCommand.RunAsync(new("skip", "q1", "go", "nobody raises it"), new NodeRecordFiles(Home), output, instance: null, _ => null);
        Assert.Equal((output.ToString().TrimEnd('\n'), code),
            (string.Join("\n", said.GetProperty("out").EnumerateArray().Select(line => line.GetString())), said.GetProperty("code").GetInt32()));
        Assert.Equal(1, code);
    }

    [Fact]
    public async Task A_refusal_names_what_to_name_and_a_run_waits_for_the_driver()
    {
        var neither = await RefusalAsync(Module(), "WORKFLOW_RUN", new { });
        var two = await RefusalAsync(Module(), "WORKFLOW_RUN", new { quest = "q1", ask = "a1" });
        var early = await RefusalAsync(Module(), "WORKFLOW_RUN", new { quest = "q1" });

        Assert.Contains("a run is read for a `session`, a `quest` or an `ask` — name one of them.", neither);
        Assert.Contains("name one of them", two);
        Assert.Contains("still coming up", early);
    }

    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>A plugin switched on, speaking on the points named.</summary>
    private void Plugin(string id, string[] points)
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins,id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), JsonSerializer.Serialize(new
        {
            id,
            hooks = new { command = new[] { "node", "${plugin}/hook.mjs" }, points },
        }));
        PluginState.Enable(Home, id);
    }

    /// <summary>The module over a loop come up on the stand-in service below.</summary>
    private async Task<(DriverModule Module, DriverLoop Loop)> UpAsync(string? tree = null, bool askUnread = false)
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInService(tree, askUnread))));
        return (module, loop);
    }

    /// <summary>
    /// A stand-in service: `engine` in the `aurora` workspace, ask `a1`'s one quest done there by session `s1`, and a chat. Every
    /// other door answers an empty list.
    /// </summary>
    private sealed class StandInService(string? tree = null, bool askUnread = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.PathAndQuery switch
            {
                "/api/registry" => """[{"repository":"engine","workspace":"aurora"}]""",
                "/api/quests?includeClosed=true" =>
                    """[{"id":"q1","from":"ask #a1","to":"engine","title":"Quest q1","body":"","status":"Done","updated":"2026-10-09T09:30:00Z"}]""",
                "/api/sessions?includeClosed=true" => """
                    [{"id":"s1","repository":"engine","state":"completed","quest":"q1","adapter":"claude-code",
                      "created":"2026-10-09T09:00:00Z","updated":"2026-10-09T09:20:00Z","evidence":"commits landed:\nabc1234 the change"},
                     {"id":"c1","repository":"engine","state":"working","kind":"chat","created":"2026-10-09T08:00:00Z"}]
                    """,
                "/api/asks/a1" => """{"id":"a1","workspace":"aurora","sentence":"Fix it","state":"Open","tier":"none","quests":["q1"]}""",
                _ => "[]",
            };
            if (tree is not null && request.RequestUri!.PathAndQuery == "/api/sessions?includeClosed=true")
                body = JsonSerializer.Serialize(new[] { new { id = "s1", repository = "engine", state = "completed", quest = "q1", tree } });
            if (request.RequestUri!.PathAndQuery == "/api/quests/q1")
                body = """{"id":"q1","from":"ask #a1","to":"engine","title":"Quest q1","body":"","status":"Done"}""";
            var status = askUnread && request.RequestUri!.PathAndQuery == "/api/asks/a1"
                ? System.Net.HttpStatusCode.ServiceUnavailable : System.Net.HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
