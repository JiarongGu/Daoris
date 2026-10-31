namespace Daoris.Knowledge;

/// <summary>A reference a session read, at the revision it read (the read set is the op store's, the memory coordinator design §3).</summary>
public sealed record MemoryRead(MemoryReference Reference, long Revision);
