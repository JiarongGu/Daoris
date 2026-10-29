namespace Daoris.Driver;

/// <summary>
/// What a unit still owes beyond its branch records (FABRIC-PERFORM1d; the fabric events design §D3): each receipt its plugins' attempts
/// gave, with its disposition, and each dispatched attempt whose outcome is not settled. Read from the units' node and resource records by
/// Workflows, which owns them (<see cref="WorkflowObligations"/>, installed as <see cref="IWorkOrdersWorkflows"/>), derived as the rest of the
/// inventory is, and joined to the unit of the same id by every adapter, so a unit retires only once its end is over, every entry is terminal
/// and no attempt is unresolved (<see cref="WorkUnit.State"/>). Read-only.
/// </summary>
public static class WorkObligations
{
    /// <summary>Every unit's obligations under <paramref name="home"/>, by unit id; none where neither folder holds a record.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<InventoryEntry>> Read(string home, DateTimeOffset now) =>
        WorkOrdersWorkflows.Current.Obligations(home, now);

    /// <summary>
    /// The units that still owe anything (§D3, canary 35): a receipt not ended, or an attempt not settled. What the traces' pruning never
    /// drops, whatever their branch's removal said. A record that does not read keeps the unit it still names: what it holds is not known.
    /// </summary>
    public static IReadOnlySet<string> Owing(string home) => WorkOrdersWorkflows.Current.Owing(home);

    /// <summary>Each unit with its obligations added to its entries, by its id; a unit that owes nothing is as it was.</summary>
    public static IReadOnlyList<WorkUnit> Attach(IReadOnlyList<WorkUnit> units, IReadOnlyDictionary<string, IReadOnlyList<InventoryEntry>> owed) =>
        owed.Count == 0 ? units
        : [.. units.Select(unit => owed.TryGetValue(unit.Id, out var entries) ? unit with { Entries = [.. unit.Entries, .. entries] } : unit)];
}
