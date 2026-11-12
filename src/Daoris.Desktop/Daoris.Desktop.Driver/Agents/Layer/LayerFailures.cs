using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// The failure ingress's builder: the layer's <see cref="AgentFailure"/>, by its released members (kind,
/// <c>By</c>, the restriction and the delivery), translated once into Daoris's <see cref="NormalizedFailure"/>, the one place one is built.
/// </summary>
/// <remarks>
/// <para>A reset the layer states reaches the cool-off bounds as it is stated (<see cref="LayerStatedReset"/>), an instant it only resolved
/// as <see cref="ResetPrecision.Resolved"/>, with no grace; the restriction's window and scope name the window and what is hit, and an
/// access failure's two readings stay apart. The delivery is read from what the prompt's own turn
/// showed (<see cref="LayerDelivery"/>), the layer's own stamp standing only for a terminal that states no turn.</para>
/// </remarks>
internal static class LayerFailures
{
    /// <summary>The mark <see cref="NormalizedFailure.By"/> carries before the layer's entry.</summary>
    public const string Mark = "layer:";

    /// <summary>
    /// <paramref name="failure"/> as Daoris's outcome, met at <paramref name="where"/>, the operation the run is performing.
    /// </summary>
    /// <param name="door">The bound door's transport word, for the log (<c>protocol</c>, <c>pipe</c>).</param>
    /// <param name="words">The terminal's own text, for the log alone: no word is read again.</param>
    /// <param name="coolOff">The default cool-off, for a recovery not stated.</param>
    /// <param name="turn">What the turn showed after its prompt is written, which the delivery is read from; null where the layer states none.</param>
    public static NormalizedFailure Normalize(
        AgentFailure failure, string door, string where, string? words, DateTimeOffset seen, TimeSpan coolOff, AgentTurnEvidence? turn = null,
        IReadOnlyDictionary<string, string>? windows = null)
    {
        var kind = KindOf(failure.Kind);
        var by = Mark + failure.By;
        var restriction = failure.Restriction is { } held
            ? new FailureRestriction(CauseOf(held.Cause), SubjectOf(held.Subject), RecoveryOf(held.Recovery))
            : null;
        var limit = kind == AgentErrorKind.Limit ? LimitOf(failure.By, failure.Restriction, restriction?.Recovery, seen, coolOff, windows) : null;
        var access = kind == AgentErrorKind.Access ? AccessOf(failure.AccessReadings) : null;

        return new NormalizedFailure(
            kind,
            new DoorFailure(door, null, kind.ToString().ToLowerInvariant(), null, words, where),
            by,
            limit,
            access,
            restriction,
            LayerDelivery.Of(turn) ?? DeliveryOf(failure.Delivery));
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
    /// A limit whole: a stated reset or retry-not-before its end under the cool-off bounds; an unknown recovery, an owner's action (the credit
    /// case, never a made-up date) or none, the default, not stated. What is hit is the entry that read it, as a limit read by its
    /// code is read.
    /// </summary>
    private static LimitSeen LimitOf(
        string entry, AgentRestriction? held, FailureRecovery? recovery, DateTimeOffset seen, TimeSpan coolOff,
        IReadOnlyDictionary<string, string>? windows)
    {
        var stated = LayerStatedReset.Of(held?.Recovery.Stated, recovery, seen);
        var window = held?.Window is { Length: > 0 } word ? windows?.GetValueOrDefault(word, word) ?? word : null;
        // A scope names what is hit, which is what a model family's cool-off reads it from.
        return ResetBounds.Limit(held?.Scope is { Length: > 0 } scope ? scope : entry, window, stated, seen, coolOff);
    }

    /// <summary>
    /// The field's reading and the words' reading of an access failure, kept apart as the account readers each read their own; null where
    /// the layer states neither.
    /// </summary>
    private static AccessEvidence? AccessOf(AgentAccessReadings? readings) => readings is null
        ? null
        : new AccessEvidence(
            ReasonOf(readings.ByField),
            WordsSignIn: readings.ByWords == Lyntai.Agents.AccessReason.SignInAgain,
            WordsRefused: readings.ByWords == Lyntai.Agents.AccessReason.CredentialRefused);

    private static Daoris.Driver.AccessReason? ReasonOf(Lyntai.Agents.AccessReason? reason) => reason switch
    {
        Lyntai.Agents.AccessReason.SignInAgain => Daoris.Driver.AccessReason.SignInAgain,
        Lyntai.Agents.AccessReason.CredentialRefused => Daoris.Driver.AccessReason.CredentialRefused,
        _ => null,
    };

    private static OwnerNeed NeedOf(OwnerActionReason reason) => reason switch
    {
        OwnerActionReason.Refill => OwnerNeed.Refill,
        OwnerActionReason.SignIn => OwnerNeed.SignIn,
        OwnerActionReason.Grant => OwnerNeed.Grant,
        OwnerActionReason.Configure => OwnerNeed.Configure,
        _ => OwnerNeed.Unknown,
    };

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
        RecoveryKind.OwnerAction => new FailureRecovery(RecoveryBasis.OwnerAction) { Need = NeedOf(recovery.Reason) },
        _ => new FailureRecovery(RecoveryBasis.Unknown),
    };

    private static FailureDelivery DeliveryOf(AgentDelivery delivery) => delivery switch
    {
        AgentDelivery.Taken => FailureDelivery.Taken,
        AgentDelivery.NeverSent => FailureDelivery.NeverSent,
        _ => FailureDelivery.Uncertain,
    };
}
