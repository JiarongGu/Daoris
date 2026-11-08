namespace Daoris.Knowledge;

/// <summary>A sweep's end awaiting the person: the row to decide, the entry it ends at the revision the sweep read, and why.</summary>
public sealed record MemoryEndWait(string Op, string Repository, string Target, long? Revision, string? Reason);
