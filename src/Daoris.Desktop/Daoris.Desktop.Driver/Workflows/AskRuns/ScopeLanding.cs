namespace Daoris.Driver;

/// <summary>A reviewer the landing role offers, in its order: its adapter, its maker, and whether it is ready now.</summary>
public sealed record ScopeReviewer(string Adapter, string Maker, bool Ready);

/// <summary>One of a scope's two reviews: its breadth (<c>wide</c> or <c>close</c>), its reviewer, and whether the pair is independent (two makers).</summary>
public sealed record ScopeReview(string Breadth, string Adapter, string Maker, bool Independent);

/// <summary>
/// The end of an ask planned as one scope (the plan's <c>integration</c>, the canon's scope mode): its parts merge into the ask's
/// branch with no gate each, the full set runs once on the branch, and two reviews, wide (the design and effects beyond the change)
/// and close (details, edge cases, tests), come from the landing role's reviewers of two makers where two are ready. A plan
/// with no integration, or <c>per-part</c>, gates each part as before. Decided with no I/O.
/// </summary>
public static class ScopeLanding
{
    public const string OneScope = "one-scope";
    public const string PerPart = "per-part";
    public const string Full = "full";
    public const string Wide = "wide";
    public const string Close = "close";

    /// <summary>Whether a part's landing on the ask's branch runs its own gates: every plan but a one-scope one's.</summary>
    public static bool GatesEachPart(string? integration) => integration != OneScope;

    /// <summary>The tier the ask's branch is checked at before it lands: the full set for one scope; null keeps the run's own.</summary>
    public static string? EndTier(string? integration) => integration == OneScope ? Full : null;

    /// <summary>
    /// The two reviews: the first ready reviewer is the wide one, and the close one is the first ready of another maker; with
    /// one maker ready, its next reviewer (or the same one again), and both say they are not independent. None ready, none.
    /// </summary>
    public static IReadOnlyList<ScopeReview> Reviews(IReadOnlyList<ScopeReviewer> offered)
    {
        var ready = offered.Where(each => each.Ready).ToList();
        if (ready.Count == 0) return [];
        var wide = ready[0];
        var other = ready.FirstOrDefault(each => !string.Equals(each.Maker, wide.Maker, StringComparison.OrdinalIgnoreCase));
        var close = other ?? ready.Skip(1).FirstOrDefault() ?? wide;
        var independent = other is not null;
        return [new ScopeReview(Wide, wide.Adapter, wide.Maker, independent), new ScopeReview(Close, close.Adapter, close.Maker, independent)];
    }

    /// <summary>
    /// What a run's check sends at the end of its ask, and the reviews the end asks: for an ask's delivery run whose plan is one scope, the
    /// full set (every declared gate, no reached reading) and the two reviews; for any other run, or a plan not read, the send as it was
    /// and no review. The plan is read once per send, so the end follows the standing revision.
    /// </summary>
    public static async Task<ScopeEnd> EndAsync(string home, VerifySend send, ITrainPlans? plans, IReadOnlyList<ScopeReviewer> offered, CancellationToken ct)
    {
        if (plans is null || WorkflowRunBindings.Read(home, send.Run) is not { } binding || !AskRunKey.Is(binding)) return new(send, []);
        var plan = await plans.OfAsync(binding.Ask!, ct).ConfigureAwait(false);
        if (EndTier(plan?.Integration) != Full) return new(send, []);
        return new(send with { Full = true, Reached = false, Gates = null, Reach = null, Base = null }, Reviews(offered));
    }
}

/// <summary>A scope's end as its ask's delivery check asks it: the send to the heavy executor, and the two reviews (none outside one scope).</summary>
public sealed record ScopeEnd(VerifySend Send, IReadOnlyList<ScopeReview> Reviews);
