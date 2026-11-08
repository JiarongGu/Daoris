namespace Daoris.Contracts.Sessions;

/// <summary>A sweep's end awaiting the person: the row to decide, the entry it ends at the revision it read, and why.</summary>
public sealed record MemoryEndWaitResponse(string Op, string Repository, string Target, long? Revision, string? Reason);
