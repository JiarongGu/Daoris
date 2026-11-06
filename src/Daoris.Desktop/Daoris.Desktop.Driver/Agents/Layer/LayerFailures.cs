using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// The failure ingress's builder (the agent-lyntai design §5.3): the layer's <see cref="AgentFailure"/>, by its released members (kind,
/// <c>By</c>, the restriction and the delivery), translated once into Daoris's <see cref="NormalizedFailure"/>, the one place one is built.
/// </summary>
/// <remarks>
/// <para>The layer's recovery keeps a kind and an instant it resolved, never how the reset was stated, so the instant reaches D125's
/// bounds as <see cref="ResetPrecision.Resolved"/>, with no grace; the stated reset, its window, what was hit and an access failure's
/// reason are the later parts' (1b-iii), each landing before the move that reads it.</para>
/// </remarks>
internal static class LayerFailures
{
    /// <summary>The mark <see cref="NormalizedFailure.By"/> carries before the layer's entry.</summary>
    public const string Mark = "layer:";

    /// <summary>
    /// <paramref name="failure"/> as Daoris's outcome, met at <paramref name="where"/>, the operation the run was performing.
    /// </summary>
    /// <param name="door">The bound door's transport word, for the log (<c>protocol</c>, <c>pipe</c>).</param>
    /// <param name="words">The terminal's own text, for the log alone: no word is read again.</param>
    /// <param name="coolOff">D125's default cool-off, for a recovery not stated.</param>
    public static NormalizedFailure Normalize(
        AgentFailure failure, string door, string where, string? words, DateTimeOffset seen, TimeSpan coolOff)
    {
        var kind = KindOf(failure.Kind);
        var by = Mark + failure.By;
        var restriction = failure.Restriction is { } held
            ? new FailureRestriction(CauseOf(held.Cause), SubjectOf(held.Subject), RecoveryOf(held.Recovery))
            : null;
        var limit = kind == AgentErrorKind.Limit ? LimitOf(failure.By, restriction?.Recovery, seen, coolOff) : null;

        return new NormalizedFailure(
            kind,
            new DoorFailure(door, null, kind.ToString().ToLowerInvariant(), null, words, where),
            by,
            limit,
            Access: null,
            restriction,
            DeliveryOf(failure.Delivery));
    }

    /// <summary>The four kinds, agreeing one to one.</summary>
    private static AgentErrorKind KindOf(AgentFailureKind kind) => kind switch
    {
        AgentFailureKind.Limit => AgentErrorKind.Limit,
        AgentFailureKind.Access => AgentErrorKind.Access,
        AgentFailureKind.Transient => AgentErrorKind.Transient,
        _ => AgentErrorKind.Fatal,
    };

    /// <summary>
    /// A limit whole: a stated reset or retry-not-before its end under D125's bounds; an unknown recovery, an owner's action (today's credit
    /// case, never a made-up date) or none, D125's default, not stated. What was hit is the entry that read it, as a limit read by its
    /// code is today.
    /// </summary>
    private static LimitSeen LimitOf(string entry, FailureRecovery? recovery, DateTimeOffset seen, TimeSpan coolOff)
    {
        var stated = recovery is { Basis: RecoveryBasis.ResetAt or RecoveryBasis.RetryNotBefore, At: { } at }
            ? new StatedReset(at, ResetPrecision.Resolved, AssumedZone: false)
            : null;
        return ResetBounds.Limit(entry, window: null, stated, seen, coolOff);
    }

    private static FailureCause CauseOf(RestrictionCause cause) => cause switch
    {
        RestrictionCause.Quota => FailureCause.Quota,
        RestrictionCause.Rate => FailureCause.Rate,
        RestrictionCause.Credit => FailureCause.Credit,
        RestrictionCause.Concurrency => FailureCause.Concurrency,
        RestrictionCause.Capacity => FailureCause.Capacity,
        _ => FailureCause.Unknown,
    };

    private static FailureSubject SubjectOf(RestrictionSubject subject) => subject switch
    {
        RestrictionSubject.Account => FailureSubject.Account,
        RestrictionSubject.Pool => FailureSubject.Pool,
        RestrictionSubject.Model => FailureSubject.Model,
        RestrictionSubject.Endpoint => FailureSubject.Endpoint,
        _ => FailureSubject.Unknown,
    };

    private static FailureRecovery RecoveryOf(RestrictionRecovery recovery) => recovery.Kind switch
    {
        RecoveryKind.ResetAt => new FailureRecovery(RecoveryBasis.ResetAt, recovery.At),
        RecoveryKind.RetryNotBefore => new FailureRecovery(RecoveryBasis.RetryNotBefore, recovery.At),
        RecoveryKind.OwnerAction => new FailureRecovery(RecoveryBasis.OwnerAction),
        _ => new FailureRecovery(RecoveryBasis.Unknown),
    };

    private static FailureDelivery DeliveryOf(AgentDelivery delivery) => delivery switch
    {
        AgentDelivery.Taken => FailureDelivery.Taken,
        AgentDelivery.NeverSent => FailureDelivery.NeverSent,
        _ => FailureDelivery.Uncertain,
    };
}
