using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The bindings the one table's tests move on, and what a kind's authority forbids of each row.</summary>
internal static class NodeLifecycleRows
{
    public const string Unit = "unit-a";

    /// <summary>The binding an attempt starts on: what Daoris's evaluation hands the start.</summary>
    public static NodeAcceptance Bind(int attempt, string node = "n1") => new(Unit, "tip-1", "graph-1", node, 1, attempt);

    /// <summary>That binding reached, with its evidence.</summary>
    public static NodeAcceptance Accept(int attempt, string node = "n1") =>
        Bind(attempt, node) with { Evidence = new NodePort("acme/receipt@1", $"{node}#{attempt}", "tip-1", null) };

    /// <summary>That binding reached on the person's word: its evidence the person's, never a plugin's reference.</summary>
    public static NodeAcceptance OnWord(int attempt) =>
        Bind(attempt) with { Evidence = new NodePort(NodeSource.Person, $"person:n1#{attempt}", "tip-1", null) };

    /// <summary>What a kind's authority forbids of a row, and nothing else: a person's judgement, a failure, a skip, a decision's answer again.</summary>
    public static string? Forbidden(NodeTransition row, NodeAuthority authority) => row.To switch
    {
        // A failed node is gone on without, by the person, for a request alone.
        NodeStates.Skipped when row is { From: NodeStates.Failed, By: NodeCause.Person } => authority.Kind == NodeKinds.Request ? null : NodeRefusal.NotSkippable,
        NodeStates.Reached or NodeStates.Failed when row.By == NodeCause.Person && !authority.PersonSettles => NodeRefusal.NotSettled,
        NodeStates.Reached when row.From == NodeStates.Reached && !authority.Reanswers => NodeRefusal.NotReanswered,
        NodeStates.Rejected when !authority.PersonJudges => NodeRefusal.NotJudged,
        NodeStates.Failed when authority.FailureBy is null => NodeRefusal.NoFailureAuthority,
        NodeStates.Skipped when row is { From: NodeStates.Failed, By: NodeCause.Authority } =>
            authority.SkipBy is null || NodeKit.Of(authority.Kind) is not IObserves { PassesOverFailed: true } ? NodeRefusal.NotAuthority : null,
        NodeStates.Skipped when row.By == NodeCause.Authority => authority.SkipBy is null ? NodeRefusal.NotAuthority : null,
        NodeStates.Skipped when row.By != NodeCause.Daoris && !authority.PersonSkips => NodeRefusal.NotSkippable,
        _ => null,
    };
}
