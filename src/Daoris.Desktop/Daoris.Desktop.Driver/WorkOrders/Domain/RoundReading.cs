namespace Daoris.Driver;

/// <summary>What a plan round reads of the records for a quest: the record it goes on from, carries on from, or is held by.</summary>
internal sealed class RoundReading(Snapshot snapshot, DriverConfig config, IRoleStarts? roles, Action<LineCourse>? decided)
{
    /// <summary>
    /// The record a taken quest that waits on nothing is carried on from: a newer stand-down whose lost take the person brought
    /// forward, first, since that is the work they chose and its tree; else its last session here, where that carries on.
    /// </summary>
    internal PriorSession? CarriedOnFrom(QuestView quest) =>
        BroughtForwardFrom(quest)
        ?? (snapshot.LastRun.TryGetValue(quest.Id, out var last) && Planner.CarriesOn(last) && !HeldForPerson(last) ? last : null);

    /// <summary>
    /// The record an open quest is carried on from: its last session here, cut off before it took the quest, whose tree the look
    /// found commits in beyond its start; else null, and the quest is a first start.
    /// </summary>
    internal PriorSession? RetriedFrom(QuestView quest) =>
        snapshot.Committed.GetValueOrDefault(quest.Id) > 0
        && snapshot.LastRun.TryGetValue(quest.Id, out var last)
        && CommittedWork.CutOff(last)
        && !HeldForPerson(last)
            ? last
            : null;

    /// <summary>The record an open quest's limited line resumes from: its last session here, ended by an account's limit.</summary>
    internal PriorSession? LimitedFrom(QuestView quest) =>
        snapshot.LastRun.TryGetValue(quest.Id, out var last)
        && last is { Limit: true, State: "failed" }
        && !HeldForPerson(last)
            ? last
            : null;

    // A record whose words wait marked because another client holds its conversation waits for the person, as a park does, so no
    // carry-on moves its quest to a new session meanwhile; so does one whose answers the answers-alone bound holds, or that they
    // kept with Not now: a carry-on would take the line on with nobody between.
    private bool HeldForPerson(PriorSession last) =>
        snapshot.Unable.GetValueOrDefault(last.Session) is { Why: ContinueWhy.Elsewhere or GoOnMarks.AnswersAlone or GoOnMarks.NotNow } mark
        && GoOnMarks.Judged(last, mark);

    // The stand-down a bring-forward carries on, or null: the quest's newest record here stood down, and the take the claim reads
    // stands by a press made after that record began. Once, by that alone, as the ledger opens it: the record carried on is newer
    // than the press, so its own stand-down carries nothing.
    private PriorSession? BroughtForwardFrom(QuestView quest) =>
        snapshot.StoodDown.TryGetValue(quest.Id, out var lost)
        && snapshot.Takes.TryGetValue(quest.Id, out var take)
        && Planner.BroughtForward(lost, take)
            ? lost
            : null;

