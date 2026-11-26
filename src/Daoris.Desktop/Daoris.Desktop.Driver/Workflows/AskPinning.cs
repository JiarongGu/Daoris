namespace Daoris.Driver;

/// <summary>
/// An ask across repositories lands its consumer part with the pin moved to the commit the producer's part landed. Once every part the ask gives
/// a consumer's repository is on its ask branch, and a producer the plan gives a part has landed on its line, the consumer's ask branch takes the pin
/// as a commit of its own, before its check and review, so the gates run against the pinned producer. Until then its <c>parts</c> waits
/// (<see cref="PartsReport.PinsWaiting"/>). Only the lease's loop writes, as the fan-out does.
/// </summary>
public static class AskPinning
{
    /// <summary>The pins moved onto the ask branches of the open runs whose own parts are all landed: the lines the move said.</summary>
    public static async Task<IReadOnlyList<string>> RunAsync(
        IReadOnlyList<RepoView> registry, IReadOnlyDictionary<string, PartsReport> reports, IEnumerable<AskRunOf> runs, IAskPinDoors doors, CancellationToken ct)
    {
        var said = new List<string>();
        foreach (var run in runs)
        {
            if (!reports.TryGetValue(run.Ask, out var report) || !report.For(run.Repository).Reached) continue;
            var row = registry.FirstOrDefault(each => each.Root is { Length: > 0 } && FamilyFanout.Same(each.Repository, run.Repository));
            if (row is null) continue;
            foreach (var pin in await AskPins.ReadAsync(registry, run.Repository, run.Ask, report.Repositories.Values, doors, ct).ConfigureAwait(false))
            {
                if (pin.State != AskPinStates.Owed) continue;
                var moved = await doors.WriteAsync(row, run.Ask, pin.Edge.Producer, pin.Landed!, ct).ConfigureAwait(false);
                said.Add($"family  ask #{run.Ask}: {moved.Said}");
            }
        }

        return said;
    }

    /// <summary>The commit each open run's consumer part lands its pin at, by edge: what the producer's part landed as, whether or not it is pinned yet.</summary>
    public static async Task<IReadOnlyDictionary<PinEdge, string>> CarriedAsync(
        IReadOnlyList<RepoView> registry, IReadOnlyDictionary<string, PartsReport> reports, IEnumerable<AskRunOf> runs, IAskPinDoors doors, CancellationToken ct)
    {
        var carried = new Dictionary<PinEdge, string>();
        foreach (var run in runs)
        {
            if (!reports.TryGetValue(run.Ask, out var report)) continue;
            foreach (var pin in await AskPins.ReadAsync(registry, run.Repository, run.Ask, report.Repositories.Values, doors, ct).ConfigureAwait(false))
            {
                if (pin.Landed is not null) carried[pin.Edge] = pin.Landed;
            }
        }

        return carried;
    }

    /// <summary>The reports with the pins each ask's runs still wait on set, for the runs' <c>parts</c> to read; unchanged where no family stands or no run waits.</summary>
    public static async Task<IReadOnlyDictionary<string, PartsReport>> WithWaitsAsync(
        IReadOnlyList<RepoView> registry, IReadOnlyDictionary<string, PartsReport> reports, IReadOnlyList<AskRunOf> runs, IAskPinDoors doors, CancellationToken ct)
    {
        if (runs.Count == 0 || !FamilyFanout.HasFamily(registry)) return reports;
        var waits = await WaitsAsync(registry, reports, runs, doors, ct).ConfigureAwait(false);
        return reports.ToDictionary(each => each.Key, each => waits.TryGetValue(each.Key, out var held) ? each.Value with { PinWaits = held } : each.Value, StringComparer.Ordinal);
    }

    /// <summary>The producers each open run's repository still waits on for its pin, by ask and then repository, for the run's <c>parts</c> to say.</summary>
    public static async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>> WaitsAsync(
        IReadOnlyList<RepoView> registry, IReadOnlyDictionary<string, PartsReport> reports, IEnumerable<AskRunOf> runs, IAskPinDoors doors, CancellationToken ct)
    {
        var waits = new Dictionary<string, Dictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal);
        foreach (var run in runs)
        {
            if (!reports.TryGetValue(run.Ask, out var report)) continue;
            var holding = (await AskPins.ReadAsync(registry, run.Repository, run.Ask, report.Repositories.Values, doors, ct).ConfigureAwait(false))
                .Where(pin => pin.Holds).Select(pin => pin.Edge.Producer).ToList();
            if (holding.Count == 0) continue;
            if (!waits.TryGetValue(run.Ask, out var ofAsk)) waits[run.Ask] = ofAsk = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            ofAsk[run.Repository] = holding;
        }

        return waits.ToDictionary(each => each.Key, each => (IReadOnlyDictionary<string, IReadOnlyList<string>>)each.Value, StringComparer.Ordinal);
    }
}
