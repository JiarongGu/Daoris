using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// A limit's reset as the layer states it, put on <see cref="StatedReset"/>: how the failure wrote the reset decides whether the grace on a dated reset
/// applies, and the instant the layer resolved it to is the moment the bounds weigh. A reset the layer only resolved, with no statement
/// kept, is <see cref="ResetPrecision.Resolved"/>; a recovery that ends by no moment, or whose moment cannot be resolved, states none.
/// </summary>
internal static class LayerStatedReset
{
    public static StatedReset? Of(StatedRecoveryParts? parts, FailureRecovery? recovery, DateTimeOffset seen)
    {
        var resolved = recovery is { Basis: RecoveryBasis.ResetAt or RecoveryBasis.RetryNotBefore } ? recovery.At : null;
        if (parts is null) return resolved is { } instant ? new StatedReset(instant, ResetPrecision.Resolved, AssumedZone: false) : null;

        DateTimeOffset? waited = parts.Wait is { } wait ? seen + wait : null;
        var (moment, precision, assumed) = parts.Shape switch
        {
            StatedTimeShape.Moment => (parts.Moment ?? resolved, ResetPrecision.Absolute, false),
            StatedTimeShape.Wait => (resolved ?? waited, ResetPrecision.RelativeWait, false),
            StatedTimeShape.DateAndTime => (resolved, ResetPrecision.DateAndTime, !parts.ZoneNamed),
            StatedTimeShape.WeekdayAndTime => (resolved, ResetPrecision.WeekdayAndTime, !parts.ZoneNamed),
            StatedTimeShape.TimeOfDay => (resolved, ResetPrecision.TimeOfDay, !parts.ZoneNamed),
            _ => ((DateTimeOffset?)null, ResetPrecision.Resolved, false),
        };
        return moment is { } at ? new StatedReset(at, precision, assumed) : null;
    }
}
