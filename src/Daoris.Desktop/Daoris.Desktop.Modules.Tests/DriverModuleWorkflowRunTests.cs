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
    private static readonly DateTimeOffset T = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

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
    private async Task<(DriverModule Module, DriverLoop Loop)> UpAsync()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInService())));
        return (module, loop);
    }

    /// <summary>
    /// A stand-in service: `engine` in the `aurora` workspace, ask `a1`'s one quest done there by session `s1`, and a chat. Every
    /// other door answers an empty list.
    /// </summary>
    private sealed class StandInService : HttpMessageHandler
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
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
