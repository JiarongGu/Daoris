using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The dependency store's rule, the Disk space card's <c>deps</c> kind (<see cref="KeepKinds.Deps"/>): once a day in the tidy's Keeping
/// pass, the entries not placed or sealed within the kind's days go, then the least recently placed while the store holds more than its
/// size limit (<see cref="DependencyStore.Evict(string, TimeSpan, long, DateTimeOffset)"/>). A placed copy is its tree's, so it stays.
/// </summary>
internal sealed class DepsKeeping(Func<DateTimeOffset> clock)
{
    private readonly object _gate = new();
    private DateTimeOffset? _daily;

    /// <summary>Whether the day's eviction is due: a day since the last, and an entry to weigh.</summary>
    internal bool Due(string home)
    {
        lock (_gate)
        {
            if (_daily is { } ran && clock() - ran < TimeSpan.FromDays(1)) return false;
        }

        return DependencyStore.Entries(home).Count > 0;
    }

    /// <summary>The day's eviction: how many entries went, and their bytes.</summary>
    internal (int Removed, long Bytes) Keep(string home, KeepPolicy policy)
    {
        var now = clock();
        var sizes = DependencyStore.Entries(home).ToDictionary(each => each.Key, each => each.Bytes, StringComparer.Ordinal);
        var limit = policy.Limits.DepsGb is { } gb ? (long)gb << 30 : long.MaxValue;
        var gone = DependencyStore.Evict(home, policy.DepsDays, limit, now);
        lock (_gate) _daily = now;
        return (gone.Count, gone.Sum(key => sizes.GetValueOrDefault(key)));
    }

    /// <summary>When the rule next takes an entry by its days: the earliest day, how many go then, and their bytes; null where none is held.</summary>
    internal static JsonObject? Next(string home, KeepPolicy policy)
    {
        var entries = DependencyStore.Entries(home);
        if (entries.Count == 0) return null;
        var day = entries.Min(each => each.Placed).UtcDateTime.Date;
        var that = entries.Where(each => each.Placed.UtcDateTime.Date == day).ToList();
        var at = that.Min(each => each.Placed) + policy.DepsDays;
        return new JsonObject { ["at"] = Moments.Write(at), ["count"] = that.Count, ["bytes"] = that.Sum(each => each.Bytes) };
    }
}
