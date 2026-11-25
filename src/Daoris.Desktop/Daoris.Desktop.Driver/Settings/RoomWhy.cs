namespace Daoris.Driver;

/// <summary>The signals that move the concurrency limit, by name, as the reserve's row says them.</summary>
public static class RoomSignals
{
    public const string Start = "start";
    public const string Memory = "memory";
    public const string Cpu = "cpu";
    public const string Timeouts = "timeouts";
    public const string Clear = "clear";
}

/// <summary>
/// Why the room is what it is, for the reserve's row to say: the person's share of the machine and what it swung by,
/// what is kept free and by which of the floor and the headroom, the limit and the signal behind it, the processors' busy, and the need
/// of the largest gate the machine has learned (or the declared one where none has).
/// </summary>
public sealed record RoomWhy(
    long Person, long Swing, long Margin, long Floor, long Keep, string KeepBy, int Limit, int Ceiling, string Signal, double? Busy,
    long Need, string NeedSource, int NeedRuns);
