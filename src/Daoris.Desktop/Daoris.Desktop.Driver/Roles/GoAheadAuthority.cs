namespace Daoris.Driver;

/// <summary>
/// What a role's answer to a go-ahead is pinned to: its number on the ask, its kind, its target and its act. The release reads the
/// go-ahead again and posts only where all four still stand, so an act that changed since is no longer what the role judged.
/// </summary>
public sealed record GoAheadPin(int Number, string Kind, string On, string Act)
{
    /// <summary>The pin as a held answer keeps it: <c>number|kind|target|act</c>.</summary>
    public string Said => $"{Number}|{Kind}|{On}|{Act}";

    /// <summary>The kind and target a kept pin names (its number holds no bar); null for one that does not read.</summary>
    public static (string Kind, string On)? Read(string? said) =>
        said?.Split('|', 4) is [_, { Length: > 0 } kind, var on, _] ? (kind, on) : null;
}

/// <summary>
/// Who answers a go-ahead: the approver's yes is a role act, <see cref="Act"/>, which an appointed role whose grants cover it takes, held
/// for the owner's overrule as <c>landing.accept</c> is, wherever the risk policy does not class the go-ahead's act the owner's. Its act is
/// read by <see cref="RiskAskHolds.GoAheadAct"/> and judged at the asking session's place (<see cref="RiskGate"/>): a place at high risk, a
/// production-releases mark (every release, write or run, however its target is worded) and a row classing the act <c>owner</c> keep it the
/// owner role's, <c>policy-owner</c>; a kind this build does not know reads the owner's too. A sign-in is a person in a browser, the owner
/// role's by its kind (<c>owner-class</c>). Judged as the policy stands at the answer and again at the release, and on every grant of the
/// caller's chain, never on whether the caller is a person or an agent.
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
    /// Why the go-ahead of <paramref name="kind"/> on <paramref name="on"/> stays the owner role's at <paramref name="place"/> under
    /// <paramref name="policy"/>: a sign-in (<c>owner-class</c>), and an act the judge classes the owner's or a kind it cannot read
    /// (<c>policy-owner</c>). None where a role may answer it.
    /// </summary>
    public static string? OwnersOwn(RiskPolicy policy, RiskPlace place, string kind, string on)
    {
        if (kind == SignIn) return SupervisorWhy.OwnerClass;
        if (RiskAskHolds.GoAheadAct(kind, on) is not { } act) return SupervisorWhy.PolicyOwner;
        return RiskGate.Judge(policy, act, "", place, "").Outcome == RiskVerdict.OwnerOutcome ? SupervisorWhy.PolicyOwner : null;
    }

    /// <summary>
    /// Why a role may not answer the go-ahead of <paramref name="kind"/> on <paramref name="on"/> at <paramref name="place"/>, by
    /// <paramref name="chain"/>'s grants (the acting role's, then each delegator's): the owner role's code (<see cref="OwnersOwn"/>), else the
    /// grant missing; none where the role answers it.
    /// </summary>
    public static string? Refusal(RiskPolicy policy, RiskPlace place, string kind, string on, IReadOnlyList<IReadOnlyCollection<string>> chain) =>
        OwnersOwn(policy, place, kind, on) ?? (chain.Count > 0 && chain.All(grants => grants.Contains(Act)) ? null : Act);

    /// <summary><see cref="OwnersOwn"/> with the policy and the place read from <paramref name="config"/>.</summary>
    public static string? OwnersOwn(DriverConfig config, RolePlace place, string kind, string on) =>
        OwnersOwn(RiskPolicy.Of(config), RiskPlace.Of(config, place.Workspace, place.Repository), kind, on);
}
