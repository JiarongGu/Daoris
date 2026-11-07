namespace Daoris.Driver;

/// <summary>The considerations of the quests a record goes on, carries on or resumes, each with the sentence that says whose words or whose cut-off it is.</summary>
internal sealed class RoundSaying
{
    private readonly PlanRound round;
    private readonly RoundReading reading;
    private readonly RoundJoining joining;

    internal RoundSaying(PlanRound round, RoundReading reading)
    {
        this.round = round;
        this.reading = reading;
        Considering = new RoundConsidering(round);
        joining = new RoundJoining(round);
    }

    internal RoundConsidering Considering { get; }

    private Snapshot snapshot => round.Snapshot;

    internal Consideration Joining(Consideration start) => joining.Join(start);

    /// <summary>
    /// Words going on in the record they were said to, <c>continuing</c> it: a park is still an active record holding its tree, its
    /// quest and its slot, which it is never busy with itself; an ended record holds nothing, so it takes a slot, ahead of every
    /// start planned after it. The sentence names whose words: a park's answer, or what the person wrote to a session that had ended.
    /// </summary>
    internal Consideration GoOn(QuestView quest, PriorSession written)
    {
        var considered = Considering.Consider(quest, into: written, continuing: true);
        if (considered.Verdict != StartVerdict.Start) return considered;

        var waiting = written.Waiting;
        var said = waiting.Count == 1 ? waiting[0].Text : $"{waiting[0].Text} (and {waiting.Count - 1} more)";
        // The sentence follows who answered; with none of the owner's words it never says "you", and names the authors that are
        // there, the supervisor's choice by its label.
        var others = waiting.Any(word => word.Persons) ? [] : Continuations.AuthorsOf(waiting);
        var chose = snapshot.SupervisorChose.GetValueOrDefault(written.Session);
        var besides = Continuations.AuthorsSaid([.. others.Where(code => code != "supervisor")]);
        var supervised = others.Contains("supervisor")
            ? $"the supervisor answered session `{written.Session}` on your behalf{(chose is null ? "" : $" ({chose})")}"
              + (besides.Length > 0 ? $", with {besides}" : "")
            : null;
        return considered with
        {
            Reason = others.Count > 0
                ? written.Parked
                    ? $"carrying on in `{quest.To}` — {supervised ?? $"session `{written.Session}` goes on with {besides}"}"
                    : $"going on in `{quest.To}` with {Continuations.AuthorsSaid(others)}"
                : written.Parked
                ? $"carrying on in `{quest.To}` — you answered session `{written.Session}`: {SaidWordView.PersonsJoined(waiting) ?? said}"
                : $"going on in `{quest.To}` — you wrote to session `{written.Session}`: {said}",
            Resumes = written,
            GoesOn = true,
        };
    }

    /// <summary>
    /// A cut-off: this machine's session took the quest and failed before closing it — timed out, refused, crashed, or ended by the
    /// sweep or a shutdown — so the take is still here and the work is in its tree. Carried on like a failed start is retried: the
    /// strikes count every cut-off, and the third parks it — bar one an account's limit made, which is no strike: its carry-on is
    /// planned as any is, and held at spawn while the account cools. A stand-down the person brought forward is carried on the same
    /// way, its sentence naming the press and, offline, that the fresh take is not settled.
    /// </summary>
    internal Consideration CarryOn(QuestView quest, PriorSession cutOff)
    {
        var considered = Considering.Consider(quest, into: cutOff);
        var forward = string.Equals(cutOff.State, "stood-down", StringComparison.OrdinalIgnoreCase)
            ? snapshot.Takes.GetValueOrDefault(quest.Id)?.Forward
            : null;
        return considered.Verdict == StartVerdict.Start
            ? considered with
            {
                Reason = forward is not null
                    ? $"carrying on in `{quest.To}` — you brought the work of session `{cutOff.Session}` forward after its take lost"
                      + (forward.Standing == "held"
                          ? ", and the remote confirmed the fresh take."
                          : "; the fresh take is unconfirmed until a sync reaches the remote, and may still lose there.")
                    : SaidWordView.PersonsJoined(cutOff.Waiting) is { } answer
                    ? $"carrying on in `{quest.To}` — you answered session `{cutOff.Session}`: {answer}"
                    : Planner.StoppedByReopen(cutOff)
                        ? $"carrying on in `{quest.To}` — you went on with it on the new work of `#{quest.Parent}`, after session "
                          + $"`{cutOff.Session}` stopped where that work was reopened."
                    : cutOff.PersonStopped && snapshot.Handed.TryGetValue(quest.Id, out var handed) && reading.HandedFrom(quest, cutOff)
                        ? $"carrying on in `{quest.To}` — session `{cutOff.Session}` was handed over to {handed.Agent}."
                    : cutOff.StewardStopped
                        ? $"carrying on in `{quest.To}` — the steward stopped session `{cutOff.Session}`, and it was released."
                    : cutOff.PersonStopped
                        ? $"carrying on in `{quest.To}` — you stopped session `{cutOff.Session}`, and released it."
                    // An open quest whose session an account's limit cut off before it took the quest.
                    : quest.Status == "Open" && cutOff.Limit && snapshot.Committed.GetValueOrDefault(quest.Id) == 0
                        ? $"carrying on in `{quest.To}` — session `{cutOff.Session}` was cut off by its account's limit before it took the quest: "
                          + (cutOff.Note is { Length: > 0 } limit ? limit : "it ended before closing the quest.")
                    // An open quest whose cut-off session committed work before it took the quest.
                    : quest.Status == "Open"
                        ? $"carrying on in `{quest.To}` — session `{cutOff.Session}` failed before it took the quest, and its tree holds "
                          + $"{Commits(snapshot.Committed.GetValueOrDefault(quest.Id))} beyond its start: "
                          + (cutOff.Note is { Length: > 0 } failure ? failure : "it ended before closing the quest.")
                        : $"carrying on in `{quest.To}` — session `{cutOff.Session}` was cut off: "
                          + (cutOff.Note is { Length: > 0 } note ? note : "it ended before closing the quest."),
                Resumes = cutOff,
                BroughtForward = forward,
            }
            : considered;
    }

