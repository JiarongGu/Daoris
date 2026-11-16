using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

// The one table's rows as the canary asks them: each row's move as its cause would make it, and what a kind's authority forbids.
public sealed partial class NodeLifecycleTests
{
    /// <summary>Every built-in kind, and a plugin's own node: the same table for each.</summary>
    public static TheoryData<string> Kinds() => new([.. NodeKit.Kinds, "acme.deploy/deploy"]);
    /// <summary>The move a row asks for, as its cause would make it.</summary>
    private static NodeMove MoveFor(NodeTransition row, NodeAuthority authority, NodeStatus status) => (row.To, row.By) switch
    {
        // The person settles a held attempt on their own word, their evidence the person's.
        (NodeStates.Reached, NodeCause.Person) => new NodeMove(row.To, row.By) { Source = NodeSource.Person, Attempt = status.Attempt, Acceptance = OnWord(status.Attempt) },
        (NodeStates.Failed, NodeCause.Person) => new NodeMove(row.To, row.By) { Source = NodeSource.Person, Attempt = status.Attempt, Code = HeldResolutions.DidNot },
        (NodeStates.Reached, _) => new NodeMove(row.To, row.By) { Source = authority.ReachBy, Attempt = status.Attempt, Acceptance = Accept(status.Attempt) },
        (NodeStates.Failed, _) => new NodeMove(row.To, row.By)
        {
            Source = authority.FailureBy ?? NodeSource.Plugin, Attempt = status.Attempt, Code = "deploy-failed", Transient = true,
        },
        (NodeStates.Rejected, _) => new NodeMove(row.To, row.By) { Attempt = status.Attempt, Words = "not what was asked" },
        // A record's skip names its source and the attempt, never the person's words.
        (NodeStates.Skipped, NodeCause.Authority) => new NodeMove(row.To, row.By) { Source = authority.SkipBy ?? authority.ReachBy, Attempt = status.Attempt, Scope = Run },
        // A path's skip; a decision's pass alone rewords a skip.
        (NodeStates.Skipped, NodeCause.Daoris) => new NodeMove(row.To, row.By)
        {
            Source = row.From == NodeStates.Skipped ? NodeSource.Decision : NodeSource.Path, Words = "w", Scope = Run,
        },
        (NodeStates.Skipped, _) => new NodeMove(row.To, row.By) { Words = "not what was asked", Scope = Run },
        (NodeStates.Unknown, _) => new NodeMove(row.To, row.By) { Attempt = status.Attempt },
        (NodeStates.Active, _) => new NodeMove(row.To, row.By) { Binding = Bind(status.Attempt + 1), Scope = Run },
        _ => new NodeMove(row.To, row.By),
    };

    /// <summary>What a kind's authority forbids of a row, and nothing else: a person's judgement, a failure, a skip, a decision's answer again.</summary>
    private static string? Forbidden(NodeTransition row, NodeAuthority authority) => row.To switch
    {
        // A failed node is gone on without, by the person, for a request alone.
        NodeStates.Skipped when row is { From: NodeStates.Failed, By: NodeCause.Person } => authority.Kind == NodeKinds.Request ? null : NodeRefusal.NotSkippable,
        NodeStates.Reached or NodeStates.Failed when row.By == NodeCause.Person && !authority.PersonSettles => NodeRefusal.NotSettled,
        NodeStates.Reached when row.From == NodeStates.Reached && !authority.Reanswers => NodeRefusal.NotReanswered,
        NodeStates.Rejected when !authority.PersonJudges => NodeRefusal.NotJudged,
        NodeStates.Failed when authority.FailureBy is null => NodeRefusal.NoFailureAuthority,
        NodeStates.Skipped when row.By == NodeCause.Authority => authority.SkipBy is null ? NodeRefusal.NotAuthority : null,
        NodeStates.Skipped when row.By != NodeCause.Daoris && !authority.PersonSkips => NodeRefusal.NotSkippable,
        _ => null,
    };
}
