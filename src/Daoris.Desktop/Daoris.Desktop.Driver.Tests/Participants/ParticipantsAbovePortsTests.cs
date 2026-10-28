using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xii part 2, the Participants cuts of the three families above it: the seams they add. The looks' facts held through the
/// facets the review's records implement; a loop's reserved and bound starts held through the facets the planner's and the workflows'
/// records implement; the tree rule spelled once by the workplace; the helper's and the partner's verbs reading the terminal through
/// facets; and the operations Work orders and Workflows install, which the participants resolve only where reached. What the cuts keep
/// is <c>ParticipantsWorkBoundaryTests</c>'.
/// </summary>
public sealed class ParticipantsAbovePortsTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "participants-above-ports", Guid.NewGuid().ToString("N")[..8]);

    public ParticipantsAbovePortsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    [Fact]
    public void The_review_s_records_answer_the_facets_the_looks_keep_them_by()
    {
        var act = new OpinionAct(null!, "key", "claim", [], "landing", Person: false, null!);
        IOpinionFact fact = new OpinionFact(
            new Dictionary<string, OpinionSit> { ["q1"] = new("sits", new SittingFact("opinion")), ["q2"] = new("sits", new SittingFact("opinion")) }, null, act);

        Assert.Equal(["q1", "q2"], fact.SatQuests);
        Assert.Same(act, fact.Act);

        var taken = fact.WithoutAct();

        Assert.Null(taken.Act);
        Assert.Equal(["q1", "q2"], taken.SatQuests);
        Assert.Same(act, fact.Act);
    }

    [Fact]
    public void The_planner_s_start_and_the_workflows_bind_answer_the_facets_a_loop_holds_them_by()
    {
        var run = new RunningSessions();
        var bind = new WorkflowBind(null!, WorkflowBindCame.Bound, "q1", null, null);
        var start = new Consideration(null!, StartVerdict.Start, "ready");
        var begun = new StartBegun("q1", null, "engine", null, "claude-code-acp", start);

        run.Bound(bind);

        Assert.Same(start, begun.Start);
        Assert.Same(bind, Assert.Single(run.TakeBound()));
    }

    [Theory]
    [InlineData(@"C:\trees\one", "c:/trees/ONE/", true)]
    [InlineData("/trees/one", "/trees/one/", true)]
    [InlineData("/trees/one", "/trees/two", false)]
    [InlineData(null, "/trees/one", false)]
    [InlineData("", "", false)]
    [InlineData(null, null, false)]
    public void Two_tree_paths_are_one_tree_by_the_workplace_s_rule_and_the_planner_answers_through_it(string? a, string? b, bool same)
    {
        Assert.Equal(same, TreeNames.Same(a, b));
        Assert.Equal(same, Planner.SameTree(a, b));
    }

    [Fact]
    public void The_helper_s_and_the_partner_s_verbs_make_the_ask_the_sessions_verb_hands_them_and_read_the_terminal_through_the_facets()
    {
        var made = new List<string>();
        static string Shape(string verb, IReadOnlyList<string> ids, string? text, string? agent, IReadOnlyList<string> paths) =>
            $"{verb}|{string.Join(',', ids)}|{text}|{agent}|{string.Join(',', paths)}";
        string Say(string verb, IReadOnlyList<string> ids, string? text, string? agent, IReadOnlyList<string> paths)
        {
            made.Add(Shape(verb, ids, text, agent, paths));
            return verb;
        }

        Assert.Equal("helper-approve", SessionsHelperCommand.Read(["helper", "--approve", "a1"], out _, Say));
        Assert.Equal("helper", SessionsHelperCommand.Read(["helper", "s1", " Why? ", "--agent", " codex-acp "], out _, Say));
        Assert.Equal("partner-stop", SessionsPartnerCommand.Read(["partner", "#p1", "--stop"], out _, Say));
        Assert.Equal("partner", SessionsPartnerCommand.Read(["partner", "s1", "Write it", "--paths", "a.cs", "b.cs", "--agent", "codex-acp"], out _, Say));
        Assert.Null(SessionsHelperCommand.Read(["helper", "s1"], out var problem, Say));
        Assert.Contains("takes one session's id", problem);

        Assert.Equal(["helper-approve|a1|||", "helper|s1|Why?|codex-acp|", "partner-stop|p1|||", "partner|s1|Write it|codex-acp|a.cs,b.cs"], made);

        var ask = SessionsCommand.Read(["partner", "s1", "Write it", "--paths", "a.cs", "--agent", "codex-acp"], out _)!;
        Assert.IsAssignableFrom<IParticipantsAsk>(ask);
        Assert.Equal("partner|s1|Write it|codex-acp|a.cs", Shape(ask.Verb, ask.Ids, ask.Text, ask.Agent, ask.Paths));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Unanswered()));
        Assert.IsAssignableFrom<IParticipantsWorld>(new SessionsWorld(service, _home, DriverConfig.Empty, SessionWire.Pipe, null));
    }

    [Fact]
    public async Task A_helper_s_ask_is_written_through_the_facets_alone()
    {
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Unanswered()));
        var output = new StringWriter();

        var code = await SessionsHelperCommand.RunAsync(new World(service, _home), new Ask("helper", ["s1"], new string('a', SessionsHelperCommand.MaxQuestion + 1), null, []), output);

        Assert.Equal(1, code);
        Assert.Contains("a question is at most", output.ToString());
    }

    private sealed record Ask(string Verb, IReadOnlyList<string> Ids, string? Text, string? Agent, IReadOnlyList<string> Paths) : IParticipantsAsk;

    private sealed record World(ServiceClient Service, string Home) : IParticipantsWorld
    {
        public Func<string, CancellationToken, Task<PieceBase>> PieceBase { get; init; } = (_, _) => Task.FromResult(new PieceBase(null, []));
    }

    private sealed class Unanswered : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("the stand-in answers nothing");
    }

    [Fact]
    public void The_installed_work_orders_operation_is_the_connector_s_list_a_partner_is_denied_but_for_what_it_is_offered()
    {
        Assert.Same(BrainstormPermissions.ConnectorTools, ParticipantsWorkOrders.Current.ConnectorTools);
        Assert.Equal(
            BrainstormPermissions.ConnectorTools.Where(tool => !PartnerPermissions.Offered.Contains(tool)),
            PartnerPermissions.Acting);
    }

    [Fact]
    public void The_installed_workflows_operations_do_what_the_chat_run_s_callers_did()
    {
        var operations = ParticipantsWorkflows.Current;
        var config = DriverConfig.Parse("""{"sessionChecks":{"engine":"light"}}""");
        var plugins = WorkplacePlugins.Current.Load(_home);
        var at = new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

        Assert.False(operations.RunBound(_home, "chat-e1a2b3c4"));

        var bound = operations.Bind(config, _home, "chat-e1a2b3c4", "engine", "aurora", plugins, at);

        Assert.True(bound.Wrote);
        Assert.Null(bound.Failed);
        Assert.Null(bound.Told());
        Assert.True(operations.RunBound(_home, "chat-e1a2b3c4"));
        Assert.True(WorkflowRunBindings.Read(_home, "chat-e1a2b3c4")!.IsCurrent);
        Assert.False(bound.Process(_home, config, "engine", "aurora", "chat-e1a2b3c4").Named);
        Assert.False(operations.Bind(config, _home, "chat-e1a2b3c4", "engine", "aurora", plugins, at).Wrote);
        Assert.Contains("could not be bound", bound.FailedLine("no room").Note);
        Assert.Equal(
            WorkflowProcesses.Read(_home, config, "engine", "aurora", "chat-e1a2b3c4").Name,
            operations.ReadProcess(_home, config, "engine", "aurora", "chat-e1a2b3c4").Name);

        var records = Trace.ParseSessions(JsonSerializer.Serialize(new[] { new { id = "e1a2b3c4", repository = "engine", state = "running", kind = "chat", tree = "/t" } }));
        Assert.Equal(
            ChatRuns.ChatOf(records[0], records, _ => true),
            operations.ChatOf(records[0], records, _ => true));
        Assert.Equal("chat-e1a2b3c4", operations.ChatOf(records[0], records, _ => true));
        Assert.Null(operations.ChatOf(records[0], records, _ => false));
    }

    [Fact]
    public void A_chat_run_s_process_is_the_workplace_s_facet_that_a_named_opinion_rule_answers_findings_through()
    {
        IWorkplaceProcess named = WorkflowProcesses.Bound(_home, DriverConfig.Parse("""{"opinions":{"engine":{"reviewers":["codex-acp"]}}}"""), "engine", "aurora", "chat-f1a2b3c4", null);
        IWorkplaceProcess none = WorkflowProcesses.Bound(_home, DriverConfig.Empty, "engine", "aurora", "chat-f1a2b3c4", null);

        Assert.True(ChatRunBinds.AnswersFindings(named));
        Assert.False(ChatRunBinds.AnswersFindings(none));
    }
}
