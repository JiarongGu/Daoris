namespace Daoris.Driver;

/// <summary>What a <see cref="DesktopStore"/> is opened with: who it is, how it learns the start is settled, and the steps it migrates by.</summary>
public sealed record DesktopStoreOptions
{
    /// <summary>What this process is, written with its run: <c>shell</c>, <c>host</c> or <c>verb</c>.</summary>
    public string Kind { get; init; } = "verb";

    /// <summary>
    /// Whether the start's confirmation is settled, asked at each import and each stamp-raising migration, never earlier. A store
    /// handed none holds both: it is not settled until a reading says it is.
    /// </summary>
    public Func<StoreSettled>? Settled { get; init; }

    /// <summary>The migrations, contiguous from 1; null for the build's own (<see cref="DesktopStore.Steps"/>).</summary>
    public IReadOnlyList<StoreMigration>? Steps { get; init; }

    /// <summary>The writer stamp this build writes with: a store stamped past it is refused for writes.</summary>
    public int Writer { get; init; } = DesktopStore.WriterStamp;

    /// <summary>
    /// The run this process writes under, for a caller that opens the store for one use and closes it: every open of one process names the
    /// same run, so <c>store_runs</c> holds one row for the process however often it opens. Null draws a run of its own.
    /// </summary>
    public string? Run { get; init; }

    /// <summary>The build writing, kept on the store's version row and on each import; null for the assembly's own version.</summary>
    public string? Build { get; init; }

    /// <summary>The clock of every moment written.</summary>
    public TimeProvider? Clock { get; init; }
}
