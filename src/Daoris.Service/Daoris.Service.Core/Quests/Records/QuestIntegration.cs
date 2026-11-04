namespace Daoris.Knowledge;

/// <param name="Cursor">The number this machine has now fetched through.</param>
/// <param name="Conflicts">The moves the rebase turned into conflicts, as they now stand in the log.</param>
public sealed record QuestIntegration(long Cursor, IReadOnlyList<QuestOperation> Conflicts);
