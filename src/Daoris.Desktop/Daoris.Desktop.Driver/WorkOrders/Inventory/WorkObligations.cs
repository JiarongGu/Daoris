namespace Daoris.Driver;

/// <summary>
/// What a unit still owes beyond its branch records (FABRIC-PERFORM1d; the fabric events design §D3): each receipt its plugins' attempts
/// gave, with its disposition, and each dispatched attempt whose outcome is not settled. Read from the units' node and resource records,
/// derived as the rest of the inventory is, and joined to the unit of the same id by every adapter, so a unit retires only once its end is
/// over, every entry is terminal and no attempt is unresolved (<see cref="WorkUnit.State"/>). Read-only.
/// </summary>
public static class WorkObligations
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<InventoryEntry>> None = new Dictionary<string, IReadOnlyList<InventoryEntry>>();

    /// <summary>Every unit's obligations under <paramref name="home"/>, by unit id; none where neither folder holds a record.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<InventoryEntry>> Read(string home, DateTimeOffset now)
    {
        if (!Directory.Exists(NodeRecordFiles.FolderOf(home)) && !Directory.Exists(ResourceRecordFiles.FolderOf(home))) return None;
        return Of(new NodeRecordFiles(home, () => false).All().Records, new ResourceRecordFiles(home, () => false).All().Records, now);
    }

    /// <summary>The obligations the records name, by unit id: each receipt, then each attempt not settled.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<InventoryEntry>> Of(
        IReadOnlyList<NodeRecord> nodes, IReadOnlyList<ResourceRecord> resources, DateTimeOffset now)
    {
        var owed = new Dictionary<string, List<InventoryEntry>>(StringComparer.Ordinal);
        List<InventoryEntry> Of(string unit) => owed.TryGetValue(unit, out var list) ? list : owed[unit] = [];

        foreach (var record in resources)
        {
            foreach (var receipt in record.Receipts) Of(record.Unit).Add(Entry(record.Unit, receipt, now));
        }

        foreach (var record in nodes)
        {
            foreach (var attempt in record.Attempts.Where(attempt => attempt.State != AttemptStates.Settled)) Of(record.Unit).Add(Entry(record.Unit, attempt));
        }

        return owed.ToDictionary(each => each.Key, each => (IReadOnlyList<InventoryEntry>)each.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// The units that still owe anything (§D3, canary 35): a receipt not ended (unsettled and not a result, or of an undeclared schema), or an
    /// attempt not settled. What the traces' pruning never drops, whatever their branch's removal said. A record that does not read keeps the
    /// unit it still names (<see cref="NodeRecordFiles.Unread"/>): what it holds is not known.
    /// </summary>
    public static IReadOnlySet<string> Owing(string home)
    {
        var owing = new HashSet<string>(StringComparer.Ordinal);
        if (Directory.Exists(NodeRecordFiles.FolderOf(home)))
        {
            var nodes = new NodeRecordFiles(home, () => false);
            foreach (var record in nodes.All().Records.Where(record => record.Attempts.Any(attempt => attempt.State != AttemptStates.Settled))) owing.Add(record.Unit);
            foreach (var unread in nodes.Unread()) if (unread.Unit is { } unit) owing.Add(unit);
        }

        if (Directory.Exists(ResourceRecordFiles.FolderOf(home)))
        {
            foreach (var record in new ResourceRecordFiles(home, () => false).All().Records.Where(record => record.Receipts.Any(Owes))) owing.Add(record.Unit);
        }

        return owing;
    }

    /// <summary>Each unit with its obligations added to its entries, by its id; a unit that owes nothing is as it was.</summary>
    public static IReadOnlyList<WorkUnit> Attach(IReadOnlyList<WorkUnit> units, IReadOnlyDictionary<string, IReadOnlyList<InventoryEntry>> owed) =>
        owed.Count == 0 ? units
        : [.. units.Select(unit => owed.TryGetValue(unit.Id, out var entries) ? unit with { Entries = [.. unit.Entries, .. entries] } : unit)];

    /// <summary>Whether a receipt keeps its unit owing: not settled, and not a result kept as one.</summary>
    private static bool Owes(ResourceReceipt receipt) => !receipt.Settled && receipt.Effective != PluginResourceRoles.Result;

    /// <summary>
    /// A receipt as an inventory entry (§D3's dispositions): removed or already gone with its proof, retained with the plugin's reason, a
    /// result kept, a cleanup pending with its tries, or blocked: an undeclared schema, or a cleanup failing for seven days, with the plugin's
    /// words. It never disappears.
    /// </summary>
    private static InventoryEntry Entry(string unit, ResourceReceipt receipt, DateTimeOffset now)
    {
        var name = $"{receipt.Schema}:{receipt.Resource}";
        var role = receipt.Effective == PluginResourceRoles.Result ? InventoryRole.Result : InventoryRole.Temporary;
        var by = $"`{receipt.Plugin}`";
        if (receipt.Role == ResourceReceipt.Undeclared)
        {
            return new InventoryEntry(unit, InventoryKind.PluginResource, name, InventoryRole.Temporary, InventoryDisposition.Blocked,
                $"{by} declares no resource `{receipt.Schema}`, so nothing says who removes it: it is never cleaned by itself")
            {
                Code = InventoryCode.Undeclared,
                Facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["plugin"] = receipt.Plugin, ["schema"] = receipt.Schema },
            };
        }

        if (receipt.Cleanup is { State: CleanupStates.Settled } settled)
        {
            var at = WorkInventory.Minute(settled.SettledAt ?? settled.At);
            return settled.Disposition == CleanupDispositions.Retained
                ? new InventoryEntry(unit, InventoryKind.PluginResource, name, role, InventoryDisposition.Retained,
                    $"{by} keeps it, it said {at} UTC" + (settled.Message is { Length: > 0 } why ? $": {why}" : ""))
                : new InventoryEntry(unit, InventoryKind.PluginResource, name, role, InventoryDisposition.Removed,
                    settled.Disposition == CleanupDispositions.AlreadyGone ? $"{by} had already removed it, it said {at} UTC" : $"{by} removed it {at} UTC")
                {
                    Proof = settled.Proof,
                };
        }

        if (role == InventoryRole.Result)
        {
            return new InventoryEntry(unit, InventoryKind.PluginResource, name, role, InventoryDisposition.Kept,
                $"kept as the work's result; only {by} removes it, through its cleanup point");
        }

        if (receipt.Cleanup is { } asked)
        {
            var words = asked.Message is { Length: > 0 } message ? $": {message}" : "";
            if (ResourceCleaner.Blocked(receipt, now))
            {
                return new InventoryEntry(unit, InventoryKind.PluginResource, name, role, InventoryDisposition.Blocked,
                    $"{by} was asked to remove it from {WorkInventory.Minute(asked.At)} UTC and has not, after {asked.Tries} tries{words}; it is asked again daily")
                {
                    Code = InventoryCode.CleanupBlocked,
                    Facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["plugin"] = receipt.Plugin, ["tries"] = $"{asked.Tries}" },
                };
            }

            return new InventoryEntry(unit, InventoryKind.PluginResource, name, role, InventoryDisposition.Pending,
                asked.NextAt is { } next
                    ? $"{by} was asked to remove it and has not yet, after {asked.Tries} tries{words}; asked again from {WorkInventory.Minute(next)} UTC"
                    : $"{by} is asked to remove it");
        }

        return new InventoryEntry(unit, InventoryKind.PluginResource, name, role, InventoryDisposition.Pending,
            receipt.Superseded
                ? $"its attempt was superseded, so it has no result: {by} is asked to remove it at once"
                : $"{by} is asked to remove it once the work ends");
    }

    /// <summary>An attempt not settled: pending while Daoris establishes its outcome, blocked while it is held for the person.</summary>
    private static InventoryEntry Entry(string unit, NodeAttempt attempt)
    {
        var name = $"{attempt.Node} #{attempt.Attempt}";
        return attempt.State == AttemptStates.Held
            ? new InventoryEntry(unit, InventoryKind.Attempt, name, InventoryRole.Temporary, InventoryDisposition.Blocked,
                $"its outcome is held for you ({attempt.Code ?? "held"}): nothing establishes it by itself, so the work does not retire until you settle it")
            {
                Code = InventoryCode.AttemptHeld,
                Facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["node"] = attempt.Node },
            }
            : new InventoryEntry(unit, InventoryKind.Attempt, name, InventoryRole.Temporary, InventoryDisposition.Pending,
                $"its outcome is not settled yet ({attempt.State}), so the work does not retire until it is");
    }
}
