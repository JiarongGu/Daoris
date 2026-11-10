namespace Daoris.Driver;

/// <summary>A change a session intends: a resource given a value, or removed. The adapter types it and names its precondition.</summary>
public sealed record SystemIntent(string Resource, string? Value, bool Remove = false);

/// <summary>What an operation is applied under: the system's kind of guard and the version the plan read; null where it must be absent.</summary>
public sealed record SystemPrecondition(string Kind, string? Version);

/// <summary>One typed operation of a change set: its resource, <c>create</c>, <c>update</c> or <c>delete</c>, its value before and after, and its guard.</summary>
public sealed record SystemOperation(string Resource, string Op, string? Before, string? After, SystemPrecondition Precondition);

/// <summary>
/// A change set: what a session's turn ends with in a system place, the adapter's typed operations in their order, kept on the record
/// as its <paramref name="Plan"/>th plan. Nothing in it has changed the system; one applier applies it after review.
/// </summary>
public sealed record SystemChangeSet(string Place, int Plan, IReadOnlyList<SystemOperation> Operations, DateTimeOffset At);

/// <summary>A resource as the system holds it now: its value and version, or absent.</summary>
public sealed record SystemPresent(string Resource, bool Exists, string? Value, string? Version);

/// <summary>The outcomes an apply comes to.</summary>
public static class SystemApplyStates
{
    /// <summary>Every operation applied under its precondition.</summary>
    public const string Applied = "applied";

    /// <summary>A precondition the system refused: the plan read a version that has since moved, so it is planned again.</summary>
    public const string Stale = "stale";

    /// <summary>A refusal that is not stale, said in the adapter's words.</summary>
    public const string Failed = "failed";

    /// <summary>Nothing to change: every resource already held what the plan asks, so the system wrote nothing.</summary>
    public const string None = "none";
}

/// <summary>
/// What an apply came to: the resources applied in order, and on <c>stale</c> the first refused with what each stale resource is now,
/// or on <c>failed</c> the adapter's words.
/// </summary>
public sealed record SystemApplied(string State, IReadOnlyList<string> Applied, string? Refused, IReadOnlyList<SystemPresent> Present, string? Message);
