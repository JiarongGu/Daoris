namespace Daoris.Driver;

/// <summary>
/// What a role's answer to a go-ahead is pinned to (D306): its number on the ask, its kind, its target and its act. The release reads the
/// go-ahead again and posts only where all four still stand, so an act that changed since is no longer what the role judged.
/// </summary>
public sealed record GoAheadPin(int Number, string Kind, string On, string Act)
{
    /// <summary>The pin as a held answer keeps it: <c>number|kind|target|act</c>.</summary>
    public string Said => $"{Number}|{Kind}|{On}|{Act}";

    /// <summary>The kind a kept pin names (its number and kind hold no bar); null for one that does not read.</summary>
    public static string? KindOf(string? said) => said?.Split('|', 3) is [_, { Length: > 0 } kind, _] ? kind : null;
}

/// <summary>
/// Who answers a go-ahead (GOAHEADACT1, D304 point 4): the approver's yes is a role act, <see cref="Act"/>, which an appointed role whose
/// grants cover it takes at low and medium risk, held for the owner's overrule as <c>landing.accept</c> is. It stays the owner role's at
/// high risk (<c>high-risk</c>), at a place whose production releases the owner marked (<c>marked</c>, failing closed), and for a sign-in,
/// an owner-class kind (<c>owner-class</c>, doctrine rule 1). Judged on the place's risk record as it stands at the answer and again at
/// the release, and on every grant of the caller's chain, never on whether the caller is a person or an agent.
/// </summary>
public static class GoAheadAuthority
{
    /// <summary>The role act a go-ahead's answer is.</summary>
    public const string Act = "goahead.answer";

    /// <summary>The go-ahead kind that is a sign-in, which only the owner role gives.</summary>
    public const string SignIn = "sign-in";

    /// <summary>The go-ahead kind that is a release.</summary>
    public const string Release = "release";

    /// <summary>The go-ahead kind that reaches no target beyond the remote, which a place's production mark never covers.</summary>
    public const string Push = "push";

    /// <summary>
    /// Why the go-ahead of <paramref name="kind"/> stays the owner role's at a place standing at <paramref name="risk"/>: a sign-in
    /// (<c>owner-class</c>), a high-risk place (<c>high-risk</c>), and at a place whose production releases the owner marked, every kind but a
    /// push (<c>marked</c>) unless its release target's stage, as its plugin names it, is not production. The asker's words for the target
    /// never clear it: they may be in any language or name a system by its own name. None where a role may answer it.
    /// </summary>
    /// <param name="stage">The release target's stage its plugin names; null where none is named, which at a marked place fails closed.</param>
    public static string? OwnersOwn(PlaceRiskNow risk, string kind, ReleaseStage? stage = null)
    {
        if (kind == SignIn) return SupervisorWhy.OwnerClass;
        if (!PlaceRisks.RoleMayHold(risk.Risk)) return SupervisorWhy.HighRisk;
        return risk.Marked(PlaceMarks.ProductionReleases) && kind != Push && stage is not { Production: false } ? SupervisorWhy.Marked : null;
    }

    /// <summary>
    /// Why a role may not answer the go-ahead of <paramref name="kind"/> at a place standing at <paramref name="risk"/>, by
    /// <paramref name="chain"/>'s grants (the acting role's, then each delegator's): the owner role's code (<see cref="OwnersOwn"/>), else the
    /// grant missing; none where the role answers it.
    /// </summary>
    public static string? Refusal(PlaceRiskNow risk, string kind, IReadOnlyList<IReadOnlyCollection<string>> chain, ReleaseStage? stage = null) =>
        OwnersOwn(risk, kind, stage) ?? (chain.Count > 0 && chain.All(grants => grants.Contains(Act)) ? null : Act);
}

/// <summary>A release target's stage as the release step's plugin names it: its name and whether it reaches production.</summary>
public sealed record ReleaseStage(string Name, bool Production);