    private static string Commits(int count) => count == 1 ? "1 commit" : $"{count} commits";

    /// <summary>
    /// A take that is not this machine's, as the ledger judges it and in its words: another machine's by the claim, named by a
    /// teammate's record where one came, or one made here after the session ended that no record here marks, a chat's or work outside
    /// Daoris. An unmarked take made while the session ran is still its own, as the ledger reads it: the HTTP door marks none. Null
    /// where the take is this machine's, or is not read. Whose it is rides beside the sentence as facts, from the same reading, so
    /// the page's words and the driver's never disagree.
    /// </summary>
    internal Consideration? TakenElsewhere(QuestView quest, PriorSession cutOff)
    {
        if (!snapshot.Takes.TryGetValue(quest.Id, out var take)) return null;

        var over = $"the take is theirs, so session `{cutOff.Session}` is not carried on over it.";
        if (take.Reopened)
            return new(quest, StartVerdict.TakenElsewhere,
                $"Quest `#{quest.Id}` was lost to your reopen of the work it grew from: the take is not this machine's, so session `{cutOff.Session}` is not carried on over it.")
            {
                TakenBy = new TakenBy(cutOff.Session, Reopened: true),
            };

        if (!take.Here)
        {
            var machine = take.Teammate?.Split('/')[0];
            return new(quest, StartVerdict.TakenElsewhere, take.Teammate is { } teammate
                ? $"Quest `#{quest.Id}` is taken on `{machine}`, by session `{teammate}`: {over}"
                : $"Quest `#{quest.Id}` is taken on another machine: {over}")
            {
                TakenBy = new TakenBy(cutOff.Session, machine, take.Teammate),
            };
        }

        // A take made after it ended by the person's bring-forward of it is the press's, never a chat's.
        return !take.Took && Planner.TakenAt(quest, take) is { } taken && cutOff.Updated is { } ended && taken > ended && !Planner.BroughtForward(cutOff, take)
            ? new(quest, StartVerdict.TakenElsewhere,
                $"Quest `#{quest.Id}` was taken here after session `{cutOff.Session}` ended, by a chat or by work outside Daoris: {over}")
            {
                TakenBy = new TakenBy(cutOff.Session, Here: true),
            }
            : null;
    }

    /// <summary>
    /// A waiting quest: the open list holds open and taken quests only, so a question still in it is unanswered, and one absent from
    /// it has closed. A resume is a start in every other way — the person's hold, the strikes, busy and the cap all still stand
    /// between it and a spawn.
    /// </summary>
    internal Consideration Resume(QuestView quest, string awaits, PriorSession prior)
    {
        var question = snapshot.Quests.FirstOrDefault(q =>
            string.Equals(q.Id, awaits, StringComparison.OrdinalIgnoreCase));
        // A question closed done departing from what the person required stays on the open list, held for their yes, and what waits
        // on it waits with it: the sentence names whose move it is, and its door. One held for its evidence is not the person's while
        // the session that made the done runs, since its end reads the evidence.
        if (question is { Held: true })
        {
            var (said, why) = HeldQuestionSaid.Of(snapshot, question);
            return new(quest, StartVerdict.Waiting, why) { Said = said };
        }

        if (question is not null)
        {
            return new(quest, StartVerdict.Waiting,
                $"waits on `#{question.Id}`, asked of `{question.To}` — it resumes, in the same tree, "
                + "once that is answered.")
            {
                Said = new(SittingFact.Asked) { Quest = question.Id, Repository = question.To },
            };
        }

        var considered = Considering.Consider(quest, into: prior);
        return considered.Verdict == StartVerdict.Start
            ? considered with { Reason = $"resuming in `{quest.To}` — `#{awaits}` is answered.", Resumes = prior }
            : considered;
    }
}
