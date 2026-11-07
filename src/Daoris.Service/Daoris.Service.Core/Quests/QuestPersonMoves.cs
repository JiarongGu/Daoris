namespace Daoris.Knowledge;

/// <summary>
/// The person's own moves on a quest: their yes to a departure, a reopen of a sealed submission, the word to go on with a held
/// step, and their done. The person's doors, never an agent's: no connector tool reaches any of them. Each commits here and
/// travels like any verb.
/// </summary>
internal sealed class QuestPersonMoves(QuestStore quests, QuestReviews reviews)
{
    /// <summary>
    /// The sentence a person's done writes on its quest: theirs, with their words after it where
    /// they gave any. The record's English, byte for byte: the page words its lead from <see cref="PersonDoneParts"/>.
    /// </summary>
    public static string PersonDoneNote(string? words) =>
        QuestWords.Blank(words) is { } said ? $"The person marked this done: {said}" : "The person marked this done.";

    /// <summary>
    /// The note's parts beside <see cref="PersonDoneNote"/>: Daoris's lead by its code, which a
    /// page words in its reader's language, then the person's words as a part of their own, as written. Each part's English is
    /// the note's own, so the lead's ends on the colon where words follow it.
    /// </summary>
    public static string PersonDoneParts(string? words) =>
        NoteParts.After(null, null, QuestWords.Blank(words) is { } said
            ? [NoteLine.Coded(LedgerNoteCodes.PersonDone, "The person marked this done:"), NoteLine.Said(said, "person")]
            : [NoteLine.Coded(LedgerNoteCodes.PersonDone, "The person marked this done.")]);

