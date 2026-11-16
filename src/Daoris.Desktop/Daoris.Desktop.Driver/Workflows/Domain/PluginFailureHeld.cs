namespace Daoris.Driver;

/// <summary>
/// A failed plugin step's newest attempt as the run's view holds it for the person: a failure kept for them, which their
/// <c>again</c> on a held attempt (<c>WORK_HELD</c>) tries anew, the one way a failed step is tried again. Their word, once given,
/// is its resolution until the next pass applies it.
/// </summary>
internal static class PluginFailureHeld
{
    /// <remarks>
    /// A failure kept for the person offers <i>Try again</i> and <i>Done outside Daoris</i>; a step whose attempt is under way or held offers
    /// <i>Done outside Daoris</i> alone (DONEBYHOLDER1). The word kept reads <c>done-outside</c> where it carries its link.
    /// </remarks>
    public static WorkflowRunHeld? Of(NodeRecord? record, NodeDeclaration node, NodeStatus status)
    {
        if (record is null || NodeKit.Of(node.Kind) is not PluginStep || record.Newest(node.Id) is not { } attempt) return null;
        var word = attempt.Link is not null ? HeldResolutions.DoneOutside : attempt.Resolution;
        if (status.State == NodeStates.Failed && WorkPerformer.KeptFailure(record, attempt))
        {
            return new WorkflowRunHeld(record.Unit, attempt.Attempt, word is null ? [.. HeldResolutions.Failure, HeldResolutions.DoneOutside] : []) { Resolution = word };
        }

        return StandDowns.Outside(record, attempt)
            ? new WorkflowRunHeld(record.Unit, attempt.Attempt, word is null ? [HeldResolutions.DoneOutside] : []) { Resolution = word }
            : null;
    }
}
