using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Who answers a go-ahead: the approver's yes is a role act, <c>goahead.answer</c>, an appointed role's at
/// low and medium risk where its grants cover it; the owner role's at high risk, at a place whose production releases the owner marked
/// (failing closed: a release, write or run there is the owner's unless its plugin names its stage non-production), and for a sign-in.
/// </summary>
public sealed class GoAheadAuthorityTests
{
    private static readonly IReadOnlyList<IReadOnlyCollection<string>> Granted = [[GoAheadAuthority.Act]];

    private static PlaceRiskNow At(string risk, params string[] marks) => new(risk, PlaceRiskBook.OwnSource, marks);

    [Fact]
    public void An_appointed_role_granted_the_act_answers_a_low_risk_go_ahead()
    {
        Assert.Null(GoAheadAuthority.Refusal(At(PlaceRisks.Low), "write", Granted));
        Assert.Null(GoAheadAuthority.Refusal(At(PlaceRisks.Medium), "push", Granted));
        // Unmarked, a release follows the place's risk whatever its target says.
        Assert.Null(GoAheadAuthority.Refusal(At(PlaceRisks.Low), "release", Granted));
    }

    /// <summary>
    /// 🔴 At a place whose production releases are marked, a release, write or run stays the owner role's however its target is worded
    /// (<c>生产</c>, <c>Portal CD</c>, <c>staging</c> alike: the asker's words never clear it); only a stage its plugin names
    /// non-production lets the role answer, and a push follows the place's risk.
    /// </summary>
    [Fact]
    public void A_marked_place_fails_closed_unless_the_plugin_names_the_stage_non_production()
    {
        var marked = At(PlaceRisks.Low, PlaceMarks.ProductionReleases);

        Assert.Equal(SupervisorWhy.Marked, GoAheadAuthority.Refusal(marked, "release", Granted));
        Assert.Equal(SupervisorWhy.Marked, GoAheadAuthority.Refusal(marked, "write", Granted));
        Assert.Equal(SupervisorWhy.Marked, GoAheadAuthority.Refusal(marked, "run", Granted));
        Assert.Equal(SupervisorWhy.Marked, GoAheadAuthority.Refusal(marked, "release", Granted, new ReleaseStage("Portal CD", Production: true)));
        Assert.Equal(SupervisorWhy.Marked, GoAheadAuthority.Refusal(marked, "release", Granted, new ReleaseStage("生产", Production: true)));
        // A stage the plugin names non-production (a configuration version beside the released one) follows the place's risk.
        Assert.Null(GoAheadAuthority.Refusal(marked, "release", Granted, new ReleaseStage("staging", Production: false)));
        Assert.Null(GoAheadAuthority.Refusal(marked, "write", Granted, new ReleaseStage("beside the released version", Production: false)));
        Assert.Null(GoAheadAuthority.Refusal(marked, "push", Granted));
    }

    [Fact]
    public void High_risk_a_sign_in_and_a_missing_grant_keep_it_from_the_role()
    {
        Assert.Equal(SupervisorWhy.HighRisk, GoAheadAuthority.Refusal(At(PlaceRisks.High), "write", Granted));
        Assert.Equal(SupervisorWhy.OwnerClass, GoAheadAuthority.Refusal(At(PlaceRisks.Low), "sign-in", Granted));
        Assert.Equal(GoAheadAuthority.Act, GoAheadAuthority.Refusal(At(PlaceRisks.Low), "write", [["question.answer"]]));
        // Every grant on the chain covers it, a delegate never more than its delegator.
        Assert.Equal(GoAheadAuthority.Act, GoAheadAuthority.Refusal(At(PlaceRisks.Low), "write", [[GoAheadAuthority.Act], []]));
    }
}
