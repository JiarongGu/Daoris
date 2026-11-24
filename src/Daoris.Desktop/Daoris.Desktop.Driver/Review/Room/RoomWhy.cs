namespace Daoris.Driver;

/// <summary>
/// Why the room is what it is, for the reserve's row to say: the person's share of the machine and what it swung by,
/// what is kept free and by which of the floor and the headroom, the limit and the signal behind it, the processors' busy, and the need
/// of the largest gate the machine has learned (or the declared one where none has).
/// </summary>
public sealed record RoomWhy(
    long Person, long Swing, long Margin, long Floor, long Keep, string KeepBy, int Limit, int Ceiling, string Signal, double? Busy,
    long Need, string NeedSource, int NeedRuns)
{
    /// <summary>The room as the home's files and the memory reading say it now; read only, nothing is written.</summary>
    public static RoomWhy Read(string home, MemoryReading? reading, int? reserveGb, DateTimeOffset now, int ceiling)
    {
        var state = RoomLedger.Read(home) ?? RoomState.Empty(ceiling);
        var view = RoomLedger.ViewOf(state, reading, ReserveSetting.Bytes(reserveGb), now);
        var learned = LearnedNeeds.Read(LearnedNeeds.PathOf(home))
            .Select(pair => (Need: LearnedNeeds.Need(pair.Value, LoadKind.Declared(LoadKind.Test)), pair.Key))
            .Where(each => each.Need.Source == NeedReading.Learned)
            .OrderByDescending(each => each.Need.Cost.Memory)
            .ToList();
        var need = learned.Count > 0 ? learned[0].Need : new NeedReading(LoadKind.Declared(LoadKind.Test), NeedReading.DeclaredSource, 0);
        return new RoomWhy(
            view.Person.Person, view.Person.Swing, view.Person.Margin, view.Floor, view.Keep, view.KeepBy, Math.Min(view.Limit, ceiling), ceiling, view.Signal, view.Busy,
            need.Cost.Memory, need.Source, need.Samples);
    }
}
