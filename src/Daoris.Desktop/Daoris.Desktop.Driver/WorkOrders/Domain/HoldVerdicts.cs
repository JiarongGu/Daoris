namespace Daoris.Driver;

/// <summary>The considerations of the verdicts that hold a quest for the person, each said from the facts alone.</summary>
internal static class HoldVerdicts
{
    /// <summary>Held for the person's choice: the sentence names the step it grew from and both choices.</summary>
    internal static Consideration Reopened(QuestView quest) =>
        new(quest, StartVerdict.SourceReopened,
            $"Quest `#{quest.Id}` grew from the work of `#{quest.Parent}`, which you reopened: "
            + (quest.Status == "Open" ? "it is withdrawn, and nothing starts it" : "it is held, and nothing carries it on")
            + " until you go on with it on the new work or abandon it.")
        {
            Said = new(quest.Status == "Open" ? SittingFact.Withdrawn : SittingFact.HeldByReopen) { Quest = quest.Parent },
        };

    /// <summary>The role policy's ask, or its bound of moves, holds the quest for the person's yes.</summary>
    internal static Consideration PolicyHeld(QuestView quest, PriorSession exhausted, string code) =>
        new(quest, StartVerdict.PolicyHeld, code == SittingFact.PolicyAsk
            ? "Its role's policy asks before it moves this quest to another choice: it waits for your yes."
            : "This quest made its role's policy's moves to another choice for today: it waits for your yes.")
        {
            HeldBy = exhausted,
            Said = new(code == SittingFact.PolicyAsk ? SittingFact.PolicyAsk : SittingFact.MovesBound) { Session = exhausted.Session },
        };

    /// <summary>Held for the person: no slot; the record is its HeldBy, as a stop's verdict names its stop.</summary>
    internal static Consideration TransientHeld(QuestView quest, PriorSession exhausted) =>
        new(quest, StartVerdict.TransientHeld, "Its last session's provider kept failing: it waits for your words or Try again.")
        {
            HeldBy = exhausted,
            Said = new(SittingFact.Transient) { Session = exhausted.Session },
        };

    /// <summary>Held by a pause: the sentence names the pause, what Resume does and the terminal's door. It says paused, never held, since a hold is a repository's and stops nothing that runs.</summary>
    internal static Consideration Paused(QuestView quest, PausedBy pause)
    {
        var resumes = quest.Status == "Taken" ? "carries it on" : "starts it";
        return new(quest, StartVerdict.Paused, pause.Scope == WorkScope.Ask
            ? $"paused with ask `#{pause.Id}`; Resume {resumes} — `daoris ask resume {pause.Id}`."
            : $"you paused `#{pause.Id}`; Resume {resumes} — `daoris quest resume {pause.Id}`.")
        {
            PausedBy = pause,
        };
    }

    /// <summary>Held by the person's stop: the sentence says whose stop, what Try again does, and the terminal's door, which names the session since `daoris driver` cannot see this verdict.</summary>
    internal static Consideration Stopped(QuestView quest, PriorSession stop) =>
        stop.StewardStopped
            // The steward's stop, held the same way and said as the steward's, never as the person's.
            ? new(quest, StartVerdict.Stopped,
                $"the steward stopped session `{stop.Session}`; Try again, by the steward or the owner, "
                + $"{(quest.Status == "Taken" ? "carries it on" : "starts it again")} — `daoris quest retry {quest.Id} --session {stop.Session}`.")
            {
                HeldBy = stop,
                Said = new(SittingFact.Steward) { Session = stop.Session },
            }
            : new(quest, StartVerdict.Stopped,
                $"you stopped session `{stop.Session}`; Try again {(quest.Status == "Taken" ? "carries it on" : "starts it again")} — "
                + $"`daoris quest retry {quest.Id} --session {stop.Session}`.")
            {
                HeldBy = stop,
            };
}
