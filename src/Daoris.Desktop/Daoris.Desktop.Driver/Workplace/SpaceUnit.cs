using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One thing the press lists (KEEP1e, the cleanup design §3.3): its kind, its id (what a press sends back), whether its rule takes it
/// now or it could go early by the person's word, and what the list says of it.
/// </summary>
/// <param name="Kind"><c>set-aside</c>, <c>downloads</c>, <c>logs</c>, <c>browser-cache</c>, <c>left-over</c> or <c>update-failed</c>.</param>
/// <param name="Id">A set-aside folder's <c>&lt;workspace&gt;/&lt;repository&gt;/&lt;folder&gt;</c>, a download's
/// <c>&lt;agent|tool&gt;/&lt;name&gt;/&lt;version&gt;</c>, a log's file name, a cache's or an earlier build's path, the failed update's.</param>
/// <param name="Early">It goes before its rule's time, by the person's word; false where its rule takes it now.</param>
/// <param name="Bytes">Its size, where it is known: a download's and a path's walked as listed, a set-aside folder's from its kind's walk.</param>
public sealed record SpaceUnit(string Kind, string Id, bool Early, long? Bytes)
{
    /// <summary>What the rule takes of it now: <c>past</c> (its days ended), <c>slim</c> (its package folders, an hour after its move) or
    /// <c>unfinished</c> (a download an earlier clearing moved aside); for an early one, <c>early</c> or <c>kept</c> (a set-aside folder past
    /// its days the purge keeps, since it holds files of its own).</summary>
    public string? Why { get; init; }

    /// <summary>When its rule takes it: a set-aside folder's end of days, a download's day; null where none is known.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>A set-aside folder's workspace and repository.</summary>
    public string? Workspace { get; init; }

    /// <inheritdoc cref="Workspace"/>
    public string? Repository { get; init; }

    /// <summary>A set-aside folder holds files of its own (written after its move, or a link in it): the person's to weigh.</summary>
    public bool? Own { get; init; }

    /// <summary>A set-aside folder's class (KEEPSIZE1b, <see cref="SetAsideClass"/>), which the press lists it by: recorded, awaiting its record, stopped, refused, unmarked.</summary>
    public string? Class { get; init; }

    /// <summary>A recorded folder's recovery ref at its tip, where one holds it.</summary>
    public string? Ref { get; init; }
}