    /// <summary>
    /// The person accepts a done's departure from what they required, or a done held for its evidence
    /// as it stands: what it held goes on — the chain's next step is published now, and a quest waiting
    /// on it resumes at the driver's next look. Only a held quest takes a yes; any other is refused saying why, as a
    /// state, not a malformed ask.
    /// </summary>
    public async Task<QuestRespondOutcome> AcceptAsync(string id, DateTimeOffset now, CancellationToken ct = default)
    {
        var quest = id.TrimStart('#');
        var move = await quests.AcceptAsync(quest, now, ct).ConfigureAwait(false);
        if (move.Quest is null)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{quest}`. Ids come from `quest_list`.", Quest: null);
        }

        if (!move.Moved)
        {
            var standing = move.Quest;
            return new(QuestRespondRefusal.NotHeld, standing switch
            {
                { Status: not QuestStatus.Done } =>
                    $"Quest `#{standing.Id}` is {standing.Status}: only a quest closed done with a departure from what you "
                    + "required waits for your yes.",
                // A yes accepts a departure or its evidence, never a review: said for what it is.
                { Hold: QuestHold.Unreviewed } =>
                    $"Quest `#{standing.Id}` waits for your review in `{standing.SetUpIn}`, which a yes does not give: say "
                    + "`reviewed` once you have looked at what it shows, or skip the review for this work.",
                { Accepted: { } at } =>
                    $"Quest `#{standing.Id}`'s departure was already accepted, {QuestWords.When(at)}: nothing waits for a yes.",
                _ => $"Quest `#{standing.Id}` closed done departing from none of what you required: nothing waits for a yes.",
            }, Quest: null);
        }

        var resumes = QuestWords.Resumes(await quests.WaitingOnAsync(move.Quest.Id, ct).ConfigureAwait(false));
        // A done held for its evidence alone is accepted as it stands, unread or missing; a departure's
        // words are as they were, since its yes is the same yes.
        var accepted = move.Quest.Answers.Any(answer => answer.IsDeparture)
            ? $"Accepted the departure on quest `#{move.Quest.Id}`"
            : $"Accepted quest `#{move.Quest.Id}`'s done as it stands, its evidence {(move.Quest.Evidence is null ? "unread" : "not found")}";
        // A set-up step's review still holds it once a yes accepts the rest, and what follows waits on that.
        if (move.Quest.Hold == QuestHold.Unreviewed)
        {
            return new(QuestRespondRefusal.None, $"{accepted}.{await reviews.ReviewWaitsAsync(move.Quest, ct).ConfigureAwait(false)}", move.Quest);
        }

        return new(QuestRespondRefusal.None, $"{accepted}: what it held goes on.{resumes}{QuestWords.Then(move.FollowUp)}", move.Quest);
    }

    /// <summary>
    /// The person reopens a sealed submission: the quest stays taken with no current submission, so a
    /// close naming it is stale and its agent's next <c>ready</c> is judged anew; the step it published is withdrawn if nobody
    /// took it, and held with its reason if somebody did. Only the machine holding the take reopens, as only it submits.
    /// </summary>
    public async Task<QuestRespondOutcome> ReopenAsync(
        string id, QuestOperationRef submission, string? note, DateTimeOffset now, CancellationToken ct = default)
    {
        var quest = id.TrimStart('#');
        var reopening = await quests.ReopenAsync(
            quest, submission, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), now, ct).ConfigureAwait(false);
        var standing = reopening.Quest;
        var step = reopening.Step;
        return reopening.Refusal switch
        {
            QuestReopenRefusal.None => new(
                QuestRespondRefusal.None,
                $"Reopened quest `#{quest}`'s submission: it stays Taken with none current, so its graph closes nothing on it, and its "
                + "agent's next `ready` is judged from the start."
                + (step is { Status: QuestStatus.Taken }
                    ? $" Its next step, `#{step!.Id}`, was already taken: it is held, its session goes no further, and you choose to go "
                      + "on with it once the new work is ready, or to abandon it."
                    : $" Its next step, `#{step!.Id}`, is withdrawn: no session takes it, and the next unheld submission publishes it "
                      + "again as the same quest."),
                standing),
            QuestReopenRefusal.NotFound => new(QuestRespondRefusal.NotFound, $"No quest `#{quest}`. Ids come from `quest_list`.", null),
            QuestReopenRefusal.NotTaken => new(
                QuestRespondRefusal.Closed,
                $"Quest `#{quest}` is {standing!.Status}: only a taken quest's submission is reopened"
                + (standing.Status == QuestStatus.Done ? ", and its close stands" : "") + ". Nothing moved.",
                null),
            QuestReopenRefusal.NotHeld => new(
                QuestRespondRefusal.AlreadyTaken,
                $"Quest `#{quest}` is taken on another machine, or this machine's take on it lost: its submission is reopened on the "
                + "machine that made it. Nothing moved.",
                null),
            QuestReopenRefusal.NotCurrent => new(
                QuestRespondRefusal.NotSealed,
                standing!.Submission is null
                    ? $"Quest `#{quest}` has no current submission to reopen: its agent's next `ready` makes one. Nothing moved."
                    : $"Quest `#{quest}`'s current submission is not the one named: only the current one is reopened. Nothing moved.",
                null),
            QuestReopenRefusal.StepClosed => new(
                QuestRespondRefusal.NotSealed,
                $"Quest `#{quest}`'s next step, `#{step!.Id}`, is already {step.Status} on that submission, and a reopen takes no "
                + $"close back. Nothing moved. Close `#{quest}` yourself, or ask anew.",
                null),
            _ => new(
                QuestRespondRefusal.NotSealed,
                $"Quest `#{quest}`'s submission published no step that stands on it, so nothing seals it: its agent's next `ready` "
                + "replaces it. Nothing moved.",
                null),
        };
    }

    /// <summary>
    /// The person's word to go on with a step its source's reopen held: it grows from the source's current
    /// submission once that is unheld, its hold let go, and any result it submitted on the reopened work cleared, with
    /// the next step that result published withdrawn or held. To abandon it instead is their decline, at the decline's
    /// own door.
    /// </summary>
    public async Task<QuestRespondOutcome> ContinueAsync(string id, DateTimeOffset now, CancellationToken ct = default)
    {
        var quest = id.TrimStart('#');
        var going = await quests.ContinueAsync(quest, now, ct).ConfigureAwait(false);
        var parent = going.Quest?.Parent;
        return going.Refusal switch
        {
            QuestReopenRefusal.None => new(
                QuestRespondRefusal.None,
                $"Quest `#{quest}` goes on: it grows from `#{parent}`'s current submission now, and its hold is let go. Its work "
                + "stands on what was reopened, so any result it submitted is cleared: bring its branch up to date with that submission "
                + "first, and it must submit again from there."
                + SuccessorWords(going.Successor),
                going.Quest),
            QuestReopenRefusal.NotFound => new(QuestRespondRefusal.NotFound, $"No quest `#{quest}`. Ids come from `quest_list`.", null),
            QuestReopenRefusal.NotWithdrawn => new(
                QuestRespondRefusal.NotHeld,
                $"Quest `#{quest}` is {going.Quest!.Status}, and no reopen of the work it grew from holds it: nothing waits for your "
                + "word to go on.",
                null),
            QuestReopenRefusal.StepClosed => new(
                QuestRespondRefusal.NotSealed,
                $"Quest `#{quest}`'s next step, `#{going.Step!.Id}`, is already {going.Step.Status} on the result it submitted, and a "
                + "reopen takes no close back, so going on would leave its next submission unsealed for good. Nothing moved. Close "
                + $"`#{quest}` yourself, decline it, or ask anew.",
                null),
            _ => new(
                QuestRespondRefusal.NotSealed,
                $"Quest `#{quest}` grew from `#{parent}`, whose work has no unheld submission here yet: it can go on once that work is "
                + "ready again. Nothing moved.",
                null),
        };
    }

    /// <summary>
    /// What the go-on did to the step's own successor, which the cleared result had published: withdrawn until the step
    /// submits again, or held for the person's word as the step is. Nothing where none stood on that result.
    /// </summary>
    private static string SuccessorWords(Quest? successor) => successor switch
    {
        null => "",
        { Status: QuestStatus.Open } => $" Its next step `#{successor.Id}`, which that result published, is withdrawn until it submits again.",
        _ => $" Its next step `#{successor.Id}`, which that result published, is held: once this one submits again, it goes on by your word.",
    };

    /// <summary>
    /// The person marks a quest done, open or taken: what the quest page's *Mark done…* and
    /// <c>daoris quest done</c> say, after a session finished at a checkpoint left its quest taken, or whenever they
    /// decide its work is done. It is the person's done, so it answers none of their requirements one by one: they are the
    /// person's own words, and the done is their word on them. Its note says it is theirs (<see cref="PersonDoneNote"/>), its
    /// parts beside it (<see cref="PersonDoneParts"/>).
    /// </summary>
    /// <remarks>
    /// A closed quest does not move, and a done on a take this machine lost is the lost take's stand-down, each said
    /// as the respond door says it. It publishes a chain's next step and lets a quest waiting on it resume.
    /// </remarks>
    public async Task<QuestRespondOutcome> PersonDoneAsync(string id, string? words, DateTimeOffset now, CancellationToken ct = default)
    {
        var quest = id.TrimStart('#');

        // A set-up step's done has a set-up, the person's own included (*I set it up myself…*).
        if (await quests.FindAsync(quest, ct).ConfigureAwait(false) is { Status: QuestStatus.Open or QuestStatus.Taken } live
            && await reviews.NotShownAsync(live, ct).ConfigureAwait(false) is { } unshown)
        {
            return new(QuestRespondRefusal.NotShown, unshown, Quest: null);
        }

        var move = await quests.MoveAsync(
            quest, QuestStatus.Done, PersonDoneNote(words), now, ct, noteParts: PersonDoneParts(words)).ConfigureAwait(false);
        if (move.Quest is null)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{quest}`. Ids come from `quest_list`.", Quest: null);
        }

        if (move.ClaimLost)
        {
            return new(
                QuestRespondRefusal.AlreadyTaken,
                QuestWords.LostStep(move.Quest.Id, "so this done is not this machine's to make. Nothing moved.", move),
                Quest: null);
        }

        if (!move.Moved)
        {
            return new(
                QuestRespondRefusal.Closed,
                $"Quest `#{move.Quest.Id}` is {move.Quest.Status} — a closed quest does not move; a new ask is a new title.",
                Quest: null);
        }

        var resumes = QuestWords.Resumes(await quests.WaitingOnAsync(move.Quest.Id, ct).ConfigureAwait(false));
        return new(
            QuestRespondRefusal.None,
            $"Quest `#{move.Quest.Id}` is now Done: you marked it done.{await reviews.ReviewWaitsAsync(move.Quest, ct).ConfigureAwait(false)}"
            + $"{resumes}{QuestWords.Then(move.FollowUp)}",
            move.Quest);
    }
}
