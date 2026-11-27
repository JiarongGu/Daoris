using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 A check sent at a commit the work then moved past is ended at the next pass, so the check at the new tip starts as soon as the old
/// one answers, never after the old one's gates run to a verdict nothing reads: on the install those waits were half the time the
/// development graph billed to <c>content.holds</c>. A check judged again at the commit it runs keeps running.
/// </summary>
public sealed class CheckSupersededTests : NodeRunFixture
{
    private const string Session = "s-work";
    private const string Branch = "daoris/s1";

    private readonly Checks _checks = new();

    private static string Graph() => DriverWorkflowsCommand.GraphIn(
        JsonNode.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "tools", "workflows", "daoris-dev.json"))), out var graph)
        ? graph!.ToJsonString()
        : throw new InvalidOperationException("the development workflow file holds no graph");

    /// <summary>The executor as a check sees it: an attempt runs until it is stopped, then answers as a stopped one.</summary>
    private sealed class Checks : ICheckPort
    {
        public List<NodeAttempt> Sent { get; } = [];

        public List<(string Invocation, string? Why)> Stopped { get; } = [];

        public Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct)
        {
            Sent.Add(attempt);
            return Task.CompletedTask;
        }

        public Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct) =>
            Task.FromResult(Stopped.Any(each => each.Invocation == attempt.Call.Invocation)
                ? CheckExecution.AnswerOf(new VerifyResult(attempt.Call.Invocation, attempt.Call.SubjectRevision, VerifyVerdicts.Unknown, "stopped", []) { Code = VerifyResult.Stopped })
                : new WorkReconcileAnswer(ReconcileStates.Running));

        public bool Answered(string invocation) => Stopped.Any(each => each.Invocation == invocation);

        public void Stop(string invocation) => Stopped.Add((invocation, null));

        public void Supersede(string invocation, string why) => Stopped.Add((invocation, why));
    }

    private NodeRuntimeLook RunsLook() => RunLook() with { Content = (_, _, _) => Task.FromResult<HoldContent?>(new HoldContent(null, Unlanded: 2, Ahead: 2)) };

    private NodeRuntime Started()
    {
        var runtime = Runtime();
        runtime.Doors = NodeDoors.None.With(NodeKinds.Check, _checks);
        runtime.SessionsRead = _ => Task.FromResult<IReadOnlyList<SessionRecord>?>([new SessionRecord(Session, "Owner", "working") { Updated = _now }]);
        return runtime;
    }

    private async Task PassesAsync(NodeRuntime runtime, int count = 6)
    {
        for (var pass = 0; pass < count; pass++) await runtime.PassAsync(RunsLook(), CancellationToken.None);
    }

    private void Submitted(string tip)
    {
        _branchTips[Branch] = tip;
        _units =
        [
            RunHome.QuestUnit(RunHome.Quest(RunId) with
            {
                Closure = "run:" + RunId, Submission = new QuestSubmissionView("m1", 1, _now, "Built.", Commit: tip, Session: Session),
            }) with { Executors = [Session] },
            RunHome.Branch("unit-b1", Branch, Opened, [RunId], []),
        ];
    }

    [Fact]
    public async Task A_check_whose_commit_the_work_moved_past_is_ended_and_the_check_at_the_new_tip_starts_once_it_answers()
    {
        BoundTo(Graph());
        var runtime = Started();
        Submitted(Tip1);
        await PassesAsync(runtime);
        var first = Assert.Single(_checks.Sent);
        Assert.Equal(Tip1, first.Call.SubjectRevision);

        // The work commits again while the check of its last commit still waits or runs.
        _now = _now.AddMinutes(1);
        Submitted(Tip2);
        await PassesAsync(runtime);

        var stopped = Assert.Single(_checks.Stopped);
        Assert.Equal(first.Call.Invocation, stopped.Invocation);
        Assert.Contains(Tip2[..8], stopped.Why, StringComparison.Ordinal);
        Assert.Equal(2, _checks.Sent.Count);
        Assert.Equal((Tip2, 2), (_checks.Sent[1].Call.SubjectRevision, _checks.Sent[1].Attempt));
        Assert.Equal(NodeStates.Active, Files.Read(RunUnit)!.Statuses["check"].State);
    }

    [Fact]
    public async Task A_check_at_the_tip_the_work_still_stands_on_is_never_ended()
    {
        BoundTo(Graph());
        var runtime = Started();
        Submitted(Tip1);
        await PassesAsync(runtime);
        await PassesAsync(runtime);

        Assert.Single(_checks.Sent);
        Assert.Empty(_checks.Stopped);
    }
}
