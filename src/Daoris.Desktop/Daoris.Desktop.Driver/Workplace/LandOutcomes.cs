using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// What the performer's tries came to, in memory: the end of each start's last try (<see cref="LandEnds"/>), by unit and tip, which the
/// press that asked for the pass reads to answer. The delivery and the landing record are the durable facts; this is only their words for a
/// press that waited, so a restart loses it and the press then answers from them.
/// </summary>
public static class LandOutcomes
{
    private static readonly ConcurrentDictionary<string, TreeLanding> Landed = new(StringComparer.Ordinal);

    /// <summary>Keeps the end of a try and tees it as the bus's <c>land.outcome</c>, which the run's progression goes on from.</summary>
    internal static void Kept(string home, string unit, string tip, TreeLanding landed)
    {
        Landed[$"{unit}\n{tip}"] = landed;
        BusLog.Of(home)?.Tee(() => new BusDraft(BusTypes.LandOutcome, unit)
        {
            Commit = true,
            Payload = [("landed", landed.Landed), ("code", landed.Landed ? "landed" : landed.Refusal ?? "refused"), ("tip", tip)],
        });
    }

    /// <summary>The end the performer's last try of <paramref name="unit"/> at <paramref name="tip"/> came to; null where none is kept.</summary>
    public static TreeLanding? Of(string unit, string tip) => Landed.GetValueOrDefault($"{unit}\n{tip}");

    /// <summary>Forgets a tip's outcome, so a press that asks to try again reads the next one and not the last.</summary>
    public static void Forget(string unit, string tip) => Landed.TryRemove($"{unit}\n{tip}", out _);
}
