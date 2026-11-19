using System.Text.Json;

using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's step whose manifest declares a goal, performed by an agent: its attempt names its plugin and is sent to the step agents, never
/// the plugin's process; its session is handed the plugin's tools and <c>step_answer</c> alone; the answer is the step's completion under the
/// plugin, and a session that ends unanswered, or one a restart left behind, fails it <c>no-answer</c>. Unwired, it is sent nowhere.
/// </summary>
public sealed class StepAgentPerformTests : IDisposable
{
    private const string Unit = "run:v-0123456789abcdef";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";
    private const string Plugin = "acme-tickets";

    private static readonly NodeDeclaration Step = new("open", $"{Plugin}/open", 1, NodeReach.Performed)
    {
        Goal = AuthoredText.Of("Open the ticket for the pushed branch."),
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
        Evidence = "acme-tickets/receipt@1",
        Outcomes = ["opened", "found"],
    };

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "step-agent", Guid.NewGuid().ToString("N")[..8]);
    private readonly Sessions _sessions;
    private readonly Plugins _plugins = new();
    private DateTimeOffset _now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    public StepAgentPerformTests() => _sessions = new Sessions(_home);

    public void Dispose() => GitSeed.Discard(_home);

    /// <summary>
    /// The loop's sessions as the port sees them: each run ends as <see cref="Ends"/> says in turn, else as one that ran; one that ran
    /// answers as <see cref="Answer"/> says, or not at all.
    /// </summary>
    private sealed class Sessions(string home) : IStepSessions
    {
        public List<(StepAgentAsk Ask, ServerEntry Tools)> Ran { get; } = [];

        /// <summary>The account each run was handed as refused before it, in the order run.</summary>
        public List<string?> MovedOff { get; } = [];

        public Queue<StepSessionEnd> Ends { get; } = new();

        public string? Answer { get; set; }

        public List<string> Stopped { get; } = [];

        public Task<StepSessionEnd> RunAsync(StepAgentAsk ask, Func<string, ServerEntry> tools, string? movedOff, CancellationToken ct)
        {
            Ran.Add((ask, tools("s-step")));
            MovedOff.Add(movedOff);
            var end = Ends.Count > 0 ? Ends.Dequeue() : StepSessionEnd.None;
            if (end == StepSessionEnd.None && Answer is { } answer) Assert.Null(StepAnswer.Answer(home, ask, JsonDocument.Parse(answer).RootElement.Clone()));
            return Task.FromResult(end);
        }

        public void Stop(string invocation) => Stopped.Add(invocation);
    }

    /// <summary>The plugins' processes: what is sent to them, which an agent's step never is.</summary>
    private sealed class Plugins : IWorkPerformPort
    {
        public List<string> Sent { get; } = [];

        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
        {
            Sent.Add(plugin);
            return Task.FromResult(1);
        }

        public List<string> Asked { get; } = [];

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct)
        {
            Asked.Add(call.Invocation);
            return Task.FromResult((new WorkReconcileAnswer(ReconcileStates.Done, new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence("acme-tickets/receipt@1:r", null, null, "found")) { Decision = "found" }), 1));
        }

        public int? GenerationOf(string plugin) => 1;

        public void Refused(string plugin, string code)
        {
        }
    }

    private NodeRecordFiles Files => new(_home, () => true);

    private StepAgents Agents() => new(_home, _sessions, (unit, attempt, node, _) => Task.FromResult<StepAgentAsk?>(new StepAgentAsk(
        attempt.Call.Invocation, unit, node.Id, attempt.Attempt, Plugin, "open", node.Goal!.AsWritten, "engine", "games", "feature/x",
        "https://tickets.example/acme/engine.git", "main", attempt.Call.SubjectRevision)
    {
        Outcomes = node.Outcomes, Evidence = node.Evidence,
    }), program: "daoris-driver");

    private WorkPerformer Performer(StepAgents? agents) =>
        new(Files, _plugins, () => _now) { Doors = agents is null ? NodeDoors.None : NodeDoors.None.With(StepAgents.Door, agents) };

    private NodeDeclaration? Declared(string unit, string node) => unit == Unit && node == Step.Id ? Step : null;

    private NodeStatus Status() => Files.Read(Unit)!.Statuses[Step.Id];

    private NodeAttempt Attempt() => Files.Read(Unit)!.Attempts.Single();

    private async Task<WorkPerformed> StartAsync(WorkPerformer performer, NodeDeclaration? declared = null)
    {
        var step = declared ?? Step;
        var record = Files.Read(Unit);
        var before = new Dictionary<string, NodeStatus>(StringComparer.Ordinal) { [step.Id] = record?.Statuses.GetValueOrDefault(step.Id) ?? NodeLifecycle.Start(Unit, step.Id) };
        var evaluated = WireEvaluation.Evaluate([step], [], before,
            new WireRun("graph-1", "v-0123456789abcdef") { Revisions = new Dictionary<string, string?> { [Unit] = Tip }, Fired = record?.Fired.ToHashSet(StringComparer.Ordinal) ?? [] });
        Assert.Null(evaluated.Refusal);
        performer.Keep(before, evaluated);
        return await performer.StartAsync(step, Assert.Single(evaluated.Starts), CancellationToken.None);
    }

    [Fact]
    public async Task The_step_is_sent_to_its_agent_whose_session_holds_the_plugin_s_tools_and_its_answer_reaches_the_step_under_the_plugin()
    {
        _sessions.Answer = """{ "state": "reached", "decision": "opened", "url": "https://tickets.example/acme/engine/7", "message": "opened ticket 7" }""";
        var agents = Agents();
        var performer = Performer(agents);

        Assert.Equal(PerformedWhat.Sent, (await StartAsync(performer)).What);
        await agents.IdleAsync();

        Assert.Empty(_plugins.Sent);
        var attempt = Attempt();
        Assert.Equal(Plugin, attempt.Plugin);
        var (ask, tools) = Assert.Single(_sessions.Ran);
        Assert.Equal("Open the ticket for the pushed branch.", ask.Goal);
        Assert.Contains("step_answer", ask.Prompt());
        Assert.Equal(PluginToolsServer.Name, tools.Name);
        string Word(string name) => tools.Arguments[tools.Arguments.ToList().IndexOf(name) + 1];
        Assert.Equal([Plugin, attempt.Call.Invocation, "s-step"], [Word("--plugin"), Word("--step"), Word("--session")]);

        await performer.ReconcileAsync(Declared, CancellationToken.None);
        var status = Status();
        Assert.Equal(NodeStates.Reached, status.State);
        Assert.Equal(NodeSource.Plugin, status.By);
        Assert.NotNull(status.Acceptance);
    }

    [Fact]
    public async Task A_session_that_ends_unanswered_fails_the_step_no_answer()
    {
        var agents = Agents();
        var performer = Performer(agents);

        await StartAsync(performer);
        await agents.IdleAsync();
        await performer.ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal((NodeStates.Failed, StepAnswer.NoAnswer), (Status().State, Status().Code));
    }

    // ——— A sign-in (ACCOUNTREVOKED1): a session its agent refused for its account's sign-in goes on once under the walk's next account,
    // by Daoris itself; where none is left the step fails `sign-in`, naming the accounts and the sign-in to make, never a bare `no-answer`.

    private async Task<StepEnded> PerformedAsync()
    {
        var agents = Agents();
        var performer = Performer(agents);
        await StartAsync(performer);
        await agents.IdleAsync();
        await performer.ReconcileAsync(Declared, CancellationToken.None);
        return new StepEnded(Status(), StepAnswer.Read(_home, Attempt().Call.Invocation)!.Outcome!.Evidence!.Message!);
    }

    /// <summary>The step's status after its answer is reconciled, and its answer's message, which the run's step shows.</summary>
    private sealed record StepEnded(NodeStatus Status, string Message);

    [Fact]
    public async Task A_session_refused_for_its_account_s_sign_in_goes_on_once_under_the_next_account_with_no_press()
    {
        _sessions.Ends.Enqueue(StepSessionEnd.Refused("claude-code", "account-1"));
        _sessions.Answer = """{ "state": "reached", "decision": "opened", "message": "opened ticket 7" }""";

        var done = await PerformedAsync();

        Assert.Equal([null, "account-1"], _sessions.MovedOff);
        Assert.Equal(NodeStates.Reached, done.Status.State);
    }

    [Fact]
    public async Task A_second_refusal_fails_the_step_sign_in_naming_both_accounts_and_the_sign_in()
    {
        _sessions.Ends.Enqueue(StepSessionEnd.Refused("claude-code", "account-1"));
        _sessions.Ends.Enqueue(StepSessionEnd.Refused("claude-code", "account-2"));

        var done = await PerformedAsync();

        Assert.Equal(2, _sessions.Ran.Count);
        Assert.Equal((NodeStates.Failed, StepAnswer.SignIn), (done.Status.State, done.Status.Code));
        Assert.Equal(
            "`claude-code` refused this step's session on `account-1` and `account-2` for their sign-in. Sign one in on its card under Agents, "
            + "or with `daoris agent login claude-code --account account-1`, then try the step again.",
            done.Message);
    }

    [Fact]
    public async Task With_no_account_left_after_a_refusal_the_step_fails_sign_in_naming_each_account()
    {
        _sessions.Ends.Enqueue(StepSessionEnd.Refused("claude-code", "account-1"));
        _sessions.Ends.Enqueue(StepSessionEnd.NoAccount("claude-code", ["account-1", "account-2"]));

        var done = await PerformedAsync();

        Assert.Equal((NodeStates.Failed, StepAnswer.SignIn), (done.Status.State, done.Status.Code));
        Assert.Equal(
            "`claude-code` refused this step's session on `account-1` for its sign-in, and no other account could take it: `account-2` is "
            + "not signed in either. Sign one in on its card under Agents, or with `daoris agent login claude-code --account account-1`, then try "
            + "the step again.",
            done.Message);
    }

    [Fact]
    public async Task A_step_no_account_could_take_for_its_sign_ins_fails_sign_in_at_once()
    {
        _sessions.Ends.Enqueue(StepSessionEnd.NoAccount("claude-code", ["account-1", "Gmail"]));

        var done = await PerformedAsync();

        Assert.Single(_sessions.Ran);
        Assert.Equal((NodeStates.Failed, StepAnswer.SignIn), (done.Status.State, done.Status.Code));
        Assert.Equal(
            "No `claude-code` account could take this step: `account-1` and `Gmail` are not signed in. Sign one in on its card under Agents, "
            + "or with `daoris agent login claude-code --account account-1`, then try the step again.",
            done.Message);
    }

    /// <summary>
    /// The loop's side: a staffing walk held because every agent it may use has no account signed in ends the session with no account,
    /// naming the accounts the walk passed; held for anything else, it ends as any session does.
    /// </summary>
    [Fact]
    public void A_walk_held_for_its_sign_ins_ends_with_no_account_naming_the_accounts_it_passed()
    {
        var signedOut = new ReviewerTried("claude-code-acp", ReviewerLabels.AnotherMaker, ReviewerHeld.SignedOut, "no account is signed in")
        {
            SignedOut = new SignedOutAccounts("claude-code", ["account-1", "Gmail"]),
        };
        var cooling = new ReviewerTried("codex-acp", ReviewerLabels.AnotherMaker, ReviewerHeld.Cooling, "cooling");

        var held = StepSessionEnd.Held(new ReviewerChoice(null, null, ReviewerUnavailable.SignedOut, "held") { Tried = [signedOut] });
        var other = StepSessionEnd.Held(new ReviewerChoice(null, null, ReviewerUnavailable.Refused, "held") { Tried = [signedOut, cooling] });

        Assert.Equal("claude-code", held.Agent);
        Assert.Equal(["account-1", "Gmail"], held.SignedOut);
        Assert.Same(StepSessionEnd.None, other);
    }

    [Fact]
    public async Task A_refusal_whose_next_account_ends_unanswered_fails_no_answer_as_any_session_does()
    {
        _sessions.Ends.Enqueue(StepSessionEnd.Refused("claude-code", "account-1"));

        var done = await PerformedAsync();

        Assert.Equal(2, _sessions.Ran.Count);
        Assert.Equal((NodeStates.Failed, StepAnswer.NoAnswer), (done.Status.State, done.Status.Code));
    }

    [Fact]
    public async Task An_ask_a_restart_left_with_no_session_here_fails_no_answer_and_one_never_sent_is_not_started()
    {
        var agents = Agents();
        await StartAsync(Performer(agents));
        await agents.IdleAsync();
        var invocation = Attempt().Call.Invocation;
        File.Delete(StepAnswer.AnswerPath(_home, invocation));

        var again = Agents();
        Assert.True(again.Holds(invocation));
        var answer = await again.ReconcileAsync(Unit, Attempt(), CancellationToken.None);
        Assert.Equal((ReconcileStates.Done, WorkStates.Failed, StepAnswer.NoAnswer), (answer.State, answer.Outcome!.State, answer.Outcome.Code));

        var never = Attempt() with { Call = Attempt().Call with { Invocation = "i-never" } };
        Assert.Equal(ReconcileStates.NotStarted, (await again.ReconcileAsync(Unit, never, CancellationToken.None)).State);
    }

    [Fact]
    public async Task Unwired_a_goal_step_is_never_sent_to_its_plugin_s_work_perform()
    {
        var started = await StartAsync(Performer(agents: null));

        Assert.Equal(PerformedWhat.Unknown, started.What);
        Assert.Empty(_plugins.Sent);
        Assert.Empty(_sessions.Ran);
    }

    [Fact]
    public async Task An_attempt_begun_on_work_perform_is_settled_by_its_plugin_once_the_agents_are_wired()
    {
        await StartAsync(Performer(agents: null), Step with { Goal = null });
        Assert.Equal([Plugin], _plugins.Sent);
        var invocation = Attempt().Call.Invocation;

        await Performer(Agents()).ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal([invocation], _plugins.Asked);
        Assert.Empty(_sessions.Ran);
        Assert.False(File.Exists(StepAnswer.AskPath(_home, invocation)));
        Assert.Equal(NodeStates.Reached, Status().State);
    }

    [Fact]
    public void An_answer_is_kept_once_in_the_wire_s_shape_its_route_and_outputs_the_step_s_own()
    {
        var ask = new StepAgentAsk("i-1", Unit, "open", 1, Plugin, "open", "goal", "engine", null, "b", null, "main", Tip)
        {
            Outcomes = ["opened"], Evidence = "acme-tickets/receipt@1",
            Outputs = new Dictionary<string, string>(StringComparer.Ordinal) { ["ticket"] = "acme-tickets/ticket@1" },
        };
        JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

        Assert.NotNull(StepAnswer.Answer(_home, ask, Json("""{ "state": "done", "message": "m" }""")));
        Assert.NotNull(StepAnswer.Answer(_home, ask, Json("""{ "state": "reached", "decision": "merged", "message": "m" }""")));
        Assert.NotNull(StepAnswer.Answer(_home, ask, Json("""{ "state": "reached", "message": "m", "outputs": { "other": "7" } }""")));
        Assert.Null(StepAnswer.Answer(_home, ask, Json("""{ "state": "reached", "decision": "opened", "message": "opened 7", "url": "https://tickets.example/7", "outputs": { "ticket": "7" } }""")));
        Assert.NotNull(StepAnswer.Answer(_home, ask, Json("""{ "state": "failed", "code": "late", "message": "m" }""")));

        var kept = StepAnswer.Read(_home, "i-1")!;
        Assert.Equal((ReconcileStates.Done, WorkStates.Reached, "opened"), (kept.State, kept.Outcome!.State, kept.Outcome.Decision));
        Assert.Equal("acme-tickets/receipt@1:i-1", kept.Outcome.Evidence!.Ref);
        Assert.Equal("acme-tickets/ticket@1:7", kept.Outcome.Outputs!["ticket"].Ref);
    }
}
