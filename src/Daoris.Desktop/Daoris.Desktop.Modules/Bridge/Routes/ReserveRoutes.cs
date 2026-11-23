using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The `reserveGb` `daoris settings reserve <gb>|auto` edits: a number in its bounds, or null for auto; out of them is the driver's refusal.
/// </summary>
public sealed class ReserveRoutes(DriverContext context)
{
    [DriverRoute("SET_RESERVE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_RESERVE", None = OwnerNone.MachineSetting)]
    [GroundRead("SET_RESERVE", nameof(PipeReads.Reserve))]
    [Publishes("SET_RESERVE", StateScopes.Driver)]
    private object? SetReserve(SetReserve press)
    {
        var gb = press.Gb is { } value ? ReserveSetting.Takes(value) ? (int)value : throw new DriverException(ReserveSetting.Refusal) : (int?)null;
        context.Change(config => config.WithReserve(gb));
        return DriverRoutes.State(context.Loop);
    }
}

/// <summary>The reserve as the state says it: the GB the person pinned (null for auto), the GB in force, and how the machine's memory stands, each in GB.</summary>
public sealed record ReserveShown(int? Gb, double Kept, double? Total, double? FreeCommit, double? Daoris)
{
    private static double? InGb(long? bytes) => bytes is { } value ? Math.Round(value / (double)(1L << 30), 1) : null;

    public static ReserveShown Of(string home, DriverConfig config)
    {
        var use = MachineUse.Read(home, config.ReserveGb);
        return new(config.ReserveGb, InGb(use.Reserve) ?? 0, InGb(use.Total), InGb(use.FreeCommit), InGb(use.Daoris));
    }
}
