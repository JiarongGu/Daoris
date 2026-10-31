namespace Daoris.Knowledge;

/// <summary>
/// Where a reader of the memory's moves stands (the memory coordinator design §3): the journal's store and position, and the scope it read,
/// the repositories of its workspace.
/// </summary>
public sealed record MemoryCursor(string Store, long Position, string Scope);

/// <summary>A reader's start: the rows not yet settled in its scope, and the cursor read with them.</summary>
public sealed record MemoryBootstrap(MemoryCursor Cursor, IReadOnlyList<MemoryOp> Unsettled);

/// <summary>
/// The entries whose rows moved past a cursor, an unsettled move included, and the cursor after them; or <see cref="Revalidate"/>, where
/// the cursor no longer reads this journal whole and the reader bootstraps again.
/// </summary>
public sealed record MemoryChanges(bool Revalidate, MemoryCursor Cursor, IReadOnlyList<MemoryReference> Changed);
