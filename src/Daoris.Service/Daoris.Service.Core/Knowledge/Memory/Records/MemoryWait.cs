namespace Daoris.Knowledge;

/// <summary>
/// What waits on the person in memory, since the oldest: for a session, ends and replacements awaiting and the staged rows of a stopped
/// one; for an end the sweep wrote, which has no session, that one end, kept by <paramref name="End"/>.
/// </summary>
public sealed record MemoryWait(string? Session, int Ends, int Replacements, int Staged, DateTimeOffset Since, MemoryEndWait? End = null);

/// <summary>A sweep's end awaiting the person: the row to decide, the entry it ends at the revision the sweep read, and why.</summary>
public sealed record MemoryEndWait(string Op, string Repository, string Target, long? Revision, string? Reason);
