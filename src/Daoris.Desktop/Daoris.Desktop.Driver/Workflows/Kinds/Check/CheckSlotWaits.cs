using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// What a run's sent check waits for while the heavy executor has not started it: the machine's one check slot (the home's verification
/// slot, <see cref="VerifySlot"/>), held by another run's check since a time, else its place in the slot's queue. Read from the slot's files
/// at each look and never stored, so a check whose gates run, one settled, or one not sent waits on no slot.
/// </summary>
public static class CheckSlotWaits
{
    /// <summary>The wait's code where another run's check holds the slot: its values name that run and this check's place in the queue.</summary>
    public const string HeldBy = "slot";

    /// <summary>The wait's code where no check holds the slot now (another repository's heavy run, or the next look takes it): its place in the queue.</summary>
    public const string Queued = "slot-queued";

    /// <summary>The slot's waits of a run's graph, read from the home's slot only where a check of its record is under way.</summary>
    public static IReadOnlyDictionary<string, WorkflowRunWait> Read(string home, NodeGraph graph, NodeRecord? record) =>
        record is not null && graph.Nodes.Any(node => node.Kind == NodeKinds.Check && record.Statuses.GetValueOrDefault(node.Id)?.State == NodeStates.Active)
            ? Of(graph, record, VerifySlot.Look(home))
            : new Dictionary<string, WorkflowRunWait>(StringComparer.Ordinal);

    /// <summary>The wait of each active check of <paramref name="record"/> whose attempt out now is among the slot's waiters, by node.</summary>
    public static IReadOnlyDictionary<string, WorkflowRunWait> Of(NodeGraph graph, NodeRecord? record, VerifyLook look)
    {
        var waits = new Dictionary<string, WorkflowRunWait>(StringComparer.Ordinal);
        if (record is null || look.Waiters.Count == 0) return waits;
        foreach (var node in graph.Nodes.Where(each => each.Kind == NodeKinds.Check))
        {
            var status = WorkPerformer.StatusOf(record, node.Id);
            if (status.State != NodeStates.Active || record.Newest(node.Id) is not { State: AttemptStates.Recorded or AttemptStates.Sent } attempt
                || attempt.Attempt != status.Attempt)
            {
                continue;
            }

            var at = look.Waiters.Select((waiter, index) => (waiter, index)).FirstOrDefault(each => each.waiter.Invocation == attempt.Call.Invocation);
            if (at.waiter is null) continue;
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["position"] = (at.index + 1).ToString(CultureInfo.InvariantCulture), ["of"] = look.Waiters.Count.ToString(CultureInfo.InvariantCulture),
            };
            if (look.Holder is { } holder)
            {
                values["run"] = holder.Run;
                values["heldSince"] = holder.Since.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            }

            waits[node.Id] = new WorkflowRunWait(look.Holder is null ? Queued : HeldBy, values, at.waiter.Since);
        }

        return waits;
    }

    /// <summary>The terminal's words for a slot's wait: the run holding it and since when, and the check's place in the queue.</summary>
    public static string Said(WorkflowRunWait wait)
    {
        var queue = $"{wait.Values.GetValueOrDefault("position") ?? "?"} of {wait.Values.GetValueOrDefault("of") ?? "?"} in its queue";
        if (wait.Code != HeldBy) return $"Waits for the machine's check slot: {queue}.";
        var since = DateTimeOffset.TryParse(wait.Values.GetValueOrDefault("heldSince"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? $" since {at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)}"
            : "";
        return $"Waits for the machine's check slot, held by run {wait.Values.GetValueOrDefault("run")}'s check{since}: {queue}.";
    }
}
