namespace Daoris.Driver;

/// <summary>
/// A change of Ask Daoris's mode or checkouts turned off (<see cref="ConfiguredAccess"/>) under a conversation that runs. It takes
/// the next turn: the agent ends at its turn's end, or at once where idle, and its next words resume the same conversation under
/// the access chosen then, since a start hands its access five ways and none is changed while it runs.
/// </summary>
public static class HelpSwap
{
    /// <summary>What changed, in clauses joined for a sentence (<c>its mode is now full access</c>), or null where none did.</summary>
    /// <param name="mode">The mode its start got.</param>
    /// <param name="handed">The configured access its start handed, each grant in its built order.</param>
    /// <param name="chosenMode">The mode the person chose now.</param>
    /// <param name="chosen">The configured access the record holds now, in the same order.</param>
    public static string? Changed(string mode, ConfiguredAccess handed, string chosenMode, ConfiguredAccess chosen)
    {
        var changed = new List<string>(3);
        if (HelpModes.Recorded(mode) != HelpModes.Recorded(chosenMode))
        {
            changed.Add($"its mode is now {(HelpModes.Recorded(chosenMode) == HelpModes.Full ? "full access" : "restricted")}");
        }

        if (!handed.CheckoutsOff.SequenceEqual(chosen.CheckoutsOff, StringComparer.OrdinalIgnoreCase))
        {
            changed.Add(chosen.CheckoutsOff.Count == 0
                ? "it reads every checkout it may read now"
                : $"the checkouts turned off for it are {Named(chosen.CheckoutsOff)} now");
        }

        return changed.Count == 0 ? null : string.Join(", and ", changed);
    }

    private static string Named(IReadOnlyList<string> codes) => string.Join(", ", codes.Select(code => $"`{code}`"));

    /// <summary>What the running conversation's console is told: what changed, and when its agent ends.</summary>
    /// <param name="changed">What <see cref="Changed"/> said.</param>
    /// <param name="idle">Whether no turn runs, so it ends now.</param>
    public static string Said(string changed, bool idle) =>
        $"— Ask Daoris's access changed under this conversation: {changed}. Its agent ends "
        + (idle ? "now" : "at its turn's end, never mid-turn")
        + ", and your next words resume this conversation under the new access, its history kept.";
}