    /// <summary>
    /// Whether the loop says anything of this quest: every open one, and a taken one whose last session here is a stop, a cut-off,
    /// an answered park or the asker of a question. A take this machine never ran, and one whose session runs or waits on the
    /// person, is not planned, so a pause says nothing of it either.
    /// </summary>
    internal bool Plans(QuestView quest) =>
        quest.Status == "Open"
        || WrittenTo(quest)
        || (quest.Status == "Taken"
            && snapshot.LastRun.TryGetValue(quest.Id, out var run)
            && ((quest.Awaits is { Length: > 0 } && !ResumedOnAnswer(quest))
                || run.AnsweredPark
                || string.Equals(run.State, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(run.State, "stopped", StringComparison.OrdinalIgnoreCase)
                || run is { State: "completed", PersonSaid: true }
                || Planner.StoppedByReopen(run)))
        // A stand-down the person's bring-forward carries on, whatever ran here before it.
        || (quest.Status == "Taken" && BroughtForwardFrom(quest) is not null);

    /// <summary>Whether the quest was handed to another agent from this session: the store names the session whose stop it is.</summary>
    internal bool HandedFrom(QuestView quest, PriorSession stop) =>
        snapshot.Handed.TryGetValue(quest.Id, out var handed) && string.Equals(handed.From, stop.Session, StringComparison.Ordinal);

    /// <summary>
    /// The quest's last session here, ended past its transient bound, where nothing released it: its hold. A hand-over made from
    /// that record passes it (as <see cref="StopHolding"/> passes one), and so does Try again's release of that end, keyed to the
    /// record's session and its <c>updated</c>, so the record's next end is held again.
    /// </summary>
    internal PriorSession? TransientHolding(QuestView quest) =>
        quest.Status is "Open" or "Taken"
        && snapshot.LastRun.TryGetValue(quest.Id, out var last)
        && Planner.EndedTransient(last)
        && !HandedFrom(quest, last)
        // Past its bound the course says what follows, and Try again's release of that end is its press.
        && DrivenCourse.PastBound(last, quest.Status, TransientReleased(quest, last)) is { } course
        && Told(course) is LineCourse.Hold { Why: CourseHoldWhy.TransientBound }
            ? last
            : null;

    // The course's answer, told to the one listening (a test's, no person's), and handed back.
    private LineCourse Told(LineCourse course)
    {
        decided?.Invoke(course);
        return course;
    }

    /// <summary>
    /// The held record a hand-over was made from, or the one Try again released: what an Open quest is carried on from, in its
    /// tree, ahead of the commit rule, never started afresh.
    /// </summary>
    internal PriorSession? TransientReleasedFrom(QuestView quest) =>
        snapshot.LastRun.TryGetValue(quest.Id, out var last) && Planner.EndedTransient(last) && (HandedFrom(quest, last) || TransientReleased(quest, last))
            ? last
            : null;

    // Try again's release of this record's end: its session and its `updated` both the release's; or the role's policy moved the
    // line to another choice at that end, which the line is carried on by.
    private bool TransientReleased(QuestView quest, PriorSession last) =>
        config.ReleasesTransient(quest.Id, last.Session, last.Updated)
        || (snapshot.PolicyMoving.TryGetValue(quest.Id, out var moving) && string.Equals(moving.Session, last.Session, StringComparison.Ordinal));

    /// <summary>The person's stop of the quest's last session here, where it still holds the quest, or null.</summary>
    internal PriorSession? StopHolding(QuestView quest) =>
        quest.Status is "Open" or "Taken"
        && snapshot.LastRun.TryGetValue(quest.Id, out var last)
        && last.PersonStopped
        && !config.Releases(quest.Id, last.Session)
        && !RoleReleased(quest, last)
        // The stop that handed the quest to another agent is its own release: the person asked for it.
        && !HandedFrom(quest, last)
            ? last
            : null;

    // The steward's Try again of its own stop, written in the role's own key and a release only while this run's memory holds its
    // operation at `released`; a file's entry alone releases nothing.
    private bool RoleReleased(QuestView quest, PriorSession last) =>
        config.RoleReleaseFor(quest.Id) is { Kind: RoleReleaseKinds.Stop } release
        && string.Equals(release.Session, last.Session, StringComparison.Ordinal)
        && last.StewardStopped
        && roles?.Released(quest.Id, release) == true;

    /// <summary>Whether the person's words wait on this quest's record here for it to go on with (see <see cref="WordsOn"/>).</summary>
    internal bool WrittenTo(QuestView quest) => WordsOn(quest) is not null;

    /// <summary>
    /// Whether a taken quest's resume already began, so it waits on its question no more. The service names the question for as long
    /// as the quest is taken, so read alone it held the quest after its resume for good. Read from what the look holds: the question
    /// is off the open list (answered), and the quest's last session here is a park, which only a session started after the answer
    /// makes, since one that asks ends <c>completed</c>. A last session that ended any other way is still the resume's to carry on.
    /// </summary>
    internal bool ResumedOnAnswer(QuestView quest) =>
        quest.Awaits is { Length: > 0 } awaits
        && !snapshot.Quests.Any(asked => string.Equals(asked.Id, awaits, StringComparison.OrdinalIgnoreCase))
        && snapshot.LastRun.TryGetValue(quest.Id, out var last)
        && last.Parked;

    /// <summary>
    /// The record the person's words wait on for this quest, parked or ended, and not all words a reopen already found it could not
    /// take: its last session here, or null. A taken quest waiting on a question waits with them: the resume is its next start, which
    /// is handed them. Once that resume began (<see cref="ResumedOnAnswer"/>), the quest waits on nothing, and words to its session go
    /// on in it as any record's do. A closed quest waits on nothing, whatever question the service still keeps for it. The session a
    /// taken quest's reopen names, where a later one is its last here, goes first with the words it was told; not while the person's
    /// stop of the last session holds the quest: the reopen's words are Daoris's, and release no stop the person made.
    /// </summary>
    internal PriorSession? WordsOn(QuestView quest)
    {
        if (quest.Awaits is { Length: > 0 } && !ResumedOnAnswer(quest)) return null;
        if (snapshot.NamedByReopen.TryGetValue(quest.Id, out var named) && Waits(named) && StopHolding(quest) is null) return named;
        return snapshot.LastRun.TryGetValue(quest.Id, out var written) && Waits(written) ? written : null;

        // Never the record the person ended with End and carry on: its words go to the carry-on, never to a resume of the run that
        // hung; never while a supervisor's decision on it is held for the owner's overrule.
        bool Waits(PriorSession record) =>
            record.WordsWaiting && !GoOnMarks.Judged(record, snapshot.Unable.GetValueOrDefault(record.Session))
            && !Observation.IsQuietPressed(record.NoteParts) && !snapshot.SupervisorHolds(record)
            // A record past its transient bound goes on only with a word of the person's said after it ended. A helper's answer,
            // Daoris's word or another session's message would resume an exhausted line with nobody between; a word said before the
            // end never releases it. Try again's release of that end does: a word kept meanwhile goes on under it, by this go-on.
            // The course reads those words and that release, a hand-over from the record passing the hold as above.
            && !(Planner.EndedTransient(record) && !HandedFrom(quest, record)
                && Told(DrivenCourse.PastBound(record, quest.Status, TransientReleased(quest, record))) is LineCourse.Hold { Why: CourseHoldWhy.TransientBound });
    }
}
