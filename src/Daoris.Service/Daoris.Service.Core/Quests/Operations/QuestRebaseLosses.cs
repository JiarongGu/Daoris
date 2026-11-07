namespace Daoris.Knowledge;

/// <summary>
/// What one quest's rebase has replayed so far, and what the operations that went with this machine's lost take have said, which the
/// kinds that go with a take (a wait, a submission) add to as their <see cref="IQuestOperation.LostAsync"/> runs, and the rebase reads
/// to word the take's conflict once the quest is replayed.
/// </summary>
internal sealed class QuestRebaseLosses(IReadOnlyList<QuestOperation> replayed, string machine)
{
    /// <summary>The questions the waits that went with the lost take were on.</summary>
    internal List<string> Waits { get; } = [];

    /// <summary>Whether a submission went with the lost take.</summary>
    internal bool Submission { get; set; }

    /// <summary>Whether this machine's take lost to the person's reopen, read from the history the rebase has left so far.</summary>
    internal bool LostToTheReopen => QuestLog.LostToTheReopen(replayed, machine);

    /// <summary>Whether anything went with the take that its conflict's note must now name.</summary>
    internal bool Any => Waits.Count > 0 || Submission;

    /// <summary>The lost take's note as given, then the waits and the submission that went with it.</summary>
    internal string? Named(string? note)
    {
        var withWaits = Waits.Count > 0 ? QuestWaitOperation.WaitsWentWith(note, Waits) : note;
        return Submission ? QuestSubmitOperation.SubmissionWentWith(withWaits, LostToTheReopen) : withWaits;
    }
}
