using System.Text.Json;
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
        using var process = System.Diagnostics.Process.Start(info)!;
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
        var folder = Path.Combine(Home, "plugins", id);
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
