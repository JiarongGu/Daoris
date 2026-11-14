namespace Daoris.Knowledge;

/// <summary>The agent's <c>done</c> on a quest its graph or its run closes: its result kept as a submission while the quest stays taken.</summary>
internal sealed class QuestSubmissions(QuestStore quests, QuestReviews reviews, QuestAnswering answering)
{
    /// <summary>
    /// The agent's <c>done</c> on a quest a closure graph or a run is bound to: its result, carrying exactly what a done carries, its
    /// note and an answer to each requirement, kept as a submission while the quest stays taken. Its holds are a done's, and an
    /// unheld one publishes the chain's next step, which seals it. Refused on a take this machine lost and on a sealed submission.
    /// </summary>
    /// <param name="session">The session saying it, as its connector's intake names it: kept on the submission.</param>
    public async Task<QuestRespondOutcome> SubmitAsync(
        string id, string? note, IReadOnlyList<QuestAnswer> answers, DateTimeOffset now, CancellationToken ct, string? session)
    {
        if (await quests.FindAsync(id, ct).ConfigureAwait(false) is not { } live)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{id}`. Ids come from `quest_list`.", Quest: null);
        }

        if (NotBound(live) is { } why) return new(QuestRespondRefusal.NotInGraph, why, Quest: null);

        // A done's own judgements, unchanged: a set-up step's result has a set-up, and each requirement is answered.
        if (await reviews.NotShownAsync(live, ct).ConfigureAwait(false) is { } unshown) return new(QuestRespondRefusal.NotShown, unshown, Quest: null);
        var judged = await answering.JudgeAnswersAsync(live, answers, ct).ConfigureAwait(false);
        if (judged.Refusal is { } refusal) return new(refusal, judged.Message!, Quest: null);

        var move = await quests.SubmitAsync(id, note, judged.Answers, now, ct, session).ConfigureAwait(false);
        if (move.Quest is not { } standing)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{id}`. Ids come from `quest_list`.", Quest: null);
        }

        if (move.ClaimLost)
        {
            return new(QuestRespondRefusal.AlreadyTaken, QuestWords.LostTake(id, "so this done is not this machine's to say. Nothing moved."), Quest: null);
        }

        if (move.SealedBy is { } successor)
        {
            return new(
                QuestRespondRefusal.Sealed,
                $"Quest `#{id}`'s submission already published its next step, `#{successor}`, which stands on it: a later `done` "
                + "would change the work that step grew from. Nothing moved. Only the person can reopen it.",
                Quest: null);
        }

        if (move.SourceReopened) return new(QuestRespondRefusal.SourceReopened, QuestWords.SourceReopenedWords(standing), Quest: null);

        if (!move.Moved)
        {
            return new(QuestRespondRefusal.NotInGraph, NotBound(standing)
                ?? $"Quest `#{id}` is taken on another machine: its `done` is that machine's to say. Nothing moved.", Quest: null);
        }

        return new(
            QuestRespondRefusal.None,
            $"Quest `#{id}` is submitted: it stays Taken, and its workflow writes its Done at its end, once its steps are through. "
            + $"End your turn now: a step that sends the work back wakes you here. A later done replaces this one until its next step is "
            + $"published.{await answering.AnsweredAsync(standing, ct).ConfigureAwait(false)}"
            + QuestWords.Then(move.FollowUp),
            standing);
    }

    /// <summary>Why a done on <paramref name="quest"/> is no submission as it stands, or null where it is one.</summary>
    private static string? NotBound(Quest quest) => quest switch
    {
        { Status: QuestStatus.Taken, Closure: not null } => null,
        _ => $"Quest `#{quest.Id}` is {quest.Status}, not taken in a graph or a run that closes it, so its done submits nothing. Nothing moved.",
    };
}
