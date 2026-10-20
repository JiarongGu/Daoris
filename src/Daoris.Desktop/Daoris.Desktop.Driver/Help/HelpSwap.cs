namespace Daoris.Driver;

/// <summary>
/// A change of Ask Daoris's access under a conversation that runs (ACCESSSWAP1, D183 point 2): its mode or its configured access
/// (the housekeeping grant and the chosen settings, <see cref="ConfiguredAccess"/>) chosen otherwise than its start was handed. Every such change takes the
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
    /// <param name="handed">The configured access its start was handed, each grant in its built order.</param>
    /// <param name="chosenMode">The mode the person chose now.</param>
    /// <param name="chosen">The configured access the record holds now, in the same order.</param>
    public static string? Changed(string mode, ConfiguredAccess handed, string chosenMode, ConfiguredAccess chosen)
    {
        var changed = new List<string>(3);
        if (HelpModes.Recorded(mode) != HelpModes.Recorded(chosenMode))
        {
            changed.Add($"its mode is now {(HelpModes.Recorded(chosenMode) == HelpModes.Full ? "full access" : "restricted")}");
        }

        if (!handed.Housekeeping.SequenceEqual(chosen.Housekeeping, StringComparer.Ordinal))
        {
            changed.Add(chosen.Housekeeping.Count == 0
                ? "its housekeeping grants nothing now"
                : $"its housekeeping grants {Named(chosen.Housekeeping)} now");
        }

        // HELPMODE1c3: the chosen settings, said the same way.
        if (!handed.Settings.SequenceEqual(chosen.Settings, StringComparer.Ordinal))
        {
            changed.Add($"its chosen settings are {(chosen.Settings.Count == 0 ? "none" : Named(chosen.Settings))} now");
        }

        return changed.Count == 0 ? null : string.Join(", and ", changed);
    }

    private static string Named(IReadOnlyList<string> codes) => string.Join(", ", codes.Select(code => $"`{code}`"));

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
