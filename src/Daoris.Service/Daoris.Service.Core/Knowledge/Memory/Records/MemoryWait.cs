namespace Daoris.Knowledge;

/// <summary>What waits on the person in memory, since the oldest: a session's ends, replacements and staged rows, or, by <paramref name="End"/>, one end the sweep wrote.</summary>
public sealed record MemoryWait(string? Session, int Ends, int Replacements, int Staged, DateTimeOffset Since, MemoryEndWait? End = null);
