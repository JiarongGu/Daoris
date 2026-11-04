namespace Daoris.Knowledge;

/// <summary>A recheck the sweep read of a cited entry (§3): it holds at <paramref name="Revision"/>, or failed for <paramref name="Reason"/>.</summary>
public sealed record MemoryRecheck(string Repository, string Entry, long Revision, bool Holds, string? Reason = null);
