using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's <c>check</c> on the repository's own development graph starts once the steps before it are reached, and one that stays ready says
/// why in the machine log, once per change of reason. A red at the commit it binds holds it while the work may still answer; once the work's
/// session waits on the run's next step since that red, with no new commit, the check is asked again at the same commit, so the check and the
/// session never wait on each other for good.
/// </summary>
public sealed class CheckStartTests : NodeRunFixture
{
    private const string Session = "s-work";
    private const string Branch = "daoris/s1";

    private readonly List<(string Code, string Said)> _said = [];
    private readonly Checks _checks = new();
    private List<SessionRecord> _sessions = [];
    private readonly List<(string Session, string Text)> _told = [];

    private static string Graph() => DriverWorkflowsCommand.GraphIn(
        JsonNode.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "tools", "workflows", "daoris-dev.json"))), out var graph)
        ? graph!.ToJsonString()
        : throw new InvalidOperationException("the development workflow file holds no graph");

    private sealed class Checks : ICheckPort
    {
        public List<NodeAttempt> Sent { get; } = [];

        public HashSet<string> Answers { get; } = [];

        public Func<NodeAttempt, WorkReconcileAnswer> Answer { get; set; } = _ => new(ReconcileStates.Running);

        public Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct)
        {
            Sent.Add(attempt);
            return Task.CompletedTask;
        }

        public Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct) => Task.FromResult(Answer(attempt));

        public bool Answered(string invocation) => Answers.Contains(invocation);

        public void Stop(string invocation)
        {
        }
    }

    private NodeRuntimeLook RunsLook() => RunLook() with { Content = (_, _, _) => Task.FromResult<HoldContent?>(new HoldContent(null, Unlanded: 2, Ahead: 2)) };

    private NodeRuntime Started(bool door = true)
    {
        var runtime = Runtime();
        runtime.Doors = (door ? NodeDoors.None.With(NodeKinds.Check, _checks) : NodeDoors.None).Saying((code, said) => _said.Add((code, said)));
        runtime.Say = (session, text, _, _) =>
        {
            _told.Add((session, text));
            return Task.FromResult(new SayAnswer(true, "kept"));
        };
        runtime.SessionsRead = _ => Task.FromResult<IReadOnlyList<SessionRecord>?>(_sessions);
        return runtime;
    }

    private async Task PassesAsync(NodeRuntime runtime, int count = 6)
    {
        for (var pass = 0; pass < count; pass++) await runtime.PassAsync(RunsLook(), CancellationToken.None);
    }

    private void Submitted(string tip, IReadOnlyList<string>? executors = null, string? maker = null)
    {
        _branchTips[Branch] = tip;
        _units =
        [
            RunHome.QuestUnit(RunHome.Quest(RunId) with
            {
                Closure = "run:" + RunId, Submission = new QuestSubmissionView("m1", 1, _now, "Built.", Commit: tip, Session: maker),
            }) with { Executors = executors ?? [Session] },
            RunHome.Branch("unit-b1", Branch, Opened, [RunId], []),
        ];
    }

    private SessionRecord Working() => new(Session, "Owner", "working") { Updated = _now };

    private SessionRecord AwaitingStep() =>
        new(Session, "Owner", SessionStates.AwaitingStep) { NoteParts = new StepWait(RunId, "work", Tip: Tip1).Note().Parts, Updated = _now };

    private string State(string node) => Files.Read(RunUnit)?.Statuses.GetValueOrDefault(node)?.State ?? "-";

    private IEnumerable<string> Waits() => _said.Where(each => each.Code == CheckStartWaits.Event).Select(each => each.Said);

    [Fact]
    public async Task A_check_red_at_its_commit_is_asked_again_there_once_the_works_session_waits_on_the_runs_next_step_since_the_red()
    {
        BoundTo(Graph());
        var runtime = Started();
        _sessions = [AwaitingStep()];

        // Content is reached on the branch: the check is asked of its commit at once.
        Submitted(Tip1);
        await PassesAsync(runtime);
        var first = Assert.Single(_checks.Sent);
        Assert.Equal(("unit-b1", Tip1), (first.Call.Unit, first.Call.SubjectRevision));

        // Its red goes back to the work, whose session takes it as a turn: the check waits at that commit, said once over many passes.
        _now = _now.AddMinutes(1);
        _sessions = [Working()];
        _checks.Answers.Add(first.Call.Invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Verify(VerifyVerdicts.Failed, attempt));
        await PassesAsync(runtime);
        await PassesAsync(runtime);
        Assert.Single(_checks.Sent);
        Assert.Equal(NodeStates.Ready, State("check"));
        var wait = Assert.Single(Waits());
        Assert.StartsWith($"run {RunId}: `check` waits ({CheckStartWaits.RedHere})", wait, StringComparison.Ordinal);

        // Its turn ends with no new commit and it waits on the run's next step: the check is asked again at the same commit, its next round.
        _now = _now.AddMinutes(1);
        _sessions = [AwaitingStep()];
        _checks.Answers.Clear();
        _checks.Answer = _ => new(ReconcileStates.Running);
        await PassesAsync(runtime);
        Assert.Equal(2, _checks.Sent.Count);
        Assert.Equal(("unit-b1", Tip1, 2), (_checks.Sent[1].Call.Unit, _checks.Sent[1].Call.SubjectRevision, _checks.Sent[1].Attempt));
        Assert.Single(Waits());
    }

    [Fact]
    public async Task A_red_whose_words_have_not_reached_a_turn_holds_the_check_and_unread_sessions_say_so()
    {
        BoundTo(Graph());
        var runtime = Started();
        _sessions = [AwaitingStep()];
        Submitted(Tip1);
        await PassesAsync(runtime);
        var first = Assert.Single(_checks.Sent);

        // The session waits from before the red: the red's words have not reached a turn yet, so nothing is asked again.
        _now = _now.AddMinutes(1);
        _checks.Answers.Add(first.Call.Invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Verify(VerifyVerdicts.Failed, attempt));
        await PassesAsync(runtime);
        Assert.Single(_checks.Sent);

        // The sessions stop reading: a new reason, said once.
        runtime.SessionsRead = _ => Task.FromResult<IReadOnlyList<SessionRecord>?>(null);
        await PassesAsync(runtime);
        Assert.Single(_checks.Sent);
        Assert.Collection(Waits(),
            each => Assert.Contains($"({CheckStartWaits.RedHere})", each),
            each => Assert.Contains($"({CheckStartWaits.SessionsUnread})", each));
    }

    [Fact]
    public async Task A_check_with_no_executor_wired_stays_ready_and_says_why_once()
    {
        BoundTo(Graph());
        var runtime = Started(door: false);
        _sessions = [AwaitingStep()];
        Submitted(Tip1);
        await PassesAsync(runtime);
        await PassesAsync(runtime);

        Assert.Equal(NodeStates.Ready, State("check"));
        var wait = Assert.Single(Waits());
        Assert.StartsWith($"run {RunId}: `check` waits ({CheckStartWaits.NoDoor})", wait, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_telling_that_failed_for_want_of_a_session_is_offered_again_once_one_starts()
    {
        BoundTo(Graph());
        var runtime = Started();
        _sessions = [AwaitingStep()];
        Submitted(Tip1, executors: []);
        await PassesAsync(runtime);
        var first = Assert.Single(_checks.Sent);
        _now = _now.AddMinutes(1);
        _checks.Answers.Add(first.Call.Invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Verify(VerifyVerdicts.Failed, attempt));
        await PassesAsync(runtime);

        var none = Assert.Single(Files.Read(RunUnit)!.Told);
        Assert.Equal((null, NodeTold.NoSession), (none.Session, none.Why));
        Assert.Empty(_told);

        // Nobody is told again on each pass while no session serves its work.
        await PassesAsync(runtime);
        Assert.Single(Files.Read(RunUnit)!.Told);

        Submitted(Tip1, executors: ["s-late"]);
        await PassesAsync(runtime);

        var told = Assert.Single(_told);
        Assert.Equal("s-late", told.Session);
        Assert.Contains("`check` failed", told.Text);
        var kept = Files.Read(RunUnit)!.ToldOf("check", 1)!;
        Assert.Equal(("s-late", null), (kept.Session, kept.Why));
        await PassesAsync(runtime);
        Assert.Single(_told);
    }

    [Fact]
    public async Task A_check_failing_while_its_session_works_is_told_through_its_inbox_and_the_run_keeps_where_the_words_reach_it()
    {
        BoundTo(Graph());
        var runtime = Started();
        runtime.Say = (session, text, _, _) =>
        {
            _told.Add((session, text));
            return Task.FromResult(new SayAnswer(true, "held") { Reaches = SayAnswer.NextStep });
        };
        _sessions = [Working()];
        Submitted(Tip1);
        await PassesAsync(runtime);
        var first = Assert.Single(_checks.Sent);
        _now = _now.AddMinutes(1);
        _checks.Answers.Add(first.Call.Invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Verify(VerifyVerdicts.Failed, attempt));
        await PassesAsync(runtime);

        Assert.Equal(Session, Assert.Single(_told).Session);
        var kept = Files.Read(RunUnit)!.ToldOf("check", 1)!;
        Assert.Equal((Session, null, SayAnswer.NextStep), (kept.Session, kept.Why, kept.Reaches));
        Assert.Equal("its words went to session `s-work`, which was working, and reach it at its next step.", WorkflowRunTold.Of(Files.Read(RunUnit), "check", 1).Said);
    }

    [Fact]
    public async Task A_check_failing_while_its_session_works_is_told_through_the_real_inbox_as_the_driver_wires_it_and_the_pass_says_so()
    {
        BoundTo(Graph());
        var runtime = Started();
        var record = new Daoris.Driver.Tests.StandInSaid();
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null);
        runtime.Say = new WorkSay(_ => inbox, _ => record, (_, _, _, _) => Task.FromResult(new SayAnswer(false, "running") { Refusal = "running" })).SayAsync;
        _sessions = [Working()];
        Submitted(Tip1);
        await PassesAsync(runtime);
        var first = Assert.Single(_checks.Sent);
        _now = _now.AddMinutes(1);
        _checks.Answers.Add(first.Call.Invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Verify(VerifyVerdicts.Failed, attempt));
        await PassesAsync(runtime);

        Assert.Contains("`check` failed", Assert.Single(record.Said).Text);
        Assert.Contains("`check` failed", inbox.NextOrClose()?.Held?.Text);
        var kept = Files.Read(RunUnit)!.ToldOf("check", 1)!;
        Assert.Equal((Session, SayAnswer.TurnEnd), (kept.Session, kept.Reaches));
    }

    [Fact]
    public async Task A_telling_refused_because_its_session_runs_where_no_door_here_reaches_is_offered_again_until_it_is_told()
    {
        BoundTo(Graph());
        var runtime = Started();
        var running = true;
        runtime.Say = (session, text, _, _) =>
        {
            if (running) return Task.FromResult(new SayAnswer(false, "running") { Refusal = "running" });
            _told.Add((session, text));
            return Task.FromResult(new SayAnswer(true, "kept"));
        };
        _sessions = [Working()];
        Submitted(Tip1);
        await PassesAsync(runtime);
        var first = Assert.Single(_checks.Sent);
        _now = _now.AddMinutes(1);
        _checks.Answers.Add(first.Call.Invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Verify(VerifyVerdicts.Failed, attempt));
        await PassesAsync(runtime);

        Assert.Empty(_told);
        Assert.Equal((null, NodeTold.Running), (Files.Read(RunUnit)!.ToldOf("check", 1)!.Session, Files.Read(RunUnit)!.ToldOf("check", 1)!.Why));

        running = false;
        var said = new List<string>();
        for (var pass = 0; pass < 6; pass++) said.AddRange((await runtime.PassAsync(RunsLook(), CancellationToken.None)).Said);

        Assert.Single(said, line => line.Contains($"session {Session} is told what `check` sent back", StringComparison.Ordinal));
        Assert.Equal(Session, Assert.Single(_told).Session);
        Assert.Equal((Session, null), (Files.Read(RunUnit)!.ToldOf("check", 1)!.Session, Files.Read(RunUnit)!.ToldOf("check", 1)!.Why));
    }

    private static VerifyResult Verify(string verdict, NodeAttempt attempt) =>
        new(attempt.Call.Invocation, Tip1, verdict, "why", [new VerifyGate("suite", "fail", 3)]) { Failed = "suite" };
}
