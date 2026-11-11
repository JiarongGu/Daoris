namespace Daoris.Driver;

/// <summary>
/// A kind that writes the run's work where the person keeps it (<c>land</c>, <c>return</c>, <c>apply</c>): an effect, which comes after the
/// review where a graph holds one and is fed the record it accepted (<see cref="EffectOrder"/>). A plugin's step is none: it takes what its
/// wires feed it.
/// </summary>
internal interface IWritesWork;
