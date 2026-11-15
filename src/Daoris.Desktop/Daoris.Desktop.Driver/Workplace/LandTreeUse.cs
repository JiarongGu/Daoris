namespace Daoris.Driver;

/// <summary>
/// Whether a session on a run's tree keeps the run's <c>land</c> from landing now. A live session uses the tree, except the run's own part
/// whose turn ended waiting on the run's next step (<c>awaiting-step</c>): it runs nothing and waits on this very landing, so counting it would
/// leave each waiting on the other for good. A part is the run's own by what it serves: an executor of the run's branch, or a session of the
/// run's quest.
/// </summary>
internal static class LandTreeUse
{
    /// <summary>Whether <paramref name="record"/>, a session on the tree <paramref name="start"/> lands, uses that tree now.</summary>
    /// <param name="quest">The quest whose work lands; empty for a conversation's run.</param>
    public static bool Uses(SessionRecord record, LandStart start, string? quest) =>
        record.Live && !(record.State == SessionStates.AwaitingStep && Serves(record, start, quest));

    private static bool Serves(SessionRecord record, LandStart start, string? quest) =>
        start.Executors.Contains(record.Id, StringComparer.OrdinalIgnoreCase)
        || (record.Quest is { Length: > 0 } own
            && (string.Equals(own, quest, StringComparison.OrdinalIgnoreCase) || string.Equals(own, start.Run, StringComparison.OrdinalIgnoreCase)));
}
