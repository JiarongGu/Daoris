namespace Daoris.Driver;

/// <summary>Reads why the room is what it is (<see cref="RoomWhy"/>) from the home's files and a memory reading; read only, nothing is written.</summary>
public static class RoomWhys
{
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
