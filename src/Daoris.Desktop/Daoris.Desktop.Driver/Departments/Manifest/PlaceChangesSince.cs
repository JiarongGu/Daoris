namespace Daoris.Driver;

/// <summary>
/// What changed in a git place between two manifests: between a turn's start and the place's last end, what the person or another
/// program changed outside every session; between a turn's start and its end, the turn's own change set. A path listed on one side only
/// reads as changed, since the other side is <c>HEAD</c>'s; a path neither lists changed only where a commit between them changed it.
/// </summary>
public sealed record PlaceChangesSince(IReadOnlyList<string> Added, IReadOnlyList<string> Changed, IReadOnlyList<string> Removed)
{
    /// <summary>Every path, sorted.</summary>
    public IReadOnlyList<string> All => [.. Added.Concat(Changed).Concat(Removed).Order(StringComparer.Ordinal)];

    /// <summary>The paths changed from <paramref name="earlier"/> to <paramref name="later"/>, with those <paramref name="committed"/> between.</summary>
    public static PlaceChangesSince Between(RunManifest earlier, RunManifest later, IReadOnlyCollection<string> committed)
    {
        List<string> added = [], changed = [], removed = [];
        var paths = earlier.Entries.Select(entry => entry.Path).Concat(later.Entries.Select(entry => entry.Path)).Concat(committed)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var (before, after) = (earlier.Listed(path), later.Listed(path));
            if (before is null && after is null)
            {
                changed.Add(path);
                continue;
            }

            if (before is not null && after is not null && before.Sha256 == after.Sha256 && !committed.Contains(path)) continue;
            if (after is { Sha256: null }) removed.Add(path);
            else if (after is { Status: "??" } && before is null) added.Add(path);
            else changed.Add(path);
        }

        return new(added, changed, removed);
    }

    /// <summary>
    /// The turn's own paths still uncommitted at its end: changed from <paramref name="start"/> to <paramref name="end"/> and listed at
    /// the end. What a git line keeps its place with.
    /// </summary>
    public static IReadOnlyList<string> OwnUncommitted(RunManifest start, RunManifest end) =>
        [.. Between(start, end, []).All.Where(path => end.Listed(path) is not null)];

    /// <summary>The paths uncommitted at the turn's start that the turn left as they were: the person's, or another line's.</summary>
    public static IReadOnlyList<string> NotOwn(RunManifest start, RunManifest end)
    {
        var own = Between(start, end, []).All.ToHashSet(StringComparer.Ordinal);
        return [.. start.Entries.Select(entry => entry.Path).Where(path => !own.Contains(path))];
    }
}
