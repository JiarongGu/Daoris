using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Who answers a go-ahead (GOAHEADACT1, D304 point 4): the approver's yes is a role act, <c>goahead.answer</c>, an appointed role's at
/// low and medium risk where its grants cover it; the owner role's at high risk, at a destination the owner marked, and for a sign-in.
/// </summary>
public sealed class GoAheadAuthorityTests
{
    private static readonly IReadOnlyList<IReadOnlyCollection<string>> Granted = [[GoAheadAuthority.Act]];

    private static PlaceRiskNow At(string risk, params string[] marks) => new(risk, PlaceRiskBook.OwnSource, marks);

    [Fact]
    public void An_appointed_role_granted_the_act_answers_a_low_risk_go_ahead()
    {
        Assert.Null(GoAheadAuthority.Refusal(At(PlaceRisks.Low), "write", "the staging database", "add the field", Granted));
        Assert.Null(GoAheadAuthority.Refusal(At(PlaceRisks.Medium), "push", "origin", "push the branch", Granted));
    }

    [Fact]
    public void A_marked_destination_stays_the_owner_roles_whatever_the_grant()
    {
        var marked = At(PlaceRisks.Low, PlaceMarks.ProductionReleases);

        Assert.Equal(SupervisorWhy.Marked, GoAheadAuthority.Refusal(marked, "release", "production", "release 4.2", Granted));
        Assert.Equal(PlaceMarks.ProductionReleases, GoAheadAuthority.Destination("release", "live-site"));
        // A release that is not a production one, and a production write beside the released version, follow the place's risk (D309 point 5).
        Assert.Null(GoAheadAuthority.Refusal(marked, "release", "staging", "release 4.2", Granted));
        Assert.Null(GoAheadAuthority.Refusal(marked, "write", "production", "a configuration version beside the released one", Granted));
    }

    [Fact]
    public void High_risk_a_sign_in_and_a_missing_grant_keep_it_from_the_role()
    {
        Assert.Equal(SupervisorWhy.HighRisk, GoAheadAuthority.Refusal(At(PlaceRisks.High), "write", "staging", "add the field", Granted));
        Assert.Equal(SupervisorWhy.OwnerClass, GoAheadAuthority.Refusal(At(PlaceRisks.Low), "sign-in", "the cloud console", "sign in", Granted));
        Assert.Equal(GoAheadAuthority.Act, GoAheadAuthority.Refusal(At(PlaceRisks.Low), "write", "staging", "add the field", [["question.answer"]]));
        // Every grant on the chain covers it, a delegate never more than its delegator (D306).
        Assert.Equal(GoAheadAuthority.Act, GoAheadAuthority.Refusal(At(PlaceRisks.Low), "write", "staging", "add the field", [[GoAheadAuthority.Act], []]));
    }
}
