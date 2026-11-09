using static Daoris.Driver.WorkPerformer;

namespace Daoris.Driver;

/// <summary>The receipts an attempt's completion or answer names, taken on its unit's resource record before the outcome is applied, and marked as it is judged.</summary>
internal sealed class WorkReceipting(
    NodeRecordFiles records, Func<DateTimeOffset> clock, Func<ResourceRecordFiles?> receipts, Func<string, IReadOnlyList<PluginResource>?> declaredResources)
{
    private ResourceRecordFiles? Receipts => receipts();

    /// <summary>
    /// An attempt's receipts taken on its unit's resource record before its outcome is applied: each resource its
    /// plugin names, once, with the role its manifest declares, and marked superseded where its node has already moved off the attempt (a
    /// guess the outcome's own judgement corrects, <see cref="Supersede"/>). Null where they are taken or none is named; else what came of
    /// the completion, which is then not applied: held for the person where the record would keep too many, not taken where the record
    /// could not be written.
    /// </summary>
    internal WorkPerformed? Receipted(string unit, NodeAttempt attempt, NodeDeclaration node, IReadOnlyList<WorkResource> resources, bool lapsed)
    {
        if (resources.Count == 0 || Receipts is null) return null;
        var unsettled = attempt.State != AttemptStates.Settled;
        var superseded = unsettled && (lapsed || records.Read(unit) is not { } now || !OnAttempt(StatusOf(now, node.Id), attempt));
        var declared = declaredResources(attempt.Plugin) ?? [];
        var at = clock();
        var (refused, code) = Receipts.Edit<string?>(unit, record =>
        {
            var fresh = resources.Where(resource => !record.Gave(attempt.Plugin, resource.Schema, resource.Id)).ToList();
            if (fresh.Count == 0) return (null, null);
            var owing = record.Receipts.Count(receipt => !receipt.Settled);
            if (owing + fresh.Count > ResourceRecord.ReceiptsKept) return (null, TooManyResources);
            var taken = fresh.Select(resource => new ResourceReceipt(
                Random("r-", 12), attempt.Plugin, resource.Schema, resource.Id,
                declared.FirstOrDefault(each => each.Schema == resource.Schema)?.Role ?? ResourceReceipt.Undeclared,
                attempt.Node, attempt.Attempt, attempt.Call.Invocation, at) { Superseded = superseded });
            var dropped = record.Receipts.Where(receipt => receipt.Settled).Take(Math.Max(0, record.Receipts.Count + fresh.Count - ResourceRecord.ReceiptsKept)).ToList();
            var kept = record.Receipts.Where(receipt => !dropped.Contains(receipt));
            var guard = record.Dropped.Concat(dropped.Select(receipt => new ResourceKey(receipt.Plugin, receipt.Schema, receipt.Resource)));
            return (record with { Receipts = [.. kept, .. taken], Dropped = [.. guard.TakeLast(ResourceRecord.GuardKept)] }, null);
        });
        if ((refused ?? code) is not { } why) return null;
        if (why == TooManyResources && unsettled)
        {
            return records.Edited(unit, node.Id, record => record.Invoked(attempt.Call.Invocation) is { State: not AttemptStates.Settled } current
                ? Hold(record, current, TooManyResources)
                : (null, new WorkPerformed(unit, node.Id, PerformedWhat.NotTaken, TooManyResources)));
        }

        return new(unit, node.Id, PerformedWhat.NotTaken, why);
    }

    /// <summary>
    /// The attempt's receipts marked as its outcome is judged: superseded where it came as a stale attempt's, which has
    /// no result, so what it made is temporary and cleaned at once; else as its declared role. Only an attempt that is not settled before:
    /// a replay changes nothing it already said.
    /// </summary>
    internal void Supersede(string unit, NodeAttempt attempt, IReadOnlyList<WorkResource> resources, WorkPerformed applied)
    {
        if (resources.Count == 0 || Receipts is null || attempt.State == AttemptStates.Settled) return;
        if (applied.What is not (PerformedWhat.Applied or PerformedWhat.Stale)) return;
        var stale = applied.What == PerformedWhat.Stale;
        Receipts.Edit<bool>(unit, record =>
        {
            var changed = false;
            var receipts = record.Receipts.Select(receipt =>
            {
                if (receipt.Invocation != attempt.Call.Invocation || receipt.Superseded == stale || receipt.Settled) return receipt;
                changed = true;
                return receipt with { Superseded = stale };
            }).ToList();
            return (changed ? record with { Receipts = receipts } : null, changed);
        });
    }

    /// <summary>Whether a node still stands on an attempt: its current one, active or unknown, so its outcome is the node's own.</summary>
    private static bool OnAttempt(NodeStatus status, NodeAttempt attempt) =>
        status.Attempt == attempt.Attempt && status.State is NodeStates.Active or NodeStates.Unknown;
}
