using System.Collections.Frozen;
using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// What each person's press over the pipe rests on (CONFIRMWAIT1a-i, the confirm-wait design §5.1): one row per route the census makes
/// <see cref="PipeClass.Person"/>, in every form, naming the payload's fields that name what it acts on and the grounds read from them.
/// A press with no row is refused <see cref="InstancePipe.Ungrounded"/> at its ask, never treated as timeless.
/// </summary>
/// <remarks>
/// <para><b>One table, not two</b> (§5.1): D209's targets (a repository, a quest or a session a steward's act names) are this table's
/// other column, which STEWARDROLE1a fills; this column is the grounds, and adds <c>ask</c>, which the steward's marks may ignore.</para>
/// <para><b>Timeless only for a press that deletes nothing, moves no line or branch and starts no work</b>: a read mark, news marked
/// read, a conversation's name or pin. <c>PipeCensusTests</c> holds every person route to a row, each named field to its handler, and
/// each timeless row to what the census comment calls it.</para>
/// <para>A route added later is grounded in the change that adds it (<c>bridge-presses.md</c>).</para>
/// </remarks>
public sealed class PipeTargets
{
    private readonly FrozenDictionary<string, PipeGroundRow> _rows;

    private PipeTargets(IDictionary<string, PipeGroundRow> rows) => _rows = rows.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The desktop's table.</summary>
    public static PipeTargets Daoris { get; } = new(PipeTargetRows.All());

    /// <summary>The row of <paramref name="module"/>'s <paramref name="route"/>, or null where it has none.</summary>
    public PipeGroundRow? Row(string module, string route, JsonElement? payload = null) =>
        _rows.TryGetValue(Key(module, route), out var row) ? row : null;

    /// <summary>Every row by its module and route, as the census test reads them.</summary>
    public IReadOnlyDictionary<string, PipeGroundRow> Rows => _rows;

    /// <summary>A row's key: the route alone for the driver's, <c>MODULE.ROUTE</c> for another module's.</summary>
    public static string Key(string module, string route) =>
        string.Equals(module, DriverModule.Name, StringComparison.OrdinalIgnoreCase) ? route : $"{module.ToUpperInvariant()}.{route}";
}
