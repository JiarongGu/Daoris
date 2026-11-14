namespace Daoris.Driver;

/// <summary>
/// A run's branch the line already holds at its tip, read before the quest's holds wait: work merged outside the run needs no yes on
/// its quest's done, so the hold lets go and the steps after it reach on the same record (<c>content.holds</c> and <c>land</c> read
/// the branch's held fact), ending the run landed.
/// </summary>
internal static class BranchOnLine
{
    /// <summary>
    /// The hold reached by the line's hold of the run's branch at the tip the pass read; null where there is none, or the branch never
    /// moved from the line (<see cref="NothingToDeliver"/> says that).
    /// </summary>
    public static NodeObservation? Observe(NodeReading reading)
    {
        if (reading.RunBranch is not { Records.Branch: { Held: { } held } branch }) return null;
        if (held.By == EndBy.Empty || !string.Equals(held.Tip, reading.RunBranchTip, StringComparison.OrdinalIgnoreCase)) return null;
        return HoldReading.Reached(reading, SessionBranches.FileName, reading.Binding.SubjectRevision,
            $"`{branch.Branch}` was merged outside the run: {WorkInventory.ProofSaid(held.By, held.On)}, so no earlier hold of its quest waits");
    }
}
