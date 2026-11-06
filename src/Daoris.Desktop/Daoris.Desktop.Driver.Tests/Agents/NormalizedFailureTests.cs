using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The trusted ingress's answer (the agent-lyntai design §5.3, round 2): a normalized failure arrives already read, so
/// <see cref="AgentErrors.OfNormalized"/> answers the one <see cref="AgentError"/> with no table and no word read again, its access
/// disposition the opening's reading of the evidence kept apart.
/// </summary>
public sealed class NormalizedFailureTests
{
    private static readonly DoorFailure Logged = new("protocol", null, "access", null, "the terminal's own text", DoorWhere.Open);

    [Fact]
    public void A_normalized_failure_answers_its_own_reading_with_no_table()
    {
        var limit = new LimitSeen("weekly", AccountWindows.Weekly, DateTimeOffset.UnixEpoch, Stated: true, AssumedZone: false);
        var normalized = new NormalizedFailure(
            AgentErrorKind.Limit, Logged with { Words = "You've hit your weekly limit" }, "layer:tests:limit", limit, null, null, FailureDelivery.Taken);

        Assert.Equal(new AgentError(AgentErrorKind.Limit, normalized.Failure, "layer:tests:limit", limit), AgentErrors.OfNormalized(normalized));
    }

    [Fact]
    public void An_access_failure_s_disposition_is_its_evidence_s_opening_and_none_without_evidence()
    {
        var signIn = new NormalizedFailure(
            AgentErrorKind.Access, Logged, "layer:tests:access", null, new AccessEvidence(null, WordsSignIn: true, WordsRefused: false), null, FailureDelivery.NeverSent);
        Assert.Equal(AccessDisposition.SignedOutByWords, AgentErrors.OfNormalized(signIn).Access);

        Assert.Null(AgentErrors.OfNormalized(signIn with { Access = null }).Access);
    }
}
