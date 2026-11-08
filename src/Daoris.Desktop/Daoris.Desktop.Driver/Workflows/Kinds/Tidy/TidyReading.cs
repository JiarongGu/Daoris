namespace Daoris.Driver;

/// <summary>
/// A <c>tidy</c>'s reading: reached once the landing record holds what its door ended at the landing's tip (<see cref="ChainTidied"/>),
/// pending until then. Nothing fails it: a request that did not merge never starts it.
/// </summary>
public static class TidyReading
{
    /// <summary>Whether the landing record holds what the tidy ended at <paramref name="landing"/>'s tip.</summary>
    public static bool Tidied(LandedBranch? landing) =>
        landing?.Tidied is { Partly: false } tidied && string.Equals(tidied.Tip, landing.Tip, StringComparison.OrdinalIgnoreCase);

    internal static NodeObservation Observe(NodeReading reading)
    {
        if (NodeObserving.OfBranch(reading) is { } refused) return refused;
        var landing = NodeObserving.LandingOf(reading.Unit);
        if (!Tidied(landing)) return NodeObserving.Seen(reading, null, "its chain's parts were not tidied yet");

        var parts = landing!.Tidied!.Parts;
        var kept = parts.Count(part => part.Kept is not null);
        return NodeObserving.Reached(reading, NodeSource.Landing, landing.Tip, LandedBranches.FileName, landing.Branch,
            $"{parts.Count} part(s) of its chain ended on the line" + (kept == 0 ? "" : $", {kept} of their branches kept"));
    }
}
