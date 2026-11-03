using Daoris.Contracts.Knowledge;

namespace Daoris.Driver;

/// <summary>The knowledge reads, read into the driver's own views.</summary>
public static class KnowledgeViews
{
    /// <summary>
    /// One wording's search of one repository's local records for a brainstorm's packet: each hit's entry read whole for its
    /// text, and the tier the search's header names, <c>meaning</c> where the semantic tier answered beside the words tier,
    /// which always answers. An entry the host no longer holds is left out. Only reads: a search moves nothing.
    /// </summary>
    /// <exception cref="DriverException">A host that refused the search, in its words.</exception>
    public static async Task<BrainstormSearch> BrainstormSearchAsync(this ServiceClient service, string words, string repository, CancellationToken ct = default)
    {
        var searched = await service.SendAsync(new Search(words, Repositories: repository, Limit: 12), ct).ConfigureAwait(false);
        if (searched.Answer is not { } hits) throw new DriverException($"the service would not search `{repository}`: {searched.Reply.Payload}");

        var found = new List<BrainstormFound>();
        foreach (var hit in hits)
        {
            if (hit.Id is null || hit.Path is not { Length: > 0 } relative) continue;
            if ((await service.SendAsync(new ReadEntry(hit.Id), ct).ConfigureAwait(false)).Answer is not { } entry) continue;
            found.Add(new BrainstormFound(hit.Kind?.ToLowerInvariant() ?? "entry", relative, hit.FirstLine, hit.LastLine, entry.Body ?? "", words));
        }

        return new BrainstormSearch(
            found, searched.Reply.Tier?.Contains("semantic", StringComparison.Ordinal) == true ? BrainstormTier.Meaning : BrainstormTier.Words);
    }
}
