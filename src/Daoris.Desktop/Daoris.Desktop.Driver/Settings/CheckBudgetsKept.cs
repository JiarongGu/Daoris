namespace Daoris.Driver;

/// <summary>A gate's green run kept: its wall time in milliseconds and when it ended.</summary>
public sealed record GreenRun(double Ms, string At);

/// <summary>A timeout the owner pinned over what the green times teach, with the owner's reason for it.</summary>
public sealed record GatePin(double Minutes, string Why);

/// <summary>One gate's record in the home: what the machine learned of it and what its door set (<see cref="CheckBudgets"/>).</summary>
/// <param name="Green">The newest green wall times, newest last.</param>
/// <param name="Said">The learned timeout last said, in minutes: a rise is judged against it.</param>
public sealed record GateKept(IReadOnlyList<GreenRun> Green, GatePin? Pin, double? MemoryGb, bool Alone, double? Said)
{
    /// <summary>A gate the machine has learned and set nothing of.</summary>
    public static GateKept None { get; } = new([], null, null, false, null);
}

/// <summary>A learned timeout that rose past the bound: the minutes last said and the minutes now.</summary>
public sealed record GateTimeoutRise(double From, double To);

/// <summary>A gate's timeout in minutes and where it comes from: <c>pin</c>, <c>learned</c> or the lane's <c>default</c>.</summary>
public sealed record GateTimeout(double Minutes, string Source, string? Why = null, GateTimeoutRise? Rise = null);

/// <summary>
/// One repository's record in the home. A member the home does not hold is null (a list the home does not hold is null, not
/// empty, since an empty list is the owner's word that there are none), so a reader falls back to what an older declaration says.
/// </summary>
/// <param name="Gates">By the gate's name.</param>
/// <param name="SetTimeoutMs">A test set's per-test timeout in milliseconds, by <c>&lt;gate&gt;/&lt;set&gt;</c>.</param>
public sealed record RepositoryBudgets(
    IReadOnlyDictionary<string, GateKept> Gates, IReadOnlyDictionary<string, long> SetTimeoutMs, string? TreeBaseline, string? Devkit,
    IReadOnlyList<string>? LocalInputs, string? PatternsFile)
{
    /// <summary>A read with no home, or nothing kept for the repository: the product's defaults.</summary>
    public static RepositoryBudgets None { get; } = new(
        new Dictionary<string, GateKept>(StringComparer.Ordinal), new Dictionary<string, long>(StringComparer.Ordinal), null, null, null, null);

    /// <summary>The gate's record, or nothing learned of it where the home holds none.</summary>
    public GateKept Gate(string name) => Gates.GetValueOrDefault(name) ?? GateKept.None;
}
