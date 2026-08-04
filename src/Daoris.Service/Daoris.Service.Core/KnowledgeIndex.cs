namespace Daoris.Knowledge;

/// <summary>
/// Reads a source into a store, applying the disclosure policy on the way in.
/// </summary>
/// <remarks>
/// The policy is applied at <b>ingest</b>, not at query time. Withholding at query time means the
/// material is already in the store and one forgotten filter discloses it; withholding at ingest
/// means it was never there to leak.
/// </remarks>
public sealed class KnowledgeIndex(IKnowledgeStore store, IDisclosurePolicy? disclosure = null)
{
    private readonly IDisclosurePolicy _disclosure = disclosure ?? DisclosurePolicy.LocalOnly;

    public async Task<IndexReport> RefreshAsync(IKnowledgeSource source, CancellationToken ct = default)
    {
        var read = await source.ReadAsync(ct).ConfigureAwait(false);
        var permitted = read.Where(_disclosure.MayLeaveMachine).ToList();

        var byRepository = permitted.GroupBy(e => e.Repository, StringComparer.Ordinal).ToList();
        foreach (var group in byRepository)
        {
            await store.ReplaceRepositoryAsync(group.Key, group.ToList(), ct).ConfigureAwait(false);
        }

        return new IndexReport(source.Name, byRepository.Count, permitted.Count, read.Count - permitted.Count);
    }
}
