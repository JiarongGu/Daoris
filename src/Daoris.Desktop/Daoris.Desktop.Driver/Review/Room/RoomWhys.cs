namespace Daoris.Driver;

/// <summary>Reads why the room is what it is (<see cref="RoomWhy"/>) from the home's files and a memory reading; read only, nothing is written.</summary>
public static class RoomWhys
{
    public static RoomWhy Read(string home, MemoryReading? reading, int? reserveGb, DateTimeOffset now, int ceiling)
    {
        var state = RoomLedger.Read(home) ?? RoomState.Empty(ceiling);

        // The file is written only when an admission looks, so what it says may be hours old: the row shows the limit as a look now would leave it
        // with no pressure seen, and a pressure signal older than the calm span is shown as cleared.
        var limit = RoomPressure.Next(state.Limit, null, ceiling, now);
        if (now - limit.PressureAt >= RoomPressure.Calm && limit.Signal is RoomSignals.Memory or RoomSignals.Cpu or RoomSignals.Timeouts) limit = limit with { Signal = RoomSignals.Clear };
        state = state with { Limit = limit };
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
