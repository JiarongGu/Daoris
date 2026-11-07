namespace Daoris.Knowledge;

/// <summary>The agent's <c>ready</c> in a closure graph: its result kept as a submission while the quest stays taken.</summary>
internal sealed class QuestReadies(QuestStore quests, QuestReviews reviews, QuestAnswering answering)
{
    /// <summary>
    /// The agent's <c>ready</c> in a closure graph: its result, carrying exactly what a done carries, its
    /// note and an answer to each requirement, kept as a submission while the quest stays taken. Its holds are a done's, and an
    /// unheld one publishes the chain's next step, which seals it. Refused where no closure graph holds the quest, on a take this
    /// machine lost, and on a sealed submission.
    /// </summary>
    /// <param name="session">The session saying it, as its connector's intake names it: kept on the submission.</param>
    public async Task<QuestRespondOutcome> ReadyAsync(
        string id, string? note, IReadOnlyList<QuestAnswer> answers, DateTimeOffset now, CancellationToken ct, string? session)
    {
        if (await quests.FindAsync(id, ct).ConfigureAwait(false) is not { } live)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{id}`. Ids come from `quest_list`.", Quest: null);
        }

        if (NotReady(live) is { } why) return new(QuestRespondRefusal.NotInGraph, why, Quest: null);

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
            return new(QuestRespondRefusal.AlreadyTaken, QuestWords.LostTake(id, "so this ready is not this machine's to say. Nothing moved."), Quest: null);
        }

        if (move.SealedBy is { } successor)
        {
            return new(
                QuestRespondRefusal.Sealed,
                $"Quest `#{id}`'s submission already published its next step, `#{successor}`, which stands on it: a later `ready` "
                + "would change the work that step grew from. Nothing moved. Only the person can reopen it.",
                Quest: null);
        }

        if (move.SourceReopened) return new(QuestRespondRefusal.SourceReopened, QuestWords.SourceReopenedWords(standing), Quest: null);

        if (!move.Moved)
        {
            return new(QuestRespondRefusal.NotInGraph, NotReady(standing)
                ?? $"Quest `#{id}` is taken on another machine: its `ready` is that machine's to say. Nothing moved.", Quest: null);
        }

        return new(
            QuestRespondRefusal.None,
            $"Quest `#{id}` is submitted: it stays Taken, and its graph writes its Done at the work's end, once its holds and gates "
            + $"are through. A later `ready` replaces this one until its next step is published.{await answering.AnsweredAsync(standing, ct).ConfigureAwait(false)}"
            + QuestWords.Then(move.FollowUp),
            standing);
    }

    /// <summary>Why a <c>ready</c> on <paramref name="quest"/> is refused as a state — or null where one may be said.</summary>
    private static string? NotReady(Quest quest) => quest switch
    {
        { Status: QuestStatus.Taken, Closure: not null } => null,
        { Status: QuestStatus.Taken } =>
            $"Quest `#{quest.Id}` is not in a graph that closes it: say `done` when its work is done. Nothing moved.",
        _ => $"Quest `#{quest.Id}` is {quest.Status}: `ready` submits a taken quest's work in a graph that closes it. Nothing moved.",
    };
}
