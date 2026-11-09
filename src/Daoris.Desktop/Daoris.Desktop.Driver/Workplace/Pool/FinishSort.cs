namespace Daoris.Driver;

/// <summary>
/// What a finish sets aside of a slot's ignored tops (the tree-pool design §35.1): each top whose effective rule's <c>finish</c> is
/// <c>aside</c>, its kind's default where the rule says none (kept where its take keeps it: a dependency, an intermediate or a cache,
/// a base; else set aside), read at the line's tip. Daoris's own copies and shares are never the line's to set aside.
/// </summary>
internal static class FinishSort
{
    internal static async Task<IReadOnlyList<string>> AsideAsync(PoolSite site, string slot, string tip, SlotIgnored judged, CancellationToken ct)
    {
        IReadOnlyList<string> tops = [.. judged.Own, .. judged.Outputs, .. judged.Intermediates, .. judged.Kept, .. judged.Based];
        if (tops.Count == 0) return [];
        var rules = await StoreDependencyRules.ReadAsync(site.Home, slot, site.Repository, tip, ct).ConfigureAwait(false);
        return [.. tops.Where(top => rules.Rules.For(top, rules.Floor).Rule is not { } rule || FileKindSets.FinishOf(rule) == "aside")];
    }
}
