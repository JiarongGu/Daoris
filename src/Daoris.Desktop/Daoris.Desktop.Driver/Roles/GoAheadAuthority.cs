namespace Daoris.Driver;

/// <summary>
/// Who answers a go-ahead (GOAHEADACT1, D304 point 4): the approver's yes is a role act, <see cref="Act"/>, which an appointed role whose
/// grants cover it takes at low and medium risk, held for the owner's overrule as <c>landing.accept</c> is. It stays the owner role's at
/// high risk (<c>high-risk</c>), where the owner marked its destination (a place's production releases, <c>marked</c>), and for a sign-in,
/// an owner-class kind (<c>owner-class</c>, doctrine rule 1). Judged on the place's risk record as it stands at the answer (PLACERISK1)
/// and on every grant of the caller's chain (D306), never on whether the caller is a person or an agent.
/// </summary>
public static class GoAheadAuthority
{
    /// <summary>The role act a go-ahead's answer is.</summary>
    public const string Act = "goahead.answer";

    /// <summary>The go-ahead kind that is a sign-in, which only the owner role gives.</summary>
    public const string SignIn = "sign-in";

    /// <summary>The go-ahead kind that is a release.</summary>
    public const string Release = "release";

    /// <summary>
    /// The destination mark a go-ahead falls under: a release whose target reads as production is the place's production releases; none
    /// for the rest, a production write beside the released version included, which follows the place's risk (D309 point 5).
    /// </summary>
    public static string? Destination(string kind, string on) =>
        kind == Release && on.Split((char[])[' ', '-', '_', '/', '.', ':'], StringSplitOptions.RemoveEmptyEntries)
            .Any(word => ReviewKey.Production.Contains(word.ToLowerInvariant(), StringComparer.Ordinal))
            ? PlaceMarks.ProductionReleases
            : null;

    /// <summary>
    /// Why a role may not answer the go-ahead of <paramref name="kind"/> on <paramref name="on"/> at a place standing at
    /// <paramref name="risk"/>, by <paramref name="chain"/>'s grants (the acting role's, then each delegator's): the owner role's code, else
    /// the grant missing; none where the role answers it.
    /// </summary>
    /// <param name="act">What the go-ahead lets through, as asked; the answer is pinned to it with the kind and the target.</param>
    public static string? Refusal(PlaceRiskNow risk, string kind, string on, string act, IReadOnlyList<IReadOnlyCollection<string>> chain)
    {
        ArgumentNullException.ThrowIfNull(act);
        if (kind == SignIn) return SupervisorWhy.OwnerClass;
        if (!PlaceRisks.RoleMayHold(risk.Risk)) return SupervisorWhy.HighRisk;
        if (Destination(kind, on) is { } mark && risk.Marked(mark)) return SupervisorWhy.Marked;
        return chain.Count > 0 && chain.All(grants => grants.Contains(Act)) ? null : Act;
    }
}
