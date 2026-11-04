using System.Collections.Frozen;
using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// What each person's press over the pipe rests on: one row per route the census makes <see cref="PipeClass.Person"/>, in every
/// form, naming the payload's fields that name what it acts on and the grounds read from them. A press with no row is refused
/// <see cref="InstancePipe.Ungrounded"/> at its ask, never treated as timeless.
/// </summary>
/// <remarks>
/// <para><b>One table, not two</b>: what a steward's act names (a repository, a quest or a session) is this table's other column,
/// <see cref="Mark"/>, read off each person's and window's route's <see cref="OwnerAttribute"/>; this column is the grounds, read off
/// its <see cref="RouteGroundAttribute"/>s in their order.</para>
/// <para><b>Timeless only for a press that deletes nothing, moves no line or branch and starts no work</b>: a read mark, news marked
/// read, a conversation's name or pin.</para>
/// </remarks>
public sealed class PipeTargets
{
    private readonly FrozenDictionary<string, PipeGroundRow> _rows;
    private readonly FrozenDictionary<string, OwnerTarget> _marks;

    private PipeTargets(
        IDictionary<string, PipeGroundRow> rows, IDictionary<string, OwnerTarget> marks, IDictionary<string, string> notAlready,
        IDictionary<string, IReadOnlyList<PipeWord>> words)
    {
        _rows = rows.ToFrozenDictionary(StringComparer.Ordinal);
        _marks = marks.ToFrozenDictionary(StringComparer.Ordinal);
        NotAlready = notAlready.ToFrozenDictionary(StringComparer.Ordinal);
        Words = words.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>The desktop's table, the census's.</summary>
    public static PipeTargets Daoris => PipeCensus.Daoris.Targets;

    /// <summary>The table <paramref name="marks"/> make, a module classed whole as a window's (<paramref name="windows"/>) naming nothing.</summary>
    public static PipeTargets Of(IReadOnlyList<RouteMark> marks, IReadOnlyList<string> windows)
    {
        var owners = marks.Where(each => each.Owner is not null).ToDictionary(each => Key(each.Module, each.Route), each => each.Owner!, StringComparer.Ordinal);
        foreach (var window in windows) owners.Add(window.ToUpperInvariant(), OwnerTarget.Nothing(OwnerNone.NamesNothing));
        return new PipeTargets(
            marks.Where(each => each.Grounds is not null).ToDictionary(each => Key(each.Module, each.Route), each => each.Grounds!, StringComparer.Ordinal),
            owners,
            marks.Where(each => each.NotAlready is not null).ToDictionary(each => Key(each.Module, each.Route), each => each.NotAlready!, StringComparer.Ordinal),
            marks.Where(each => each.Words is not null).ToDictionary(each => Key(each.Module, each.Route), each => each.Words!, StringComparer.Ordinal));
    }

    /// <summary>The values each person's press's card names by role, by its module and route; a press naming none says its act alone.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<PipeWord>> Words { get; }

    /// <summary>The row of <paramref name="module"/>'s <paramref name="route"/>, or null where it has none.</summary>
    public PipeGroundRow? Row(string module, string route, JsonElement? payload = null) =>
        _rows.TryGetValue(Key(module, route), out var row) ? row : null;

    /// <summary>Every row by its module and route, as the census test reads them.</summary>
    public IReadOnlyDictionary<string, PipeGroundRow> Rows => _rows;

    /// <summary>The rows whose <c>setting</c> ground names no value it sets, so they are never <c>already</c>, each with why.</summary>
    public IReadOnlyDictionary<string, string> NotAlready { get; }

    /// <summary>
    /// What <paramref name="module"/>'s <paramref name="route"/> marks <c>owner-set</c>, by the route's row or, for a module classed
    /// whole, the module's; null where it has none.
    /// </summary>
    public OwnerTarget? Mark(string module, string? route) =>
        route is not null && _marks.TryGetValue(Key(module, route), out var row) ? row
        : _marks.TryGetValue(module.ToUpperInvariant(), out var whole) ? whole
        : null;

    /// <summary>Every owner's mark by its module and route (a module classed whole by its name), as the census sweep reads them.</summary>
    public IReadOnlyDictionary<string, OwnerTarget> Marks => _marks;

    /// <summary>A row's key: the route alone for the driver's, <c>MODULE.ROUTE</c> for another module's.</summary>
    public static string Key(string module, string route) =>
        string.Equals(module, DriverModule.Name, StringComparison.OrdinalIgnoreCase) ? route : $"{module.ToUpperInvariant()}.{route}";
}
