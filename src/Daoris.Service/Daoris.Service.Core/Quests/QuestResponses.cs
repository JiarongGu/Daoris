namespace Daoris.Knowledge;

/// <summary>Answering a quest: take, done, ready, decline or wait.</summary>
internal sealed class QuestResponses(
    QuestStore quests, QuestReviews reviews, QuestAnswering answering, QuestReadies readies, QuestRemoteClaims claims)
{
    /// <summary>
    /// Answer a quest: take, done, or decline. Declining without a reason is refused — a bare refusal
    /// gives the asker nothing to act on.
    /// </summary>
    public async Task<QuestRespondOutcome> RespondAsync(
        string id, string action, string? reason, DateTimeOffset now, CancellationToken ct,
        // The question a `wait` waits on: a quest the waiting session asked another repository.
        string? on,
        // How a `done` answers each of the quest's requirements.
        IReadOnlyList<QuestAnswer>? answers,
        // A decline that applies only while the quest is open: an abandon's.
        bool whileOpen,
        // The session answering, as its connector's intake names it. A done or a decline keeps it on its operation, as a ready's
        // submission does: which session wrote the handback.
        string? session,
        // The note is a session's handback: the connector's `quest_respond`, so a close whose note only points
        // elsewhere is refused. Every other door, the person's among them, keeps its words as given.
        bool handback)
    {
        // Answers are a done's: carried by any other verb, a wait included, they would be dropped, and
        // dropped looks kept.
        if (answers is { Count: > 0 } && action.ToLowerInvariant() is "take" or "decline" or "wait")
        {
            return new(
                QuestRespondRefusal.BadAnswer,
                $"Answers are given when closing `done`, one for each requirement; a `{action.ToLowerInvariant()}` carries none. "
                + "Nothing moved.",
                Quest: null);
        }

        // The flag is a decline's: carried by any other verb it would be dropped, and dropped looks kept.
        if (whileOpen && !string.Equals(action, "decline", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                QuestRespondRefusal.UnknownAction,
                "`whileOpen` is a decline's: it makes the decline apply only while the quest is open, and a "
                + $"`{action.ToLowerInvariant()}` declines nothing. Nothing moved.",
                Quest: null);
        }

        if (string.Equals(action, "wait", StringComparison.OrdinalIgnoreCase))
        {
            return await WaitAsync(id.TrimStart('#'), on?.TrimStart('#'), now, ct).ConfigureAwait(false);
        }

        // A session's closing note is its handback, the one record of its work whoever reads the quest later reads.
        // One that only points at another message, a commit or a file is refused before anything is read or written.
        if (handback && action.ToLowerInvariant() is "done" or "ready" or "decline" && HandbackNotes.PointsElsewhere(reason) is { } pointer)
        {
            return new(QuestRespondRefusal.PointsElsewhere, HandbackNotes.Refusal(pointer), Quest: null);
        }

        if (string.Equals(action, "ready", StringComparison.OrdinalIgnoreCase))
        {
            return await readies.ReadyAsync(id.TrimStart('#'), reason, answers ?? [], now, ct, session).ConfigureAwait(false);
        }

        var status = action.ToLowerInvariant() switch
        {
            "take" => QuestStatus.Taken,
            "done" => QuestStatus.Done,
            "decline" => QuestStatus.Declined,
            _ => (QuestStatus?)null,
        };

        if (status is null)
        {
            return new(
                QuestRespondRefusal.UnknownAction,
                $"Unknown action '{action}' — one of: take, done, ready, decline, wait.",
                Quest: null);
        }

        if (status == QuestStatus.Declined && string.IsNullOrWhiteSpace(reason))
        {
            return new(
                QuestRespondRefusal.MissingReason,
                "Declining needs a reason: it is the part the asker can act on.",
                Quest: null);
        }

        // A done answers each of the person's requirements, judged before the move. A quest's
        // requirements are fixed at its publish, so a look outside the write judges what the write will hold; whether
        // it may still close is the store's, inside it.
        IReadOnlyList<QuestAnswer> answered = [];
        if (status == QuestStatus.Done
            && await quests.FindAsync(id.TrimStart('#'), ct).ConfigureAwait(false) is { Status: QuestStatus.Open or QuestStatus.Taken } live)
        {
            // A set-up step closes done once it has said what it showed, so a done always has a
            // set-up for the person to look at.
            if (await reviews.NotShownAsync(live, ct).ConfigureAwait(false) is { } unshown) return new(QuestRespondRefusal.NotShown, unshown, Quest: null);

            var judged = await answering.JudgeAnswersAsync(live, answers ?? [], ct).ConfigureAwait(false);
            if (judged.Refusal is { } refusal) return new(refusal, judged.Message!, Quest: null);
            answered = judged.Answers;
        }

        // Every verb commits here, shared or not: the next sync carries it to the remote, where the
        // first push wins and a later one is kept as a conflict rather than lost.
        // A take judges the graph named for it inside its own write: by the session taking, and by the remotes map as
        // it reads at that moment, never by a look taken here before it.
        var move = await quests.MoveAsync(
                id.TrimStart('#'), status.Value, reason, now, ct, answered, whileOpen, byAgent: status == QuestStatus.Done,
                // A close names the session that wrote its handback, in the same write.
                session: status == QuestStatus.Taken ? null : session)
            .ConfigureAwait(false);

        if (move.Quest is null)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{id.TrimStart('#')}`. Ids come from `quest_list`.", Quest: null);
        }

        // A take or an agent's done on a step whose source the person reopened: a stand-down, with its reason.
        if (move.SourceReopened)
        {
            return new(QuestRespondRefusal.SourceReopened, QuestWords.SourceReopenedWords(move.Quest), Quest: null);
        }

        // A done where the quest's graph closes it: the result is a submission, said as the action to take.
        if (move.ClosedByGraph)
        {
            return new(
                QuestRespondRefusal.ClosedByGraph,
                $"Quest `#{move.Quest.Id}` closes through its graph: say `ready` with the same note and answers, and the graph "
                + "writes its Done at the work's end, once its holds and gates are through. Nothing moved.",
                Quest: null);
        }

        if (move.Moved && status == QuestStatus.Taken
            && await claims.ClaimByPushAsync(move.Quest, ct).ConfigureAwait(false) is { } claimed)
        {
            return claimed;
        }

        if (move.Moved)
        {
            // The chain moved on with the close — said here, because the next step is now
            // somebody's open quest and whoever closed this one should know whose.
            return new(
                QuestRespondRefusal.None,
                $"Quest `#{move.Quest.Id}` is now {move.Quest.Status}.{await answering.AnsweredAsync(move.Quest, ct).ConfigureAwait(false)}"
                // Only the next sync can say whether another machine took it first, so the answer says
                // what that would mean; a quest nobody shares has no other machine to take it.
                + (whileOpen && await claims.SharedAtAsync(move.Quest, ct).ConfigureAwait(false) is not null
                    ? " It applies only while it is open: if another machine's take reached the remote first, the next "
                      + "sync keeps this decline on the quest as a conflict, and the take stands."
                    : "")
                + QuestWords.Then(move.FollowUp),
                move.Quest);
        }

        // A close on the winner's take, made after the pass that found this machine's take lost: the stand-down
        // the take's own loss says, since the session making it may not have heard that yet. The person's choices follow, since a
        // decline here may be theirs.
        if (move.ClaimLost)
        {
            return new(
                QuestRespondRefusal.AlreadyTaken,
                QuestWords.LostStep(move.Quest.Id, $"so this {action.ToLowerInvariant()} is not this machine's to make. Nothing moved.", move),
                Quest: null);
        }

        // A decline made while open, refused because somebody took the quest first: said as the decline it
        // is, never as the take race's stand-down, which is a taker's sentence.
        if (whileOpen && move.Quest.Status == QuestStatus.Taken)
        {
            return new(
                QuestRespondRefusal.AlreadyTaken,
                $"Quest `#{move.Quest.Id}` is Taken: this decline applies only while it is open, so nothing was declined "
                + "and the take stands. Its work is its taker's; decline it without `whileOpen` if you mean to stop that work.",
                Quest: null);
        }

        // The store refused the transition; the quest comes back unchanged so the answer can name the
        // state that refused it. For a take that is the race resolving — the same message a
        // session racing an outside session already acts on.
        return move.Quest.Status == QuestStatus.Taken
            ? new(
                QuestRespondRefusal.AlreadyTaken,
                $"Quest `#{move.Quest.Id}` is already taken — someone is working it. Stand down rather than doubling the work.",
                Quest: null)
            : new(
                QuestRespondRefusal.Closed,
                $"Quest `#{move.Quest.Id}` is {move.Quest.Status} — a closed quest does not move; a new ask is a new title.",
                Quest: null);
    }

    /// <summary>
    /// Wait on a question asked of another repository: the quest stays taken, marked, and the
    /// driver resumes it once the question is answered — done or declined — with the answer in hand.
    /// </summary>
    private async Task<QuestRespondOutcome> WaitAsync(string id, string? on, DateTimeOffset now, CancellationToken ct)
    {
        QuestRespondOutcome Refused(string why) => new(QuestRespondRefusal.CannotWait, why, Quest: null);

        if (string.IsNullOrWhiteSpace(on))
        {
            return Refused("A wait names the quest it waits on (`on`): publish the question to the repository "
                + "that knows, then wait on the quest that publish answered.");
        }

        if (string.Equals(on, id, StringComparison.OrdinalIgnoreCase))
        {
            return Refused($"Quest `#{id}` cannot wait on itself — wait on the question you asked another repository.");
        }

        if (await quests.FindAsync(on, ct).ConfigureAwait(false) is not { } question)
        {
            return Refused($"No quest `#{on}` to wait on. Ids come from `quest_publish` and `quest_list`.");
        }

        if (question.Status is QuestStatus.Done or QuestStatus.Declined)
        {
            return Refused($"Quest `#{on}` is already {question.Status}: its answer is there to read now "
                + "(`quest_list` with `includeClosed`), so there is nothing to wait for.");
        }

        var move = await quests.WaitAsync(id, on, now, ct).ConfigureAwait(false);
        if (move.Quest is null)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{id}`. Ids come from `quest_list`.", Quest: null);
        }

        // A wait on the winner's take, made after the pass that found this machine's take lost: the take's
        // stand-down, not a malformed wait, and the question the session asked is still asked.
        if (move.ClaimLost)
        {
            return new(
                QuestRespondRefusal.AlreadyTaken,
                QuestWords.LostTake(id, $"so it does not wait on `#{on}` here. Nothing moved; `#{on}` stays a quest of its own."),
                Quest: null);
        }

        if (!move.Moved)
        {
            return Refused($"Quest `#{id}` is {move.Quest.Status} — only a quest its taker is working can wait, "
                + "because the work in progress is what waits.");
        }

        return new(QuestRespondRefusal.None,
            $"Quest `#{id}` waits on `#{on}` (`{question.To}`). It stays taken by `{move.Quest.To}`; end your turn "
            + "now. The driver starts it again in the same tree once `#" + on + "` is answered, with the answer "
            + "in the instruction.", move.Quest);
    }
}
