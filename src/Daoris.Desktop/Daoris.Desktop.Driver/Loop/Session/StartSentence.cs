namespace Daoris.Driver;

/// <summary>
/// The creating sentence a start's record carries while it runs, each line with its code: its tree's opening; a resume once its
/// question closed, and in which tree; or a carry-on, why, whether on another account, in which tree, and why the person's words did
/// not go on in their own session.
/// </summary>
internal static class StartSentence
{
    /// <summary>Null where a start in the root says nothing.</summary>
    /// <param name="resumes">The record it carries on or resumes after, or null.</param>
    /// <param name="written">The record the person's words were written to, as the look read it, or null.</param>
    /// <param name="elsewhere">Whether it runs on another account than the record it carries on.</param>
    /// <param name="handedOver">The quest was handed over from <paramref name="resumes"/>: said by the agent it came from.</param>
    /// <param name="walked">The account the hand-over named could not run it, and another account of the named agent did.</param>
    /// <param name="joinedSession">The earlier part's session whose free tree this start went into, or null.</param>
    /// <param name="broughtForward">It carries on a stand-down whose lost take the person brought forward: said as that.</param>
    /// <param name="carriesWork">It carries an open quest on in the tree its failed session committed work in: said as that.</param>
    public static Noted? Of(
        TreeOpened? opened, QuestView quest, QuestView? answered, string? resumedIn, bool carryingOn,
        PriorSession? resumes, PriorSession? written, bool elsewhere, ContinueReason? fellBack, bool handedOver = false, bool walked = false,
        string? joinedSession = null, bool broughtForward = false, bool carriesWork = false)
    {
        if (opened is not null) return opened.Opening ?? Noted.From(opened.Sentence, null);

        if (answered is not null)
        {
            var resumed = Noted.Of(
                NoteCodes.StartedResumesAnswered, $"resumes `#{quest.Id}` now that `#{answered.Id}` is answered",
                ("quest", quest.Id), ("answered", answered.Id));
            return resumedIn is null
                ? resumed.Then(".")
                : resumed.Then(", ", Noted.Of(
                    NoteCodes.StartedInAskingTree, $"in the tree session `{resumes!.Session}` asked from.", ("session", resumes.Session)));
        }

        // A further part gone into a free tree of its entry is said, as a start in the root is not. A part whose tree was removed grows
        // again, and its opening (`started.tree`) already says whose tree went.
        if (!carryingOn)
        {
            return joinedSession is null
                ? null
                : Noted.Of(
                    NoteCodes.StartedJoined,
                    $"goes into the tree of session `{joinedSession}`, which holds the earlier part of this work.",
                    ("session", joinedSession));
        }

        var session = resumes!.Session;
        (string Name, object? Value)[] facts = [("quest", quest.Id), ("session", session)];
        var carried = resumes.PersonSaid
            ? (written is { Parked: false, Said: not null }
                // Words to a session that had ended: no answer, and an open quest is started.
                ? quest.Status == "Open"
                    ? Noted.Of(NoteCodes.StartedWordsOpen, $"starts `#{quest.Id}` with your words to session `{session}`", facts)
                    : Noted.Of(NoteCodes.StartedWordsTaken, $"carries `#{quest.Id}` on with your words to session `{session}`", facts)
                : Noted.Of(NoteCodes.StartedAnswer, $"carries `#{quest.Id}` on with your answer to session `{session}`", facts))
            : handedOver
                // The agent it came from, as its record names it; never an account's name or a path (the note travels).
                ? Noted.Of(
                    NoteCodes.StartedHandedOver,
                    $"carries `#{quest.Id}` on: session `{session}` was handed over from {resumes.Adapter ?? "its agent"}",
                    [.. facts, ("agent", resumes.Adapter ?? "its agent")])
            : broughtForward
                ? Noted.Of(
                    NoteCodes.StartedBroughtForward,
                    $"carries `#{quest.Id}` on: you brought the work of session `{session}` forward after its take lost", facts)
            // The steward's stop released, said as the steward's in the resumed brief, never as the person's.
            : resumes.StewardStopped
                ? Noted.Of(NoteCodes.StartedReleasedSteward, $"carries `#{quest.Id}` on: the steward stopped session `{session}`, and it was released", facts)
            : resumes.PersonStopped
                ? Noted.Of(NoteCodes.StartedReleased, $"carries `#{quest.Id}` on: you stopped session `{session}`, and released it", facts)
            // An open quest carried on in the tree its failed session committed work in before it took the quest.
            : carriesWork
                ? Noted.Of(NoteCodes.StartedCarriedWork, $"carries `#{quest.Id}` on with the work session `{session}` committed before it failed", facts)
                : Noted.Of(NoteCodes.StartedCutOff, $"carries `#{quest.Id}` on after session `{session}` was cut off", facts);

        // 🔴 Which account, never: the note travels, and its scrubber elides only the record's own account, so another account's name
        // here would reach a teammate.
        if (handedOver && walked)
        {
            carried = carried.Then(", ", Noted.Of(NoteCodes.StartedHandedWalked, "the account it named could not run it, so another of its agent's did"));
        }
        else if (elsewhere && !handedOver)
        {
            carried = carried.Then(", ", Noted.Of(NoteCodes.StartedOtherAccount, "on another account"));
        }
        carried = resumedIn is null ? carried.Then(".") : carried.Then(", ", Noted.Of(NoteCodes.StartedSameTree, "in the tree it worked in."));

        // Why the answer did not go on in its own conversation.
        return fellBack is null ? carried : carried.Then(" ", Continuations.CarriedOn(fellBack, resumes.Adapter));
    }
}
