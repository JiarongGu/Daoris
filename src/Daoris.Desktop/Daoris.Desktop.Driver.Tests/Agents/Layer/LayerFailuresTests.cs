using Daoris.Driver;
using Lyntai.Agents;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The failure ingress's builder (the agent-lyntai design §5.3): the layer's failure, its released members alone (kind, <c>By</c>, the
/// restriction and the delivery), translated once into Daoris's <see cref="NormalizedFailure"/>. Its kind by one four-row map; its limit
/// built whole from the restriction's recovery under D125's bounds, an unstated or an owner's recovery D125's default and never a made-up
/// date; its phase the operation the run was performing; its provenance the layer's own entry, kept apart. Proven on a failure the tests
/// define, never a production door's.
/// </summary>
public sealed class LayerFailuresTests
{
    private static readonly DateTimeOffset Seen = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private static NormalizedFailure Read(AgentFailure failure, string where = DoorWhere.Prompt, string? words = "the terminal's own text") =>
        LayerFailures.Normalize(failure, "protocol", where, words, Seen, Hour);

    [Theory]
    [InlineData(AgentFailureKind.Fatal, AgentErrorKind.Fatal)]
    [InlineData(AgentFailureKind.Limit, AgentErrorKind.Limit)]
    [InlineData(AgentFailureKind.Access, AgentErrorKind.Access)]
    [InlineData(AgentFailureKind.Transient, AgentErrorKind.Transient)]
    public void Each_of_the_four_kinds_reads_as_its_own(AgentFailureKind layer, AgentErrorKind daoris)
    {
        Assert.Equal(daoris, Read(new AgentFailure(layer, "tests:entry")).Kind);
    }

    [Fact]
    public void A_stated_reset_or_retry_not_before_is_the_limit_s_end_under_d125_s_margin()
    {
        var resetAt = Read(new AgentFailure(AgentFailureKind.Limit, "tests:limit")
        {
            Restriction = new AgentRestriction(RestrictionCause.Quota, RestrictionSubject.Account, RestrictionRecovery.ResetAt(Seen.AddHours(5))),
        });
        Assert.Equal((Seen.AddHours(5) + AccountLimits.Margin, true), (resetAt.Limit!.Until, resetAt.Limit.Stated));

        var notBefore = Read(new AgentFailure(AgentFailureKind.Limit, "tests:limit")
        {
            Restriction = new AgentRestriction(RestrictionCause.Rate, RestrictionSubject.Account, RestrictionRecovery.RetryNotBefore(Seen.AddMinutes(10))),
        });
        Assert.Equal((Seen.AddMinutes(10) + AccountLimits.Margin, true), (notBefore.Limit!.Until, notBefore.Limit.Stated));
    }

    /// <summary>An instant the layer resolved keeps no statement of its precision, so a moment just behind the clock has no grace in 1b-i.</summary>
    [Fact]
    public void A_resolved_instant_just_past_is_not_believed_and_one_beyond_the_furthest_neither()
    {
        foreach (var at in new[] { Seen.AddSeconds(-30), Seen + AccountLimits.Furthest + Hour })
        {
            var read = Read(new AgentFailure(AgentFailureKind.Limit, "tests:limit")
            {
                Restriction = new AgentRestriction(RestrictionCause.Quota, RestrictionSubject.Account, RestrictionRecovery.ResetAt(at)),
            });
            Assert.Equal((Seen + Hour, false, true), (read.Limit!.Until, read.Limit.Stated, read.Limit.NotBelieved));
        }
    }

    [Fact]
    public void An_unknown_or_an_owner_s_recovery_and_a_limit_with_no_restriction_cool_for_d125_s_default_unstated()
    {
        foreach (var recovery in new[] { RestrictionRecovery.Unknown, RestrictionRecovery.OwnerAction })
        {
            var read = Read(new AgentFailure(AgentFailureKind.Limit, "tests:limit")
            {
                Restriction = new AgentRestriction(RestrictionCause.Credit, RestrictionSubject.Account, recovery),
            });
            Assert.Equal((Seen + Hour, false), (read.Limit!.Until, read.Limit.Stated));
        }

        Assert.Equal((Seen + Hour, false), (Read(new AgentFailure(AgentFailureKind.Limit, "tests:limit")).Limit!.Until, false));
        Assert.Null(Read(new AgentFailure(AgentFailureKind.Transient, "tests:transient")).Limit);
    }

    [Fact]
    public void The_restriction_and_the_delivery_cross_as_daoris_s_own_and_the_phase_is_the_run_s()
    {
        var read = Read(
            new AgentFailure(AgentFailureKind.Fatal, "tests:model")
            {
                Restriction = new AgentRestriction(RestrictionCause.Unknown, RestrictionSubject.Model, RestrictionRecovery.OwnerAction),
                Delivery = AgentDelivery.Uncertain,
            },
            DoorWhere.Open);

        Assert.Equal(new FailureRestriction(FailureCause.Unknown, FailureSubject.Model, new FailureRecovery(RecoveryBasis.OwnerAction)), read.Restriction);
        Assert.Equal(FailureDelivery.Uncertain, read.Delivery);
        Assert.Equal(new DoorFailure("protocol", null, "fatal", null, "the terminal's own text", DoorWhere.Open), read.Failure);
    }

    /// <summary>Provenance is kept apart: the log's name of what decided it is the layer's entry, marked so, and no reader reads policy from it.</summary>
    [Fact]
    public void The_provenance_is_the_layer_s_entry_marked_as_the_layer_s()
    {
        var read = Read(new AgentFailure(AgentFailureKind.Access, "tests:access"));

        Assert.Equal("layer:tests:access", read.By);
        Assert.Equal(AgentErrors.OfNormalized(read), new AgentError(AgentErrorKind.Access, read.Failure, "layer:tests:access"));
    }
}
