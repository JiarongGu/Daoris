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

/// <summary>The reserve as the state says it: the GB the person pinned (null for auto), the GB in force, how the machine's memory stands, each in GB, and why the room is what it is.</summary>
public sealed record ReserveShown(int? Gb, double Kept, double? Total, double? FreeCommit, double? Daoris, ReserveWhy? Why)
{
    private static double? InGb(long? bytes) => bytes is { } value ? Math.Round(value / (double)(1L << 30), 1) : null;

    public static ReserveShown Of(string home, DriverConfig config)
    {
        var use = MachineUses.Read(home, config.ReserveGb);
        return new(config.ReserveGb, InGb(use.Reserve) ?? 0, InGb(use.Total), InGb(use.FreeCommit), InGb(use.Daoris), use.Why is { } why ? ReserveWhy.Of(why) : null);
    }
}

/// <summary>Why the room is what it is, as the reserve's row says it: the person's share and its swing, the floor, what keeps the room (<c>floor</c> or <c>headroom</c>), the limit and its signal, the processors' busy, and what a gate needs.</summary>
public sealed record ReserveWhy(
    double Person, double Swing, double Margin, double Floor, string KeepBy, int Limit, int Ceiling, string Signal, double? Busy, double Need, string NeedSource, int NeedRuns)
{
    private static double InGb(long bytes) => Math.Round(bytes / (double)(1L << 30), 1);

    public static ReserveWhy Of(RoomWhy why) =>
        new(InGb(why.Person), InGb(why.Swing), InGb(why.Margin), InGb(why.Floor), why.KeepBy, why.Limit, why.Ceiling, why.Signal, why.Busy is { } busy ? Math.Round(busy, 2) : null, InGb(why.Need), why.NeedSource, why.NeedRuns);
}
