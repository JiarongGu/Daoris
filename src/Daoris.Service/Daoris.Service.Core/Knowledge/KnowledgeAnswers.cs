namespace Daoris.Knowledge;

/// <summary>
/// A search, and which tier ANSWERED: a door reports this, never
/// <see cref="KnowledgeService.SemanticEnabled"/>, which is only what is configured.
/// </summary>
/// <remarks>
/// <para>A composition that is not the hybrid is the lexical search alone — the factory builds nothing
/// else — so it answers by words, and a failure there propagates as it always has.</para>
///
/// <para>A search that names no kinds answers with the best <see cref="KnowledgeService.IndexEntriesUnasked"/> index entries, at
/// the places they rank, and fills the rest from every other kind in the order the same search ranks them
/// here, in Core, so every door answers one question alike. The answer says when more matched.</para>
/// </remarks>
internal sealed class KnowledgeAnswers(
    IKnowledgeSearch search,
    IWorkRecordSearch? records,
    Action refuseNewerIndex,
    Func<CancellationToken, Task> ensureIndexed)
{
    private const int RankConstant = 60;

    /// <summary>Every kind but the index: what a search naming no kinds fills its places with once two index entries hold theirs.</summary>
    private static readonly IReadOnlySet<EntryKind> AllButIndex =
        Enum.GetValues<EntryKind>().Where(kind => kind != EntryKind.Index).ToHashSet();

    public async Task<SearchAnswer> AnswerAsync(KnowledgeQuery query, CancellationToken ct)
    {
        if (!PresentFirst.Applies(query)) return await AnswerMixedAsync(query, ct).ConfigureAwait(false);

        var present = await AnswerMixedAsync(query with { Present = true }, ct).ConfigureAwait(false);
        var rest = await AnswerMixedAsync(query with { Present = false }, ct).ConfigureAwait(false);
        return PresentFirst.Order(query.Text, present, rest, query.Limit);
    }

    private async Task<SearchAnswer> AnswerMixedAsync(KnowledgeQuery query, CancellationToken ct)
    {
        var answer = await AnswerEntriesAsync(query, ct).ConfigureAwait(false);
        if (records is null || query.Provenance == Provenance.Canonical || query.Present == true) return answer;
        if (query.Kinds is not null && !query.Kinds.Contains(EntryKind.Record)) return answer;

        IReadOnlyList<KnowledgeHit> found;
        try
        {
            found = await records.SearchRecordsAsync(query, ct).ConfigureAwait(false);
        }
        catch (NewerWorkRecordsException)
        {
            return answer;
        }

        if (query.Kinds is null) found = [.. found.Take(KnowledgeService.RecordsUnasked)];
        if (found.Count == 0) return answer;

        // Reciprocal rank: the two tables' bm25 scores are never compared, only each hit's place in its own list.
        var scores = new Dictionary<string, double>(StringComparer.Ordinal);
        var hits = new Dictionary<string, KnowledgeHit>(StringComparer.Ordinal);
        foreach (var list in new[] { answer.Hits, found })
        {
            for (var rank = 0; rank < list.Count; rank++)
            {
                var id = list[rank].Entry.Id;
                scores[id] = scores.GetValueOrDefault(id) + 1.0 / (RankConstant + rank + 1);
                hits.TryAdd(id, list[rank]);
            }
        }

        var merged = scores.OrderByDescending(pair => pair.Value).Take(query.Limit).Select(pair => hits[pair.Key] with { Score = pair.Value }).ToList();
        return answer with { Hits = merged };
    }

    private async Task<SearchAnswer> AnswerEntriesAsync(KnowledgeQuery query, CancellationToken ct)
    {
        refuseNewerIndex();
        await ensureIndexed(ct).ConfigureAwait(false);
        var answer = await AnswerAnyAsync(query, ct).ConfigureAwait(false);
        if (query.Kinds is not null) return answer;

        var kept = new List<KnowledgeHit>();
        var index = 0;
        foreach (var hit in answer.Hits)
        {
            if (hit.Entry.Kind == EntryKind.Index && ++index > KnowledgeService.IndexEntriesUnasked) continue;
            kept.Add(hit);
        }
        if (index <= KnowledgeService.IndexEntriesUnasked) return answer;

        // What ranks below the window: every other kind, in the order the same search gives it. An entry the
        // window already holds keeps its place; the rest come after, as they would have ranked.
        var rest = await AnswerAnyAsync(query with { Kinds = AllButIndex }, ct).ConfigureAwait(false);
        var held = kept.Select(hit => hit.Entry.Id).ToHashSet(StringComparer.Ordinal);
        kept.AddRange(rest.Hits.Where(hit => held.Add(hit.Entry.Id)).Take(Math.Max(0, query.Limit - kept.Count)));

        var failures = new[] { answer.Failure, rest.Failure }.OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        return new SearchAnswer(
            kept, answer.Lexical && rest.Lexical, answer.Semantic && rest.Semantic,
            failures.Count == 0 ? null : string.Join("; ", failures), MoreIndex: true);
    }

    private async Task<SearchAnswer> AnswerAnyAsync(KnowledgeQuery query, CancellationToken ct) =>
        search is IAnsweringSearch answering
            ? await answering.AnswerAsync(query, ct).ConfigureAwait(false)
            : new SearchAnswer(await search.SearchAsync(query, ct).ConfigureAwait(false), Lexical: true, Semantic: false);
}
