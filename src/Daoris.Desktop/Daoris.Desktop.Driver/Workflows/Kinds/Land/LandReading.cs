namespace Daoris.Driver;

/// <summary>
/// A <c>land</c>'s reading, and a <c>review</c>'s on a branch through <see cref="ReviewReading"/>: the work handed on, reached by the source
/// the kind names, since the doors that accept a branch's work land it in one press.
/// </summary>
internal static class LandReading
{
    public static NodeObservation Observe(NodeReading reading, string source)
    {
        if (NodeObserving.OfBranch(reading) is { } refused) return refused;
        var (unit, binding) = (reading.Unit, reading.Binding);
        var branch = unit.Records.Branch;
        if (binding.Subject is not null)
        {
            // A run's `land` is its session's work handed on, reached where the session
            // branch's landing holds the tip it is bound to, made at it or squashing it (LandedBranch.Squashed), so a squash, whose commit is
            // another, still reaches it. Its acceptance keeps the bound tip, which its binding lapses on and the one table checks; its evidence
            // names the landing's tip, which what follows binds. Merged into the line with no branch landed, as below.
            if (binding.SubjectRevision is { } bound
                && unit.Records.Landings.LastOrDefault(each => each.Tip == bound || each.Squashed == bound) is { } holding)
            {
                return NodeObserving.ReachedAt(reading, source, bound, holding.Tip, LandedBranches.FileName, holding.Branch,
                    $"`{holding.Branch}` was made at {TextBounds.ShortCommit(holding.Tip, 8)} {WorkInventory.Minute(holding.LandedAt)} UTC"
                    + (holding.Tip == bound ? "" : $", squashing {TextBounds.ShortCommit(bound, 8)}"));
            }

            if (binding.SubjectRevision is { } tip && reading.Git?.Content?.Invoke(unit.Id, tip)?.Line is { HoldsAll: true } onLine)
            {
                return NodeObserving.ReachedAt(reading, source, tip, onLine.Held.Onto, "git", $"refs/heads/{onLine.Name}", onLine.Said)
                    with { Words = onLine.Said };
            }

            return branch?.Held is { } joined
                ? NodeObserving.Reached(reading, source, joined.Tip, SessionBranches.FileName, branch.Branch, $"merged: {WorkInventory.ProofSaid(joined.By, joined.On)}")
                : NodeObserving.Seen(reading, null, "nothing was handed on at the bound commit yet");
        }

        if (NodeObserving.LandingOf(unit) is { } landing)
        {
            return NodeObserving.Reached(reading, source, landing.Tip, LandedBranches.FileName, landing.Branch,
                $"`{landing.Branch}` was made at {TextBounds.ShortCommit(landing.Tip, 8)} {WorkInventory.Minute(landing.LandedAt)} UTC");
        }

        return branch?.Held is { } merged
            ? NodeObserving.Reached(reading, source, merged.Tip, SessionBranches.FileName, branch.Branch, $"merged: {WorkInventory.ProofSaid(merged.By, merged.On)}")
            : NodeObserving.Seen(reading, null, "nothing was handed on yet");
    }
}
