namespace Daoris.Driver;

/// <summary>Why a failure restricts a run: the host's own, read from a normalized failure.</summary>
public enum FailureCause
{
    Unknown,
    Quota,
    Rate,
    Credit,
    Concurrency,
    Capacity,
}

/// <summary>What a failure restricts: the account, a shared pool, a model or an endpoint.</summary>
public enum FailureSubject
{
    Unknown,
    Account,
    Pool,
    Model,
    Endpoint,
}

/// <summary>How a restriction ends.</summary>
public enum RecoveryBasis
{
    /// <summary>Not stated: the default stands in.</summary>
    Unknown,

    /// <summary>At a stated reset.</summary>
    ResetAt,

    /// <summary>Not before a stated moment.</summary>
    RetryNotBefore,

    /// <summary>When a person acts (a credit refusal): never a made-up date.</summary>
    OwnerAction,
}

/// <summary>How a restriction ends, and when where it is stated.</summary>
public sealed record FailureRecovery(RecoveryBasis Basis, DateTimeOffset? At = null);

/// <summary>
/// A normalized failure's restriction, on Daoris's own types: read by its subject and cause before its
/// recovery, so a limit or a capacity scoped to a model is never a refused model.
/// </summary>
public sealed record FailureRestriction(FailureCause Cause, FailureSubject Subject, FailureRecovery Recovery);
