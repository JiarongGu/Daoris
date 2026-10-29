using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xviii, the Workflows cut: what it keeps. A workflow node's execution and its record kept whole, a chat run's room for
/// Ask Daoris (a chat run is no conversation of Ask Daoris's) and a workflow run's read of a session in that room, each through the surface
/// that stood on both sides of the move and of the cut.
/// </summary>
public sealed class WorkflowsBoundaryTests : IDisposable
{
    private const string Unit = "quest:q7";

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workflows-boundary", Guid.NewGuid().ToString("N")[..8]);

    public WorkflowsBoundaryTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    [Fact]
    public void A_performed_node_is_started_and_reached_by_the_one_lifecycle_and_its_record_is_kept_whole()
    {
        var node = new NodeDeclaration("deploy", "acme.deploy/deploy", 1, NodeReach.Performed)
        {
            Retry = new NodeRetry(3, [30, 60]),
            Perform = new NodePerform(Idempotent: true, Reconcile: false, Transient: ["unavailable"]),
        };
        var binding = new NodeAcceptance(Unit, "tip-1", "graph-1", "deploy", 1, 1);

        var ready = new NodeStatus(Unit, "deploy", NodeStates.Ready);
        var started = NodeLifecycle.Apply(node, ready, new NodeMove(NodeStates.Active, NodeCause.Daoris) { Binding = binding, Scope = new NodeScope("graph-1", "run-1") });
        Assert.Null(started.Refused);
        Assert.Equal((NodeStates.Active, 1), (started.Status.State, started.Status.Attempt));

        // Daoris never reports an outcome: the table refuses it, and the plugin's report reaches.
        var claimed = NodeLifecycle.Apply(node, started.Status, new NodeMove(NodeStates.Reached, NodeCause.Daoris) { Source = NodeSource.Plugin, Attempt = 1 });
        Assert.Equal(NodeRefusal.NotCause, claimed.Refused);
        var evidence = binding with { Evidence = new NodePort("acme/receipt@1", "deploy#1", "tip-1", null) };
        var reached = NodeLifecycle.Apply(node, started.Status, new NodeMove(NodeStates.Reached, NodeCause.Authority) { Source = NodeSource.Plugin, Attempt = 1, Acceptance = evidence });
        Assert.Null(reached.Refused);
        Assert.Equal(NodeStates.Reached, reached.Status.State);

        var files = new NodeRecordFiles(_home, () => true);
        var written = new NodeRecord(Unit) { Statuses = new Dictionary<string, NodeStatus> { ["deploy"] = reached.Status } };
        files.Write(written);

        var read = new NodeRecordFiles(_home, () => true).Read(Unit)!;
        Assert.Equal(NodeRecordFiles.ToJson(written), NodeRecordFiles.ToJson(read));
        Assert.Equal(NodeStates.Reached, read.Statuses["deploy"].State);
        Assert.Equal(written.Statuses["deploy"].Acceptance!.Id, read.Statuses["deploy"].Acceptance!.Id);
        Assert.True(written.Statuses["deploy"].Acceptance!.SameResult(read.Statuses["deploy"].Acceptance!));
    }

    [Fact]
    public void A_chat_run_is_a_chat_in_a_tree_of_its_own_and_never_a_conversation_of_Ask_Daoris()
    {
        // The room is told by the participants' work rule's spelling, which is the Help room's.
        Assert.Equal(HelpRoom.Repository, ParticipantWork.HelpRepository);
        TracedSession Chat(string id, string repository) =>
            new(id, repository, "working") { Kind = "chat", Tree = "trees/default/engine/s-c1c1c1c1", Entry = $"session:{id}/{repository}" };
        IReadOnlyList<TracedSession> sessions = [Chat("c1c1c1c1", "engine"), Chat("a1a1a1a1", "daoris:help"), Chat("a2a2a2a2", "Daoris:Help")];

        Assert.Equal("chat-c1c1c1c1", ChatRuns.ChatOf(sessions[0], sessions, _ => true));
        Assert.Null(ChatRuns.ChatOf(sessions[1], sessions, _ => true));
        Assert.Null(ChatRuns.ChatOf(sessions[2], sessions, _ => true));
        Assert.Null(ChatRuns.ChatOf(sessions[0], sessions, _ => false));

        var records = JsonSerializer.Serialize(new[]
        {
            new { id = "c1c1c1c1", repository = "engine", state = "running", kind = "chat", tree = "trees/default/engine/s-c1c1c1c1" },
            new { id = "a1a1a1a1", repository = "daoris:help", state = "running", kind = "chat", tree = "trees/default/engine/s-a1a1a1a1" },
        });
        Assert.Equal("chat-c1c1c1c1", ChatRuns.ChatOf("c1c1c1c1", records, _ => true));
        Assert.Null(ChatRuns.ChatOf("a1a1a1a1", records, _ => true));
    }

    [Fact]
    public void A_workflow_run_read_of_a_session_in_Ask_Daoris_s_room_says_there_is_no_run_and_why()
    {
        IReadOnlyList<TracedSession> sessions =
        [
            new("h1", "daoris:help", "working") { Kind = "chat", Entry = "session:h1/daoris:help" },
            new("h2", "Daoris:Help", "working") { Kind = "chat", Entry = "session:h2/daoris:help" },
            new("c1", "engine", "working") { Kind = "chat", Tree = "trees/default/engine/s-c1c1c1c1", Entry = "session:c1/engine" },
        ];
        var trees = new WorkflowRunTests.Trees(holds: tree => tree.StartsWith("trees/", StringComparison.Ordinal));

        foreach (var id in new[] { "h1", "h2" })
        {
            var read = WorkflowRunReader.ChainsOf(new(Session: id), [], sessions, trees);
            Assert.Equal((WorkflowNoRun.AskDaoris, WorkflowNoRun.Said(WorkflowNoRun.AskDaoris, null)), (read.Code, read.Problem));
            Assert.Empty(read.Chains);
        }

        // A chat in a tree of its own beside it reads its chat run.
        Assert.Equal("c1", WorkflowRunReader.ChainsOf(new(Session: "c1"), [], sessions, trees).Chat?.Id);
    }
}
