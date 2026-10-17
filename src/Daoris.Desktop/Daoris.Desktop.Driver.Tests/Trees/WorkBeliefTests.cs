using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PERFORM1c (the fabric events design §C4, §D4, §D6, §E2): a plugin's completion is believed only where §C4's five rules hold for
/// the attempt its invocation names, only a plugin's own node is reached on its word, and an input is read only from its producer's current
/// acceptance.
/// </summary>
public sealed class WorkBeliefTests
{
    private const string Plugin = "acme.deploy";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";
    private static readonly string Newer = new('f', 40);
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private static readonly NodeDeclaration Deploy = new("deploy", Plugin + "/deploy", 1, NodeReach.Performed)
    {
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
    };

    private static readonly NodeAttempt Attempt = new("deploy", 1, Plugin,
        new WorkPerformCall("unit-a", Plugin + "/deploy", 1, "i-1", "k-1", Tip, [], Now.AddMinutes(15)), AttemptStates.Sent);

    private static readonly WatchedUnit Unit = new("engine", "default", "checkout", "feature/x", Tip, null, null, ["deploy"]);

    private static WorkStepHeard Heard(
        string step = "deploy", string plugin = Plugin, int generation = 2, DateTimeOffset? at = null, string? repository = null, string? branch = null,
        string? commit = null) =>
        new(plugin, generation, new WorkStepFrame(step, new WorkOutcome(WorkStates.Reached), at ?? Now, "e1", repository, branch, commit, "i-1"));

    private static string? Judged(WorkStepHeard heard) => WorkBelief.Completion(heard, Attempt, 2, Deploy, Unit, Now);

    [Fact]
    public void A_completion_from_the_attempts_plugin_at_its_current_generation_naming_its_step_and_its_work_is_believed()
    {
        Assert.Null(Judged(Heard()));
        Assert.Null(Judged(Heard(Plugin + "/deploy")));
        Assert.Null(Judged(Heard(repository: "engine", branch: "feature/x", commit: Tip, at: Now.AddMinutes(1))));
    }

    [Fact]
    public void Rule_one_the_sender_is_the_attempts_plugins_current_process_never_what_the_frame_says()
    {
        Assert.Equal(WorkRefusals.NotSender, Judged(Heard(plugin: "other.plugin")));
        Assert.Equal(WorkRefusals.NotSender, Judged(Heard(generation: 1)));
        Assert.Equal(WorkRefusals.NotSender, WorkBelief.Completion(Heard(), Attempt, null, Deploy, Unit, Now));
    }

    [Fact]
    public void Rule_two_the_sender_is_the_plugin_its_rule_names_for_the_step_and_the_step_still_declared()
    {
        Assert.Equal(WorkRefusals.NotNamed, Judged(Heard("notify")));
        Assert.Equal(WorkRefusals.NotNamed, Judged(Heard("other.plugin/deploy")));

        // The graph now has another plugin perform the node: the attempt's plugin is no longer the one its rule names.
        Assert.Equal(WorkRefusals.NotNamed, WorkBelief.Completion(Heard(), Attempt, 2, Deploy with { Kind = "other.plugin/deploy" }, Unit, Now));

        // A pull request's state is Daoris's own reading (its ask, D148's checks), never a frame's word.
        Assert.Equal(WorkRefusals.NotNamed,
            WorkBelief.Completion(Heard(), Attempt, 2, new NodeDeclaration("deploy", NodeKinds.PullRequest, 1, NodeReach.Observed), Unit, Now));

        // No longer declared by its plugin, or no longer included by the cycle governing its unit.
        Assert.Equal(WorkRefusals.NotDeclared, WorkBelief.Completion(Heard(), Attempt, 2, null, Unit, Now));
    }

    [Fact]
    public void Rule_three_it_names_a_unit_daoris_tells_about_and_no_other()
    {
        Assert.Equal(WorkRefusals.NotWatched, WorkBelief.Completion(Heard(), Attempt, 2, Deploy, null, Now));
        Assert.Equal(WorkRefusals.NotWatched, Judged(Heard(repository: "other")));
        Assert.Equal(WorkRefusals.NotWatched, Judged(Heard(branch: "feature/y")));
    }

    [Fact]
    public void Rule_four_it_is_bound_to_its_attempts_revision_and_a_newer_one_makes_it_stale()
    {
        Assert.Equal(WorkRefusals.Stale, Judged(Heard(commit: Newer)));

        // The frame is its attempt's, but the work moved past the revision it was dispatched on: its outcome is a stale attempt's.
        Assert.False(WorkBelief.Lapsed(Attempt, Unit));
        Assert.True(WorkBelief.Lapsed(Attempt, Unit with { Tip = Newer }));
    }

    [Fact]
    public void Rule_five_its_time_is_no_more_than_a_minute_ahead()
    {
        Assert.Null(Judged(Heard(at: Now.AddMinutes(1))));
        Assert.Equal(WorkRefusals.Shape, Judged(Heard(at: Now.AddMinutes(1).AddSeconds(1))));
    }

    [Fact]
    public void Only_a_plugins_own_node_is_reached_on_the_plugins_word()
    {
        Assert.True(NodeAuthorities.Of(Deploy)!.OnPluginWord);
        foreach (var kind in new[] { NodeKinds.Pushed, NodeKinds.PullRequest, NodeKinds.Handed, NodeKinds.Line, NodeKinds.Claimed, NodeKinds.Human, NodeKinds.WaitUnit, NodeKinds.QuestClose })
        {
            Assert.False(NodeAuthorities.BuiltIn[kind].OnPluginWord, kind);
        }
    }

    [Fact]
    public void An_input_is_read_only_from_its_producers_current_acceptance()
    {
        var produced = new NodeAcceptance("unit-a", Tip, "graph-1", "build", 1, 1);
        var output = new NodePort("acme.deploy/artifact@1", "acme.deploy/artifact@1:a1", Tip, produced.Id);
        var reached = new NodeStatus("unit-a", "build", NodeStates.Reached)
        {
            Attempt = 1, Acceptance = produced with { Outputs = new Dictionary<string, NodePort> { ["artifact"] = output } },
        };
        NodeRecord Holding(NodeStatus status) => new("unit-a") { Statuses = new Dictionary<string, NodeStatus> { ["build"] = status } };
        NodeAcceptance Taking(NodePort port) => new("unit-a", Tip, "graph-1", "deploy", 1, 1) { Inputs = new Dictionary<string, NodePort> { ["release"] = port } };

        Assert.Null(WorkBelief.Unresolved(Holding(reached), Taking(output)));

        // Its producer invalidated (a new tip, an upstream lapse): the output it exposed is stale and never read.
        Assert.Equal("release", WorkBelief.Unresolved(Holding(reached with { State = NodeStates.Invalidated }), Taking(output)));

        // An output of another acceptance than the producer's current one, or another reference than it exposes.
        Assert.Equal("release", WorkBelief.Unresolved(Holding(reached), Taking(output with { Acceptance = "acc-older" })));
        Assert.Equal("release", WorkBelief.Unresolved(Holding(reached), Taking(output with { Ref = "acme.deploy/artifact@1:a0" })));

        // A producer the record does not hold, or one accepted on another revision than the consumer's.
        Assert.Equal("release", WorkBelief.Unresolved(new NodeRecord("unit-a"), Taking(output)));
        Assert.Equal("release", WorkBelief.Unresolved(Holding(reached), Taking(output) with { SubjectRevision = Newer }));
    }
}
