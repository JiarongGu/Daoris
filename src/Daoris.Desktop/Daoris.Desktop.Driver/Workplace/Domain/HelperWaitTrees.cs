namespace Daoris.Driver;

/// <summary>
/// What a record that ended waiting on its own helper holds (HELPERWAIT1; Codex's #5 on COWORKUX1): the tidy, the lock's let-go and the
/// planner's further part read it beside the live sessions, so the workplace names this pure rule and
/// <see cref="HelperWaits"/> keeps it under its own names for the readers above.
/// </summary>
internal static class HelperWaitTrees
{
    /// <summary>
    /// Whether <paramref name="record"/> ended waiting on its own helper and so still holds its tree: completed by that code, a record of
    /// this machine's, words waiting or not, since it goes on in that tree with them.
    /// </summary>
    internal static bool HoldsTree(PriorSession record) =>
        !record.Teammate && string.Equals(record.State, "completed", StringComparison.OrdinalIgnoreCase)
        && Carries(record.NoteParts, NoteCodes.EndedAwaitsHelper);

    /// <summary>Whether a note's parts carry a line of <paramref name="code"/>.</summary>
    internal static bool Carries(IReadOnlyList<NotePart>? parts, NoteCode code) =>
        parts?.Any(part => string.Equals(part.Code, code.Code, StringComparison.Ordinal)) == true;
}
