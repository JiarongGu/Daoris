namespace Daoris.Driver;

/// <summary>
/// A failed plugin step's newest attempt as the run's view holds it for the person: a failure kept for them, which their
/// <c>again</c> on a held attempt (<c>WORK_HELD</c>) tries anew, the one way a failed step is tried again. Their word, once given,
/// is its resolution until the next pass applies it.
/// </summary>
internal static class PluginFailureHeld
{
    public static WorkflowRunHeld? Of(NodeRecord? record, NodeDeclaration node, NodeStatus status)
    {
        if (record is null || status.State != NodeStates.Failed || NodeKit.Of(node.Kind) is not PluginStep) return null;
        if (record.Newest(node.Id) is not { } attempt || !WorkPerformer.KeptFailure(record, attempt)) return null;
        return new WorkflowRunHeld(record.Unit, attempt.Attempt, attempt.Resolution is null ? HeldResolutions.Failure : []) { Resolution = attempt.Resolution };
    }
}
