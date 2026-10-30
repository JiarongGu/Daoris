using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>A store seeded as a write that reads no source makes it, for a case that needs entries held and no reading of its own.</summary>
internal static class StoreSeeding
{
    public static Task<bool> ReplaceRepositoryAsync(
        this IKnowledgeStore store, string repository, IReadOnlyList<KnowledgeEntry> entries, CancellationToken ct = default) =>
        store.ReplaceRepositoryAsync(repository, entries, ReadingWrite.Taken(DateTimeOffset.UnixEpoch), ct: ct);
}
