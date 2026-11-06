using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Host policy (the agent-lyntai design §5.3's round-5 paragraph): a refused model is told by its whole outcome, kind fatal, subject the
/// model, recovery an owner's action and the door's model-refusal entry, never by its subject alone, since a limit or a capacity is scoped
/// to a model too; and it is then mapped by door and phase. Daoris's own types, never a proof that the layer supplies the outcome.
/// </summary>
public sealed class HostPolicyModelRefusalTests
{
    private static readonly IReadOnlySet<string> Entries = new HashSet<string>(StringComparer.Ordinal) { "model_not_found" };

    private static readonly FailureRestriction Refused = new(FailureCause.Unknown, FailureSubject.Model, new FailureRecovery(RecoveryBasis.OwnerAction));

    [Fact]
    public void Host_policy_tells_a_refused_model_by_its_whole_outcome()
    {
        Assert.True(ModelRefusals.Refuses(AgentErrorKind.Fatal, "model_not_found", Refused, Entries));

        // A credit limit scoped to one model, a model at capacity, an entry the door's relation lacks, a refusal of the account: none.
        Assert.False(ModelRefusals.Refuses(AgentErrorKind.Limit, "model_not_found", Refused with { Cause = FailureCause.Credit }, Entries));
        Assert.False(ModelRefusals.Refuses(
            AgentErrorKind.Transient, "model_not_found", Refused with { Cause = FailureCause.Capacity, Recovery = new FailureRecovery(RecoveryBasis.Unknown) }, Entries));
        Assert.False(ModelRefusals.Refuses(AgentErrorKind.Fatal, "invalid_request", Refused, Entries));
        Assert.False(ModelRefusals.Refuses(AgentErrorKind.Fatal, "model_not_found", Refused with { Subject = FailureSubject.Account }, Entries));
        Assert.False(ModelRefusals.Refuses(AgentErrorKind.Fatal, "model_not_found", null, Entries));
    }

    /// <summary>
    /// On the native door after a prompt it is the ending's refused model under today's guard, a failed, unstopped turn that ran nothing;
    /// at a protocol open it is D205's re-ask on the same session, never the ending's flag; anywhere else it reads as today's tables read
    /// the same failure.
    /// </summary>
    [Theory]
    [InlineData(Transport.Native, DoorWhere.Result, true, false, ModelRefusalPath.Ending)]
    [InlineData(Transport.Native, DoorWhere.Result, false, false, ModelRefusalPath.AsToday)]
    [InlineData(Transport.Native, DoorWhere.Result, true, true, ModelRefusalPath.AsToday)]
    [InlineData(Transport.Native, DoorWhere.Harness, true, false, ModelRefusalPath.AsToday)]
    [InlineData(Transport.Protocol, DoorWhere.Open, true, false, ModelRefusalPath.ReAsk)]
    [InlineData(Transport.Protocol, DoorWhere.Prompt, true, false, ModelRefusalPath.AsToday)]
    public void Host_policy_maps_a_refused_model_by_door_and_phase(Transport door, string where, bool ranNothing, bool stopped, ModelRefusalPath path)
    {
        Assert.Equal(path, ModelRefusals.Path(door, where, ranNothing, stopped));
    }
}
