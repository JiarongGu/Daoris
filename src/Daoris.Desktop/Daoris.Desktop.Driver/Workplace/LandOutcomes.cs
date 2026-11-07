using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// What the performer's landings came to, in memory: the landing of each run's newest, by unit and tip, which the press that
/// asked for the pass reads to answer. The delivery and the landing record are the durable facts; this is only their words for a press that
/// waited, so a restart loses it and the press then answers from them.
/// </summary>
public static class LandOutcomes
{
    private static readonly ConcurrentDictionary<string, TreeLanding> Landed = new(StringComparer.Ordinal);

    internal static void Kept(string unit, string tip, TreeLanding landed) => Landed[$"{unit}\n{tip}"] = landed;

    /// <summary>The landing the performer made of <paramref name="unit"/> at <paramref name="tip"/>, or is refused; null where none is kept.</summary>
    public static TreeLanding? Of(string unit, string tip) => Landed.GetValueOrDefault($"{unit}\n{tip}");

    /// <summary>Forgets a tip's outcome, so a press that asks to try again reads the next one and not the last.</summary>
    public static void Forget(string unit, string tip) => Landed.TryRemove($"{unit}\n{tip}", out _);
}
