using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GateFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The <c>opinion</c> step on a graph run while it stands on someone: with its findings handed to the work's session it waits on
/// that session's answer, said so and never <i>working</i>; a pass whose reviewer ended without an opinion fails it, saying so.
/// </summary>
public sealed class OpinionStepTests
{
    private const string Run = "q1";

    private static readonly string Self = RunUnits.IdOf(Run);

    private const string Digest = "gph-0123456789abcdef0123456789abcdef";

    private const string ClaimedOpinionHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"opinion","kind":"opinion","version":1,"reach":"observed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
         "wires":[{"id":"c>o","kind":"after","from":"claimed","to":"opinion"},{"id":"o>h","kind":"after","from":"opinion","to":"handed"}]}
        """;

    private static WorkflowRun Derive(NodeStatus opinion, OpinionGateState gate)
    {
        var (graph, problems) = NodeGraphs.Read(ClaimedOpinionHanded);
        Assert.True(graph is not null, string.Join("; ", problems.Select(problem => problem.Words)));
        var binding = new WorkflowRunBinding(Run, "engine", "default", "docs", WorkflowLevels.Repository, Then) { Version = 1, Digest = Digest };
        var facts = new WorkflowRunFacts("engine", new CurrentWorkflow([], [], Digest))
        {
            Quests = [new QuestView(Run, "ask:a1", "engine", "The work", "Body.", "Taken")],
            Sessions = [new TracedSession("s1", "engine", "awaiting-step") { Quest = Run, Tree = Path.Combine("trees", "q1-tree"), Created = Then }],
            Process = WorkflowProcess.Declared(DriverConfig.Empty, "engine", "default") with { Binding = binding, Run = Run },
            Opinion = gate,
        };
        var record = new NodeRecord(Self)
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["claimed"] = new(Self, "claimed", NodeStates.Reached) { Attempt = 1 },
                ["opinion"] = opinion,
            },
        };
        return WorkflowGraphRuns.Derive(new WorkflowGraphRunFacts(facts, Run, graph, record), "default", "a1");
    }

    private static NodeStatus Opinion(string state, string? holds = null, string? code = null) =>
        new(Self, "opinion", state) { Attempt = 1, Holds = holds, Code = code, Since = Then };

    /// <summary>
    /// 🔴 Its findings with the work's session, the step waits on that session's answer to the opinion: a wait of the opinion's kind naming
    /// the session, held by that agent, in the run's view and in the terminal's words; never <i>working</i>, and never the person's.
    /// </summary>
    [Fact]
    public void An_opinion_with_the_works_session_waits_on_its_answer_and_says_whose()
    {
        var gate = new OpinionGateState(OpinionGateStates.WithSession, "engine") { Session = "s1", Tip = Tip };

        var run = Derive(Opinion(NodeStates.Active, OpinionGateStates.WithSession), gate);

        var step = run.Steps.Single(each => each.Step.Id == "opinion");
        Assert.Equal(WorkflowRunStates.Waiting, step.State);
        Assert.Equal((OpinionStepWaits.Answer, "s1"), (step.Wait?.Code, step.Wait?.Values.GetValueOrDefault("session")));
        Assert.Equal((StepHolders.Agent, "s1"), (step.Holder?.Kind, step.Holder?.Name));
        Assert.Contains("Waits on `s1`'s answer to the second opinion.", WorkflowRunWords.Say([run], null));
        Assert.Contains(OpinionStepWaits.Answer, NodeKit.Of(NodeKinds.Opinion) is BuiltInNode built ? built.Waits : []);

        // A pass still reading, or any other hold, is no wait on the session.
        var reading = Derive(Opinion(NodeStates.Active, OpinionGateStates.Reading), gate with { State = OpinionGateStates.Reading });
        Assert.Null(reading.Steps.Single(each => each.Step.Id == "opinion").Wait);
    }

    /// <summary>🔴 A gate holding the work for the person (a dispute, commits nobody read) is their wait with its count, said in the
    /// run's view and the terminal's words, never a silent <i>working</i>.</summary>
    [Theory]
    [InlineData(OpinionGateStates.Disputed, OpinionStepWaits.Disputed, "Waits on your answer to 2 disputed findings of the second opinion")]
    [InlineData(OpinionGateStates.CommitsSince, OpinionStepWaits.CommitsSince, "Waits on your answer to 2 commits no other agent read")]
    public void An_opinion_whose_gate_holds_for_your_answer_says_so_and_never_working(string state, string code, string said)
    {
        var gate = new OpinionGateState(state, "engine")
        {
            Session = "s1", Tip = Tip, Disputes = new OpinionDisputes([1], [2]), Since = 2,
        };

        var run = Derive(Opinion(NodeStates.Active, state), gate);

        var step = run.Steps.Single(each => each.Step.Id == "opinion");
        Assert.Equal(WorkflowRunStates.Waiting, step.State);
        Assert.Equal((code, "2", "s1"), (step.Wait?.Code, step.Wait?.Values.GetValueOrDefault("count"), step.Wait?.Values.GetValueOrDefault("session")));
        Assert.Equal(StepHolders.Person, step.Holder?.Kind);
        Assert.Contains(said, WorkflowRunWords.Say([run], null));
        Assert.Contains(code, NodeKit.Of(NodeKinds.Opinion) is BuiltInNode built ? built.Waits : []);
    }

    /// <summary>
    /// 🔴 A pass whose reviewer's session ended without giving an opinion, or whose minutes ran out first, fails the step with that said, its
    /// code kept; a gate with nobody free to read it is no failed pass and stands as before.
    /// </summary>
    [Theory]
    [InlineData("ended", "ended without giving an opinion")]
    [InlineData("out-of-time", "ran out before it gave an opinion")]
    public void A_pass_that_ended_without_an_opinion_fails_the_step_saying_so(string code, string said)
    {
        var node = Node(NodeKinds.Opinion);
        var gate = new OpinionGateState(OpinionGateStates.Unavailable, "engine") { Tip = Tip, Code = code };

        var moved = Applied(node, Reads.Opinion(Branch(), node, Binding(NodeKinds.Opinion), gate));

        Assert.Equal((NodeStates.Failed, code), (moved.Status.State, moved.Status.Code));
        Assert.True(((IObserves)NodeKit.Of(NodeKinds.Opinion)!).ObservesFailed(moved.Status));
        var run = Derive(Opinion(NodeStates.Failed, code: code), gate);
        Assert.Contains(said, WorkflowRunWords.Say([run], null));

        var nobody = Applied(node, Reads.Opinion(Branch(), node, Binding(NodeKinds.Opinion), gate with { Code = ReviewerUnavailable.Cooling }));
        Assert.Equal(NodeStates.Skipped, nobody.Status.State);
    }
}
