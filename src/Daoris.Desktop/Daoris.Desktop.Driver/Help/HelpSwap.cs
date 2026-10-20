namespace Daoris.Driver;

/// <summary>
/// A change of Ask Daoris's access under a conversation that runs (ACCESSSWAP1, D183 point 2): its mode or its configured access
/// (the housekeeping grant, <see cref="ConfiguredAccess"/>) chosen otherwise than its start was handed. Every such change takes the
/// conversation's next turn: its agent ends at its turn's end, never mid-turn, or at once where it is idle, and its next words resume
/// the same conversation under the access chosen then, its history kept (D131), since a start hands its access five ways (its harness's
/// posture, its rules, its tree guard, its servers and its room) and none of them is changed while it runs (D183's HELPMODE1a2 note).
/// </summary>
public static class HelpSwap
{
    /// <summary>
    /// What changed, in clauses joined for a sentence (<c>its mode is now full access, and its housekeeping grants `inspect` now</c>),
    /// or null where the conversation runs under the access chosen.
    /// </summary>
    /// <param name="mode">The mode its start got.</param>
    /// <param name="housekeeping">The housekeeping operations its start was handed, in <see cref="ConfiguredAccess.HousekeepingBuilt"/>'s order.</param>
    /// <param name="chosenMode">The mode the person chose now.</param>
    /// <param name="chosenHousekeeping">The housekeeping operations the record holds now, in the same order.</param>
    public static string? Changed(
        string mode, IReadOnlyList<string> housekeeping, string chosenMode, IReadOnlyList<string> chosenHousekeeping)
    {
        var changed = new List<string>(2);
        if (HelpModes.Recorded(mode) != HelpModes.Recorded(chosenMode))
        {
            changed.Add($"its mode is now {(HelpModes.Recorded(chosenMode) == HelpModes.Full ? "full access" : "restricted")}");
        }

        if (!housekeeping.SequenceEqual(chosenHousekeeping, StringComparer.Ordinal))
        {
            changed.Add(chosenHousekeeping.Count == 0
                ? "its housekeeping grants nothing now"
                : $"its housekeeping grants {string.Join(", ", chosenHousekeeping.Select(operation => $"`{operation}`"))} now");
        }

        return changed.Count == 0 ? null : string.Join(", and ", changed);
    }

    /// <summary>
    /// What the running conversation's console is told (ACCESSSWAP1): what changed, and when it applies: its agent ends now where it
    /// is idle, else at its turn's end, never mid-turn; and its next words resume this conversation under the new access.
    /// </summary>
    /// <param name="changed">What <see cref="Changed"/> said.</param>
    /// <param name="idle">Whether no turn runs, so it ends now.</param>
    public static string Said(string changed, bool idle) =>
        $"— Ask Daoris's access changed under this conversation: {changed}. Its agent ends "
        + (idle ? "now" : "at its turn's end, never mid-turn")
        + ", and your next words resume this conversation under the new access, its history kept.";
}
