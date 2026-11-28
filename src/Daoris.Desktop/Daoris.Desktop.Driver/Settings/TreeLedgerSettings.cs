namespace Daoris.Driver;

/// <summary>The copies' floor and budget in GB (<see cref="TreeLedgerSettings"/>), kept in a part of their own beside the record's.</summary>
public sealed partial record DriverConfig
{
    /// <summary>The copies' drive floor in GB; null is <see cref="TreeLedgerSettings.DefaultFloorGb"/>.</summary>
    public int? TreeFloorGb { get; init; }

    /// <summary>The most a repository's copies may hold, in GB; null is none.</summary>
    public int? TreeBudgetGb { get; init; }
}

/// <summary>
/// The copies' floor and budget on this machine, in <c>driver.json</c>: <c>treeFloorGb</c>, the free space under which the ledger's
/// reading writes its floor notice, 30 GB where absent; <c>treeBudgetGb</c>, the most a repository's copies may hold before it writes its
/// budget notice, none where absent. Whole GB from 1 to 4096; a value outside reads as absent. Written only when set. A repository's own
/// file narrows the budget once the keeper reads one.
/// </summary>
public static class TreeLedgerSettings
{
    public const string FloorFile = "treeFloorGb";

    public const string BudgetFile = "treeBudgetGb";

    public const int DefaultFloorGb = 30;

    private const long Gb = 1L << 30;

    /// <summary>A stored value as it reads: a whole number of GB in range, else null.</summary>
    public static int? Read(long? stored) => stored is >= 1 and <= 4096 ? (int)stored.Value : null;

    public static long FloorBytes(DriverConfig config) => (config.TreeFloorGb ?? DefaultFloorGb) * Gb;

    public static long? BudgetBytes(DriverConfig config) => config.TreeBudgetGb * Gb;
}
