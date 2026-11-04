namespace Daoris.Knowledge;

/// <summary>What a session leaves for the person in memory (KNOWCENTRAL2e): ends and replacements awaiting, staged rows of a stopped session, since the oldest.</summary>
public sealed record MemoryWait(string Session, int Ends, int Replacements, int Staged, DateTimeOffset Since);
