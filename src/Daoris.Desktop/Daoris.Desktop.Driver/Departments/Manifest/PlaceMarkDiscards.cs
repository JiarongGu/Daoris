using Daoris.Contracts.Sessions;

namespace Daoris.Driver;

/// <summary>The paths a person's act in a repository's own checkout took out of a parked line's mark: each marked path that no longer differs from <c>HEAD</c>.</summary>
public static class PlaceMarkDiscards
{
    /// <summary>Each parked line of <paramref name="repository"/> in <paramref name="checkout"/> with the marked paths now clean.</summary>
    public static async Task<IReadOnlyList<(string Session, IReadOnlyList<string> Paths)>> DiscardedAsync(
        IEnumerable<SessionView> active, string repository, string checkout, CancellationToken ct)
    {
        var discarded = new List<(string, IReadOnlyList<string>)>();
        foreach (var line in active.Where(session =>
                     string.Equals(session.Repository, repository, StringComparison.OrdinalIgnoreCase)
                     && session.Tree is { } tree && TreeGit.SamePath(tree, checkout)
                     && SessionStates.IsParked(session.State) && session.HoldsPlace is { Count: > 0 }))
        {
            if (await ChangedAsync(checkout, line.HoldsPlace!, ct).ConfigureAwait(false) is not { } changed) continue;
            var clean = line.HoldsPlace!.Where(path => !changed.Contains(path)).ToList();
            if (clean.Count > 0) discarded.Add((line.Id, clean));
        }

        return discarded;
    }

    /// <summary>
    /// Release from each parked line's mark in <paramref name="checkout"/> the paths a person's act there just took out, in the ledger.
    /// A ledger that cannot be read or refuses leaves the mark standing: it holds the place until a turn, a discard or the person's press
    /// clears it.
    /// </summary>
    /// <returns>How many paths were released.</returns>
    public static async Task<int> ReleaseAsync(ServiceClient service, string repository, string checkout, CancellationToken ct)
    {
        var released = 0;
        try
        {
            var active = await service.ActiveSessionsAsync(ct).ConfigureAwait(false);
            foreach (var (session, paths) in await DiscardedAsync(active, repository, checkout, ct).ConfigureAwait(false))
            {
                if ((await service.SendAsync(new ReleasePlace(session, [.. paths]), ct).ConfigureAwait(false)).Reply.Ok) released += paths.Count;
            }
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            // The act itself is done; the mark is read again at the next act or cleared by the line's next turn.
        }

        return released;
    }

    /// <summary>The paths of <paramref name="paths"/> git's status still lists, or null where git could not read it.</summary>
    private static async Task<HashSet<string>?> ChangedAsync(string root, IReadOnlyList<string> paths, CancellationToken ct)
    {
        try
        {
            var asked = new HashSet<string>(paths, StringComparer.Ordinal);
            return [.. (await MachinePorts.Repository.StatusEntriesAsync(root, ct).ConfigureAwait(false)).Select(each => each.Path).Where(asked.Contains)];
        }
        catch (DriverException)
        {
            return null;
        }
    }
}
