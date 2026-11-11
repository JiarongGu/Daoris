using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Where a piece of work stands in its workflow, over the bridge:
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
        var config = DriverConfig.Empty;
        StoredSteps.Add(Home, new WorkflowVersionAdded("release", "Release", "terminal", T.ToString("O"), null,
            System.Text.Json.Nodes.JsonNode.Parse("""
                {"schemaVersion":2,"entries":["claimed"],"end":"handed",
                 "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                          {"id":"handed","kind":"land","version":1,"reach":"performed","options":{"method":"branch","pattern":"feature/{quest}"}}],
                 "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
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
            // The refusal is also said in `trees land`'s words, exit 1, beside the page's fields.
            Assert.Equal([$"trees: {press.GetProperty("message").GetString()}"], press.GetProperty("said").GetProperty("out").EnumerateArray().Select(line => line.GetString()));
            Assert.Equal(1, press.GetProperty("said").GetProperty("code").GetInt32());
            Assert.Empty(new LandedBranches(Home).Entries());
            var branches = await GitAsync(root, "branch", "--list", "feature/*");
            Assert.True(string.IsNullOrWhiteSpace(branches));
            if (state == "kind-paths")
            {
                Assert.Contains("workflow keep s1", preview.GetProperty("workflow").GetProperty("says").GetString());
                Assert.Equal("outside.txt", Assert.Single(preview.GetProperty("workflow").GetProperty("outside").EnumerateArray()).GetString());
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

    /// <summary>
    /// A run bound to a graph version is answered from its record, never as a problem: its nodes as its
    /// steps, each with its own kind, its detail its record's state and the subject it observes; its graph's entries, end and wires, a fork and its
    /// join among them; and what its record says of it. The page draws it whole, so every field is named here.
    /// </summary>
    [Fact]
    public async Task A_run_bound_to_a_graph_version_is_answered_from_its_record_with_its_wires()
    {
        StoredSteps.Add(Home, new WorkflowVersionAdded("docs", "Docs", "terminal", T.ToString("O"), null, System.Text.Json.Nodes.JsonNode.Parse("""
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"build","kind":"acme.ci/built","version":1,"reach":"observed"},{"id":"lint","kind":"acme.ci/linted","version":1,"reach":"observed"},
                      {"id":"both","kind":"all","version":1,"reach":"observed"},
                      {"id":"yes","kind":"review","version":1,"reach":"pressed"},
                      {"id":"handed","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
             "wires":[{"id":"c>b","kind":"after","from":"claimed","to":"build"},{"id":"c>l","kind":"after","from":"claimed","to":"lint"},
                      {"id":"b>a","kind":"after","from":"build","to":"both"},{"id":"l>a","kind":"after","from":"lint","to":"both"},
                      {"id":"a>y","kind":"after","from":"both","to":"yes"},{"id":"y>h","kind":"after","from":"yes","to":"handed"}]}
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
             ("yes", "review", "review", "not-reached", "blocked"), ("handed", "landing", "land", "not-reached", "blocked")],
            steps.Select(step => (step.GetProperty("step").GetProperty("id").GetString(), step.GetProperty("step").GetProperty("kind").GetString(),
                step.GetProperty("node").GetString(), step.GetProperty("state").GetString(), step.GetProperty("detail").GetString())));
        Assert.Equal(("quest", "q1"), (steps[0].GetProperty("on").GetString(), steps[0].GetProperty("quest").GetString()));
        Assert.Equal("build", run.GetProperty("at").GetString());

        var graph = run.GetProperty("graph");
        Assert.Equal(("q1", "handed", "q1", true, false), (graph.GetProperty("run").GetString(), graph.GetProperty("end").GetString(),
            graph.GetProperty("quest").GetString(), graph.GetProperty("judged").GetBoolean(), graph.GetProperty("unread").GetBoolean()));
        Assert.Equal(["claimed"], graph.GetProperty("entries").EnumerateArray().Select(entry => entry.GetString()));
        Assert.Equal(
            [("claimed", "build", "after"), ("claimed", "lint", "after"), ("build", "both", "after"), ("lint", "both", "after"), ("both", "yes", "after"), ("yes", "handed", "after")],
            graph.GetProperty("wires").EnumerateArray().Select(wire => (wire.GetProperty("from").GetString(), wire.GetProperty("to").GetString(), wire.GetProperty("kind").GetString())));
        Assert.Equal(JsonValueKind.Null, graph.GetProperty("ended").ValueKind);
    }

    /// <summary>
    /// A graph's step that lacks what its place declares cannot start, and says why: an <c>opinion</c> where the place declares no reviewers is
    /// drawn so on its node; a <c>land</c> that integrates where the line declares no integration holds the run before any step.
    /// </summary>
    [Theory]
    [InlineData("opinion")]
    [InlineData("land")]
    public async Task A_bound_step_that_lacks_its_declaration_cannot_start(string kind)
    {
        var (node, method) = kind == "opinion"
            ? ("""{"id":"inspect","kind":"opinion","version":1,"reach":"observed"},""", "merge")
            : ("", "integrate");
        StoredSteps.Add(Home, new WorkflowVersionAdded("release", "Release", "terminal", T.ToString("O"), null, System.Text.Json.Nodes.JsonNode.Parse($$"""
            {"schemaVersion":2,"entries":["write"],"end":"publish",
             "nodes":[{"id":"write","kind":"claimed","version":1,"reach":"observed"},{{node}}
                      {"id":"publish","kind":"land","version":1,"reach":"performed","options":{"method":"{{method}}"} }],
             "wires":[{{(kind == "opinion"
                 ? """{"id":"w>i","kind":"after","from":"write","to":"inspect"},{"id":"i>p","kind":"after","from":"inspect","to":"publish"}"""
                 : """{"id":"w>p","kind":"after","from":"write","to":"publish"}""")}}]}
            """)), kept: []);
        Assert.True(WorkflowRunBindings.Bind(Home, WorkflowRunBindings.Plan(DriverConfig.Empty, Home, "q1", "engine", "aurora", null,
            new WorkflowTaskChoice(null, "release"), [], T)));
        var (module, _) = await UpAsync();

        var run = (await AnswerAsync(module, "WORKFLOW_RUN", new { quest = "q1" })).GetProperty("runs")[0];

        if (kind == "opinion")
        {
            var step = run.GetProperty("steps").EnumerateArray().Single(each => each.GetProperty("step").GetProperty("id").GetString() == "inspect");
            Assert.Equal("cannot-start", step.GetProperty("state").GetString());
            Assert.Contains("declares none", step.GetProperty("words").GetString());
            return;
        }

        // A landing the line cannot carry holds the whole run before any step, saying why.
        Assert.Empty(run.GetProperty("steps").EnumerateArray());
        var problem = run.GetProperty("problem").GetString();
        Assert.False(string.IsNullOrWhiteSpace(problem));
        Assert.DoesNotContain("no workflow is chosen", problem, StringComparison.Ordinal);
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
    }

    /// <summary>
    /// A conversation serves no quest. In the checkout it has no run, and its code with the code's English says why, which is an answer and
    /// not a refusal; a session not recorded reads `no-session` with its id for the page's words. One in
    /// a tree this home opened answers its chat run: keyed by its line, never `chat-checkout`, its checks named and not
    /// yet enforced.
    /// </summary>
    [Fact]
    public async Task A_chat_in_the_checkout_says_so_by_its_code_and_one_in_its_own_tree_answers_its_run()
    {
        var own = Path.Combine(DaorisHome.Entry(Home, HomeEntry.Trees), "aurora", "engine", "s-c2c2c2c2");
        Directory.CreateDirectory(own);
        DriverConfig.Parse("""{"sessionChecks":{"engine":"light"}}""").Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        var (module, _) = await UpAsync(chatTree: own);

        async Task<(string? Code, string? Id, string? Problem)> NoRunAsync(string session)
        {
            var answer = await AnswerAsync(module, "WORKFLOW_RUN", new { session });
            Assert.Empty(answer.GetProperty("runs").EnumerateArray());
            string? Text(string name) => answer.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return (Text("code"), Text("id"), Text("problem"));
        }

        Assert.Equal((WorkflowNoRun.ChatCheckout, (string?)null, WorkflowNoRun.Said(WorkflowNoRun.ChatCheckout)), await NoRunAsync("c1"));
        Assert.Equal((WorkflowNoRun.NoSession, "s9", "No session `s9` is recorded here."), await NoRunAsync("s9"));

        var chat = await AnswerAsync(module, "WORKFLOW_RUN", new { session = "c2" });
        Assert.Equal(JsonValueKind.Null, chat.GetProperty("code").ValueKind);
        var run = Assert.Single(chat.GetProperty("runs").EnumerateArray());
        Assert.Equal(("c2", 0), (run.GetProperty("session").GetString(), run.GetProperty("quests").GetArrayLength()));
        var mark = run.GetProperty("chat");
        // No workflow governs it, so its checks are its repository's rule, which its execution is held to.
        Assert.Equal(("chat-c2", "light", true),
            (mark.GetProperty("run").GetString(), mark.GetProperty("checks").GetString(), mark.GetProperty("enforced").GetBoolean()));

        // Bound to a graph whose `claimed` sets its own checks: those are enforced, and its work is drawn working.
        StoredSteps.Add(Home, new WorkflowVersionAdded("docs", "Docs", "terminal", T.ToString("O"), null, System.Text.Json.Nodes.JsonNode.Parse("""
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed","options":{"checks":"heavy-at-end"}},
                      {"id":"handed","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
             "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
            """)), kept: []);
        Assert.True(WorkflowRunBindings.Bind(Home, WorkflowRunBindings.Plan(DriverConfig.Empty, Home, "chat-c2", "engine", "aurora", null,
            new WorkflowTaskChoice(null, "docs"), [], T)));
        var unit = RunUnits.IdOf("chat-c2");
        new NodeRecordFiles(Home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus> { ["claimed"] = new(unit, "claimed", NodeStates.Active) },
        });
        var bound = Assert.Single((await AnswerAsync(module, "WORKFLOW_RUN", new { session = "c2" })).GetProperty("runs").EnumerateArray());
        Assert.Equal("heavy-at-end", bound.GetProperty("chat").GetProperty("checks").GetString());
        var work = bound.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("step").GetProperty("kind").GetString() == "work");
        Assert.Equal(("working", "active"), (work.GetProperty("state").GetString(), work.GetProperty("detail").GetString()));
    }

    /// <summary>
    /// A conversation's Accept at the node door is its press: the submission and then the keep, so it reserves the conversation first, and a
    /// live one no runner here holds is refused <c>elsewhere</c> with nothing submitted or kept. Seen failing first.
    /// </summary>
    [Fact]
    public async Task A_conversations_accept_at_the_node_door_is_its_press_reserved_first()
    {
        var own = Path.Combine(DaorisHome.Entry(Home, HomeEntry.Trees), "aurora", "engine", "s-c2c2c2c2");
        Directory.CreateDirectory(own);
        var (module, _) = await UpAsync(chatTree: own);

        var answer = await AnswerAsync(module, "WORKFLOW_NODE", new { run = "chat-c2", node = "review", act = "accept" });

        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Equal(ChatPresses.Codes.Elsewhere, answer.GetProperty("press").GetString());
        Assert.Equal(ChatPresses.Elsewhere, answer.GetProperty("message").GetString());
        Assert.False(File.Exists(Path.Combine(NodeRecordFiles.FolderOf(Home), RunUnits.IdOf("chat-c2") + ".json")));
    }

    /// <summary>Run <c>q1</c> bound to v1 of a workflow whose graph holds a go-ahead before its landing, its claim reached and its go-ahead under way.</summary>
    private NodeRecordFiles BoundWithAGoAhead()
    {
        StoredSteps.Add(Home, new WorkflowVersionAdded("docs", "Docs", "terminal", T.ToString("O"), null, System.Text.Json.Nodes.JsonNode.Parse("""
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"go","kind":"go-ahead","version":1,"reach":"pressed","options":{"act":"deploy the release"}},
                      {"id":"handed","kind":"land","version":1,"reach":"performed"}],
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
    /// Each graph run's node answers what the person may say of it now, <c>acts</c>, and the word kept on it and not
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
    /// <c>WORKFLOW_NODE</c>, the person's skip or stop of a run's node. Before the driver is up it is the cold-start sentence; a
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

    /// <summary>
    /// <c>WORKFLOW_RUN</c> says which routes were taken: a decision's outcome on the graph, the nodes its outcome left no route
    /// to, a <c>when</c> wire's outcome, and a <c>back</c> wire apart from the wires; a review waiting on the person offers their changes, and
    /// <c>WORKFLOW_NODE</c> keeps them only with words.
    /// </summary>
    [Fact]
    public async Task A_graph_run_says_its_routes_not_taken_and_a_reviews_changes_are_kept_with_words()
    {
        StoredSteps.Add(Home, new WorkflowVersionAdded("docs", "Docs", "terminal", T.ToString("O"), null, System.Text.Json.Nodes.JsonNode.Parse("""
            {"schemaVersion":2,"entries":["work"],"end":"release",
             "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"find","kind":"local-release/find","version":1,"reach":"performed","outcomes":["release","none"]},
                      {"id":"review","kind":"review","version":1,"reach":"pressed"},
                      {"id":"handed","kind":"land","version":1,"reach":"performed"},
                      {"id":"release","kind":"local-release/release","version":1,"reach":"performed"}],
             "wires":[{"id":"a","kind":"after","from":"work","to":"find"},{"id":"b","kind":"after","from":"find","to":"review"},
                      {"id":"c","kind":"after","from":"review","to":"handed"},{"id":"d","kind":"when","from":"find","to":"release","outcome":"release"},
                      {"id":"e","kind":"back","from":"review","to":"work","on":"rejected"}]}
            """)), kept: []);
        var binding = WorkflowRunBindings.Plan(DriverConfig.Empty, Home, "q1", "engine", "aurora", null, new WorkflowTaskChoice(null, "docs"), [], T);
        Assert.True(WorkflowRunBindings.Bind(Home, binding));
        var unit = RunUnits.IdOf("q1");
        NodeStatus Reached(string node, string? outcome = null) => new(unit, node, NodeStates.Reached)
        {
            Attempt = 1, Acceptance = new NodeAcceptance(unit, "moment", binding.Digest!, node, 1, 1) { Subject = "quest:q1", Outcome = outcome },
        };
        var files = new NodeRecordFiles(Home, () => true);
        files.Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["work"] = Reached("work"), ["find"] = Reached("find", "none"),
                ["review"] = new(unit, "review", NodeStates.Active) { Attempt = 1, Binding = new NodeAcceptance(unit, null, binding.Digest!, "review", 1, 1) },
            },
        });
        var (module, _) = await UpAsync();

        var run = Assert.Single((await AnswerAsync(module, "WORKFLOW_RUN", new { quest = "q1" })).GetProperty("runs").EnumerateArray());

        var graph = run.GetProperty("graph");
        Assert.Equal(["release"], graph.GetProperty("notTaken").EnumerateArray().Select(node => node.GetString()));
        Assert.Equal("none", graph.GetProperty("outcomes").GetProperty("find").GetString());
        var wires = graph.GetProperty("wires").EnumerateArray().ToList();
        Assert.Equal(["after", "after", "after", "when"], wires.Select(wire => wire.GetProperty("kind").GetString()));
        Assert.Equal("release", wires[3].GetProperty("outcome").GetString());
        Assert.False(wires[0].TryGetProperty("outcome", out _));
        var back = Assert.Single(graph.GetProperty("backs").EnumerateArray());
        Assert.Equal(("review", "work", "rejected"), (back.GetProperty("from").GetString(), back.GetProperty("to").GetString(), back.GetProperty("on").GetString()));
        var review = run.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("step").GetProperty("id").GetString() == "review");
        Assert.Equal(["changes", "stop"], review.GetProperty("acts").EnumerateArray().Select(act => act.GetString()));

        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Desktop, out _);
        Assert.NotNull(lease);
        var unworded = await AnswerAsync(module, "WORKFLOW_NODE", new { run = "q1", node = "review", act = "changes" });
        Assert.Equal((false, "no-words"), (unworded.GetProperty("done").GetBoolean(), unworded.GetProperty("refused").GetString()));
        Assert.True(ReviewRecords.Keep(Home, new ReviewRecord("q1", "review", 1, ReviewKinds.Answer, "feedc0de0123456789", T)));
        var moved = await AnswerAsync(module, "WORKFLOW_NODE", new { run = "q1", node = "review", act = "accept", attempt = 1, revision = "0badc0de" });
        Assert.Equal((false, "moved", "feedc0de0123456789"),
            (moved.GetProperty("done").GetBoolean(), moved.GetProperty("refused").GetString(), moved.GetProperty("revision").GetString()));
        var named = await AnswerAsync(module, "WORKFLOW_NODE", new { run = "q1", node = "review", act = "accept", revision = "feedc0de0123456789", @unchecked = true });
        Assert.Equal(UncheckedAccepts.NoneUnchecked, named.GetProperty("refused").GetString());
        var notReview = await AnswerAsync(module, "WORKFLOW_NODE", new { run = "q1", node = "handed", act = "stop", whole = true });
        Assert.Equal((false, RunNodeWords.NotReview), (notReview.GetProperty("done").GetBoolean(), notReview.GetProperty("refused").GetString()));
        var graphed = new GraphRuns(files).Find("q1")!.Version.Graph!;
        Assert.Equal((true, false), (AcceptWaits.Waits(graphed, "review"), AcceptWaits.Waits(graphed, "handed")));
        var kept =await AnswerAsync(module, "WORKFLOW_NODE", new { run = "q1", node = "review", act = "changes", words = "Shorter, please.", part = "q1" });
        Assert.True(kept.GetProperty("done").GetBoolean());
        var word = Assert.Single(files.Read(unit)!.Pending);
        Assert.Equal(("review", NodeWordActs.Changes, "Shorter, please.", "q1"), (word.Node, word.Act, word.Words, word.Part));
    }

    [Fact]
    public async Task A_refusal_names_what_to_name_and_a_run_waits_for_the_driver()
    {
        var neither = await RefusalAsync(Module(), "WORKFLOW_RUN", new { });
        var two = await RefusalAsync(Module(), "WORKFLOW_RUN", new { quest = "q1", ask = "a1" });
        var early = await RefusalAsync(Module(), "WORKFLOW_RUN", new { quest = "q1" });

        Assert.Contains("a run is read for a `session`, a `quest`, an `ask` or a repository's `line` — name one of them.", neither);
        Assert.Contains("name one of them", two);
        Assert.Contains("still coming up", early);
    }

    /// <summary>
    /// A repository's line answers its verification runs, each with the commit it judged, the branch it is asked of and its
    /// verdict from its record; a line with none answers no run and the sentence that says how one is asked.
    /// </summary>
    [Fact]
    public async Task A_line_answers_its_verification_runs_with_their_verdicts()
    {
        Directory.CreateDirectory(WorkflowStore.FolderOf(Home));
        File.WriteAllText(WorkflowStore.PathOf(Home, "verify"), """
            {"id":"verify","name":"Verify","pipeline":"verification","versions":[{"version":1,"at":"2026-10-31T09:00:00Z","door":"terminal","schema":2,
             "graph":{"schemaVersion":2,"entries":["verify"],"end":"verify","nodes":[{"id":"verify","kind":"check","version":1,"reach":"performed"}],"wires":[]}}]}
            """);
        var digest = new WorkflowFiles(Home).Load("verify")!.Read.Versions.Single().Digest!;
        const string run = "v-0123456789abcdef";
        VerifyRuns.Bind(Home, new VerifyRun(run, "engine", "aurora", "checkout", "verify", 1, digest, Commit, VerifyRuns.Person, T) { Branch = "feature/x" });
        var unit = RunUnits.IdOf(run);
        new NodeRecordFiles(Home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus> { ["verify"] = new(unit, "verify", NodeStates.Reached) { Attempt = 1, By = NodeSource.Gates } },
        });
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_RUN", new { line = "engine" });
        var line = answer.GetProperty("runs")[0].GetProperty("line");
        Assert.Equal((run, Commit, "feature/x", "verified"),
            (line.GetProperty("run").GetString(), line.GetProperty("commit").GetString(), line.GetProperty("branch").GetString(), line.GetProperty("verdict").GetString()));

        var none = await AnswerAsync(module, "WORKFLOW_RUN", new { line = "nowhere" });
        Assert.Equal(0, none.GetProperty("runs").GetArrayLength());
        Assert.Contains("`daoris gate verify nowhere`", none.GetProperty("problem").GetString());
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
    private async Task<(DriverModule Module, DriverLoop Loop)> UpAsync(string? tree = null, bool askUnread = false, string? chatTree = null, bool requests = false)
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(new StandInService(tree, askUnread, chatTree, requests)))));
        return (module, loop);
    }

    /// <summary>
    /// A stand-in service: `engine` in the `aurora` workspace, ask `a1`'s one quest done there by session `s1`, and a chat. Every
    /// other door answers an empty list. With <paramref name="chatTree"/>, a second chat, `c2`, works in that tree.
    /// </summary>
    private sealed class StandInService(string? tree = null, bool askUnread = false, string? chatTree = null, bool requests = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.PathAndQuery switch
            {
                "/api/registry" => """[{"repository":"engine","workspace":"aurora"}]""",
                // A quest session s1 published to another repository, done with its note, and a helper it asked.
                "/api/quests?includeClosed=true" when requests =>
                    """
                    [{"id":"q1","from":"ask #a1","to":"engine","title":"Quest q1","body":"","status":"Done","updated":"2026-10-09T09:30:00Z"},
                     {"id":"q5","from":"engine","to":"docs","title":"Quest q5","body":"","status":"Done","note":"the shape is in","publishedBy":"s1","updated":"2026-10-09T09:10:00Z"}]
                    """,
                "/api/sessions?includeClosed=true" when requests => """
                    [{"id":"s1","repository":"engine","state":"completed","quest":"q1","door":"claude-code",
                      "created":"2026-10-09T09:00:00Z","updated":"2026-10-09T09:20:00Z","evidence":"commits landed:\nabc1234 the change"},
                     {"id":"h1","repository":"engine","state":"working","serves":"s1","door":"codex-acp","created":"2026-10-09T09:05:00Z"}]
                    """,
                "/api/quests?includeClosed=true" =>
                    """[{"id":"q1","from":"ask #a1","to":"engine","title":"Quest q1","body":"","status":"Done","updated":"2026-10-09T09:30:00Z"}]""",
                "/api/sessions?includeClosed=true" => """
                    [{"id":"s1","repository":"engine","state":"completed","quest":"q1","door":"claude-code",
                      "created":"2026-10-09T09:00:00Z","updated":"2026-10-09T09:20:00Z","evidence":"commits landed:\nabc1234 the change"},
                     {"id":"c1","repository":"engine","state":"working","kind":"chat","created":"2026-10-09T08:00:00Z"}]
                    """,
                "/api/asks/a1" => """{"id":"a1","workspace":"aurora","sentence":"Fix it","state":"Open","tier":"none","quests":["q1"]}""",
                _ => "[]",
            };
            if (chatTree is not null && request.RequestUri!.PathAndQuery == "/api/sessions?includeClosed=true")
            {
                body = body.TrimEnd().TrimEnd(']')
                    + "," + JsonSerializer.Serialize(new { id = "c2", repository = "engine", state = "working", kind = "chat", tree = chatTree }) + "]";
            }

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
