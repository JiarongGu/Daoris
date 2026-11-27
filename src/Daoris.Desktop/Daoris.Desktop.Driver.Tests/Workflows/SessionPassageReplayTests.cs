using System.Text.Json.Nodes;
using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A development day for the session lifecycle, run through the runtime on the repository's own development graph: the agent's done
/// is a submission, the check's red goes back to the work, its green lets the opinion go on, the landing is reached before
/// the run's end, no check is asked of any session, and the quest's Done is written once, with the run's end.
/// </summary>
public sealed class SessionPassageReplayTests : NodeRunFixture
{
    private const string Session = "s-work";
    private const string Branch = "daoris/s1";

    private readonly List<(string Session, string Text, string Key)> _told = [];
    private readonly List<RunFinish> _finished = [];
    private readonly Checks _checks = new();
    private (OpinionGateState?, ReviewGateState?) _gates;
    private bool _landed;

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

        public void Supersede(string invocation, string why) => Stop(invocation);

        public void Stop(string invocation)
        {
        }
    }

    private NodeRuntimeLook Look() => RunLook() with
    {
        Gates = (_, _, _) => Task.FromResult(_gates),
        // The line holds none of the work until it lands, and then holds all of it.
        Content = (_, _, _) => Task.FromResult<HoldContent?>(_landed
            ? new HoldContent(null, 0, 0) { Line = new LineHolding("main", new LineHeld("l1", "l0", [new LandingCommit("c1", "The work")], [], [])) }
            : new HoldContent(null, Unlanded: 2, Ahead: 2)),
    };

    private NodeRuntime Telling()
    {
        var runtime = Runtime();
        runtime.Doors = NodeDoors.None.With(NodeKinds.Check, _checks);
        runtime.Say = (session, text, key, _) =>
        {
            _told.Add((session, text, key));
            return Task.FromResult(new SayAnswer(true, "kept"));
        };
        runtime.Finish = (end, _) =>
        {
            _finished.Add(end);
            return Task.FromResult<RunEndResponse?>(new RunEndResponse(null, "ended", [.. end.Quests.Select(quest => quest.Id)], end.Sessions));
        };
        runtime.SessionsRead = _ => Task.FromResult<IReadOnlyList<SessionRecord>?>(
            [
                new SessionRecord(Session, "Owner", SessionStates.AwaitingStep) { NoteParts = new StepWait(RunId, "work").Note().Parts },
                new SessionRecord("s-other", "Owner", SessionStates.AwaitingStep) { NoteParts = new StepWait("q-other").Note().Parts },
            ]);
        return runtime;
    }

    private async Task PassesAsync(NodeRuntime runtime, int count = 6)
    {
        for (var pass = 0; pass < count; pass++) await runtime.PassAsync(Look(), CancellationToken.None);
    }

    private void Submitted(long sequence, string tip, params LandedBranch[] landings)
    {
        _branchTips[Branch] = tip;
        _units =
        [
            RunHome.QuestUnit(RunHome.Quest(RunId) with
            {
                Closure = "run:" + RunId, Submission = new QuestSubmissionView("m1", sequence, _now, "Built.", Commit: tip),
            }) with { Executors = [Session] },
            RunHome.Branch("unit-b1", Branch, Opened, [RunId], landings),
        ];
    }

    private string State(string node) => Files.Read(RunUnit)?.Statuses.GetValueOrDefault(node)?.State ?? "-";

    [Fact]
    public async Task A_red_check_goes_back_to_the_work_a_green_one_lets_the_opinion_go_on_and_the_landing_comes_before_the_runs_end()
    {
        BoundTo(Graph());
        var runtime = Telling();

        // The agent's done is a submission at the first tip: the check is asked of the run, never of the session, and nothing goes on before it.
        Submitted(1, Tip1);
        await PassesAsync(runtime);
        var first = Assert.Single(_checks.Sent);
        Assert.Equal(("unit-b1", Tip1), (first.Call.Unit, first.Call.SubjectRevision));
        Assert.Equal((NodeStates.Blocked, NodeStates.Blocked), (State("opinion"), State("landing")));

        // Its red is said to the same session under the check's key and sends the work round again; the run does not end.
        _checks.Answers.Add(first.Call.Invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Verify(VerifyVerdicts.Failed, attempt, Tip1));
        await PassesAsync(runtime);
        var red = Assert.Single(_told);
        Assert.Equal(Session, red.Session);
        Assert.Contains("check", red.Key, StringComparison.Ordinal);
        Assert.Equal((NodeStates.Blocked, NodeStates.Blocked), (State("opinion"), State("landing")));
        Assert.Empty(_finished);

        // The red is not asked again at the commit that failed it: the check waits for the fix.
        Assert.Single(_checks.Sent);

        // Its fix is a new submission at a new tip: a new attempt checks that commit, and its green lets the gates go on.
        _checks.Answers.Clear();
        _checks.Answer = _ => new(ReconcileStates.Running);
        Submitted(2, Tip2);
        await PassesAsync(runtime);
        var second = _checks.Sent.Last();
        Assert.Equal((2, Tip2), (_checks.Sent.Count, second.Call.SubjectRevision));
        _checks.Answers.Add(second.Call.Invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Verify(VerifyVerdicts.Reached, attempt, Tip2));
        _gates = (
            new OpinionGateState(OpinionGateStates.Settled, RunHome.Repository) { Tip = Tip2 },
            new ReviewGateState(ReviewStates.Reviewed, new ReviewDecision(ReviewLevels.Repository, "preview") { Repository = RunHome.Repository })
            {
                Tip = Tip2, Verdict = new QuestReviewVerdictView(ReviewVerdicts.Reviewed),
            });
        await PassesAsync(runtime);
        Assert.Equal((NodeStates.Reached, NodeStates.Reached), (State("check"), State("opinion")));
        Assert.Empty(_finished);

        // The landing is reached on the second tip before the run ends, and the run's end is the one write of the quest's Done.
        Submitted(2, Tip2, RunHome.Landing("unit-b1", Branch, Tip2, _now, RunId));
        await PassesAsync(runtime);
        Assert.Equal((NodeStates.Reached, NodeStates.Active), (State("landing"), State("line")));
        Assert.Empty(_finished);
        _landed = true;
        await PassesAsync(runtime);
        Assert.Equal((NodeStates.Reached, NodeStates.Active), (State("line"), State("tree")));
        Assert.Empty(_finished);

        // The tree's release is the graph's last step; with it reached the run ends.
        var kept = Files.Read(RunUnit)!;
        Files.Write(kept with { Statuses = new Dictionary<string, NodeStatus>(kept.Statuses) { ["tree"] = kept.Statuses["tree"] with { State = NodeStates.Reached } } });
        await PassesAsync(runtime);
        var finish = Assert.Single(_finished);
        Assert.Equal("completed", finish.State);
        Assert.Equal([new RunEndQuest(RunId, "m1", 2)], finish.Quests);
        Assert.Equal([Session], finish.Sessions);
        Assert.All(_checks.Sent, attempt => Assert.Equal("unit-b1", attempt.Call.Unit));
    }

    private static VerifyResult Verify(string verdict, NodeAttempt attempt, string tip) =>
        new(attempt.Call.Invocation, tip, verdict, verdict == VerifyVerdicts.Reached ? null : "why", [new VerifyGate("suite", verdict == VerifyVerdicts.Failed ? "fail" : "pass", 3)])
        {
            Failed = verdict == VerifyVerdicts.Failed ? "suite" : null,
        };
}
