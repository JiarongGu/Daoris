using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1c2 (D157's WORKFLOW1c2 note; the workflow design §5.2, §7, D50): <c>daoris-driver workflow run --session|--quest|--ask
/// &lt;id&gt;</c>, the terminal's twin of the session's <i>Workflow</i> view. It prints what <see cref="WorkflowRunReader"/> reads,
/// one line per step with its state and what that state says, the step the run stands at marked. Reads only.
/// </summary>
/// <remarks>
/// In-process, on stand-in records, as the reader's module test stands them in: the service a stand-in reached through the real
/// client, the home a scratch folder written by the driver's own writers (the driver file, a plugin, the landing record).
/// </remarks>
public sealed class WorkflowRunCommandTests : IDisposable
{
    private static readonly DateTimeOffset T = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-workflow-run-" + Guid.NewGuid().ToString("N")[..8]);

    public WorkflowRunCommandTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    private static string Golden(string file) =>
        File.ReadAllText(OpinionCommandTests.GoldenPath("workflow-run", file)).ReplaceLineEndings("\n");

    // ——— the words read

    public static TheoryData<string, string?> Lines() => new()
    {
        { "run --session s1", "session=s1 quest= ask=" },
        { "run --quest #q1", "session= quest=#q1 ask=" },
        { "run --ask a1", "session= quest= ask=a1" },
        { "run", null },
        { "run --session", null },
        { "run --session  ", null },
        { "run --session s1 --quest q1", null },
        { "run --repository engine", null },
        { "run --session s1 more", null },
        { "show", null },
        { "", null },
    };

    [Theory]
    [MemberData(nameof(Lines))]
    public void The_words_name_one_session_quest_or_ask(string line, string? read)
    {
        var ask = WorkflowRunCommand.Read(line.Length == 0 ? [] : line.Split(' '), out var problem);

        if (read is null)
        {
            Assert.Null(ask);
            Assert.False(string.IsNullOrWhiteSpace(problem));
            return;
        }

        Assert.Null(problem);
        Assert.Equal(read, $"session={ask!.Session} quest={ask.Quest} ask={ask.Ask}");
    }

    // ——— the usage

    [Fact]
    public void The_verbs_usage_is_its_golden_text_and_the_hosts_usage_names_it()
    {
        Assert.Equal(Golden("usage.txt").TrimEnd('\n'), WorkflowRunCommand.Usage.ReplaceLineEndings("\n"));

        var usage = DriverCommand.Usage.ReplaceLineEndings("\n");
        Assert.Contains("\n  workflow run --session <id> | --quest <id> | --ask <id>\n", usage);

        var host = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host");
        var program = File.ReadAllText(Path.Combine(host, "Program.cs"));
        Assert.Contains("if (args is [\"workflow\", .. var workflowArgs])", program);
        // A read that writes nothing is routed before the machine log opens, whose open prunes, as `trace` is.
        Assert.True(
            program.IndexOf("args is [\"workflow\"", StringComparison.Ordinal) < program.IndexOf("MachineLog.Open(", StringComparison.Ordinal),
            "the workflow run is routed before the machine log opens");
        var console = File.ReadAllText(Path.Combine(host, "WorkflowConsole.cs"));
        Assert.Contains("WorkflowRunCommand.Read(args, out var problem)", console);
        Assert.Contains("Console.Error.WriteLine(WorkflowRunCommand.Usage);", console);
        Assert.Contains("WorkflowRunCommand.SourcesAsync(", console);
        Assert.Contains("WorkflowRunCommand.RunAsync(", console);
    }

    // ——— a run, printed

    /// <summary>
    /// A chain's work done in a repository with a second opinion, a look in <c>local</c> and a branch a plugin pushes: the work
    /// finished, the opinion settled, the look reviewed and the landing made, as the landing record keeps them, and the pull
    /// request open, where the run stands.
    /// </summary>
    [Fact]
    public async Task A_quest_s_run_prints_each_step_its_state_and_what_it_says_the_step_it_stands_at_marked()
    {
        var (said, exit) = await RunAsync("--quest", "#q1");

        Assert.Equal(0, exit);
        Assert.Equal(Golden("run.txt"), said);
    }

    /// <summary>A session's run is its quest's, with <i>this session</i> on the steps its record is; an ask's is each run it reaches.</summary>
    [Fact]
    public async Task A_session_s_run_is_its_quest_s_with_this_session_marked_and_an_ask_s_the_same()
    {
        var (byQuest, _) = await RunAsync("--quest", "q1");
        var (bySession, sessionExit) = await RunAsync("--session", "s1");
        var (byAsk, askExit) = await RunAsync("--ask", "a1");

        Assert.Equal((0, 0), (sessionExit, askExit));
        Assert.Equal(byQuest, byAsk);
        var marked = bySession.Split('\n').Where(line => line.EndsWith(" · this session", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(marked);
        Assert.Equal(byQuest, bySession.Replace(" · this session", "", StringComparison.Ordinal));
    }

    /// <summary>A conversation serves no quest: no run, and the driver's sentence says why (WORKFLOW1c). Nothing names a run, exit 1.</summary>
    [Fact]
    public async Task A_chat_has_no_run_and_says_the_drivers_sentence()
    {
        var (said, exit) = await RunAsync("--session", "c1");

        Assert.Equal(1, exit);
        Assert.Equal("workflow: session `c1` serves no quest, so no workflow runs for it.\n", said);
    }

    [Fact]
    public async Task A_quest_nothing_here_names_is_said_and_exit_1()
    {
        var (said, exit) = await RunAsync("--quest", "q9");

        Assert.Equal(1, exit);
        Assert.Equal("workflow: no quest `#q9` is here.\n", said);
    }

    /// <summary>A service that does not answer is a tool error, exit 2, in the reader's sentence.</summary>
    [Fact]
    public async Task A_service_that_does_not_answer_is_exit_2()
    {
        Setup();
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Down()));
        var sources = new WorkflowRunSources(service, _home, DriverConfig.Empty, [], _ => null);
        using var output = new StringWriter();

        var exit = await WorkflowRunCommand.RunAsync(new WorkflowRunAsk(Quest: "q1"), sources, output);

        Assert.Equal(2, exit);
        Assert.StartsWith("workflow: the service did not answer, so no run could be read: ", output.ToString());
    }

    // ——— the words

    /// <summary>
    /// Every detail each kind of step takes has its own words, as the page's has (the page's <c>runSaid.ts</c>): none is said as
    /// recorded. A detail a newer driver names is said as recorded, never dropped.
    /// </summary>
    [Fact]
    public void Every_detail_a_kind_takes_has_words_and_another_is_said_as_recorded()
    {
        foreach (var (kind, details) in WorkflowRunWords.Details)
        {
            foreach (var detail in details)
            {
                var said = WorkflowRunWords.Said(Step(kind, WorkflowRunStates.Working, detail));
                Assert.False(string.IsNullOrWhiteSpace(said), $"{kind}.{detail}");
                Assert.DoesNotContain("as recorded", said!, StringComparison.Ordinal);
            }
        }

        Assert.Equal("Shown as recorded: working, later.", WorkflowRunWords.Said(Step(WorkflowKinds.Work, WorkflowRunStates.Working, "later")));
        Assert.Equal("Shown as recorded: paused, queued.", WorkflowRunWords.Said(Step(WorkflowKinds.Work, "paused", WorkflowRunDetails.Queued)));
        Assert.Null(WorkflowRunWords.Said(Step(WorkflowKinds.Landing, WorkflowRunStates.NotReached, "")));
        Assert.Equal("paused", WorkflowRunWords.State("paused"));
    }

    /// <summary>The details the run's derivation names are the ones the words hold, the opinion's being its gate's own states.</summary>
    [Fact]
    public void The_words_hold_every_detail_the_derivation_names()
    {
        var named = typeof(WorkflowRunDetails).GetFields().Concat(typeof(OpinionGateStates).GetFields())
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetValue(null)!)
            .ToHashSet(StringComparer.Ordinal);
        var worded = WorkflowRunWords.Details.SelectMany(each => each.Value).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(named.Except(worded));
    }

    [Fact]
    public void A_steps_words_carry_its_facts()
    {
        Assert.Equal(
            "Waits for your go-ahead #3: write the configuration to dev",
            WorkflowRunWords.Said(Step(WorkflowKinds.Work, WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.GoAhead) with
            {
                GoAhead = 3, Words = "write the configuration to dev",
            }));
        Assert.Equal(
            "The agent finished: 1 quest here is done.",
            WorkflowRunWords.Said(Step(WorkflowKinds.Work, WorkflowRunStates.Done, WorkflowRunDetails.Finished) with { Count = 1 }));
        Assert.Equal(
            "2 findings are disputed: musts the working session did not fix and no recheck withdrew.",
            WorkflowRunWords.Said(Step(WorkflowKinds.Opinion, WorkflowRunStates.WaitingOnYou, OpinionGateStates.Disputed) with { Count = 2 }));
        Assert.Equal(
            "No second opinion could be had: every listed reviewer of another maker is cooling. The rule requires one, so the work waits for you.",
            WorkflowRunWords.Said(Step(WorkflowKinds.Opinion, WorkflowRunStates.WaitingOnYou, OpinionGateStates.Unavailable) with { Code = "cooling" }));
        Assert.Equal(
            "Waits for your look in `local`: shown at `01234567`.",
            WorkflowRunWords.Said(Step(WorkflowKinds.Look, WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Shown) with
            {
                Environment = "local", Commit = Commit,
            }));
        Assert.Equal(
            "Cannot land automatically: its tree holds uncommitted work.",
            WorkflowRunWords.Said(Step(WorkflowKinds.Landing, WorkflowRunStates.CannotStart, WorkflowRunDetails.Refused) with { Code = "uncommitted" }));
        Assert.Equal(
            "Waits for you to merge it on the platform; `example.pull-request` last answered no state for it.",
            WorkflowRunWords.Said(Step(WorkflowKinds.PullRequest, WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Merging) with
            {
                Code = PullRequestStates.Unknown, Plugin = "example.pull-request",
            }));
    }

    /// <summary>
    /// Where the second opinion's gate holds the work on the person, its presses are the terminal's (XAGENT1f): the lines under the
    /// step name them for its session, as the page says them until the review draws them.
    /// </summary>
    [Fact]
    public void An_opinion_waiting_on_you_names_its_terminal_presses()
    {
        var current = WorkflowCurrent.Derive(DriverConfig.Parse("""{"opinions":{"engine":{"reviewers":["codex-acp"]}}}"""), "engine", "aurora", []);
        var opinion = Step(WorkflowKinds.Opinion, WorkflowRunStates.WaitingOnYou, OpinionGateStates.CommitsSince) with { Session = "s1", Count = 2 };
        var run = new WorkflowRun("engine", "aurora", current, ["q1"], [opinion]);

        var said = WorkflowRunWords.Say([run], session: null);

        Assert.Contains("`daoris-driver opinion show s1` lists what is unsettled; `daoris-driver opinion anyway s1 \"…\"` goes on without it.", said);
    }

    // ——— stand-ins

    private static WorkflowRunStep Step(string kind, string state, string detail) =>
        new(new WorkflowStep(kind, kind, WorkflowParticipation.Agent, WorkflowExecutor.Agent, null,
            new WorkflowSource(null, LandingSource.Default), [], WorkflowRuntime.Built, null), state, detail);

    /// <summary>
    /// The home: <c>engine</c>'s rules (a second opinion, a look in <c>local</c>, and a branch <c>example.pull-request</c> pushes),
    /// that plugin switched on, and the landing of session <c>s1</c>'s work on <c>work/q1</c> with the opinion and the look it let
    /// go and its pull request open.
    /// </summary>
    private string Setup()
    {
        var path = Path.Combine(_home, "driver.json");
        File.WriteAllText(path, """
            {
              "landings": { "engine": { "form": "branch", "pattern": "work/{quest}", "plugin": "example.pull-request" } },
              "opinions": { "engine": { "reviewers": ["codex-acp"] } },
              "reviews": { "engine": { "required": true, "environments": [
                { "name": "local", "kind": "local", "procedure": "README.md", "address": "http://localhost:4200" } ] } }
            }
            """);

        var folder = Path.Combine(_home, "plugins", "example.pull-request");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), JsonSerializer.Serialize(new
        {
            id = "example.pull-request",
            hooks = new { command = new[] { "node", "${plugin}/hook.mjs" }, points = new[] { "work/land", "work/state" } },
        }));
        PluginState.Enable(_home, "example.pull-request");

        new LandedBranches(_home).Record(new LandedBranch("engine", "aurora", "work/q1", "main", Commit, "s1", "q1", "Quest q1", T.AddMinutes(40))
        {
            Plugin = "example.pull-request",
            Pushed = true,
            PullRequest = "https://example.test/pull/7",
            AcceptedBy = AcceptedBy.Person,
            Review = new LandingReview(ReviewVerdicts.Reviewed, "local", "q1") { Commit = Commit, At = T.AddMinutes(35) },
            Opinion = new LandingOpinion(OpinionGateStates.Settled) { Reviewer = "another maker's agent", Tip = Commit },
            PullRequestState = new PullRequestState(PullRequestStates.Open) { AskedAt = T.AddMinutes(45) },
        });
        return path;
    }

    /// <summary>The verb's run over the stand-ins, through the sources the host builds: what it printed, and its exit.</summary>
    private async Task<(string Said, int Exit)> RunAsync(params string[] words)
    {
        var path = Setup();
        var asked = WorkflowRunCommand.Read(["run", .. words], out var problem) ?? throw new InvalidOperationException(problem);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new StandInService()));
        var sources = await WorkflowRunCommand.SourcesAsync(service, path);
        using var output = new StringWriter { NewLine = "\n" };

        var exit = await WorkflowRunCommand.RunAsync(asked, sources, output);
        return (output.ToString(), exit);
    }

    /// <summary>
    /// A stand-in service: <c>engine</c> in the <c>aurora</c> workspace, ask <c>a1</c>'s one quest done there by session <c>s1</c>,
    /// and a chat. Every other door answers an empty list.
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
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    /// <summary>A service that answers nothing a reader can use.</summary>
    private sealed class Down : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("connection refused");
    }
}
