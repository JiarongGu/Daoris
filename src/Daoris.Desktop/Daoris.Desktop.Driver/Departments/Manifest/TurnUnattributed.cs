namespace Daoris.Driver;

/// <summary>
/// The paths a turn in place changed that no tool event of its run names: its end's manifest against its start's, less every path a
/// file tool of the turn named (the run's tool events from the turn's start on). The lease cannot hold the person's own editor, so such a
/// path was changed while it ran by the person or another program, or by a command the harness does not report as a file tool. It is
/// said on the turn's review and told to the line at its next turn.
/// </summary>
public static class TurnUnattributed
{
    /// <summary>The unattributed paths of <paramref name="record"/>'s last turn in place, sorted; none before its end is read.</summary>
    /// <param name="events">The record's events; read from the home's session records where none are given.</param>
    public static IReadOnlyList<string> Of(string home, string record, IReadOnlyList<SessionEvent>? events = null)
    {
        if (PlaceTurns.Find(home, record) is not { } found || found.Store.Read(record, ManifestMoment.End) is not { } end) return [];
        return Of(found.Start, end, events ?? WorkplaceEventRecords.Current.Open(DaorisHome.Entry(home, HomeEntry.Sessions)).ReadAll(record));
    }

    /// <summary>The paths changed from <paramref name="start"/> to <paramref name="end"/> that no tool event of the turn named.</summary>
    public static IReadOnlyList<string> Of(RunManifest start, RunManifest end, IEnumerable<SessionEvent> events)
    {
        if (start.Root is not { } root || end.At < start.At) return [];
        var changed = start.Kind == PlaceKind.Folder ? FolderChanges.Between(start, end).All : PlaceChangesSince.Between(start, end, []).All;
        if (changed.Count == 0) return [];
        var named = events
            .Where(each => each.Kind == SessionEventKind.Tool && each.At >= start.At && each.At <= end.At)
            .SelectMany(each => each.Locations ?? [])
            .Select(location => Relative(root, location))
            .ToHashSet(StringComparer.Ordinal);
        return [.. changed.Where(path => !named.Contains(path))];
    }

    private static string Relative(string root, string location) =>
        (Path.IsPathRooted(location) ? Path.GetRelativePath(root, location) : location).Replace('\\', '/');
}
