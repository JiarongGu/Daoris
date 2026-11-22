using System.Text.Json.Nodes;
using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A quest closed Done outside its run, whoever wrote the Done, ends the run as a stop does: on the repository's own
/// development graph, a quest marked done while its check runs starts no further step, its check's executor is told to stop, the run ends
/// stopped by Daoris and its waiting session with it, and the end's line names the commits its branch holds that the line does not.
/// </summary>
public sealed class QuestDoneRunEndTests : NodeRunFixture
{
    private const string Session = "s-work";
    private const string Branch = "daoris/s1";

    private readonly List<RunFinish> _finished = [];
    private readonly Checks _checks = new();
    private readonly List<string> _lacksAsked = [];
    private IReadOnlyList<LandingCommit>? _lacks = [new("aaaaaaaa11111111111111111111111111111111", "Mend the parser"), new("bbbbbbbb22222222222222222222222222222222", "Test it")];

    private static string Graph() => DriverWorkflowsCommand.GraphIn(
        JsonNode.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "tools", "workflows", "daoris-dev.json"))), out var graph)
        ? graph!.ToJsonString()
        : throw new InvalidOperationException("the development workflow file holds no graph");

    private sealed class Checks : ICheckPort
    {
        public List<NodeAttempt> Sent { get; } = [];

        public List<string> Stopped { get; } = [];

        public Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct)
        {
            Sent.Add(attempt);
            return Task.CompletedTask;
        }

        public bool Green { get; set; }

        public Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct) => Task.FromResult(Green
            ? new WorkReconcileAnswer(ReconcileStates.Done, new WorkOutcome(WorkStates.Reached))
            : new WorkReconcileAnswer(ReconcileStates.Running));

        public bool Answered(string invocation) => Green;

        public void Stop(string invocation) => Stopped.Add(invocation);
    }

    private NodeRuntimeLook Look() => RunLook() with
    {
        Content = (_, _, _) => Task.FromResult<HoldContent?>(new HoldContent(null, Unlanded: 2, Ahead: 2)),
        Lacks = (_, _, _, tip, _) =>
        {
            _lacksAsked.Add(tip);
            return Task.FromResult(_lacks);
        },
    };

    private NodeRuntime Driving()
    {
        var runtime = Runtime();
        runtime.Doors = NodeDoors.None.With(NodeKinds.Check, _checks);
        runtime.Say = (_, _, _, _) => Task.FromResult(new SayAnswer(true, "kept"));
        runtime.Finish = (end, _) =>
        {
            _finished.Add(end);
            return Task.FromResult<RunEndResponse?>(new RunEndResponse(null, "ended", [.. end.Quests.Select(quest => quest.Id)], end.Sessions));
        };
        runtime.SessionsRead = _ => Task.FromResult<IReadOnlyList<SessionRecord>?>(
            [new SessionRecord(Session, "Owner", SessionStates.AwaitingStep) { NoteParts = new StepWait(RunId, "check").Note().Parts }]);
        return runtime;
    }

    private async Task<List<string>> PassesAsync(NodeRuntime runtime, int count = 6)
    {
        var said = new List<string>();
        for (var pass = 0; pass < count; pass++) said.AddRange((await runtime.PassAsync(Look(), CancellationToken.None)).Said);
        return said;
    }

    private void QuestAt(string status, string? closure = "run:" + RunId, string tip = Tip1, params QuestStepView[] then)
    {
        _branchTips[Branch] = tip;
        _units =
        [
            RunHome.QuestUnit(RunHome.Quest(RunId, status, then: then) with
            {
                Closure = closure, Submission = new QuestSubmissionView("m1", 1, _now, "Built.", Commit: tip),
            }) with { Executors = [Session] },
            RunHome.Branch("unit-b1", Branch, Opened, [RunId]),
        ];
    }

    private string State(string node) => Files.Read(RunUnit)?.Statuses.GetValueOrDefault(node)?.State ?? "-";

    [Fact]
    public async Task A_quest_marked_done_during_its_check_starts_no_further_step_and_ends_its_run_and_session_naming_what_the_line_lacks()
    {
        BoundTo(Graph());
        var runtime = Driving();
        QuestAt("Taken");
        await PassesAsync(runtime);
        var check = Assert.Single(_checks.Sent);
        Assert.Equal(NodeStates.Active, State("check"));
        Assert.Empty(_finished);

        // The person marks the quest done while its check runs.
        QuestAt("Done");
        var said = await PassesAsync(runtime);

        Assert.Single(_checks.Sent);
        Assert.Equal([check.Call.Invocation], _checks.Stopped);
        Assert.Equal(NodeStates.Stopped, State("check"));
        Assert.All(new[] { "opinion", "landing", "line", "tree" }, node => Assert.Contains(State(node), new[] { NodeStates.Stopped, "-" }));
        var record = Files.Read(RunUnit)!;
        Assert.DoesNotContain(record.Attempts, attempt => attempt.Node is "opinion" or "landing" or "line" or "tree");
        Assert.All(record.Attempts, attempt => Assert.Equal(AttemptStates.Settled, attempt.State));

        var ended = record.Ended;
        Assert.Equal((RunOutcomes.Stopped, "check", NodeSource.Daoris, QuestDoneRuns.Why), (ended?.Outcome, ended?.Node, ended?.By, ended?.Why));
        var finish = Assert.Single(_finished);
        Assert.Equal("stopped", finish.State);
        Assert.Equal([Session], finish.Sessions);
        Assert.Empty(finish.Quests);
        Assert.Contains("closed Done", finish.Note, StringComparison.Ordinal);

        Assert.Contains(Tip1, _lacksAsked);
        var line = Assert.Single(said, each => each.Contains("closed Done outside", StringComparison.Ordinal));
        Assert.Contains("2 commits the line does not", line, StringComparison.Ordinal);
        Assert.Contains("aaaaaaaa Mend the parser", line, StringComparison.Ordinal);
        Assert.Contains("bbbbbbbb Test it", line, StringComparison.Ordinal);
        Assert.Contains(Branch, line, StringComparison.Ordinal);
    }

    /// <summary>A green check and a moved tip with the done in one pass: the reached steps are never bound again, so nothing starts and the run ends there.</summary>
    [Fact]
    public async Task A_quest_done_with_its_branch_moved_past_a_green_check_starts_no_new_check_and_ends_in_that_pass()
    {
        BoundTo(Graph());
        var runtime = Driving();
        QuestAt("Taken");
        await PassesAsync(runtime);
        _checks.Green = true;
        await PassesAsync(runtime);
        Assert.Equal((NodeStates.Reached, NodeStates.Active), (State("check"), State("opinion")));
        var sent = _checks.Sent.Count;

        QuestAt("Done", tip: Tip2);
        await PassesAsync(runtime, count: 1);

        Assert.Equal(sent, _checks.Sent.Count);
        Assert.Equal(NodeStates.Reached, State("check"));
        Assert.Equal(QuestDoneRuns.Why, Files.Read(RunUnit)!.Ended?.Why);
        Assert.Equal([Session], Assert.Single(_finished).Sessions);
    }

    [Fact]
    public async Task A_quest_done_whose_branch_the_line_holds_says_nothing_is_left_and_one_git_cannot_read_says_so()
    {
        BoundTo(Graph());
        var runtime = Driving();
        QuestAt("Taken");
        await PassesAsync(runtime);
        _lacks = [];

        QuestAt("Done");
        var said = await PassesAsync(runtime);

        Assert.Equal(QuestDoneRuns.Why, Files.Read(RunUnit)!.Ended?.Why);
        Assert.Contains(said, each => each.Contains("closed Done outside", StringComparison.Ordinal) && each.Contains("the line holds every commit", StringComparison.Ordinal));
        Assert.Equal("git could not say which commits of `daoris/s1` the line lacks", QuestDoneRuns.Left(Branch, null));
    }

    [Fact]
    public async Task A_quest_done_that_names_its_next_step_here_or_is_bound_to_no_run_leaves_its_run_going()
    {
        BoundTo(Graph());
        var runtime = Driving();
        QuestAt("Taken");
        await PassesAsync(runtime);

        QuestAt("Done", then: new QuestStepView(RunHome.Repository, "Next", "The next step."));
        await PassesAsync(runtime);
        QuestAt("Done", closure: null);
        await PassesAsync(runtime);

        Assert.Null(Files.Read(RunUnit)!.Ended);
        Assert.Equal(NodeStates.Active, State("check"));
        Assert.Empty(_checks.Stopped);
        Assert.Empty(_finished);
    }
}
