namespace Daoris.Driver;

/// <summary>
/// A turn in a git place under the writer's lease: its start kept with the paths the line carried in and compared with the place's
/// last manifest, its end kept once, and what of its own stays uncommitted the record's <c>holds_place</c> mark. A folder is read by
/// <see cref="FolderTurns"/>; a git place whose status cannot be read reads none and is told nothing.
/// </summary>
public static class PlaceTurns
{
    /// <summary>Read and keep the start's manifest of <paramref name="record"/> working in <paramref name="root"/>, and what it is told.</summary>
    /// <param name="continues">The record a carry-on continues, whose end's own paths it carries in; null for a first start.</param>
    /// <returns>What the session is told, or null where the place reads no manifest.</returns>
    public static async Task<PlaceTold?> StartAsync(
        string home, string workspace, string department, string root, string record, string? continues, CancellationToken ct,
        PlaceExcludes? excludes = null, IReadOnlyList<SessionEvent>? events = null)
    {
        var prior = new ManifestStore(home, workspace, department).Read(record, ManifestMoment.End) is not null ? record : continues;
        var unattributed = prior is null ? [] : TurnUnattributed.Of(home, prior, events);
        return await StartReadAsync(home, workspace, department, root, record, continues, excludes, ct).ConfigureAwait(false) is { } told
            ? told with { Unattributed = unattributed }
            : null;
    }

    private static async Task<PlaceTold?> StartReadAsync(
        string home, string workspace, string department, string root, string record, string? continues, PlaceExcludes? excludes, CancellationToken ct)
    {
        if (PlaceKind.At(root) == PlaceKind.Folder) return FolderTurns.Start(home, workspace, department, root, record, null, excludes);
        var store = new ManifestStore(home, workspace, department);
        var last = store.Last();
        RunManifest start;
        try
        {
            start = await ManifestReader.OfPlaceAsync(root, store.Content, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        var carried = (store.Read(record, ManifestMoment.End) ?? (continues is null ? null : store.Read(continues, ManifestMoment.End)))?.Own ?? [];
        var own = carried.Where(path => start.Listed(path) is not null).ToList();
        store.Keep(record, ManifestMoment.Start, start with { Root = root, Own = own });
        var committed = last is null ? [] : await ManifestReader.CommittedBetweenAsync(root, last.Head, start.Head, ct).ConfigureAwait(false);
        var outside = last is null ? [] : PlaceChangesSince.Between(last, start, committed).All;
        return new PlaceTold(outside, Others(start, own));
    }

    /// <summary>
    /// Read and keep the end's manifest of <paramref name="record"/>'s turn in place, once: an end already read after its start is
    /// answered from what it kept, so the person's end of a park never takes their later edits for the turn's.
    /// </summary>
    /// <returns>The line's own uncommitted paths it keeps the place with, empty where none; null where it had no turn in place.</returns>
    public static async Task<IReadOnlyList<string>?> EndAsync(string home, string record, CancellationToken ct)
    {
        if (Find(home, record) is not { } found || found.Start.Root is not { } root) return null;
        var (store, start) = found;
        if (store.Read(record, ManifestMoment.End) is { } read && read.At >= start.At) return read.Own ?? [];
        if (start.Kind == PlaceKind.Folder) return FolderTurns.End(home, store, record, start);
        RunManifest end;
        try
        {
            end = await ManifestReader.OfPlaceAsync(root, store.Content, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        var own = PlaceChangesSince.OwnUncommitted(start, end)
            .Concat((start.Own ?? []).Where(path => end.Listed(path) is not null))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        store.Keep(record, ManifestMoment.End, end with { Root = root, Own = own });
        return own;
    }

    /// <summary>The uncommitted paths <paramref name="record"/>'s start found that are not its line's own: what its denies guard.</summary>
    public static IReadOnlyList<string> Others(string home, string record) =>
        Find(home, record) is { Start.Kind: PlaceKind.Git } found ? Others(found.Start, found.Start.Own ?? []) : [];

    private static List<string> Others(RunManifest start, IReadOnlyCollection<string> own) =>
        [.. start.Entries.Select(entry => entry.Path).Where(path => !own.Contains(path))];

    /// <summary>The store holding <paramref name="record"/>'s start, and that start, wherever under the home its place is.</summary>
    internal static (ManifestStore Store, RunManifest Start)? Find(string home, string record)
    {
        var places = DaorisHome.Entry(home, HomeEntry.Places);
        if (!Directory.Exists(places)) return null;
        foreach (var workspace in Directory.EnumerateDirectories(places))
        {
            foreach (var department in Directory.EnumerateDirectories(workspace))
            {
                if (!File.Exists(Path.Combine(department, "manifests", $"{record}-start.jsonl.gz"))) continue;
                var store = new ManifestStore(home, Path.GetFileName(workspace), Path.GetFileName(department));
                if (store.Read(record, ManifestMoment.Start) is { } start) return (store, start);
            }
        }

        return null;
    }
}
