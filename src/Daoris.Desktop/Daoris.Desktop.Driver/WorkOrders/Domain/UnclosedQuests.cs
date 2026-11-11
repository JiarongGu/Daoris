namespace Daoris.Driver;

/// <summary>
/// A taken quest whose last session here the person or the steward finished at a checkpoint, leaving the quest as it stands: the
/// session ends <c>completed</c>, nothing here carries the quest on, and the driver never writes quest state. So it is put before the
/// person (<see cref="StartVerdict.Unclosed"/>) rather than left taken in silence: their done closes it, and a word to that session goes
/// on with it in its tree, which is why the tidy keeps that tree meanwhile.
/// </summary>
internal static class UnclosedQuests
{
    /// <summary>
    /// The record that left <paramref name="quest"/> so, or null: a quest asking another repository, held by a reopen, submitted at its
    /// workflow or no longer this machine's take keeps its own reading, and so does a record the person's words wait on.
    /// </summary>
    internal static PriorSession? LeftBy(QuestView quest, Snapshot snapshot) =>
        quest is { Status: "Taken", Awaits: null or "", HeldByReopen: false, Reopened: null }
        && !QuestSubmissions.Current(quest)
        && snapshot.LastRun.TryGetValue(quest.Id, out var last)
        && last is { Teammate: false, WordsWaiting: false, PersonSaid: false }
        && string.Equals(last.State, "completed", StringComparison.OrdinalIgnoreCase)
        && Finished(last)
        && !(snapshot.Takes.TryGetValue(quest.Id, out var take) && QuestTakes.TakenAway(quest, last, take))
            ? last
            : null;

    /// <summary>The verdict: the session that left it, and the two doors that move it, the terminal's done named.</summary>
    internal static Consideration Held(QuestView quest, PriorSession left) =>
        new(quest, StartVerdict.Unclosed,
            $"session `{left.Session}` ended and left `#{quest.Id}` taken, and nothing here carries it on: your done closes it — "
            + $"`daoris quest done {quest.Id}` — or a word to `{left.Session}` goes on with it.")
        {
            HeldBy = left,
            Said = new(SittingFact.Unclosed) { Session = left.Session },
        };

    // The finish's own sentence, the person's words written in its place, or a go-on from such an end that ended with the quest as it
    // stood. Every other completed end carries a code something else reads: done, submitted, asked, reopened, its helper.
    private static bool Finished(PriorSession last) =>
        last.NoteParts?.Any(part =>
            part.Code == EndNoteCodes.StoppedCheckpointFinished.Code
            || part.Code == EndNoteCodes.StoppedCheckpointFinishedSteward.Code
            || part.Code == EndNoteCodes.EndedWentOn.Code
            || (part.Code is null && part.By == NoteBy.Person)) == true;
}
