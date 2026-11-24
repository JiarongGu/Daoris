using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Who answers a go-ahead: the approver's yes is a role act, <c>goahead.answer</c>, an appointed role's where its grants cover it and the
/// risk policy does not class the go-ahead's act the owner's (a push <c>remote.push</c>; a release, write or run <c>release.start</c>,
/// qualified by its target's destination). A place at high risk, a production-releases mark and a row classing the act <c>owner</c> keep
/// it the owner role's as the judge's verdict, <c>policy-owner</c>; a sign-in is a person in a browser, the owner role's by its kind.
/// </summary>
public sealed class GoAheadAuthorityTests
{
    private static readonly IReadOnlyList<IReadOnlyCollection<string>> Granted = [[GoAheadAuthority.Act]];

    private static string? Refusal(string config, string kind, string on = "staging", IReadOnlyList<IReadOnlyCollection<string>>? chain = null)
    {
        var read = DriverConfig.Parse(config);
        return GoAheadAuthority.Refusal(RiskPolicy.Of(read), RiskPlace.Of(read, "work", "app"), kind, on, chain ?? Granted);
    }

    [Fact]
    public void An_appointed_role_granted_the_act_answers_a_go_ahead_the_policy_does_not_class_the_owners()
    {
        Assert.Null(Refusal("{}", "write"));
        Assert.Null(Refusal("{}", "push", "origin"));
        Assert.Null(Refusal("""{ "placeRisk": { "repository/app": { "risk": "medium" } } }""", "release", "production"));
    }

    /// <summary>
    /// 🔴 At a place whose production releases are marked, a release, write or run stays the owner role's however its target is worded
    /// (<c>生产</c>, <c>Portal CD</c>, <c>staging</c> alike: the asker's words never clear it), since the mark keeps every release act at the
    /// owner; a push is no release act, so it follows the policy.
    /// </summary>
    [Fact]
    public void A_marked_place_keeps_every_release_write_and_run_the_owners_as_the_judges_verdict()
    {
        const string marked = """{ "placeRisk": { "workspace/work": { "risk": "low", "marks": ["release.production"] } } }""";

        Assert.Equal(SupervisorWhy.PolicyOwner, Refusal(marked, "release", "production"));
        Assert.Equal(SupervisorWhy.PolicyOwner, Refusal(marked, "write", "生产"));
        Assert.Equal(SupervisorWhy.PolicyOwner, Refusal(marked, "run", "Portal CD"));
        Assert.Equal(SupervisorWhy.PolicyOwner, Refusal(marked, "release", "staging"));
        Assert.Null(Refusal(marked, "push", "origin"));
    }

    [Fact]
    public void A_row_classing_the_act_the_owners_keeps_it_from_the_role_and_a_qualified_row_reads_only_its_destination()
    {
        const string rows = """{ "risk": { "repository/app": [ { "kind": "release.start/production", "class": "owner" }, { "kind": "remote.push", "class": "role" } ] } }""";

        Assert.Equal(SupervisorWhy.PolicyOwner, Refusal(rows, "write", "production"));
        Assert.Null(Refusal(rows, "write", "staging"));
        Assert.Null(Refusal(rows, "push", "origin"));
    }

    [Fact]
    public void High_risk_a_sign_in_a_kind_this_build_does_not_know_and_a_missing_grant_keep_it_from_the_role()
    {
        Assert.Equal(SupervisorWhy.PolicyOwner, Refusal("""{ "placeRisk": { "repository/app": { "risk": "high" } } }""", "write"));
        Assert.Equal(SupervisorWhy.OwnerClass, Refusal("{}", "sign-in"));
        Assert.Equal(SupervisorWhy.PolicyOwner, Refusal("{}", "dance"));
        Assert.Equal(GoAheadAuthority.Act, Refusal("{}", "write", chain: [["question.answer"]]));
        // Every grant on the chain covers it, a delegate never more than its delegator.
        Assert.Equal(GoAheadAuthority.Act, Refusal("{}", "write", chain: [[GoAheadAuthority.Act], []]));
    }

    [Fact]
    public void A_pin_reads_back_its_kind_and_target()
    {
        var pin = new GoAheadPin(2, "write", "production", "the dashboard's configuration");
        Assert.Equal(("write", "production"), GoAheadPin.Read(pin.Said));
        Assert.Null(GoAheadPin.Read("2|write"));
        Assert.Null(GoAheadPin.Read(null));
    }
}
