namespace Daoris.Knowledge;

/// <summary>
/// What an operation asks: a write's text, or an end's reason; the entry it targets and the revision its session read; a relation's
/// source and kind. A write naming a relation and a target relates the entry it writes; its <see cref="Recheck"/> is the records it cites
/// (<see cref="MemoryCitations"/>), kept on the entry for the sweep. A replacement's <see cref="Predecessors"/> are the entries its
/// successor ends, each at the revision read.
/// </summary>
public sealed record MemoryChange(
    string Kind, string? Text = null, string? Target = null, long? Revision = null, string? Source = null, string? Relation = null,
    string? Recheck = null, IReadOnlyList<MemoryPredecessor>? Predecessors = null);
