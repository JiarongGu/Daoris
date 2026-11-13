namespace Daoris.Knowledge;

/// <summary>
/// What a scout read for one part, as the lead's plan carries it: the pointers a worker reads first and the words around them. The brief a
/// worker is handed is composed from it, never written by hand. <see cref="Pointers"/> hold the blob ids <see cref="PackStaleness"/> compares.
/// </summary>
public sealed record PartPack
{
    /// <summary>Where to read, each with the blob id read.</summary>
    public IReadOnlyList<PackPointer> Pointers { get; init; } = [];

    /// <summary>The tests the worker runs, each with its filter, as words.</summary>
    public IReadOnlyList<string> Tests { get; init; } = [];

    /// <summary>Words other suites read that the worker will not run.</summary>
    public IReadOnlyList<string> Words { get; init; } = [];

    /// <summary>Outputs a tool generates, which the integrator writes and no worker edits.</summary>
    public IReadOnlyList<string> Generated { get; init; } = [];

    /// <summary>Paths expected to be shared with another part.</summary>
    public IReadOnlyList<string> Shared { get; init; } = [];

    /// <summary>What the scout could not settle.</summary>
    public IReadOnlyList<string> Unknowns { get; init; } = [];
}
