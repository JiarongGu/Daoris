namespace Daoris.Driver;

/// <summary>
/// The repair a run's review Accept carries: kept on the run's record beside the review's word. The performer keeps it when its landing is refused for work the
/// session can clear; the pass accepts the review again at the new tip where the tip moved only by the session's own commits.
/// </summary>
public static class RunRepairs
{
    /// <summary>The repair kept for <paramref name="node"/> of <paramref name="unit"/>, or null.</summary>
    public static NodeRepair? Of(NodeRecordFiles records, string unit, string node) =>
        records.Read(unit)?.Repairs.FirstOrDefault(each => each.Node == node);

    /// <summary>Keeps <paramref name="repair"/>, replacing any the node held; false where the record could not be written.</summary>
    public static bool Keep(NodeRecordFiles records, string unit, NodeRepair repair) =>
        records.Edit(unit, record => (record with { Repairs = [.. record.Repairs.Where(each => each.Node != repair.Node), repair] }, true)).Refused is null;

    /// <summary>Takes the node's repair back: a stop, a decline, the bound, or the repair spent.</summary>
    public static void Take(NodeRecordFiles records, string unit, string node)
    {
        if (Of(records, unit, node) is null) return;
        records.Edit(unit, record => (record with { Repairs = [.. record.Repairs.Where(each => each.Node != node)] }, true));
    }

    /// <summary>Whether <paramref name="tip"/> descends from the tip the Accept is given at, so the commits since are the session's own; false where git cannot say.</summary>
    public static async Task<bool> ContinuesAsync(string root, NodeRepair repair, string tip, CancellationToken ct)
    {
        if (string.Equals(repair.Tip, tip, StringComparison.OrdinalIgnoreCase)) return false;
        return await MachinePorts.Repository.IsAncestorAsync(root, repair.Tip, tip, ct).ConfigureAwait(false) == true;
    }
}
